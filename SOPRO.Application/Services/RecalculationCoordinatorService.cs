using System.Collections.Generic;
using System.Linq;
using SOPRO.Core.Entities;
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

        internal static bool RefreshLinkedMachineryAfterMaterialUpdate(SOPROContext context, int materialId, int? proyectoId)
        {
            List<Maquinaria> machines = context.Maquinaria.Where(m => m.EsCostoCalculado &&
                (proyectoId.HasValue ? m.ProyectoId == proyectoId : m.ProyectoId == null) &&
                (m.MaterialCombustibleId == materialId || m.MaterialAceiteId == materialId)).ToList();
            Material material = context.Materiales.Single(m => m.Id == materialId);
            foreach (Maquinaria machine in machines)
            {
                if (machine.MaterialCombustibleId == materialId) machine.PrecioCombustible = material.PrecioUnitario;
                if (machine.MaterialAceiteId == materialId) machine.PrecioAceite = material.PrecioUnitario;
                machine.CalcularCostoHorario();
                PricePropagationService.PropagarMaquinariaSinGuardar(context, machine.Id, machine.ProyectoId);
            }
            return machines.Count > 0;
        }

        internal static bool RefreshLinkedMachineryAfterManoDeObraUpdate(SOPROContext context, int manoDeObraId, int? proyectoId)
        {
            List<Maquinaria> machines = context.Maquinaria.Where(m => m.EsCostoCalculado && m.ProyectoId == proyectoId &&
                m.ManoDeObraOperadorId == manoDeObraId).ToList();
            ManoDeObra source = context.ManoDeObra.Single(m => m.Id == manoDeObraId);
            foreach (Maquinaria machine in machines)
            {
                machine.SalarioOperador = source.SalarioBase;
                machine.FactorSalarioReal = source.FactorSalarioReal;
                machine.CalcularCostoHorario();
                PricePropagationService.PropagarMaquinariaSinGuardar(context, machine.Id, machine.ProyectoId);
            }
            return machines.Count > 0;
        }

        public static bool RecalculateAfterInsumoDeletion(SOPROContext context, List<int> matrizIds, int? proyectoId = null)
        {
            PricePropagationService.PropagarEliminacion(context, matrizIds, proyectoId);
            return true;
        }
    }
}
