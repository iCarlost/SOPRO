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
        public static void PropagarMaterial(SOPROContext ctx, int materialId, int? proyectoId = null)
            => Propagar(ctx, TipoComponenteMatriz.Material, materialId, proyectoId);

        public static void PropagarManoDeObra(SOPROContext ctx, int manoDeObraId, int? proyectoId = null)
            => Propagar(ctx, TipoComponenteMatriz.ManoDeObra, manoDeObraId, proyectoId);

        public static void PropagarMaquinaria(SOPROContext ctx, int maquinariaId, int? proyectoId = null)
            => Propagar(ctx, TipoComponenteMatriz.Maquinaria, maquinariaId, proyectoId);

        public static void PropagarEliminacion(SOPROContext ctx, List<int> matrizIdsAfectadas, int? proyectoId = null)
        {
            if (!matrizIdsAfectadas.Any()) return;

            var matrices = CargarMatricesConNavegaciones(ctx, matrizIdsAfectadas);

            // La propagación nunca cruza proyectos: una matriz de otro proyecto
            // no debe recalcularse por un Id numérico compartido.
            if (proyectoId.HasValue)
                matrices = matrices.Where(m => m.ProyectoId == proyectoId.Value).ToList();
            if (!matrices.Any()) return;

            RecalcularConMotor(ctx, matrices);

            PropagarAuxiliares(ctx, matrizIdsAfectadas, matrices, proyectoId);

            var todasIds = matrices.Select(m => m.Id).ToList();
            ActualizarConceptos(ctx, matrices, todasIds);
            ctx.SaveChanges();
        }

        private static void Propagar(SOPROContext ctx, TipoComponenteMatriz tipo, int insumoId, int? proyectoId = null)
        {
            IQueryable<ComponenteMatriz> query = ctx.ComponentesMatriz
                .Include(c => c.Material)
                .Include(c => c.ManoDeObra)
                .Include(c => c.Maquinaria)
                .Where(c => c.TipoComponente == tipo &&
                    (tipo == TipoComponenteMatriz.Material   ? c.MaterialId   == insumoId :
                     tipo == TipoComponenteMatriz.ManoDeObra ? c.ManoDeObraId == insumoId :
                     c.MaquinariaId == insumoId));

            // Filtro de proyecto por la matriz propietaria del componente: evita
            // propagar sobre matrices de otros proyectos por colisión de Ids.
            if (proyectoId.HasValue)
                query = query.Where(c => c.Matriz.ProyectoId == proyectoId.Value);

            var componentes = query.ToList();

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
            PropagarAuxiliares(ctx, matrizIds, matrices, proyectoId);

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

            // Paridad con RefrescarPreciosDesdeDB → RecalcularTodosLosTotales:
            // los agrupadores padre se recalculan bottom-up a partir de los
            // totales de sus hijos directos (RecalculoGlobalService.RecalcularAgrupadores).
            ActualizarAgrupadores(ctx, proyectos.Values.ToList());
        }

        /// <summary>
        /// Recalcula los totales de los conceptos agrupadores de cada proyecto
        /// afectado. Paridad con la UI (FormPresupuesto → RecalcularTodosLosTotales
        /// → BudgetHierarchyService.CalculateAggregatorTotal): la jerarquía se
        /// infiere por Orden/Nivel, NO por PadreId, porque la persistencia legacy
        /// (FormPresupuesto.Persistencia.btnGuardar_Click) no establece PadreId y
        /// la UI agrupa por bloques de filas (Orden) con corte por Nivel.
        ///
        /// Cada agrupador suma los totales de los conceptos hoja (EsAgrupador =
        /// false) que le siguen en orden, hasta la primera fila con Nivel menor o
        /// igual al suyo. Un agrupador sin hojas queda en cero. El redondeo usa el
        /// motor del proyecto (misma semántica que RecalculoGlobalService, Fase 4).
        /// </summary>
        private static void ActualizarAgrupadores(SOPROContext ctx, List<Proyecto> proyectos)
        {
            foreach (var proyecto in proyectos)
            {
                var todos = ctx.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .ToList();

                var agrupadores = todos.Where(c => c.EsAgrupador).ToList();
                if (agrupadores.Count == 0) continue;

                var motor = new MotorCalculoSopro(proyecto);

                foreach (var agrupador in agrupadores)
                {
                    int idx = todos.FindIndex(c => c.Id == agrupador.Id);
                    var importesCD  = new List<decimal>();
                    var importesImp = new List<decimal>();

                    for (int j = idx + 1; j < todos.Count; j++)
                    {
                        var fila = todos[j];
                        if (fila.Nivel <= agrupador.Nivel) break;
                        if (fila.EsAgrupador) continue;
                        importesCD.Add(fila.CostoDirectoTotal);
                        importesImp.Add(fila.ImporteTotal);
                    }

                    agrupador.CostoDirectoTotal = motor.SumarImportes(importesCD);
                    agrupador.ImporteTotal       = motor.SumarImportes(importesImp);
                    agrupador.FechaModificacion  = DateTime.Now;
                }
            }
        }

        private static void PropagarAuxiliares(SOPROContext ctx, List<int> matrizIdsOrigen, List<Matriz> todasMatrices, int? proyectoId = null)
        {
            IQueryable<ComponenteMatriz> query = ctx.ComponentesMatriz
                .Where(c => c.AuxiliarId.HasValue && matrizIdsOrigen.Contains(c.AuxiliarId.Value));

            // Los auxiliares también se restringen al proyecto de la sesión.
            if (proyectoId.HasValue)
                query = query.Where(c => c.Matriz.ProyectoId == proyectoId.Value);

            var idsConAuxiliar = query
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (!idsConAuxiliar.Any()) return;

            var idsNuevos = idsConAuxiliar.Except(todasMatrices.Select(m => m.Id)).ToList();
            if (!idsNuevos.Any()) return;

            var matricesPadre = CargarMatricesConNavegaciones(ctx, idsNuevos);
            RecalcularConMotor(ctx, matricesPadre);
            todasMatrices.AddRange(matricesPadre);
            PropagarAuxiliares(ctx, idsNuevos, todasMatrices, proyectoId);
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
