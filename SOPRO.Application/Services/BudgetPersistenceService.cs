using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class BudgetPersistenceService
    {
        public static void ApplyAutoSaveChanges(SOPROContext context, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows.Where(r => r.ExistingConceptId.HasValue))
            {
                var enBd = context.ConceptosPresupuesto.Find(row.ExistingConceptId!.Value);
                if (enBd == null) continue;

                enBd.Descripcion = row.Descripcion;
                enBd.Clave = row.Clave;
                enBd.Unidad = row.Unidad;
                enBd.Cantidad = row.Cantidad;
                enBd.EsAgrupador = row.EsAgrupador;
                enBd.CostoDirectoUnitario = row.CostoDirectoUnitario;
                enBd.CostoDirectoTotal = row.CostoDirectoTotal;
                enBd.PrecioUnitario = row.PrecioUnitario;
                enBd.ImporteTotal = row.ImporteTotal;
                enBd.MatrizId = row.MatrizId;
                enBd.FechaModificacion = DateTime.Now;
            }

            context.SaveChanges();
        }

        public static BudgetSaveResult ReplaceProjectConcepts(SOPROContext context, int projectId, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            var concepts = BuildConcepts(projectId, rows).ToList();
            var conceptosExistentes = context.ConceptosPresupuesto.Where(c => c.ProyectoId == projectId);
            context.ConceptosPresupuesto.RemoveRange(conceptosExistentes);
            context.ConceptosPresupuesto.AddRange(concepts);
            context.SaveChanges();

            return new BudgetSaveResult { SavedConceptCount = concepts.Count };
        }

        public static IEnumerable<ConceptoPresupuesto> BuildConcepts(int projectId, IEnumerable<BudgetConceptRowDto> rows)
        {
            if (rows == null) throw new ArgumentNullException(nameof(rows));

            foreach (var row in rows.Where(r => r.HasContent).OrderBy(r => r.Orden))
            {
                yield return new ConceptoPresupuesto
                {
                    ProyectoId = projectId,
                    Clave = row.Clave,
                    Descripcion = row.Descripcion,
                    EsAgrupador = row.EsAgrupador,
                    Nivel = ResolveLevelFromType(row.Tipo),
                    Orden = row.Orden,
                    Unidad = row.Unidad,
                    ColumnasPersonalizadasJSON = string.Empty,
                    Notas = string.Empty,
                    Cantidad = row.Cantidad,
                    MatrizId = row.EsAgrupador ? null : row.MatrizId,
                    CostoDirectoUnitario = row.EsAgrupador ? 0 : row.CostoDirectoUnitario,
                    CostoDirectoTotal = row.CostoDirectoTotal,  // agrupadores guardan su total calculado
                    PrecioUnitario = row.EsAgrupador ? 0 : row.PrecioUnitario,
                    ImporteTotal = row.ImporteTotal,            // agrupadores guardan su total calculado
                };
            }
        }

        public static int ResolveLevelFromType(string? tipo)
        {
            return tipo switch
            {
                "Capitulo" => 0,
                "Subcapitulo" => 1,
                "Nivel 1" => 2,
                "Nivel 2" => 3,
                "Nivel 3" => 4,
                _ => 5
            };
        }
    }
}
