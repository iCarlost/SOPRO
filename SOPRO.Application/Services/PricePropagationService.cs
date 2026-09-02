using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
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
    // ║                                                                         ║
    // ║  [N5-11] Motor migrado a SoproCalculationEngine: Multiplicar → Multiply ║
    // ║          (3 sitios), RedondearImporte → RoundAmount (2 sitios),        ║
    // ║          SumarImportes → SumAmounts (1 sitio). CRUD/consultas EF       ║
    // ║          intactos, sin cambios de API ni de comportamiento observable.  ║
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

            LanzarSiHayCicloAscendente(ctx, matrizIdsAfectadas, proyectoId);

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

            // N7-1c: un ciclo de auxiliares alcanzable convergería silenciosamente a
            // costos obsoletos; se diagnostica antes de tocar nada.
            LanzarSiHayCicloAscendente(ctx, matrizIds, proyectoId);

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
                comp.Importe = new SoproCalculationEngine(dec, dec, 4).Multiply(comp.Cantidad, pu);
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
                mat.CostoDirecto = new SoproCalculationEngine(
                    proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje)
                    .RoundAmount(totals.CostoDirectoTotal);
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
                .Where(c => c.MatrizId.HasValue && matrizIds.Contains(c.MatrizId.Value)
                    && proyectoIds.Contains(c.ProyectoId))
                .ToList();

            foreach (var concepto in conceptos)
            {
                var mat = matrices.First(m => m.Id == concepto.MatrizId);
                if (!mat.ProyectoId.HasValue) continue;
                if (!proyectos.TryGetValue(mat.ProyectoId.Value, out var proyecto)) continue;

                var engine = new SoproCalculationEngine(
                    proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje);
                concepto.CostoDirectoUnitario = engine.RoundAmount(mat.CostoDirecto);
                concepto.CostoDirectoTotal    = engine.Multiply(concepto.Cantidad, concepto.CostoDirectoUnitario);

                // Paridad con el recálculo de pantalla (FormPresupuesto.RefrescarPreciosDesdeDB):
                // el Precio Unitario y el Importe Total también deben quedar al día en la
                // propagación headless, sin depender de que el presupuesto esté abierto.
                concepto.PrecioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, concepto.CostoDirectoUnitario);
                concepto.ImporteTotal   = engine.Multiply(concepto.Cantidad, concepto.PrecioUnitario);
            }

            // Paridad con RefrescarPreciosDesdeDB → RecalcularTodosLosTotales:
            // los agrupadores padre se recalculan bottom-up a partir de los
            // totales de sus hijos directos (RecalculoGlobalService.RecalcularAgrupadores).
            ActualizarAgrupadores(ctx, proyectos.Values.ToList());
        }

        /// <summary>
        /// Recalcula los totales de los conceptos agrupadores de cada proyecto
        /// afectado. Paridad exacta con la UI (FormPresupuesto → RecalcularTodosLosTotales
        /// → BudgetHierarchyService.CalculateAggregatorTotal → GridTotales):
        ///
        ///   1) La jerarquía se infiere por Orden/Nivel (la persistencia legacy
        ///      no establece PadreId), pero el nivel semántico de una fila NO
        ///      agrupadora es siempre 5 ("Concepto"), sea cual sea su Nivel
        ///      almacenado: una hoja legacy con Nivel 1 no corta el bloque de un
        ///      subcapítulo de nivel 1.
        ///   2) El total del agrupador es ÚNICO: suma del Importe de las hojas de
        ///      su bloque (el Importe que muestra el grid) y se asigna IGUAL a
        ///      CostoDirectoTotal e ImporteTotal (GridTotales.cs:313-314). La
        ///      pantalla muestra CostoDirectoTotal, así que ambos deben coincidir.
        ///   3) Un agrupador sin hojas queda en cero. Redondeo con el motor del
        ///      proyecto.
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

                var engine = new SoproCalculationEngine(
                    proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje);

                foreach (var agrupador in agrupadores)
                {
                    int idx = todos.FindIndex(c => c.Id == agrupador.Id);
                    var importes = new List<decimal>();

                    for (int j = idx + 1; j < todos.Count; j++)
                    {
                        var fila = todos[j];

                        // Toda fila no agrupadora es un concepto de nivel 5.
                        int nivelFila = fila.EsAgrupador ? fila.Nivel : 5;
                        if (nivelFila <= agrupador.Nivel) break;
                        if (fila.EsAgrupador) continue;

                        importes.Add(fila.ImporteTotal);
                    }

                    decimal total = engine.SumAmounts(importes);
                    agrupador.CostoDirectoTotal = total;
                    agrupador.ImporteTotal       = total;
                    agrupador.FechaModificacion  = DateTime.Now;
                }
            }
        }

        /// <summary>
        /// Detecta ciclos de referencias AuxiliarId alcanzables desde las matrices origen
        /// subiendo por padres, dentro del proyecto de la sesión. Un ciclo haría
        /// que la propagación por niveles converja a costos obsoletos en silencio.
        /// Las referencias externas al proyecto no participan (la propagación nunca
        /// cruza proyectos).
        /// </summary>
        private static void LanzarSiHayCicloAscendente(SOPROContext ctx, List<int> idsOrigen, int? proyectoId)
        {
            var origen = idsOrigen.Where(id => id > 0).Distinct().ToList();
            if (origen.Count == 0) return;

            var aristas = CargarAristasAuxiliar(ctx, proyectoId);

            var estado = new Dictionary<int, int>();
            var ruta = new List<int>();

            foreach (var id in origen)
                VisitarAscendente(id, aristas, estado, ruta);
        }

        /// <summary>
        /// Aristas ascendentes del proyecto: auxiliar referenciado → matrices padre que
        /// lo consumen, solo para componentes de tipo Auxiliar (un AuxiliarId en otro
        /// tipo es dato malformado y no genera dependencias).
        /// </summary>
        private static Dictionary<int, List<int>> CargarAristasAuxiliar(SOPROContext ctx, int? proyectoId)
        {
            IQueryable<ComponenteMatriz> aristasQuery = ctx.ComponentesMatriz
                .Where(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.AuxiliarId != null);
            if (proyectoId.HasValue)
                aristasQuery = aristasQuery.Where(c => c.Matriz.ProyectoId == proyectoId.Value);

            return aristasQuery
                .Select(c => new { c.MatrizId, c.AuxiliarId })
                .ToList()
                .GroupBy(a => a.AuxiliarId!.Value)
                .ToDictionary(g => g.Key, g => g.Select(a => a.MatrizId).Distinct().ToList());
        }

        private static void VisitarAscendente(
            int id,
            Dictionary<int, List<int>> padresPorAuxiliar,
            Dictionary<int, int> estado,
            List<int> ruta)
        {
            if (estado.TryGetValue(id, out var marca))
            {
                if (marca == 1)
                {
                    var inicio = ruta.IndexOf(id);
                    throw new InvalidOperationException(
                        "Ciclo de matrices detectado en la propagación de precios. Ruta: " +
                        string.Join(" -> ", ruta.Skip(inicio).Append(id)));
                }
                return;
            }

            estado[id] = 1;
            ruta.Add(id);
            if (padresPorAuxiliar.TryGetValue(id, out var padres))
                foreach (var padre in padres)
                    VisitarAscendente(padre, padresPorAuxiliar, estado, ruta);
            ruta.RemoveAt(ruta.Count - 1);
            estado[id] = 2;
        }

        /// <summary>
        /// Cierre ascendente (todas las matrices que dependen transitivamente de los
        /// orígenes, solo dentro del proyecto de la sesión) recalculado en orden
        /// topológico: cada padre consume los costos de sus auxiliares ya frescos,
        /// incluidos los DAG convergentes donde un nodo depende de varios niveles a la
        /// vez. Los orígenes ya fueron recalculados por el llamador.
        /// </summary>
        private static void PropagarAuxiliares(SOPROContext ctx, List<int> matrizIdsOrigen, List<Matriz> todasMatrices, int? proyectoId = null)
        {
            var origen = matrizIdsOrigen.Where(id => id > 0).Distinct().ToHashSet();
            if (origen.Count == 0) return;

            var padresPorAuxiliar = CargarAristasAuxiliar(ctx, proyectoId);

            var closure = new HashSet<int>(origen);
            var frente = new Queue<int>(origen);
            while (frente.Count > 0)
            {
                if (!padresPorAuxiliar.TryGetValue(frente.Dequeue(), out var padres)) continue;
                foreach (var padre in padres)
                    if (closure.Add(padre))
                        frente.Enqueue(padre);
            }

            var idsNuevos = closure.Except(origen).ToList();
            if (idsNuevos.Count == 0) return;

            var matricesPadre = CargarMatricesConNavegaciones(ctx, idsNuevos);

            // Resolver navegaciones de auxiliares desde el propio closure: las hojas
            // externas conservan el costo almacenado por Include(c => c.Auxiliar).
            var porId = matricesPadre.ToDictionary(m => m.Id);
            foreach (var mat in matricesPadre)
                foreach (var comp in mat.Componentes.Where(c =>
                             c.TipoComponente == TipoComponenteMatriz.Auxiliar && c.AuxiliarId.HasValue))
                    if (porId.TryGetValue(comp.AuxiliarId!.Value, out var aux))
                        comp.Auxiliar = aux;

            var ordenadas = MatrixGraphOrderService.OrdenTopologico(matricesPadre);
            RecalcularConMotor(ctx, ordenadas);

            todasMatrices.AddRange(matricesPadre);
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
