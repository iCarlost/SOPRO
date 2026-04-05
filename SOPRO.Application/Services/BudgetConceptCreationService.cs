using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class BudgetConceptCreationService
    {
        public static ConceptoPresupuesto CreateNewForTypeChange(SOPROContext context, int proyectoId, BudgetRowTypeChangeInput input, bool isAggregator, int level)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (input == null) throw new ArgumentNullException(nameof(input));

            int maxOrden = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == proyectoId)
                .Max(c => (int?)c.Orden) ?? -1;

            var concepto = new ConceptoPresupuesto
            {
                ProyectoId = proyectoId,
                Descripcion = input.Descripcion ?? string.Empty,
                Clave = input.Clave ?? string.Empty,
                Unidad = input.Unidad ?? string.Empty,
                EsAgrupador = isAggregator,
                Nivel = level,
                Orden = maxOrden + 1,
                Cantidad = 0m,
                CostoDirectoUnitario = 0m,
                CostoDirectoTotal = 0m,
                ColumnasPersonalizadasJSON = string.Empty,
                Notas = string.Empty
            };

            context.ConceptosPresupuesto.Add(concepto);
            context.SaveChanges();
            return concepto;
        }

        public static void UpdateExistingTypeState(SOPROContext context, ConceptoPresupuesto concepto, bool isAggregator, int level)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (concepto == null) throw new ArgumentNullException(nameof(concepto));

            concepto.EsAgrupador = isAggregator;
            concepto.Nivel = level;

            if (concepto.Id > 0)
            {
                var tracked = context.ConceptosPresupuesto.Find(concepto.Id);
                if (tracked != null && !ReferenceEquals(tracked, concepto))
                {
                    tracked.EsAgrupador = isAggregator;
                    tracked.Nivel = level;
                }

                context.SaveChanges();
            }
        }
    }
}
