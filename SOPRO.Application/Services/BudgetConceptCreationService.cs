using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class BudgetConceptCreationService
    {
        /// <summary>
        /// Prepara EN MEMORIA el concepto que representa una fila nueva cambiada de
        /// tipo, sin agregarlo al contexto ni guardar: la escritura pertenece al
        /// ejecutor de comando (un solo SaveChanges). El Id se genera en ese contexto
        /// nuevo, nunca en el llamador. Se conserva la semántica histórica de orden:
        /// el concepto nuevo recibe max(Orden) + 1.
        /// </summary>
        public static ConceptoPresupuesto PrepareNewForTypeChange(
            SOPROContext context,
            int proyectoId,
            BudgetTypeChangeCommand command,
            bool isAggregator,
            int level)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (command == null) throw new ArgumentNullException(nameof(command));

            int maxOrden = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == proyectoId)
                .Max(c => (int?)c.Orden) ?? -1;

            return new ConceptoPresupuesto
            {
                ProyectoId = proyectoId,
                Descripcion = command.Descripcion,
                Clave = command.Clave,
                Unidad = isAggregator ? string.Empty : command.Unidad,
                EsAgrupador = isAggregator,
                Nivel = level,
                Orden = maxOrden + 1,
                Cantidad = 0m,
                CostoDirectoUnitario = 0m,
                CostoDirectoTotal = 0m,
                PrecioUnitario = 0m,
                ImporteTotal = 0m,
                ColumnasPersonalizadasJSON = string.Empty,
                Notas = string.Empty
            };
        }

        /// <summary>
        /// Aplica en memoria el nuevo estado jerárquico de un concepto existente, sin
        /// buscar otra instancia ni guardar. Al convertirse en agrupador se limpian
        /// los valores que la UI deja vacíos (unidad, cantidad, precio unitario e
        /// importe); los totales los recalcula después el servicio de propagación.
        /// </summary>
        public static void ApplyExistingTypeState(
            ConceptoPresupuesto concepto,
            bool isAggregator,
            int level)
        {
            if (concepto == null) throw new ArgumentNullException(nameof(concepto));

            concepto.EsAgrupador = isAggregator;
            concepto.Nivel = level;
            concepto.FechaModificacion = DateTime.Now;

            if (!isAggregator) return;

            concepto.Unidad = string.Empty;
            concepto.Cantidad = 0m;
            concepto.PrecioUnitario = 0m;
            concepto.ImporteTotal = 0m;
        }
    }
}
