using System;
using SOPRO.Application.Services.Programacion;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using Sopro.Calculation;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║     RECÁLCULO GLOBAL — v1.0                                             ║
    // ║     Invoca el motor de cálculo en el orden correcto para actualizar      ║
    // ║     todo el presupuesto cuando el usuario cambia los decimales           ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Servicio de recálculo integral del proyecto.
    ///
    /// CUÁNDO INVOCAR:
    ///   - Al guardar cambios en DecimalesCantidad o DecimalesImporte desde
    ///     el formulario de configuración del proyecto.
    ///   - Opcionalmente, mediante un botón "Recalcular todo" en FormProyecto.
    ///
    /// ORDEN DE EJECUCIÓN (dependencias de arriba hacia abajo):
    ///   1. Componentes de Matrices  → importes individuales por componente
    ///   2. Matrices                 → CostoDirecto = suma de componentes
    ///   3. Conceptos hoja           → CostoDirectoUnitario / PrecioUnitario / Importes
    ///   4. Agrupadores              → CostoDirectoTotal = suma hijos (bottom-up)
    ///   5. Distribuciones           → importes por periodo con Ajuste de Residuo
    ///
    /// NOTAS DE ARQUITECTURA:
    ///   - Cada fase hace su propio SaveChanges para mantener consistencia parcial.
    ///   - Si el proceso es interrumpido, los datos quedan en estado válido
    ///     hasta la fase interrumpida.
    ///   - El motor se construye con el proyecto DESPUÉS de guardar la nueva
    ///     configuración de decimales (ya leída de BD).
    /// </summary>
    public sealed class RecalculoGlobalService
    {
        // ════════════════════════════════════════════════════════════════════════
        // PUNTO DE ENTRADA PRINCIPAL
        // ════════════════════════════════════════════════════════════════════════

        /// <summary>
        /// Recalcula todo el presupuesto del proyecto usando la configuración
        /// de decimales actualmente persistida en la BD.
        ///
        /// Retorna un resumen de cuántos registros fueron actualizados en cada fase.
        /// </summary>
        public RecalculoGlobalResultado Ejecutar(SOPROContext ctx, int proyectoId)
        {
            var resultado = new RecalculoGlobalResultado();

            var proyecto = ctx.Proyectos.Find(proyectoId)
                ?? throw new InvalidOperationException($"Proyecto {proyectoId} no encontrado.");

            var engine = new SoproCalculationEngine(
                proyecto.DecimalesCantidad,
                proyecto.DecimalesImporte,
                proyecto.DecimalesPorcentaje);
            var pct = BuildPercentageInput(proyecto);

            // Fase 1: Componentes de matrices
            resultado.ComponentesActualizados = RecalcularComponentesMatrices(ctx, proyectoId, engine);
            ctx.SaveChanges();

            // Fase 2: CostoDirecto de matrices (suma de componentes ya corregidos)
            resultado.MatricesActualizadas = RecalcularCostoDirectoMatrices(ctx, proyectoId, engine);
            ctx.SaveChanges();

            // Fase 3: Conceptos hoja del presupuesto
            resultado.ConceptosActualizados = RecalcularConceptosHoja(ctx, proyecto, engine, pct);
            ctx.SaveChanges();

            // Fase 4: Agrupadores bottom-up
            resultado.AgrupadoresTotalesRecalculados = RecalcularAgrupadores(ctx, proyectoId, engine);
            ctx.SaveChanges();

            // Fase 5: Distribuciones del programa de obra
            resultado.DistribucionesActualizadas = RecalcularDistribuciones(ctx, proyecto, engine);
            ctx.SaveChanges();

            // Fase 6: Programa de obra — recalcular fechas, ruta crítica y redistribuir
            resultado.ProgramaActualizado = RecalcularProgramaObra(ctx, proyecto.Id);
            // SaveChanges ya lo hace RecalcularProgramaObra internamente

            return resultado;
        }

        // ════════════════════════════════════════════════════════════════════════
        // FASE 1: COMPONENTES DE MATRICES
        // ════════════════════════════════════════════════════════════════════════

        private static int RecalcularComponentesMatrices(SOPROContext ctx, int proyectoId, SoproCalculationEngine engine)
        {
            // Cargamos todos los componentes de todas las matrices del proyecto,
            // incluyendo las relaciones necesarias para saber el P.U. del insumo.
            var matrices = ctx.Matrices
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Material)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Auxiliar)
                .Where(m => m.ProyectoId == proyectoId)
                .ToList();

            // Resolver referencias a auxiliares internas desde el propio conjunto
            // (hojas externas conservan su costo almacenado).
            MatrixGraphOrderService.ResolverAuxiliaresInternos(matrices);

            int total = 0;

            // Orden topológico real por dependencias de auxiliares internas (N7-1c):
            // cada auxiliar referenciada dentro del proyecto se recalcula antes que sus
            // padres, incluidos los básicos anidados. Un ciclo lanza diagnóstico con
            // ruta en lugar de converger silenciosamente a costos obsoletos.
            var ordenadas = MatrixGraphOrderService.OrdenTopologico(matrices);

            foreach (var matriz in ordenadas)
            {
                // Usamos el servicio existente — ya recibe decimalesImporte como parámetro ✅
                var totals = MatrixComponentCalculationService.Recalculate(
                    matriz.Componentes.ToList(), engine.AmountDecimals);

                matriz.CostoDirecto = engine.RoundAmount(totals.CostoDirectoTotal);
                total += matriz.Componentes.Count;
            }

            return total;
        }

        // ════════════════════════════════════════════════════════════════════════
        // FASE 2: COSTODIRECTO DE MATRICES
        // ════════════════════════════════════════════════════════════════════════

        private static int RecalcularCostoDirectoMatrices(SOPROContext ctx, int proyectoId, SoproCalculationEngine engine)
        {
            // Después del SaveChanges de fase 1, los componentes ya tienen Importe correcto.
            // Recalculamos el CostoDirecto de cada matriz como suma de sus componentes.
            var matrices = ctx.Matrices
                .Include(m => m.Componentes)
                .Where(m => m.ProyectoId == proyectoId)
                .ToList();

            foreach (var m in matrices)
                m.CostoDirecto = engine.SumAmounts(m.Componentes.Select(c => c.Importe));

            return matrices.Count;
        }

        // ════════════════════════════════════════════════════════════════════════
        // FASE 3: CONCEPTOS HOJA
        // ════════════════════════════════════════════════════════════════════════

        private static int RecalcularConceptosHoja(SOPROContext ctx, Proyecto proyecto,
            SoproCalculationEngine engine, PricePercentageInput pct)
        {
            var conceptos = ctx.ConceptosPresupuesto
                .Include(c => c.Matriz)
                .Where(c => c.ProyectoId == proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue)
                .ToList();

            foreach (var c in conceptos)
            {
                if (c.Matriz == null) continue;

                // CD unitario = CostoDirecto de la matriz (ya recalculado en Fase 2)
                c.CostoDirectoUnitario = engine.RoundAmount(c.Matriz.CostoDirecto);
                c.CostoDirectoTotal    = engine.Multiply(c.Cantidad, c.CostoDirectoUnitario);

                // Precio Unitario con cascada de porcentajes redondeada
                var desglose = engine.CalculateUnitPrice(c.CostoDirectoUnitario, pct);
                c.PrecioUnitario = desglose.UnitPrice;
                c.ImporteTotal   = engine.Multiply(c.Cantidad, c.PrecioUnitario);

                c.FechaModificacion = DateTime.Now;
            }

            return conceptos.Count;
        }

        // ════════════════════════════════════════════════════════════════════════
        // FASE 4: AGRUPADORES (bottom-up)
        // ════════════════════════════════════════════════════════════════════════

        private static int RecalcularAgrupadores(SOPROContext ctx, int proyectoId, SoproCalculationEngine engine)
        {
            // Cargar toda la jerarquía del presupuesto
            var todos = ctx.ConceptosPresupuesto
                .Where(c => c.ProyectoId == proyectoId)
                .OrderByDescending(c => c.Nivel) // De más profundo a más superficial
                .ToList();

            var porId = todos.ToDictionary(c => c.Id);

            int actualizados = 0;
            var agrupadores  = todos.Where(c => c.EsAgrupador).ToList();

            // Procesar por nivel descendente (hijos antes que padres)
            foreach (var agrupador in agrupadores)
            {
                // Sumar los CostoDirectoTotal de los hijos directos
                var hijos = todos.Where(c => c.PadreId == agrupador.Id).ToList();

                decimal totalCD     = engine.SumAmounts(hijos.Select(h => h.CostoDirectoTotal));
                decimal totalImporte = engine.SumAmounts(hijos.Select(h => h.ImporteTotal));

                agrupador.CostoDirectoTotal = totalCD;
                agrupador.ImporteTotal       = totalImporte;
                agrupador.FechaModificacion  = DateTime.Now;
                actualizados++;
            }

            return actualizados;
        }

        // ════════════════════════════════════════════════════════════════════════
        // FASE 5: DISTRIBUCIONES DEL PROGRAMA DE OBRA
        // ════════════════════════════════════════════════════════════════════════

        private static int RecalcularDistribuciones(SOPROContext ctx, Proyecto proyecto, SoproCalculationEngine engine)
        {
            var actividades = ctx.ActividadesProgramadas
                .Include(a => a.Distribuciones)
                .Where(a => a.ProgramaObra.ProyectoId == proyecto.Id)
                .ToList();

            int actualizadas = 0;

            foreach (var act in actividades)
            {
                if (act.Distribuciones == null || act.Distribuciones.Count == 0) continue;

                // Recalcular importe total de la actividad con la nueva precisión
                act.ImporteProgramado = engine.Multiply(act.CantidadTotal, act.PrecioUnitario);

                // Usar los porcentajes existentes como pesos para redistribuir el importe total
                var distribsOrdenadas = act.Distribuciones
                    .OrderBy(d => d.PeriodoProgramaId)
                    .ToList();

                var pesos    = distribsOrdenadas.Select(d => d.PorcentajeProgramado).ToList();
                var importes = engine.DistributeAmount(act.ImporteProgramado, pesos);
                var cantidades = engine.DistributeQuantity(act.CantidadTotal,
                    distribsOrdenadas.Select(d => d.PorcentajeProgramado).ToList());

                for (int i = 0; i < distribsOrdenadas.Count; i++)
                {
                    distribsOrdenadas[i].ImporteProgramado  = importes[i];
                    distribsOrdenadas[i].CantidadProgramada = cantidades[i];
                }

                act.FechaModificacion = DateTime.Now;
                actualizadas += distribsOrdenadas.Count;
            }

            return actualizadas;
        }

        // ════════════════════════════════════════════════════════════════════════
        // FASE 6: PROGRAMA DE OBRA
        // ════════════════════════════════════════════════════════════════════════

        private static bool RecalcularProgramaObra(SOPROContext ctx, int proyectoId)
        {
            var programa = ctx.ProgramasObra
                .AsNoTracking()
                .FirstOrDefault(p => p.ProyectoId == proyectoId && p.Activo);

            if (programa == null) return false;

            var calcSvc = new ProgramacionCalculationService();
            var distSvc = new ProgramacionDistributionService();

            calcSvc.RecalculateProgram(ctx, programa.Id);
            distSvc.DistributeUniformBatch(ctx, programa.Id);
            calcSvc.RecalculateProgram(ctx, programa.Id); // actualizar agrupadores post-distribución

            return true;
        }

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS
        // ════════════════════════════════════════════════════════════════════════

        private static PricePercentageInput BuildPercentageInput(Proyecto proyecto)
            => BudgetPercentageInput.FromProyecto(proyecto).ToPricePercentage();
    }

    // ════════════════════════════════════════════════════════════════════════════
    // RESULTADO DEL RECÁLCULO
    // ════════════════════════════════════════════════════════════════════════════

    public sealed class RecalculoGlobalResultado
    {
        public int  ComponentesActualizados          { get; set; }
        public int  MatricesActualizadas             { get; set; }
        public int  ConceptosActualizados            { get; set; }
        public int  AgrupadoresTotalesRecalculados   { get; set; }
        public int  DistribucionesActualizadas       { get; set; }
        public bool ProgramaActualizado              { get; set; }

        public int TotalRegistros =>
            ComponentesActualizados + MatricesActualizadas +
            ConceptosActualizados  + AgrupadoresTotalesRecalculados +
            DistribucionesActualizadas;

        public string ResumenTexto =>
            $"Recálculo completado: " +
            $"{ComponentesActualizados} componentes, " +
            $"{MatricesActualizadas} matrices, " +
            $"{ConceptosActualizados} conceptos, " +
            $"{AgrupadoresTotalesRecalculados} agrupadores, " +
            $"{DistribucionesActualizadas} distribuciones. " +
            $"Total: {TotalRegistros} registros actualizados.";
    }
}
