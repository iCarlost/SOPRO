using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Sopro.Calculation;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services.Programacion
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  ProgramacionInsumosService — v2.0  (Metodología OPUS Planet)          ║
    // ║                                                                         ║
    // ║  METODOLOGÍA (igual que ExplosionInsumosService v2.0):                 ║
    // ║    El importe de cada insumo en cada periodo se obtiene distribuyendo   ║
    // ║    el importe del periodo en proporción al peso de cada componente      ║
    // ║    dentro del CD unitario de la matriz, recalculado al vuelo con los    ║
    // ║    PUs actuales del catálogo y el motor.                                ║
    // ║                                                                         ║
    // ║  [N5-16] Motor migrado a SoproCalculationEngine con las tres precisiones ║
    // ║          del proyecto en Build y 4 helpers (ExplotarCanonicoRecursivo,  ║
    // ║          ExplotarImportesCanonicosPorInsumo, ExplotarMatrizEnPeriodo,   ║
    // ║          CalcularImportesUnitarios): Multiply ×8, RoundAmount ×11,      ║
    // ║          RoundQuantity ×5 (24 operaciones). Sin cambios de API.         ║
    // ║                                                                         ║
    // ║  Para insumos normales:                                                 ║
    // ║    Importe = fuente de verdad                                           ║
    // ║    PU      = catálogo (fijo)                                            ║
    // ║    Cantidad visible = INFERIDA = Importe / PU                          ║
    // ║                                                                         ║
    // ║  Para %MO:                                                              ║
    // ║    Importe = fuente de verdad                                           ║
    // ║    Cantidad = física acumulada                                          ║
    // ║    PU visible = INFERIDO = Importe / Cantidad (promedio ponderado)     ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public sealed class ProgramacionInsumosService
    {
        private sealed class ActividadInsumoFuente
        {
            public int Id { get; set; }
            public int? ConceptoPresupuestoId { get; set; }
            public DateTime? FechaInicioProgramada { get; set; }
            public DateTime? FechaFinProgramada { get; set; }
        }

        private sealed class ConceptoInsumoFuente
        {
            public int Id { get; set; }
            public int MatrizId { get; set; }
            public decimal Cantidad { get; set; }
            public decimal CostoDirectoUnitario { get; set; }
            public decimal CostoDirectoTotal { get; set; }
        }

        // Acumulador interno por insumo y periodo
        private sealed class InsumoAcum
        {
            public string Clave { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;
            public string Unidad { get; set; } = string.Empty;
            public decimal PuFijo { get; set; }
            public bool EsPorcentual { get; set; }
            public decimal RendimientoPonderado { get; set; }
            // ImportesPorPeriodo: importe acumulado por periodo
            public Dictionary<int, decimal> ImportesPorPeriodo { get; } = new();
            // CantFisicaPorPeriodo: cantidad física acumulada por periodo (para %MO y maquinaria)
            public Dictionary<int, decimal> CantFisicaPorPeriodo { get; } = new();
            // ImporteGlobalAcum: importe acumulado global crudo (sin redondear por periodo)
            // Igual que ExplosionInsumosService.ImporteAcumulado — se usa para PU inferido de %MO
            public decimal ImporteGlobalAcum { get; set; }
            // CantFisicaGlobalAcum: cantidad física global acumulada cruda (sin suma de periodos)
            // Igual que ExplosionInsumosService.CantidadFisicaAcumulada
            public decimal CantFisicaGlobalAcum { get; set; }
        }

        // ════════════════════════════════════════════════════════════════════════
        // PUNTO DE ENTRADA
        // ════════════════════════════════════════════════════════════════════════

        public ProgramaInsumosResultDto Build(SOPROContext context, Proyecto proyecto,
            ProgramaInsumoTipo tipo)
        {
            var engine = new SoproCalculationEngine(proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje);

            var programa = context.ProgramasObra
                .AsNoTracking()
                .FirstOrDefault(p => p.ProyectoId == proyecto.Id && p.Activo);

            var result = new ProgramaInsumosResultDto
            {
                Tipo = tipo,
                NombrePrograma = programa?.Nombre ?? string.Empty
            };

            if (programa == null) return result;

            var periodos = context.PeriodosPrograma
                .AsNoTracking()
                .Where(p => p.ProgramaObraId == programa.Id)
                .OrderBy(p => p.NumeroPeriodo)
                .Select(p => new ProgramaInsumoPeriodoDto
                {
                    PeriodoId = p.Id,
                    Orden     = p.NumeroPeriodo,
                    Etiqueta  = string.IsNullOrWhiteSpace(p.Etiqueta)
                        ? $"P{p.NumeroPeriodo:00}" : p.Etiqueta
                })
                .ToList();

            result.Periodos = periodos;
            if (periodos.Count == 0) return result;

            var actividades = context.ActividadesProgramadas
                .AsNoTracking()
                .Where(a => a.ProgramaObraId == programa.Id
                         && !a.EsResumen
                         && a.ConceptoPresupuestoId != null)
                .Select(a => new ActividadInsumoFuente
                {
                    Id                    = a.Id,
                    ConceptoPresupuestoId = a.ConceptoPresupuestoId,
                    FechaInicioProgramada = a.FechaInicioProgramada,
                    FechaFinProgramada    = a.FechaFinProgramada
                })
                .ToList();

            if (actividades.Count == 0) return result;

            var actividadIds = actividades.Select(a => a.Id).ToList();
            var conceptoIds  = actividades
                .Where(a => a.ConceptoPresupuestoId.HasValue)
                .Select(a => a.ConceptoPresupuestoId!.Value)
                .Distinct().ToList();

            var conceptosInfo = context.ConceptosPresupuesto
                .AsNoTracking()
                .Where(c => conceptoIds.Contains(c.Id) && c.MatrizId != null)
                .Select(c => new ConceptoInsumoFuente
                {
                    Id                   = c.Id,
                    MatrizId             = c.MatrizId!.Value,
                    Cantidad             = c.Cantidad,
                    CostoDirectoUnitario = c.CostoDirectoUnitario,
                    CostoDirectoTotal    = c.CostoDirectoTotal
                })
                .ToDictionary(x => x.Id, x => x);

            var matrizIds = conceptosInfo.Values.Select(x => x.MatrizId).Distinct().ToList();
            if (matrizIds.Count == 0) return result;

            var matrices   = CargarMatricesConAuxiliares(context, matrizIds);
            var matrizPorId = matrices.GroupBy(m => m.Id)
                                      .Select(g => g.First())
                                      .ToDictionary(m => m.Id);

            foreach (var mat in matrizPorId.Values)
                foreach (var comp in mat.Componentes.Where(c => c.AuxiliarId.HasValue))
                    if (matrizPorId.TryGetValue(comp.AuxiliarId!.Value, out var aux))
                        comp.Auxiliar = aux;

            var actividadConcepto = actividades.ToDictionary(
                a => a.Id, a => a.ConceptoPresupuestoId!.Value);

            var distribuciones = context.DistribucionesPeriodo
                .AsNoTracking()
                .Where(d => actividadIds.Contains(d.ActividadProgramadaId))
                .Select(d => new
                {
                    d.ActividadProgramadaId,
                    d.PeriodoProgramaId,
                    d.CantidadProgramada,
                    d.ImporteProgramado
                })
                .ToList();

            var rangosPorInsumo = ConstruirRangosPorInsumo(actividades, conceptosInfo, matrizPorId, tipo);
            var usoPorInsumo    = ConstruirDondeSeUsaPorInsumo(actividades, conceptosInfo, matrizPorId, tipo);

            // ── Acumulación por insumo y periodo — con reconciliación por insumo ──
            // Metodología:
            //   1. Por cada concepto, calcular el importe canónico de cada insumo
            //      (igual que ExplosionInsumosService — una sola vez para el concepto completo).
            //   2. Distribuir por periodos en un acumulador temporal.
            //   3. Al terminar todos los periodos del concepto, comparar por insumo:
            //      residuo = canónico[insumo] - Σ(periodos del insumo)
            //      Si |residuo| < 0.05, ajustar el último periodo del insumo.
            //   4. Fusionar el temporal ya reconciliado al acumulador global.
            var acums = new Dictionary<int, InsumoAcum>();

            // Agrupar distribuciones por concepto
            var distsPorConcepto = new Dictionary<int, List<(int periodoId, decimal cantProgramada)>>();
            foreach (var dist in distribuciones)
            {
                if (!actividadConcepto.TryGetValue(dist.ActividadProgramadaId, out var cid)) continue;
                if (!distsPorConcepto.ContainsKey(cid))
                    distsPorConcepto[cid] = new List<(int, decimal)>();
                distsPorConcepto[cid].Add((dist.PeriodoProgramaId, dist.CantidadProgramada));
            }

            foreach (var (conceptoId, dists) in distsPorConcepto)
            {
                if (!conceptosInfo.TryGetValue(conceptoId, out var conceptoInfo)) continue;
                if (!matrizPorId.TryGetValue(conceptoInfo.MatrizId, out var matriz)) continue;

                decimal cantConcepto = conceptoInfo.Cantidad;
                if (cantConcepto <= 0m) continue;

                decimal cdUnit = conceptoInfo.CostoDirectoUnitario;
                if (cdUnit <= 0m && conceptoInfo.CostoDirectoTotal > 0m)
                    cdUnit = conceptoInfo.CostoDirectoTotal / cantConcepto;

                // ── Paso 1: importe canónico por insumo (igual que Explosión) ─────
                decimal importeConceptoTotal = engine.Multiply(cantConcepto, cdUnit);
                var canonicoPorInsumo = ExplotarImportesCanonicosPorInsumo(
                    engine, matriz, importeConceptoTotal, cantConcepto, tipo);

                if (canonicoPorInsumo.Count == 0) continue;

                // ── Paso 2: acumular temporalmente por insumo y periodo ───────────
                // temporal: insumoId → (periodoId → importe)
                var temporal = new Dictionary<int, InsumoAcum>();
                // último periodo con importe por insumo — para ajuste de reconciliación
                var ultimoPeriodoPorInsumo = new Dictionary<int, int>();

                decimal importePeriodosAcum = 0m;
                for (int di = 0; di < dists.Count; di++)
                {
                    var (periodoId, cantProgramada) = dists[di];
                    bool esUltimoPeriodo = (di == dists.Count - 1);

                    decimal importePeriodo = engine.Multiply(cantProgramada, cdUnit);
                    if (importePeriodo == 0m) continue;

                    importePeriodosAcum += importePeriodo;

                    // Reconciliar importe total del concepto en el último periodo
                    if (esUltimoPeriodo)
                    {
                        decimal residuoConcepto = importeConceptoTotal - importePeriodosAcum;
                        if (Math.Abs(residuoConcepto) < 0.05m && residuoConcepto != 0m)
                            importePeriodo += residuoConcepto;
                    }

                    ExplotarMatrizEnPeriodo(engine, matriz, importePeriodo,
                        cantProgramada, periodoId, tipo, temporal);

                    // Rastrear último periodo con importe por insumo
                    foreach (var insumoId in temporal.Keys)
                    {
                        temporal[insumoId].ImportesPorPeriodo.TryGetValue(periodoId, out decimal impCheck);
                        // [N7-2] !=0 en lugar de >0: un importe negativo también es "último
                        // periodo con importe" y debe poder absorber el residuo de
                        // reconciliación (línea 343/354 siguen el mismo criterio).
                        if (impCheck != 0m)
                            ultimoPeriodoPorInsumo[insumoId] = periodoId;
                    }
                }

                // ── Paso 3: reconciliar por insumo contra canónico ───────────────
                foreach (var (insumoId, acumTemp) in temporal)
                {
                    if (!canonicoPorInsumo.TryGetValue(insumoId, out decimal importeCanónico)) continue;
                    if (!ultimoPeriodoPorInsumo.TryGetValue(insumoId, out int ultimoPer)) continue;

                    decimal sumaPrograma = acumTemp.ImportesPorPeriodo.Values.Sum();
                    decimal residuoInsumo = importeCanónico - sumaPrograma;

                    if (Math.Abs(residuoInsumo) < 0.05m && residuoInsumo != 0m)
                    {
                        acumTemp.ImportesPorPeriodo.TryGetValue(ultimoPer, out decimal impUlt);
                        acumTemp.ImportesPorPeriodo[ultimoPer] = impUlt + residuoInsumo;
                        acumTemp.ImporteGlobalAcum += residuoInsumo;
                    }
                }

                // ── Paso 4: fusionar temporal al acumulador global ────────────────
                foreach (var (insumoId, acumTemp) in temporal)
                {
                    if (!acums.TryGetValue(insumoId, out var acumGlobal))
                    {
                        acumGlobal = new InsumoAcum
                        {
                            Clave        = acumTemp.Clave,
                            Descripcion  = acumTemp.Descripcion,
                            Unidad       = acumTemp.Unidad,
                            PuFijo       = acumTemp.PuFijo,
                            EsPorcentual = acumTemp.EsPorcentual
                        };
                        acums[insumoId] = acumGlobal;
                    }

                    foreach (var (periodoId, imp) in acumTemp.ImportesPorPeriodo)
                    {
                        acumGlobal.ImportesPorPeriodo.TryGetValue(periodoId, out decimal impAnt);
                        acumGlobal.ImportesPorPeriodo[periodoId] = impAnt + imp;
                    }
                    foreach (var (periodoId, cant) in acumTemp.CantFisicaPorPeriodo)
                    {
                        acumGlobal.CantFisicaPorPeriodo.TryGetValue(periodoId, out decimal cantAnt);
                        acumGlobal.CantFisicaPorPeriodo[periodoId] = cantAnt + cant;
                    }
                    acumGlobal.ImporteGlobalAcum    += acumTemp.ImporteGlobalAcum;
                    acumGlobal.CantFisicaGlobalAcum += acumTemp.CantFisicaGlobalAcum;
                    acumGlobal.RendimientoPonderado += acumTemp.RendimientoPonderado;
                }
            }

            // ── Construir filas de resultado con cantidad inferida ────────────────
            var salida = new Dictionary<int, ProgramaInsumoRowDto>();

            foreach (var kvp in acums)
            {
                var src = kvp.Value;
                var row = new ProgramaInsumoRowDto
                {
                    InsumoId       = kvp.Key,
                    Clave          = src.Clave,
                    Descripcion    = src.Descripcion,
                    Unidad         = src.Unidad,
                    PrecioUnitario = src.PuFijo
                };

                if (rangosPorInsumo.TryGetValue(kvp.Key, out var rango))
                {
                    row.FechaInicio = rango.inicio;
                    row.FechaFin    = rango.fin;
                }
                if (usoPorInsumo.TryGetValue(kvp.Key, out var dondeSeUsa))
                    row.DondeSeUsa = string.Join(", ", dondeSeUsa);

                decimal runningImporte  = 0m;
                decimal runningCantidad = 0m;

                // Importe objetivo = valor que produce la explosión para este insumo
                decimal importeGlobalObjetivo = engine.RoundAmount(src.ImporteGlobalAcum);

                // Último periodo con importe — ahí se absorbe el residuo.
                // [N7-2] !=0: un importe negativo también marca "último periodo con
                // importe"; con >0 se perdía la absorción del residuo en esa fila.
                var periodosOrdenados = periodos.OrderBy(p => p.Orden).ToList();
                int idxUltimo = -1;
                for (int pi = periodosOrdenados.Count - 1; pi >= 0; pi--)
                {
                    src.ImportesPorPeriodo.TryGetValue(periodosOrdenados[pi].PeriodoId, out decimal chk);
                    if (chk != 0m) { idxUltimo = pi; break; }
                }

                for (int pi = 0; pi < periodosOrdenados.Count; pi++)
                {
                    var per = periodosOrdenados[pi];
                    // ── Importe del periodo ───────────────────────────────────────
                    src.ImportesPorPeriodo.TryGetValue(per.PeriodoId, out decimal importeRaw);
                    decimal importePer = engine.RoundAmount(importeRaw);

                    // Reconciliar en el último periodo si el residuo es centavo de redondeo
                    if (pi == idxUltimo && importePer != 0m)
                    {
                        decimal residuo = importeGlobalObjetivo - (runningImporte + importePer);
                        if (residuo != 0m && Math.Abs(residuo) < 0.05m)
                            importePer = engine.RoundAmount(importeGlobalObjetivo - runningImporte);
                    }

                    row.ImportesPorPeriodo[per.PeriodoId] = importePer;

                    runningImporte += importePer;
                    row.ImportesAcumuladosPorPeriodo[per.PeriodoId] =
                        engine.RoundAmount(runningImporte);

                    // ── Cantidad del periodo ───────────────────────────────────────
                    decimal cantPer;
                    if (src.EsPorcentual)
                    {
                        // %MO: cantidad física acumulada
                        src.CantFisicaPorPeriodo.TryGetValue(per.PeriodoId, out decimal cantFis);
                        cantPer = engine.RoundQuantity(cantFis);
                    }
                    else if (src.PuFijo > 0m)
                    {
                        // Normal: cantidad INFERIDA = importePeriodo / PU_catálogo
                        cantPer = engine.RoundQuantity(importePer / src.PuFijo);
                    }
                    else
                    {
                        // Fallback: física acumulada si PU = 0
                        src.CantFisicaPorPeriodo.TryGetValue(per.PeriodoId, out decimal cantFis);
                        cantPer = engine.RoundQuantity(cantFis);
                    }

                    row.CantidadesPorPeriodo[per.PeriodoId] = cantPer;
                    runningCantidad = engine.RoundQuantity(runningCantidad + cantPer);
                    row.AcumuladosPorPeriodo[per.PeriodoId] = runningCantidad;
                }

                row.ImporteTotal = importeGlobalObjetivo;

                // Para %MO: Total = cantidad física global acumulada (mismo criterio que Explosión)
                // Para normales: Total = suma de cantidades inferidas por periodo (ya en runningCantidad)
                if (src.EsPorcentual)
                {
                    // Usar acumulados globales crudos — mismo criterio exacto que Explosión:
                    //   ExplosionInsumosService: ImporteAcumulado / CantidadFisicaAcumulada
                    //   ProgramacionInsumos:     ImporteGlobalAcum / CantFisicaGlobalAcum
                    decimal cantGlobal = src.CantFisicaGlobalAcum;
                    row.Total = engine.RoundQuantity(cantGlobal);
                    if (cantGlobal > 0m)
                        row.PrecioUnitario = engine.RoundAmount(src.ImporteGlobalAcum / cantGlobal);
                }
                else
                {
                    row.Total = runningCantidad;
                }

                // Rendimiento ponderado para maquinaria
                if (tipo == ProgramaInsumoTipo.Maquinaria && row.Total > 0m)
                    row.Rendimiento = Math.Round(src.RendimientoPonderado / row.Total, 5,
                        MidpointRounding.AwayFromZero);

                salida[kvp.Key] = row;
            }

            result.Rows = salida.Values
                .OrderBy(r => r.Clave).ThenBy(r => r.Descripcion)
                .ToList();
            return result;
        }

        // ════════════════════════════════════════════════════════════════════════
        // EXPLOSIÓN DE MATRIZ EN UN PERIODO
        // ════════════════════════════════════════════════════════════════════════

        private static void ExplotarMatrizEnPeriodo(
            SoproCalculationEngine engine, Matriz matriz,
            decimal importeBase,    // importe del periodo a distribuir
            decimal cantidadBase,   // cantidad del concepto en el periodo (para física %MO)
            int periodoId, ProgramaInsumoTipo tipo,
            Dictionary<int, InsumoAcum> acums)
        {
            if (matriz?.Componentes == null || matriz.Componentes.Count == 0) return;

            // Recalcular importes unitarios frescos con PUs actuales y el engine
            var importesUnitarios = CalcularImportesUnitarios(engine, matriz.Componentes);
            decimal cdUnitMatriz  = importesUnitarios.Values.Sum();
            if (cdUnitMatriz == 0m) return;

            // Distribuir con reconciliación — igual que ExplosionInsumosService.ExplotarMatriz
            // para que Σ impComp == importeBase exactamente (absorbe residuo de redondeo)
            var distribComp = new List<(ComponenteMatriz comp, decimal impComp)>();
            decimal sumaDistribuida = 0m;
            foreach (var comp in matriz.Componentes)
            {
                if (!importesUnitarios.TryGetValue(comp.Id, out decimal impUnit)) continue;
                if (impUnit == 0m) continue;
                decimal impComp = engine.RoundAmount(importeBase * impUnit / cdUnitMatriz);
                if (impComp == 0m) continue;
                distribComp.Add((comp, impComp));
                sumaDistribuida += impComp;
            }

            // Absorber residuo en el último componente no-auxiliar elegible
            decimal residuoComp = engine.RoundAmount(importeBase - sumaDistribuida);
            if (residuoComp != 0m && distribComp.Count > 0)
            {
                int idxAjuste = distribComp.FindLastIndex(
                    t => t.comp.TipoComponente != TipoComponenteMatriz.Auxiliar);
                if (idxAjuste < 0) idxAjuste = distribComp.Count - 1;
                var (compAj, impAj) = distribComp[idxAjuste];
                distribComp[idxAjuste] = (compAj, impAj + residuoComp);
            }

            foreach (var (comp, impComp) in distribComp)
            {
                if (impComp == 0m) continue;

                // Cantidad física del componente en este periodo
                decimal cantFis = cantidadBase * comp.Cantidad;

                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material
                        when tipo == ProgramaInsumoTipo.Materiales
                          && comp.Material != null && comp.MaterialId.HasValue:
                        AcumularInsumo(acums, comp.MaterialId.Value,
                            comp.Material.Clave, comp.Material.Descripcion,
                            comp.Material.Unidad, comp.Material.PrecioUnitario,
                            esPorcentual: false, periodoId, impComp, cantFis, 0m);
                        break;

                    case TipoComponenteMatriz.ManoDeObra
                        when tipo == ProgramaInsumoTipo.ManoDeObra
                          && comp.ManoDeObra != null && comp.ManoDeObraId.HasValue:
                        bool esPctMO = comp.ManoDeObra.EsPorcentajeMO;
                        decimal puMO = esPctMO ? 0m
                            : (comp.ManoDeObra.SalarioReal > 0m
                                ? comp.ManoDeObra.SalarioReal
                                : comp.ManoDeObra.SalarioBase);
                        AcumularInsumo(acums, comp.ManoDeObraId.Value,
                            comp.ManoDeObra.Clave, comp.ManoDeObra.Descripcion,
                            comp.ManoDeObra.Unidad, puMO,
                            esPorcentual: esPctMO, periodoId, impComp, cantFis, 0m);
                        break;

                    case TipoComponenteMatriz.Maquinaria
                        when tipo == ProgramaInsumoTipo.Maquinaria
                          && comp.Maquinaria != null && comp.MaquinariaId.HasValue:
                        AcumularInsumo(acums, comp.MaquinariaId.Value,
                            comp.Maquinaria.Clave, comp.Maquinaria.Descripcion,
                            "hr", comp.Maquinaria.CostoHorario,
                            esPorcentual: false, periodoId, impComp, cantFis,
                            rendimiento: comp.Rendimiento);
                        break;

                    case TipoComponenteMatriz.Herramienta
                        when tipo == ProgramaInsumoTipo.Herramienta
                          && comp.Herramienta != null && comp.HerramientaId.HasValue:
                        bool esPctHer = comp.Herramienta.EsPorcentajeMO;
                        decimal puHer = esPctHer ? 0m : comp.Herramienta.PrecioUnitario;
                        AcumularInsumo(acums, comp.HerramientaId.Value,
                            comp.Herramienta.Clave, comp.Herramienta.Descripcion,
                            comp.Herramienta.Unidad, puHer,
                            esPorcentual: esPctHer, periodoId, impComp, cantFis, 0m);
                        break;

                    case TipoComponenteMatriz.Auxiliar when comp.Auxiliar != null:
                        ExplotarMatrizEnPeriodo(engine, comp.Auxiliar, impComp, cantFis,
                                               periodoId, tipo, acums);
                        break;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // IMPORTE CANÓNICO POR INSUMO — igual que ExplosionInsumosService
        // Devuelve insumoId → importe del concepto completo para ese insumo
        // ════════════════════════════════════════════════════════════════════════

        private static Dictionary<int, decimal> ExplotarImportesCanonicosPorInsumo(
            SoproCalculationEngine engine, Matriz matriz,
            decimal importeBase, decimal cantidadBase,
            ProgramaInsumoTipo tipo)
        {
            var resultado = new Dictionary<int, decimal>();
            ExplotarCanonicoRecursivo(engine, matriz, importeBase, cantidadBase, tipo, resultado);
            return resultado;
        }

        private static void ExplotarCanonicoRecursivo(
            SoproCalculationEngine engine, Matriz matriz,
            decimal importeBase, decimal cantidadBase,
            ProgramaInsumoTipo tipo,
            Dictionary<int, decimal> resultado)
        {
            if (matriz?.Componentes == null || matriz.Componentes.Count == 0) return;

            var importesUnitarios = CalcularImportesUnitarios(engine, matriz.Componentes);
            decimal cdUnitMatriz  = importesUnitarios.Values.Sum();
            if (cdUnitMatriz == 0m) return;

            // Misma distribución con reconciliación que ExplotarMatrizEnPeriodo
            var distribComp = new List<(ComponenteMatriz comp, decimal impComp)>();
            decimal sumaDistribuida = 0m;
            foreach (var comp in matriz.Componentes)
            {
                if (!importesUnitarios.TryGetValue(comp.Id, out decimal impUnit)) continue;
                if (impUnit == 0m) continue;
                decimal impComp = engine.RoundAmount(importeBase * impUnit / cdUnitMatriz);
                if (impComp == 0m) continue;
                distribComp.Add((comp, impComp));
                sumaDistribuida += impComp;
            }

            decimal residuo = engine.RoundAmount(importeBase - sumaDistribuida);
            if (residuo != 0m && distribComp.Count > 0)
            {
                int idx = distribComp.FindLastIndex(
                    t => t.comp.TipoComponente != TipoComponenteMatriz.Auxiliar);
                if (idx < 0) idx = distribComp.Count - 1;
                var (cAj, iAj) = distribComp[idx];
                distribComp[idx] = (cAj, iAj + residuo);
            }

            foreach (var (comp, impComp) in distribComp)
            {
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material
                        when tipo == ProgramaInsumoTipo.Materiales
                          && comp.MaterialId.HasValue:
                        resultado.TryGetValue(comp.MaterialId.Value, out decimal mAnt);
                        resultado[comp.MaterialId.Value] = mAnt + impComp;
                        break;

                    case TipoComponenteMatriz.ManoDeObra
                        when tipo == ProgramaInsumoTipo.ManoDeObra
                          && comp.ManoDeObraId.HasValue:
                        resultado.TryGetValue(comp.ManoDeObraId.Value, out decimal moAnt);
                        resultado[comp.ManoDeObraId.Value] = moAnt + impComp;
                        break;

                    case TipoComponenteMatriz.Maquinaria
                        when tipo == ProgramaInsumoTipo.Maquinaria
                          && comp.MaquinariaId.HasValue:
                        resultado.TryGetValue(comp.MaquinariaId.Value, out decimal mqAnt);
                        resultado[comp.MaquinariaId.Value] = mqAnt + impComp;
                        break;

                    case TipoComponenteMatriz.Herramienta
                        when tipo == ProgramaInsumoTipo.Herramienta
                          && comp.HerramientaId.HasValue:
                        resultado.TryGetValue(comp.HerramientaId.Value, out decimal hAnt);
                        resultado[comp.HerramientaId.Value] = hAnt + impComp;
                        break;

                    case TipoComponenteMatriz.Auxiliar when comp.Auxiliar != null:
                        ExplotarCanonicoRecursivo(engine, comp.Auxiliar, impComp,
                            cantidadBase * comp.Cantidad, tipo, resultado);
                        break;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // CÁLCULO DE IMPORTES UNITARIOS FRESCOS
        // (misma lógica que ExplosionInsumosService.CalcularImportesUnitarios)
        // ════════════════════════════════════════════════════════════════════════

        private static Dictionary<int, decimal> CalcularImportesUnitarios(
            SoproCalculationEngine engine, ICollection<ComponenteMatriz> componentes)
        {
            var resultado = new Dictionary<int, decimal>();

            // Paso A: MO normal + cuadrillas → calcula baseMO
            decimal baseMO = 0m;
            foreach (var comp in componentes)
            {
                decimal imp = 0m;
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra
                    && comp.ManoDeObra != null && !comp.ManoDeObra.EsPorcentajeMO)
                {
                    imp = engine.Multiply(comp.Cantidad, comp.ManoDeObra.SalarioReal);
                    baseMO += imp;
                    // [N7-2] Negativos conservados (paridad con ExplosionInsumosService
                    // y con la canónica). Solo se registran los componentes realmente
                    // calculados en este paso; el resto lo calcula el paso B (los ceros
                    // se descartan aguas abajo con impUnit == 0m, sin efecto observable).
                    resultado[comp.Id] = imp;
                }
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar
                         && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla
                         && comp.Auxiliar != null)
                {
                    imp = engine.Multiply(comp.Cantidad, comp.Auxiliar.CostoDirecto);
                    baseMO += imp;
                    resultado[comp.Id] = imp;
                }
            }

            // Paso B: resto de componentes con baseMO conocido
            foreach (var comp in componentes)
            {
                if (resultado.ContainsKey(comp.Id)) continue;

                decimal imp = comp.TipoComponente switch
                {
                    TipoComponenteMatriz.Material when comp.Material != null =>
                        engine.Multiply(comp.Cantidad, comp.Material.PrecioUnitario),

                    TipoComponenteMatriz.Maquinaria when comp.Maquinaria != null =>
                        engine.Multiply(comp.Cantidad, comp.Maquinaria.CostoHorario),

                    TipoComponenteMatriz.ManoDeObra when comp.ManoDeObra?.EsPorcentajeMO == true =>
                        engine.RoundAmount(comp.Cantidad * baseMO),

                    TipoComponenteMatriz.Herramienta when comp.Herramienta != null =>
                        comp.Herramienta.EsPorcentajeMO
                            ? engine.RoundAmount(comp.Cantidad * baseMO)
                            : engine.Multiply(comp.Cantidad, comp.Herramienta.PrecioUnitario),

                    TipoComponenteMatriz.Auxiliar when comp.Auxiliar != null
                        && comp.Auxiliar.Tipo != TipoMatriz.Cuadrilla =>
                        engine.Multiply(comp.Cantidad, comp.Auxiliar.CostoDirecto),

                    _ => 0m
                };

                // [N7-2] Ver nota del paso A.
                resultado[comp.Id] = imp;
            }

            return resultado;
        }

        // ════════════════════════════════════════════════════════════════════════
        // ACUMULADOR UNIFICADO
        // ════════════════════════════════════════════════════════════════════════

        private static void AcumularInsumo(
            Dictionary<int, InsumoAcum> acums, int id,
            string clave, string descripcion, string unidad,
            decimal puFijo, bool esPorcentual,
            int periodoId, decimal impComp, decimal cantFis, decimal rendimiento)
        {
            if (!acums.TryGetValue(id, out var acum))
            {
                acum = new InsumoAcum
                {
                    Clave        = clave       ?? string.Empty,
                    Descripcion  = descripcion ?? string.Empty,
                    Unidad       = unidad      ?? string.Empty,
                    PuFijo       = puFijo,
                    EsPorcentual = esPorcentual
                };
                acums[id] = acum;
            }

            // Importe acumulado por periodo
            acum.ImportesPorPeriodo.TryGetValue(periodoId, out decimal impAnt);
            acum.ImportesPorPeriodo[periodoId] = impAnt + impComp;
            // Importe global crudo — mismo criterio que ExplosionInsumosService
            acum.ImporteGlobalAcum += impComp;
            // Cantidad física global cruda — mismo criterio que ExplosionInsumosService
            acum.CantFisicaGlobalAcum += cantFis;

            // Cantidad física acumulada por periodo (usada para %MO y fallback)
            acum.CantFisicaPorPeriodo.TryGetValue(periodoId, out decimal cantAnt);
            acum.CantFisicaPorPeriodo[periodoId] = cantAnt + cantFis;

            // Rendimiento ponderado para maquinaria
            if (rendimiento > 0m)
                acum.RendimientoPonderado += rendimiento * cantFis;
        }

        // ════════════════════════════════════════════════════════════════════════
        // CARGA DE MATRICES CON AUXILIARES RECURSIVOS
        // ════════════════════════════════════════════════════════════════════════

        private static List<Matriz> CargarMatricesConAuxiliares(
            SOPROContext context, List<int> matrizIds)
        {
            var matrices = context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrizIds.Contains(m.Id))
                .AsNoTracking()
                .ToList();

            var cargadas  = new HashSet<int>(matrices.Select(m => m.Id));
            var faltantes = matrices
                .SelectMany(m => m.Componentes)
                .Where(c => c.AuxiliarId.HasValue)
                .Select(c => c.AuxiliarId!.Value)
                .Where(id => !cargadas.Contains(id))
                .ToHashSet();

            while (faltantes.Count > 0)
            {
                var extra = context.Matrices
                    .Include(m => m.Componentes).ThenInclude(c => c.Material)
                    .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                    .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                    .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                    .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .Where(m => faltantes.Contains(m.Id))
                    .AsNoTracking()
                    .ToList();

                if (extra.Count == 0) break;
                matrices.AddRange(extra);
                foreach (var id in extra.Select(m => m.Id)) cargadas.Add(id);

                faltantes = extra
                    .SelectMany(m => m.Componentes)
                    .Where(c => c.AuxiliarId.HasValue)
                    .Select(c => c.AuxiliarId!.Value)
                    .Where(id => !cargadas.Contains(id))
                    .ToHashSet();
            }

            return matrices;
        }

        // ════════════════════════════════════════════════════════════════════════
        // HELPERS DE RANGO Y DONDE-SE-USA
        // ════════════════════════════════════════════════════════════════════════

        private static Dictionary<int, (DateTime inicio, DateTime fin)> ConstruirRangosPorInsumo(
            List<ActividadInsumoFuente> actividades,
            Dictionary<int, ConceptoInsumoFuente> conceptosInfo,
            Dictionary<int, Matriz> matrizPorId,
            ProgramaInsumoTipo tipo)
        {
            var result = new Dictionary<int, (DateTime, DateTime)>();
            foreach (var act in actividades)
            {
                if (!act.ConceptoPresupuestoId.HasValue) continue;
                if (!conceptosInfo.TryGetValue(act.ConceptoPresupuestoId.Value, out var info)) continue;
                if (!matrizPorId.TryGetValue(info.MatrizId, out var matriz)) continue;

                var inicio = act.FechaInicioProgramada ?? DateTime.MinValue;
                var fin    = act.FechaFinProgramada    ?? inicio;

                foreach (var insumoId in ObtenerInsumoIds(matriz, tipo))
                {
                    if (result.TryGetValue(insumoId, out var rango))
                        result[insumoId] = (inicio < rango.Item1 ? inicio : rango.Item1,
                                            fin    > rango.Item2 ? fin    : rango.Item2);
                    else
                        result[insumoId] = (inicio, fin);
                }
            }
            return result;
        }

        private static Dictionary<int, List<string>> ConstruirDondeSeUsaPorInsumo(
            List<ActividadInsumoFuente> actividades,
            Dictionary<int, ConceptoInsumoFuente> conceptosInfo,
            Dictionary<int, Matriz> matrizPorId,
            ProgramaInsumoTipo tipo)
        {
            var result = new Dictionary<int, List<string>>();
            foreach (var act in actividades)
            {
                if (!act.ConceptoPresupuestoId.HasValue) continue;
                if (!conceptosInfo.TryGetValue(act.ConceptoPresupuestoId.Value, out var info)) continue;
                if (!matrizPorId.TryGetValue(info.MatrizId, out var matriz)) continue;

                foreach (var insumoId in ObtenerInsumoIds(matriz, tipo))
                {
                    if (!result.ContainsKey(insumoId))
                        result[insumoId] = new List<string>();
                    var clave = matriz.Clave ?? string.Empty;
                    if (!result[insumoId].Contains(clave))
                        result[insumoId].Add(clave);
                }
            }
            return result;
        }

        private static IEnumerable<int> ObtenerInsumoIds(Matriz matriz, ProgramaInsumoTipo tipo)
        {
            foreach (var comp in matriz.Componentes)
            {
                if (tipo == ProgramaInsumoTipo.Materiales
                    && comp.TipoComponente == TipoComponenteMatriz.Material
                    && comp.MaterialId.HasValue)
                    yield return comp.MaterialId.Value;

                else if (tipo == ProgramaInsumoTipo.ManoDeObra
                    && comp.TipoComponente == TipoComponenteMatriz.ManoDeObra
                    && comp.ManoDeObraId.HasValue)
                    yield return comp.ManoDeObraId.Value;

                else if (tipo == ProgramaInsumoTipo.Maquinaria
                    && comp.TipoComponente == TipoComponenteMatriz.Maquinaria
                    && comp.MaquinariaId.HasValue)
                    yield return comp.MaquinariaId.Value;

                else if (tipo == ProgramaInsumoTipo.Herramienta
                    && comp.TipoComponente == TipoComponenteMatriz.Herramienta
                    && comp.HerramientaId.HasValue)
                    yield return comp.HerramientaId.Value;

                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar
                    && comp.Auxiliar != null)
                    foreach (var id in ObtenerInsumoIds(comp.Auxiliar, tipo))
                        yield return id;
            }
        }
    }
}
