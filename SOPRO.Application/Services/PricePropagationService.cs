using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  PricePropagationService — VERSIÓN CORREGIDA v2.0                      ║
    // ║                                                                         ║
    // ║  [FIX] Reemplaza mat.CalcularCostoDirecto() (sin decimales) por        ║
    // ║        MatrixComponentCalculationService.Recalculate() que usa el motor ║
    // ║        con la configuración de decimales del proyecto propietario.      ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public static class PricePropagationService
    {
        public static void PropagarMaterial(SOPROContext ctx, int materialId)
            => Propagar(ctx, TipoComponenteMatriz.Material, materialId);

        public static void PropagarManoDeObra(SOPROContext ctx, int manoDeObraId)
            => Propagar(ctx, TipoComponenteMatriz.ManoDeObra, manoDeObraId);

        public static void PropagarMaquinaria(SOPROContext ctx, int maquinariaId)
            => Propagar(ctx, TipoComponenteMatriz.Maquinaria, maquinariaId);

        public static void PropagarEliminacion(SOPROContext ctx, List<int> matrizIdsAfectadas)
        {
            if (!matrizIdsAfectadas.Any()) return;

            var matrices = CargarMatricesConNavegaciones(ctx, matrizIdsAfectadas);
            RecalcularConMotor(ctx, matrices);

            PropagarAuxiliares(ctx, matrizIdsAfectadas, matrices);

            var todasIds = matrices.Select(m => m.Id).ToList();
            ActualizarConceptos(ctx, matrices, todasIds);
            ctx.SaveChanges();
        }

        private static void Propagar(SOPROContext ctx, TipoComponenteMatriz tipo, int insumoId)
        {
            var componentes = ctx.ComponentesMatriz
                .Include(c => c.Material)
                .Include(c => c.ManoDeObra)
                .Include(c => c.Maquinaria)
                .Where(c => c.TipoComponente == tipo &&
                    (tipo == TipoComponenteMatriz.Material   ? c.MaterialId   == insumoId :
                     tipo == TipoComponenteMatriz.ManoDeObra ? c.ManoDeObraId == insumoId :
                     c.MaquinariaId == insumoId))
                .ToList();

            if (!componentes.Any()) return;

            var matrizIds = componentes.Select(c => c.MatrizId).Distinct().ToList();
            var matrices  = CargarMatricesConNavegaciones(ctx, matrizIds);

            // Obtener proyectos para usar decimalesImporte correcto
            var proyectoIds = matrices.Where(m => m.ProyectoId.HasValue)
                .Select(m => m.ProyectoId!.Value).Distinct().ToList();
            var proyectos = ctx.Proyectos
                .Where(p => proyectoIds.Contains(p.Id))
                .ToDictionary(p => p.Id);

            // Actualizar importe individual con redondeo del motor (no multiplicación cruda)
            foreach (var comp in componentes)
            {
                decimal pu = tipo == TipoComponenteMatriz.Material   ? (comp.Material?.PrecioUnitario ?? 0) :
                             tipo == TipoComponenteMatriz.ManoDeObra ? (comp.ManoDeObra?.SalarioReal  ?? 0) :
                             (comp.Maquinaria?.CostoHorario ?? 0);

                // Obtener decimales del proyecto propietario de la matriz
                var mat = matrices.FirstOrDefault(m => m.Id == comp.MatrizId);
                int dec = (mat?.ProyectoId.HasValue == true && proyectos.TryGetValue(mat.ProyectoId!.Value, out var proy))
                    ? proy.DecimalesImporte : 2;
                var motor = new MotorCalculoSopro(dec, dec, 4);
                comp.Importe = motor.Multiplicar(comp.Cantidad, pu);
            }

            // [FIX] Usar motor con decimales del proyecto en lugar de CalcularCostoDirecto()
            RecalcularConMotor(ctx, matrices);
            PropagarAuxiliares(ctx, matrizIds, matrices);

            var todasIds = matrices.Select(m => m.Id).ToList();
            ActualizarConceptos(ctx, matrices, todasIds);
            ctx.SaveChanges();
        }

        /// <summary>
        /// Recalcula el CostoDirecto de las matrices dadas usando el motor
        /// con la precisión del proyecto propietario de cada una.
        /// Expuesto de forma pública para su reutilización en importación externa
        /// (ExternalMatrixImportService) sin duplicar lógica de recálculo.
        /// </summary>
        public static void RecalcularConMotor(SOPROContext ctx, List<Matriz> matrices)
        {
            var proyectoIds = matrices
                .Where(m => m.ProyectoId.HasValue)
                .Select(m => m.ProyectoId!.Value)
                .Distinct().ToList();
            var proyectos = ctx.Proyectos
                .Where(p => proyectoIds.Contains(p.Id))
                .ToDictionary(p => p.Id);

            foreach (var mat in matrices)
            {
                if (!mat.ProyectoId.HasValue) continue;
                if (!proyectos.TryGetValue(mat.ProyectoId.Value, out var proyecto)) continue;
                var totals = MatrixComponentCalculationService.Recalculate(
                    mat.Componentes.ToList(), proyecto.DecimalesImporte);
                mat.CostoDirecto = new MotorCalculoSopro(proyecto)
                    .RedondearImporte(totals.CostoDirectoTotal);
            }
        }

        private static void ActualizarConceptos(SOPROContext ctx, List<Matriz> matrices, List<int> matrizIds)
        {
            var proyectoIds = matrices
                .Where(m => m.ProyectoId.HasValue)
                .Select(m => m.ProyectoId!.Value)
                .Distinct().ToList();
            var proyectos = ctx.Proyectos
                .Where(p => proyectoIds.Contains(p.Id))
                .ToDictionary(p => p.Id);

            var conceptos = ctx.ConceptosPresupuesto
                .Where(c => c.MatrizId.HasValue && matrizIds.Contains(c.MatrizId.Value))
                .ToList();

            foreach (var concepto in conceptos)
            {
                var mat = matrices.First(m => m.Id == concepto.MatrizId);
                if (!mat.ProyectoId.HasValue) continue;
                if (!proyectos.TryGetValue(mat.ProyectoId.Value, out var proyecto)) continue;

                var motor = new MotorCalculoSopro(proyecto);
                concepto.CostoDirectoUnitario = motor.RedondearImporte(mat.CostoDirecto);
                concepto.CostoDirectoTotal    = motor.Multiplicar(concepto.Cantidad, concepto.CostoDirectoUnitario);

                // Paridad con el recálculo de pantalla (FormPresupuesto.RefrescarPreciosDesdeDB):
                // el Precio Unitario y el Importe Total también deben quedar al día en la
                // propagación headless, sin depender de que el presupuesto esté abierto.
                concepto.PrecioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, concepto.CostoDirectoUnitario);
                concepto.ImporteTotal   = motor.Multiplicar(concepto.Cantidad, concepto.PrecioUnitario);
            }
        }

        private static void PropagarAuxiliares(SOPROContext ctx, List<int> matrizIdsOrigen, List<Matriz> todasMatrices)
        {
            var idsConAuxiliar = ctx.ComponentesMatriz
                .Where(c => c.AuxiliarId.HasValue && matrizIdsOrigen.Contains(c.AuxiliarId.Value))
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (!idsConAuxiliar.Any()) return;

            var idsNuevos = idsConAuxiliar.Except(todasMatrices.Select(m => m.Id)).ToList();
            if (!idsNuevos.Any()) return;

            var matricesPadre = CargarMatricesConNavegaciones(ctx, idsNuevos);
            RecalcularConMotor(ctx, matricesPadre);
            todasMatrices.AddRange(matricesPadre);
            PropagarAuxiliares(ctx, idsNuevos, todasMatrices);
        }

        private static List<Matriz> CargarMatricesConNavegaciones(SOPROContext ctx, List<int> ids)
        {
            return ctx.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => ids.Contains(m.Id))
                .ToList();
        }
    }
}
