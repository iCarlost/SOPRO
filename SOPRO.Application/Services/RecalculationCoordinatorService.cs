using System.Collections.Generic;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class RecalculationCoordinatorService
    {
        public static bool RecalculateAfterMaterialUpdate(SOPROContext context, int materialId, int? proyectoId = null)
        {
            PricePropagationService.PropagarMaterial(context, materialId, proyectoId);
            return true;
        }

        public static bool RecalculateAfterManoDeObraUpdate(SOPROContext context, int manoDeObraId, int? proyectoId = null)
        {
            PricePropagationService.PropagarManoDeObra(context, manoDeObraId, proyectoId);
            return true;
        }

        public static bool RecalculateAfterMaquinariaUpdate(SOPROContext context, int maquinariaId, int? proyectoId = null)
        {
            PricePropagationService.PropagarMaquinaria(context, maquinariaId, proyectoId);
            return true;
        }

        public static bool RecalculateAfterInsumoDeletion(SOPROContext context, List<int> matrizIds, int? proyectoId = null)
        {
            PricePropagationService.PropagarEliminacion(context, matrizIds, proyectoId);
            return true;
        }
    }
}
