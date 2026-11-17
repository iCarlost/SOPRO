using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;

namespace SOPRO.Application.Services
{
    public class ProjectLifecycleService
    {
        internal const string DiagnosticsSource = "SOPRO.Application";
        internal const string OperationOpenProject = "AbrirProyecto";
        internal const string OperationCreateProject = "CrearProyecto";

        private const string DefaultProductVersion = "0.0.0";

        private readonly ProjectWorkspaceService _workspaceService;
        private readonly IProjectDbContextFactory _dbContextFactory;
        private readonly IDiagnosticsSink? _diagnosticsSink;
        private readonly string _productVersion;

        public ProjectLifecycleService(
            ProjectWorkspaceService workspaceService,
            IProjectDbContextFactory? dbContextFactory = null,
            IDiagnosticsSink? diagnosticsSink = null,
            string? productVersion = null)
        {
            _workspaceService = workspaceService;
            _dbContextFactory = dbContextFactory ?? new ProjectDbContextFactory();
            _diagnosticsSink = diagnosticsSink;
            _productVersion = string.IsNullOrWhiteSpace(productVersion)
                ? DefaultProductVersion
                : productVersion;
        }

        public ProjectSession CreateProject(Proyecto proyecto)
        {
            var correlationId = DiagnosticsCorrelationId.Create(null);
            var dbPath = _workspaceService.BuildProjectDatabasePath(proyecto.Nombre);

            // N4-2: el candado se toma ANTES de tocar la base; se libera al cerrar
            // la sesión (CloseProjectSession → Dispose). Si algo falla después de
            // adquirirlo, se libera aquí mismo.
            IWorkspaceLock workspaceLock;
            try
            {
                workspaceLock = WorkspaceLock.Acquire(dbPath);
            }
            catch (Exception exception)
            {
                WriteFailureDiagnostics(
                    OperationCreateProject,
                    correlationId,
                    DiagnosticsSeverity.Warning,
                    exception,
                    schemaVersion: null,
                    schemaStage: null);
                throw;
            }

            SOPROContext? context = null;
            var fase = ProjectLifecyclePhase.Defaults;
            string? schemaStage = null;
            try
            {
                // El nombre se sanea para construir la ruta: dos nombres distintos
                // pueden apuntar al mismo archivo (p. ej. "Obra/Norte" y "Obra_Norte").
                // Nunca se reutiliza un proyecto existente para "crear" otro.
                if (File.Exists(dbPath))
                {
                    fase = ProjectLifecyclePhase.ExistingFile;
                    throw new InvalidOperationException(
                        "Ya existe un archivo de proyecto con el nombre indicado. "
                        + "Elige otro nombre o usa Abrir proyecto para abrir el proyecto existente.");
                }

                context = _dbContextFactory.Create(dbPath);
                context.Database.EnsureCreated();

                // Fuente única de verdad del schema
                SchemaManager.EnsureCurrentSchema(
                    context,
                    etapa => schemaStage = etapa,
                    _diagnosticsSink,
                    _productVersion,
                    correlationId);
                schemaStage = null;

                context.Proyectos.Add(proyecto);
                context.SaveChanges();

                EnsureDefaultColumns(context, proyecto.Id);

                return new ProjectSession(context, proyecto, dbPath, workspaceLock);
            }
            catch (Exception exception)
            {
                // La lectura de la versión y la liberación ocurren ANTES del sink:
                // el evento se escribe sin contexto, candado ni transacción abiertos.
                var schemaVersion = TryReadSchemaVersion(context);
                context?.Dispose();
                workspaceLock.Dispose();
                WriteFailureDiagnostics(
                    OperationCreateProject,
                    correlationId,
                    Classify(fase, schemaStage, exception),
                    exception,
                    schemaVersion,
                    schemaStage);
                throw;
            }
        }

