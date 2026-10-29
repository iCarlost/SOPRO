using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services
{
    public class ProjectLifecycleService
    {
        private readonly ProjectWorkspaceService _workspaceService;
        private readonly IProjectDbContextFactory _dbContextFactory;

        public ProjectLifecycleService(
            ProjectWorkspaceService workspaceService,
            IProjectDbContextFactory? dbContextFactory = null)
        {
            _workspaceService = workspaceService;
            _dbContextFactory = dbContextFactory ?? new ProjectDbContextFactory();
        }

        public ProjectSession CreateProject(Proyecto proyecto)
        {
            var dbPath = _workspaceService.BuildProjectDatabasePath(proyecto.Nombre);

            // N4-2: el candado se toma ANTES de tocar la base; se libera al cerrar
            // la sesión (CloseProjectSession → Dispose). Si algo falla después de
            // adquirirlo, se libera aquí mismo.
            var workspaceLock = WorkspaceLock.Acquire(dbPath);
            try
            {
                // El nombre se sanea para construir la ruta: dos nombres distintos
                // pueden apuntar al mismo archivo (p. ej. "Obra/Norte" y "Obra_Norte").
                // Nunca se reutiliza un proyecto existente para "crear" otro.
                if (File.Exists(dbPath))
                {
                    throw new InvalidOperationException(
                        "Ya existe un archivo de proyecto con el nombre indicado. "
                        + "Elige otro nombre o usa Abrir proyecto para abrir el proyecto existente.");
                }

                var context = _dbContextFactory.Create(dbPath);
                context.Database.EnsureCreated();

                // Fuente única de verdad del schema
                SchemaManager.EnsureCurrentSchema(context);

                context.Proyectos.Add(proyecto);
                context.SaveChanges();

                EnsureDefaultColumns(context, proyecto.Id);

                return new ProjectSession(context, proyecto, dbPath, workspaceLock);
            }
            catch
            {
                workspaceLock.Dispose();
                throw;
            }
        }

        public ProjectSession OpenProject(string projectPath)
        {
            var workspaceLock = WorkspaceLock.Acquire(projectPath);
            try
            {
                var context = _dbContextFactory.Create(projectPath);

                // Fuente única de verdad del schema — actualiza cualquier DB viejo
                SchemaManager.EnsureCurrentSchema(context);

                var project = context.Proyectos.FirstOrDefault()
                    ?? throw new InvalidOperationException("El archivo no contiene un proyecto válido.");

                EnsureDefaultColumns(context, project.Id);

                return new ProjectSession(context, project, projectPath, workspaceLock);
            }
            catch
            {
                workspaceLock.Dispose();
                throw;
            }
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
