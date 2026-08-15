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
            EnsureBudgetColumns(context, projectId);
            EnsureExplosionColumns(context, projectId);
            EnsureIndirectColumns(context, projectId);
            EnsureFinanciamientoColumns(context, projectId);
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

        private static void EnsureBudgetColumns(SOPROContext context, int projectId)
        {
            bool hasBaseColumns = context.ColumnasPersonalizadas.Any(c => c.ProyectoId == projectId && c.NombreInterno == "Tipo");
            if (hasBaseColumns)
                return;

            var orphanColumns = context.ColumnasPersonalizadas
                .Where(c => c.ProyectoId == projectId)
                .ToList();

            if (orphanColumns.Any())
            {
                context.ColumnasPersonalizadas.RemoveRange(orphanColumns);
                context.SaveChanges();
            }

            context.ColumnasPersonalizadas.AddRange(
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Tipo",                 NombreInterno = "Tipo",                    TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Texto,      AnchoColumna = 120, Visible = true,  Orden = -7, Alineacion = AlineacionColumna.Centro,    Formula = string.Empty,                                                                                                                  FormatoNumerico = string.Empty, FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Clave",                NombreInterno = "Clave",                   TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Texto,      AnchoColumna = 100, Visible = true,  Orden = -6, Alineacion = AlineacionColumna.Izquierda, Formula = string.Empty,                                                                                                                  FormatoNumerico = string.Empty, FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Descripción",          NombreInterno = "Descripcion",             TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Texto,      AnchoColumna = 300, Visible = true,  Orden = -5, Alineacion = AlineacionColumna.Izquierda, Formula = string.Empty,                                                                                                                  FormatoNumerico = string.Empty, FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Unidad",               NombreInterno = "Unidad",                  TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Texto,      AnchoColumna = 80,  Visible = true,  Orden = -4, Alineacion = AlineacionColumna.Centro,    Formula = string.Empty,                                                                                                                  FormatoNumerico = string.Empty, FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Cantidad",             NombreInterno = "Cantidad",                TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Numerico,   AnchoColumna = 100, Visible = true,  Orden = -3, Alineacion = AlineacionColumna.Derecha,   Formula = string.Empty,                                                                                                                  FormatoNumerico = "N2",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "P.U.",                 NombreInterno = "PrecioUnitario",          TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 120, Visible = true,  Orden = -2, Alineacion = AlineacionColumna.Derecha,   Formula = "CostoDirectoUnitario",                                                                                                         FormatoNumerico = "C4",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Importe",              NombreInterno = "Importe",                 TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 140, Visible = true,  Orden = -1, Alineacion = AlineacionColumna.Derecha,   Formula = "Cantidad * CostoDirectoUnitario",                                                                                              FormatoNumerico = "C2",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "% Indirectos",         NombreInterno = "PorcentajeIndirectos",    TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Porcentaje, AnchoColumna = 90,  Visible = false, Orden = 1,  Alineacion = AlineacionColumna.Derecha,   Formula = string.Empty,                                                                                                                  FormatoNumerico = "0.00",       FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Indirectos",           NombreInterno = "Indirectos",              TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 110, Visible = false, Orden = 2,  Alineacion = AlineacionColumna.Derecha,   Formula = "CostoDirecto * (PorcentajeIndirectos / 100)",                                                                                  FormatoNumerico = "C2",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "% Financiamiento",     NombreInterno = "PorcentajeFinanciamiento",TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Porcentaje, AnchoColumna = 110, Visible = false, Orden = 3,  Alineacion = AlineacionColumna.Derecha,   Formula = string.Empty,                                                                                                                  FormatoNumerico = "0.00",       FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Financiamiento",       NombreInterno = "Financiamiento",          TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 110, Visible = false, Orden = 4,  Alineacion = AlineacionColumna.Derecha,   Formula = "(CostoDirecto + Indirectos) * (PorcentajeFinanciamiento / 100)",                                                               FormatoNumerico = "C2",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "% Utilidad",           NombreInterno = "PorcentajeUtilidad",      TipoColumna = TipoColumnaPersonalizada.Personal,  TipoDato = TipoDatoColumna.Porcentaje, AnchoColumna = 90,  Visible = false, Orden = 5,  Alineacion = AlineacionColumna.Derecha,   Formula = string.Empty,                                                                                                                  FormatoNumerico = "0.00",       FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Utilidad",             NombreInterno = "Utilidad",                TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 110, Visible = false, Orden = 6,  Alineacion = AlineacionColumna.Derecha,   Formula = "(CostoDirecto + Indirectos + Financiamiento) * (PorcentajeUtilidad / 100)",                                                   FormatoNumerico = "C2",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "% Cargos Adicionales", NombreInterno = "PorcentajeCargosAdicionales", TipoColumna = TipoColumnaPersonalizada.Personal, TipoDato = TipoDatoColumna.Porcentaje, AnchoColumna = 130, Visible = false, Orden = 7, Alineacion = AlineacionColumna.Derecha, Formula = string.Empty, FormatoNumerico = "0.00", FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Cargos Adicionales",   NombreInterno = "CargosAdicionales",       TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 140, Visible = false, Orden = 8,  Alineacion = AlineacionColumna.Derecha,   Formula = "(CostoDirecto + Indirectos + Financiamiento + Utilidad) * (PorcentajeCargosAdicionales / 100)",                              FormatoNumerico = "C2",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Precio Unitario Final", NombreInterno = "PrecioUnitarioFinal",    TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 140, Visible = false, Orden = 9,  Alineacion = AlineacionColumna.Derecha,   Formula = "CostoDirecto + Indirectos + Financiamiento + Utilidad + CargosAdicionales",                                                  FormatoNumerico = "C4",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty },
                new ColumnaPersonalizada { ProyectoId = projectId, Nombre = "Importe Final",         NombreInterno = "ImporteFinal",           TipoColumna = TipoColumnaPersonalizada.Calculada, TipoDato = TipoDatoColumna.Moneda,     AnchoColumna = 140, Visible = false, Orden = 10, Alineacion = AlineacionColumna.Derecha,   Formula = "Cantidad * PrecioUnitarioFinal",                                                                                               FormatoNumerico = "C2",         FormatoFecha = string.Empty, CondicionTotalizacion = string.Empty }
            );
            context.SaveChanges();
        }
    }
}