        public ProjectSession OpenProject(string projectPath)
        {
            var correlationId = DiagnosticsCorrelationId.Create(null);

            if (string.IsNullOrWhiteSpace(projectPath))
            {
                throw RegistrarFalloYDevolver(
                    new ArgumentException(
                        "Debe indicar la ruta del proyecto a abrir.", nameof(projectPath)),
                    OperationOpenProject,
                    correlationId,
                    DiagnosticsSeverity.Warning,
                    schemaVersion: null,
                    schemaStage: null);
            }

            // El archivo debe existir ANTES de tocar SQLite o el candado: abrir una
            // base inexistente crearía un archivo vacío y un .lock sobrante.
            if (!File.Exists(projectPath))
            {
                throw RegistrarFalloYDevolver(
                    new FileNotFoundException(
                        "No se encontró el archivo del proyecto.", projectPath),
                    OperationOpenProject,
                    correlationId,
                    DiagnosticsSeverity.Warning,
                    schemaVersion: null,
                    schemaStage: null);
            }

            IWorkspaceLock workspaceLock;
            try
            {
                workspaceLock = WorkspaceLock.Acquire(projectPath);
            }
            catch (Exception exception)
            {
                WriteFailureDiagnostics(
                    OperationOpenProject,
                    correlationId,
                    DiagnosticsSeverity.Warning,
                    exception,
                    schemaVersion: null,
                    schemaStage: null);
                throw;
            }

            SOPROContext? context = null;
            var fase = ProjectLifecyclePhase.Preflight;
            string? schemaStage = null;
            try
            {
                // Preflight de solo lectura con SQL crudo: un archivo que no es un
                // proyecto SOPRO se rechaza SIN migrarlo (EnsureCurrentSchema escribe).
                // No se materializa la entidad Proyecto: los esquemas legacy pueden
                // carecer de columnas nuevas y la consulta EF fallaría antes de migrar.
                EnsureValidProjectFile(projectPath);

                // Fuente única de verdad del schema — actualiza cualquier DB viejo
                fase = ProjectLifecyclePhase.Schema;
                context = _dbContextFactory.Create(projectPath);
                SchemaManager.EnsureCurrentSchema(
                    context,
                    etapa => schemaStage = etapa,
                    _diagnosticsSink,
                    _productVersion,
                    correlationId);
                schemaStage = null;

                fase = ProjectLifecyclePhase.ProjectRead;
                var project = context.Proyectos.FirstOrDefault();
                if (project is null)
                {
                    fase = ProjectLifecyclePhase.ProjectInvalid;
                    throw new InvalidOperationException("El archivo no contiene un proyecto válido.");
                }

                fase = ProjectLifecyclePhase.Defaults;
                EnsureDefaultColumns(context, project.Id);

                return new ProjectSession(context, project, projectPath, workspaceLock);
            }
            catch (Exception exception)
            {
                // La lectura de la versión y la liberación ocurren ANTES del sink:
                // el evento se escribe sin contexto, candado ni transacción abiertos.
                var schemaVersion = TryReadSchemaVersion(context);
                context?.Dispose();
                workspaceLock.Dispose();
                WriteFailureDiagnostics(
                    OperationOpenProject,
                    correlationId,
                    Classify(fase, schemaStage, exception),
                    exception,
                    schemaVersion,
                    schemaStage);
                throw;
            }
        }

        private enum ProjectLifecyclePhase
        {
            Defaults,
            ExistingFile,
            Preflight,
            Schema,
            ProjectRead,
            ProjectInvalid
        }

        private static DiagnosticsSeverity Classify(
            ProjectLifecyclePhase phase,
            string? schemaStage,
            Exception exception)
        {
            if (phase is ProjectLifecyclePhase.ExistingFile
                or ProjectLifecyclePhase.Preflight
                or ProjectLifecyclePhase.ProjectInvalid)
            {
                return DiagnosticsSeverity.Warning;
            }

            // El gating de versión (futura o ilegible) rechaza antes de migrar y no
            // reporta etapa. Es un proyecto no válido para esta instalación.
            if (phase is ProjectLifecyclePhase.Schema
                && schemaStage is null
                && exception is InvalidOperationException
                && exception is not SqliteException)
            {
                return DiagnosticsSeverity.Warning;
            }

            return DiagnosticsSeverity.Error;
        }

