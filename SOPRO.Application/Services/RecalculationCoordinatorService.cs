using System.Collections.Generic;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class RecalculationCoordinatorService
    {
        public static bool RecalculateAfterMaterialUpdate(SOPROContext context, int materialId)
        {
            PricePropagationService.PropagarMaterial(context, materialId);
            return true;
        }

        public static bool RecalculateAfterManoDeObraUpdate(SOPROContext context, int manoDeObraId)
        {
            PricePropagationService.PropagarManoDeObra(context, manoDeObraId);
            return true;
        }

        public static bool RecalculateAfterMaquinariaUpdate(SOPROContext context, int maquinariaId)
        {
            PricePropagationService.PropagarMaquinaria(context, maquinariaId);
            return true;
        }

        public static bool RecalculateAfterInsumoDeletion(SOPROContext context, List<int> matrizIds)
        {
            PricePropagationService.PropagarEliminacion(context, matrizIds);
            return true;
        }
    }
}