        /// <summary>
        /// Lee la versión de esquema registrada solo si puede hacerlo sin alterar
        /// el flujo; cualquier fallo de lectura deja el campo en <c>null</c>.
        /// </summary>
        private static string? TryReadSchemaVersion(SOPROContext? context)
        {
            if (context is null)
            {
                return null;
            }

            try
            {
                return SchemaManager.GetVersionRegistrada(context);
            }
            catch
            {
                return null;
            }
        }

        private TException RegistrarFalloYDevolver<TException>(
            TException exception,
            string operation,
            string correlationId,
            DiagnosticsSeverity severity,
            string? schemaVersion,
            string? schemaStage)
            where TException : Exception
        {
            WriteFailureDiagnostics(
                operation,
                correlationId,
                severity,
                exception,
                schemaVersion,
                schemaStage);
            return exception;
        }

        private void WriteFailureDiagnostics(
            string operation,
            string correlationId,
            DiagnosticsSeverity severity,
            Exception exception,
            string? schemaVersion,
            string? schemaStage)
        {
            if (_diagnosticsSink is null)
            {
                return;
            }

            try
            {
                _diagnosticsSink.Write(DiagnosticsEvent.FromException(
                    DateTimeOffset.UtcNow,
                    severity,
                    DiagnosticsSource,
                    operation,
                    _productVersion,
                    correlationId,
                    exception,
                    schemaVersion,
                    schemaStage));
            }
            catch
            {
                // Best-effort: el sink nunca reemplaza ni oculta la excepción original.
            }

            try
            {
                _diagnosticsSink.Flush();
            }
            catch
            {
                // El vaciado también es best-effort y nunca altera la operación.
            }
        }

        /// <summary>
        /// Verifica, sobre un contexto de solo lectura y con SQL crudo, que el archivo
        /// es un SQLite válido con las ocho tablas del modelo core, todas sus columnas
        /// base y una fila en <c>Proyectos</c>. No escribe ni migra: el archivo queda
        /// intacto.
        /// </summary>
        private void EnsureValidProjectFile(string projectPath)
        {
            try
            {
                using var readOnlyContext = _dbContextFactory.CreateReadOnly(projectPath);
                var connection = readOnlyContext.Database.GetDbConnection();
                var wasOpen = connection.State == ConnectionState.Open;
                if (!wasOpen)
                {
                    connection.Open();
                }

                try
                {
                    if (!TieneProyectoValido(connection))
                    {
                        throw new InvalidOperationException(
                            "El archivo no contiene un proyecto válido.");
                    }
                }
                finally
                {
                    if (!wasOpen)
                    {
                        connection.Close();
                    }
                }
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (SqliteException ex)
            {
                // Archivo corrupto, vacío o que no es una base SQLite.
                throw new InvalidOperationException(
                    "El archivo no es un proyecto SOPRO válido o está dañado.", ex);
            }
        }

        /// <summary>
        /// Columnas base del modelo core SOPRO, anteriores a cualquier migración
        /// M0xx (esquema histórico + entidades actuales menos lo agregado por las
        /// migraciones). Las migraciones solo AGREGAN columnas/tablas: no pueden
        /// reconstruir estas columnas, así que su ausencia delata un SQLite ajeno.
        /// Solo se exige presencia de nombres (sin tipo, afinidad, nulabilidad,
        /// orden ni columnas extra); la comparación es insensible a mayúsculas.
        /// </summary>
        private static readonly (string Tabla, string[] Columnas)[] ColumnasBaseProyecto =
        {
            ("Proyectos", new[]
            {
                "Id", "Nombre", "Descripcion", "Ubicacion", "Convocante", "Contratista",
                "ApoderadoLegal", "FechaInicio", "FechaTermino", "PlazoEjecucion",
                "PorcentajeIndirectosCentral", "PorcentajeIndirectosCampo",
                "PorcentajeFinanciamiento", "PorcentajeUtilidad", "PorcentajeIVA",
                "FactorSalarioReal", "FechaCalculoFSR", "FechaCreacion", "FechaModificacion"
            }),
            ("Materiales", new[]
            {
                "Id", "Clave", "Descripcion", "Unidad", "PrecioUnitario", "Origen",
                "MaterialMaestroId", "ProyectoId", "Notas", "FechaCreacion", "FechaModificacion"
            }),
            ("ManoDeObra", new[]
            {
                "Id", "Clave", "Descripcion", "Unidad", "SalarioBase", "FactorSalarioReal",
                "SalarioReal", "Origen", "ManoDeObraMaestraId", "ProyectoId", "Notas",
                "FechaCreacion", "FechaModificacion"
            }),
            ("Maquinaria", new[]
            {
                "Id", "Clave", "Descripcion", "PotenciaNominal", "ValorAdquisicion",
                "ValorLlantas", "ValorPiezasEspeciales", "FactorRescate", "VidaEconomica",
                "TasaInteres", "HorasEfectivasAnio", "PrimaSeguro", "FactorMantenimiento",
                "CantidadCombustible", "PrecioCombustible", "CantidadAceite", "PrecioAceite",
                "VidaEconomicaLlantas", "VidaPiezasEspeciales", "SalarioOperador",
                "FactorSalarioReal", "HorasEfectivasTurno", "CostoHorario", "Origen",
                "MaquinariaMaestraId", "ProyectoId", "Notas", "FechaCreacion", "FechaModificacion"
            }),
            ("Matrices", new[]
            {
                "Id", "Clave", "Descripcion", "Unidad", "Tipo", "CostoDirecto", "Origen",
                "MatrizMaestraId", "ProyectoId", "Notas", "FechaCreacion", "FechaModificacion",
                "FechaUltimoCalculo"
            }),
            ("ComponentesMatriz", new[]
            {
                "Id", "MatrizId", "TipoComponente", "MaterialId", "ManoDeObraId",
                "MaquinariaId", "AuxiliarId", "Cantidad", "Importe", "Orden", "Notas"
            }),
            ("ConceptosPresupuesto", new[]
            {
                "Id", "ProyectoId", "Clave", "Descripcion", "Unidad", "EsAgrupador",
                "PadreId", "Orden", "Cantidad", "MatrizId", "CostoDirectoUnitario",
                "CostoDirectoTotal", "Indirectos", "Financiamiento", "Utilidad",
                "PrecioUnitario", "ImporteTotal"
            }),
            ("ColumnasPersonalizadas", new[]
            {
                "Id", "ProyectoId", "Nombre", "NombreInterno", "TipoColumna", "TipoDato",
                "Formula", "Visible", "Imprimible", "AnchoColumna", "Orden", "Totalizar",
                "CondicionTotalizacion", "FormatoNumerico", "FormatoFecha", "Alineacion",
                "NombreFuente", "TamanoFuente", "ColorFuente", "ColorFondo", "Negrita",
                "Cursiva", "FechaCreacion", "FechaModificacion"
            })
        };

        private static bool TieneProyectoValido(DbConnection connection)
        {
            // Los objetos de ColumnasBaseProyecto pertenecen al modelo core
            // pre-migraciones. Comprobar columnas y objetos tipo table (no views)
            // evita que un SQLite ajeno con una tabla llamada Proyectos llegue a
            // EnsureCurrentSchema (que crearía __MigracionesCustom) y recién
            // entonces falle EF al consultar columnas inexistentes.
            foreach (var (tableName, requiredColumns) in ColumnasBaseProyecto)
            {
                if (!TablaExiste(connection, tableName)
                    || !TieneColumnas(connection, tableName, requiredColumns))
                {
                    return false;
                }
            }

            using var rowCommand = connection.CreateCommand();
            rowCommand.CommandText = "SELECT 1 FROM Proyectos LIMIT 1";
            return rowCommand.ExecuteScalar() is not null;
        }

        private static bool TablaExiste(DbConnection connection, string tableName)
        {
            using var command = connection.CreateCommand();
            command.CommandText = "SELECT COUNT(*) FROM sqlite_master WHERE type = 'table' AND name = @name";
            var parameter = command.CreateParameter();
            parameter.ParameterName = "@name";
            parameter.Value = tableName;
            command.Parameters.Add(parameter);
            return Convert.ToInt32(command.ExecuteScalar()) > 0;
        }

        private static bool TieneColumnas(DbConnection connection, string tableName, string[] requiredColumns)
        {
            var actualColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var command = connection.CreateCommand())
            {
                command.CommandText = $"PRAGMA table_info('{tableName}')";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    actualColumns.Add(reader.GetString(1));
                }
            }

            return requiredColumns.All(actualColumns.Contains);
        }

        public void CloseProjectSession(ProjectSession? session)
        {
            // N4-2: la sesión es la dueña de su contexto y de su candado; el cierre
            // no accede a session.Context (regla: cero accesos directos al contexto
            // desde Application).
            session?.Dispose();
        }

        /// <summary>
        /// Aplica el schema completo. Mantiene la firma pública para compatibilidad.
        /// </summary>
        public void ApplyProjectUpgrades(SOPROContext context)
        {
            SchemaManager.EnsureCurrentSchema(context);
        }

        public void EnsureDefaultColumns(SOPROContext context, int projectId)
        {
            EnsureBudgetColumns(context.DatabasePath, projectId);

            // Las demás columnas de módulo también se siembran en un contexto PROPIO:
            // el contexto de sesión que termina compartiéndose no debe confirmar
            // entidades ajenas (p. ej. un Concepto pendiente) al abrir/crear proyecto.
            using var seedContext = _dbContextFactory.Create(context.DatabasePath);
            EnsureExplosionColumns(seedContext, projectId);
            EnsureIndirectColumns(seedContext, projectId);
            EnsureFinanciamientoColumns(seedContext, projectId);
        }

        private static void EnsureExplosionColumns(SOPROContext context, int projectId)
        {
            if (context.ColumnasExplosion.Any(c => c.ProyectoId == projectId))
                return;

            context.ColumnasExplosion.AddRange(
                new ColumnaExplosion { ProyectoId = projectId, Nombre = "Clave",       NombreInterno = "Clave",          AnchoColumna = 80,  Orden = 1, Alineacion = AlineacionColumna.Izquierda, Visible = true,  FormatoNumerico = string.Empty },
                new ColumnaExplosion { ProyectoId = projectId, Nombre = "Descripcion", NombreInterno = "Descripcion",    AnchoColumna = 350, Orden = 2, Alineacion = AlineacionColumna.Izquierda, Visible = true,  FormatoNumerico = string.Empty },
                new ColumnaExplosion { ProyectoId = projectId, Nombre = "Unidad",      NombreInterno = "Unidad",         AnchoColumna = 70,  Orden = 3, Alineacion = AlineacionColumna.Centro,    Visible = true,  FormatoNumerico = string.Empty },
                new ColumnaExplosion { ProyectoId = projectId, Nombre = "Cantidad",    NombreInterno = "Cantidad",       AnchoColumna = 100, Orden = 4, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "N4" },
                new ColumnaExplosion { ProyectoId = projectId, Nombre = "P.U.",        NombreInterno = "PrecioUnitario", AnchoColumna = 120, Orden = 5, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "C4" },
                new ColumnaExplosion { ProyectoId = projectId, Nombre = "Importe",     NombreInterno = "Importe",        AnchoColumna = 130, Orden = 6, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "C2" },
                new ColumnaExplosion { ProyectoId = projectId, Nombre = "%",           NombreInterno = "Porcentaje",     AnchoColumna = 80,  Orden = 7, Alineacion = AlineacionColumna.Derecha,   Visible = true,  FormatoNumerico = "P2" }
            );
            context.SaveChanges();
        }

        private static void EnsureIndirectColumns(SOPROContext context, int projectId)
        {
            if (context.ColumnasIndirectos.Any(c => c.ProyectoId == projectId))
                return;

            context.ColumnasIndirectos.AddRange(
                new ColumnaIndirectos { ProyectoId = projectId, Nombre = "Grupo / Concepto",  NombreInterno = "Grupo",          AnchoColumna = 400, Orden = 1, Alineacion = AlineacionColumna.Izquierda, Visible = true, FormatoNumerico = string.Empty },
                new ColumnaIndirectos { ProyectoId = projectId, Nombre = "Importe Mensual $", NombreInterno = "ImporteMensual", AnchoColumna = 180, Orden = 2, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "N2" },
                new ColumnaIndirectos { ProyectoId = projectId, Nombre = "Duración (Meses)",  NombreInterno = "Duracion",       AnchoColumna = 150, Orden = 3, Alineacion = AlineacionColumna.Centro,    Visible = true, FormatoNumerico = string.Empty },
                new ColumnaIndirectos { ProyectoId = projectId, Nombre = "Importe Total $",   NombreInterno = "ImporteTotal",   AnchoColumna = 180, Orden = 4, Alineacion = AlineacionColumna.Derecha,   Visible = true, FormatoNumerico = "N2" }
            );
            context.SaveChanges();
        }

        private static void EnsureFinanciamientoColumns(SOPROContext context, int projectId)
        {
            if (context.ColumnasFinanciamiento.Any(c => c.ProyectoId == projectId))
                return;

            context.ColumnasFinanciamiento.AddRange(
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Período",        NombreInterno = "colPeriodo",   AnchoColumna = 90,  Orden = 1,  Alineacion = AlineacionColumna.Centro,  Visible = true, FormatoNumerico = string.Empty },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Inicio",         NombreInterno = "colInicio",    AnchoColumna = 75,  Orden = 2,  Alineacion = AlineacionColumna.Centro,  Visible = true, FormatoNumerico = string.Empty },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Fin",            NombreInterno = "colFin",       AnchoColumna = 75,  Orden = 3,  Alineacion = AlineacionColumna.Centro,  Visible = true, FormatoNumerico = string.Empty },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Días",           NombreInterno = "colDias",      AnchoColumna = 45,  Orden = 4,  Alineacion = AlineacionColumna.Centro,  Visible = true, FormatoNumerico = string.Empty },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "CD $",           NombreInterno = "colCD",        AnchoColumna = 95,  Orden = 5,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "CI $",           NombreInterno = "colCI",        AnchoColumna = 95,  Orden = 6,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Egreso $",       NombreInterno = "colEgresos",   AnchoColumna = 105, Orden = 7,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Anticipo $",     NombreInterno = "colAnticipo",  AnchoColumna = 95,  Orden = 8,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Estimación $",   NombreInterno = "colEstim",     AnchoColumna = 105, Orden = 9,  Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Amortización $", NombreInterno = "colAmort",     AnchoColumna = 110, Orden = 10, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Cobro neto $",   NombreInterno = "colCobro",     AnchoColumna = 105, Orden = 11, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Flujo $",        NombreInterno = "colFlujoNeto", AnchoColumna = 95,  Orden = 12, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Saldo acum. $",  NombreInterno = "colSaldo",     AnchoColumna = 110, Orden = 13, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Tasa período",   NombreInterno = "colTasa",      AnchoColumna = 85,  Orden = 14, Alineacion = AlineacionColumna.Centro,  Visible = true, FormatoNumerico = string.Empty },
                new ColumnaFinanciamiento { ProyectoId = projectId, Nombre = "Interés $",      NombreInterno = "colInteres",   AnchoColumna = 95,  Orden = 15, Alineacion = AlineacionColumna.Derecha, Visible = true, FormatoNumerico = "C2" }
            );
            context.SaveChanges();
        }

        /// <summary>
        /// Seed de las columnas legacy del presupuesto sobre un contexto NUEVO por
        /// operación (factory + transacción + un único SaveChanges). El contexto de la
        /// sesión que termina compartiéndose no escribe ni confirma entidades ajenas.
        /// Mantiene la semántica histórica: si ya existe la columna base Tipo no se
        /// toca nada; si no existe, se reemplaza la configuración vieja por las 17
        /// columnas legacy (la vista las asegura luego a 23).
        /// </summary>
        private void EnsureBudgetColumns(string databasePath, int projectId)
        {
            var resultado = BudgetColumnConfigurationService.EnsureSeedColumns(
                _dbContextFactory,
                databasePath,
                projectId,
                BudgetColumnConfigurationService.CrearDefinicionesLegacyPresupuesto());

            if (!resultado.IsCommitted)
            {
                throw new InvalidOperationException(
                    resultado.Message ?? "No se pudieron preparar las columnas del presupuesto.");
            }
        }
    }
}
