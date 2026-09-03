using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Explosion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using Sopro.Calculation;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  ExplosionInsumosService — v2.0  (Metodología OPUS Planet)             ║
    // ║                                                                         ║
    // ║  METODOLOGÍA:                                                           ║
    // ║  El importe de cada insumo en la explosión se obtiene distribuyendo     ║
    // ║  el importe del concepto (ya redondeado con el motor) en proporción     ║
    // ║  al peso de cada componente dentro del CD unitario de la matriz,        ║
    // ║  recalculado en el momento con los PUs actuales del catálogo.           ║
    // ║                                                                         ║
    // ║  Para insumos normales:                                                 ║
    // ║    Importe = acumulado de proporciones                                  ║
    // ║    PU      = del catálogo (fijo)                                        ║
    // ║    Cantidad = INFERIDA = Importe / PU                                   ║
    // ║                                                                         ║
    // ║  Para insumos %MO (PU variable por APU):                               ║
    // ║    Importe  = acumulado de proporciones                                 ║
    // ║    Cantidad = acumulada físicamente (comp.Cantidad × cant.concepto)     ║
    // ║    PU       = INFERIDO = Importe / Cantidad  (promedio ponderado)       ║
    // ║                                                                         ║
    // ║  Garantía: Σ importes explosión ≈ CD presupuesto                       ║
    // ║  (diferencia residual = solo el redondeo de proporciones)              ║
    // ║                                                                         ║
    // ║  [N7-3] Los importes unitarios por componente se calculan con la ruta   ║
    // ║  canónica compartida MatrixComponentCalculationService.ImportesUnitarios║
    // ║  (evaluador puro del grafo); la distribución y acumulación permanecen   ║
    // ║  en este servicio.                                                     ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    public sealed class ExplosionInsumosService
    {
        // ════════════════════════════════════════════════════════════════════════
        // ENTRADA PRINCIPAL
        // ════════════════════════════════════════════════════════════════════════

        public ExplosionCalculationResult Calculate(
            SOPROContext context, int proyectoId, string filtro)
        {
            var proyecto = context.Proyectos
                .AsNoTracking()
                .FirstOrDefault(p => p.Id == proyectoId);

            if (proyecto == null)
                return new ExplosionCalculationResult();

            var engine = new SoproCalculationEngine(
                proyecto.DecimalesCantidad,
                proyecto.DecimalesImporte,
                proyecto.DecimalesPorcentaje);
            var formatter = new MotorCalculoSopro(proyecto);

            var conceptos = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == proyectoId && !c.EsAgrupador && c.MatrizId != null)
                .AsNoTracking()
                .ToList();

            if (conceptos.Count == 0)
                return new ExplosionCalculationResult();

            // Cargar TODAS las matrices del proyecto con sus navegaciones en una sola query
            var todasMatrices = context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .ThenInclude(a => a.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .ThenInclude(a => a.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .ThenInclude(a => a.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                    .ThenInclude(a => a.Componentes).ThenInclude(c => c.Herramienta)
                .Where(m => m.ProyectoId == proyectoId)
                .AsNoTracking()
                .ToList();

            var matrizPorId = todasMatrices.ToDictionary(m => m.Id);

            // Resolver referencias de auxiliares anidados
            foreach (var mat in todasMatrices)
                foreach (var comp in mat.Componentes)
                    if (comp.AuxiliarId.HasValue &&
                        matrizPorId.TryGetValue(comp.AuxiliarId.Value, out var aux))
                        comp.Auxiliar = aux;

            // Asignar matrices a conceptos
            foreach (var concepto in conceptos)
                if (concepto.MatrizId.HasValue &&
                    matrizPorId.TryGetValue(concepto.MatrizId.Value, out var mat))
                    concepto.Matriz = mat;

            // ── Acumuladores por tipo de insumo ──────────────────────────────────
            var materiales   = new Dictionary<int, InsumoAcum>();
            var manoObra     = new Dictionary<int, InsumoAcum>();
            var maquinaria   = new Dictionary<int, InsumoAcum>();
            var herramientas = new Dictionary<int, InsumoAcum>();

            foreach (var concepto in conceptos)
            {
                if (concepto.Matriz == null) continue;

                // Importe del concepto con precisión de pantalla
                decimal importeConcepto = engine.Multiply(
                    concepto.Cantidad, concepto.CostoDirectoUnitario);

                if (importeConcepto == 0m) continue;

                ExplotarMatriz(engine, concepto.Matriz, importeConcepto, concepto.Cantidad,
                               materiales, manoObra, maquinaria, herramientas);
            }

            var result = new ExplosionCalculationResult();
            decimal puBaseManoObraSinPorcentuales = manoObra.Values
                .Where(i => !i.EsPorcentual)
                .Sum(i => i.ImporteAcumulado);

            TransferirAResultado(result.Materiales,   materiales);
            TransferirAResultado(result.ManoObra,     manoObra, puBaseManoObraSinPorcentuales);
            TransferirAResultado(result.Maquinaria,   maquinaria);
            TransferirAResultado(result.Herramientas, herramientas, puBaseManoObraSinPorcentuales);

            // Total explosión = suma de todos los importes acumulados
            result.CostoDirectoTotal =
                engine.SumAmounts(result.Materiales.Values.Select(i => i.Cantidad))
              + engine.SumAmounts(result.ManoObra.Values.Select(i => i.Cantidad))
              + engine.SumAmounts(result.Maquinaria.Values.Select(i => i.Cantidad))
              + engine.SumAmounts(result.Herramientas.Values.Select(i => i.Cantidad));

            // CD del presupuesto (para mostrar diferencia residual)
            result.CostoDirectoPresupuesto = engine.SumDirectCost(conceptos.Select(c => new DirectCostLine(
                c.Cantidad,
                c.CostoDirectoUnitario,
                c.EsAgrupador,
                c.MatrizId.HasValue)));

            result.Rows = BuildRows(
                result,
                filtro ?? "Todos",
                engine,
                formatter.FormatCantidad,
                formatter.FormatImporte);
            return result;
        }

        // ════════════════════════════════════════════════════════════════════════
        // EXPLOSIÓN RECURSIVA DE UNA MATRIZ
        // ════════════════════════════════════════════════════════════════════════

        private static void ExplotarMatriz(
            SoproCalculationEngine engine,
            Matriz matriz,
            decimal importeBase,       // importe del concepto (o auxiliar padre) a distribuir
            decimal cantidadBase,      // cantidad del concepto (para acumular cantidad física)
            Dictionary<int, InsumoAcum> materiales,
            Dictionary<int, InsumoAcum> manoObra,
            Dictionary<int, InsumoAcum> maquinaria,
            Dictionary<int, InsumoAcum> herramientas)
        {
            if (matriz?.Componentes == null || matriz.Componentes.Count == 0) return;

            // ── Paso 1: recalcular los importes unitarios de cada componente
            //           con la ruta canónica del grafo (N7-3)                    ────
            var importesUnitarios = MatrixComponentCalculationService
                .ImportesUnitarios(matriz.Componentes, engine.AmountDecimals);

            decimal cdUnitarioMatriz = importesUnitarios.Values.Sum();
            if (cdUnitarioMatriz == 0m) return;  // matriz vacía o sin PUs

            // ── Paso 2: distribuir importeBase con reconciliación final ─────────
            // Distribución proporcional canónica compartida (N7-4): residuo absorbido
            // en el último componente no auxiliar.
            var distribComp = MatrixComponentCalculationService.DistribuirImporteProporcional(
                engine, importesUnitarios, matriz.Componentes, importeBase, cdUnitarioMatriz);

            foreach (var (comp, importeComp) in distribComp)
            {
                // Cantidad física: cantidadBase × cantidad del componente en la receta
                decimal cantidadFisica = cantidadBase * comp.Cantidad;

                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material when comp.Material != null && comp.MaterialId.HasValue:
                        Acumular(materiales, comp.MaterialId.Value,
                            comp.Material.Clave, comp.Material.Descripcion,
                            comp.Material.Unidad, importeComp, cantidadFisica,
                            puFijo: comp.Material.PrecioUnitario,
                            esPorcentual: false);
                        break;

                    case TipoComponenteMatriz.ManoDeObra when comp.ManoDeObra != null && comp.ManoDeObraId.HasValue:
                        Acumular(manoObra, comp.ManoDeObraId.Value,
                            comp.ManoDeObra.Clave, comp.ManoDeObra.Descripcion,
                            comp.ManoDeObra.Unidad, importeComp, cantidadFisica,
                            puFijo: comp.ManoDeObra.EsPorcentajeMO ? 0m : comp.ManoDeObra.SalarioReal,
                            esPorcentual: comp.ManoDeObra.EsPorcentajeMO);
                        break;

                    case TipoComponenteMatriz.Maquinaria when comp.Maquinaria != null && comp.MaquinariaId.HasValue:
                        Acumular(maquinaria, comp.MaquinariaId.Value,
                            comp.Maquinaria.Clave, comp.Maquinaria.Descripcion,
                            "hr", importeComp, cantidadFisica,
                            puFijo: comp.Maquinaria.CostoHorario,
                            esPorcentual: false);
                        break;

                    case TipoComponenteMatriz.Herramienta when comp.Herramienta != null && comp.HerramientaId.HasValue:
                        Acumular(herramientas, comp.HerramientaId.Value,
                            comp.Herramienta.Clave, comp.Herramienta.Descripcion,
                            comp.Herramienta.Unidad, importeComp, cantidadFisica,
                            puFijo: comp.Herramienta.EsPorcentajeMO ? 0m : comp.Herramienta.PrecioUnitario,
                            esPorcentual: comp.Herramienta.EsPorcentajeMO);
                        break;

                    case TipoComponenteMatriz.Auxiliar when comp.Auxiliar != null:
                        // Recursión: el auxiliar recibe su porción del importe
                        // cantidadFisica pasa como cantidadBase para el nivel siguiente
                        ExplotarMatriz(engine, comp.Auxiliar, importeComp, cantidadFisica,
                                       materiales, manoObra, maquinaria, herramientas);
                        break;
                }
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // ACUMULADOR DE INSUMOS
        // ════════════════════════════════════════════════════════════════════════

        private static void Acumular(
            Dictionary<int, InsumoAcum> dic, int key,
            string clave, string descripcion, string unidad,
            decimal importeRedondeado,
            decimal cantidadFisica,
            decimal puFijo,
            bool esPorcentual)
        {
            if (dic.TryGetValue(key, out var existing))
            {
                existing.ImporteAcumulado += importeRedondeado;
                existing.CantidadFisicaAcumulada += cantidadFisica;
            }
            else
            {
                dic[key] = new InsumoAcum
                {
                    Clave                  = clave        ?? string.Empty,
                    Descripcion            = descripcion   ?? string.Empty,
                    Unidad                 = unidad        ?? string.Empty,
                    ImporteAcumulado       = importeRedondeado,
                    CantidadFisicaAcumulada = cantidadFisica,
                    PuFijo                 = puFijo,
                    EsPorcentual           = esPorcentual
                };
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // TRANSFERIR AL MODELO DE SALIDA (con cantidad e inferencias)
        // ════════════════════════════════════════════════════════════════════════

        private static void TransferirAResultado(
            Dictionary<int, ExplosionInsumoAccumulated> destino,
            Dictionary<int, InsumoAcum> origen,
            decimal? puBasePorcentual = null)
        {
            foreach (var kvp in origen)
            {
                var src = kvp.Value;

                // Cantidad del reporte:
                //   Normal:     INFERIDA = ImporteAcumulado / PU_catálogo
                //   Porcentual: acumulada físicamente (no tiene sentido inferir sin PU fijo)
                decimal cantidadReporte;
                decimal puReporte;

                if (src.EsPorcentual)
                {
                    // Para insumos %MO, el P.U. mostrado debe representar la base total
                    // de mano de obra sin considerar los propios insumos %MO.
                    cantidadReporte = src.CantidadFisicaAcumulada;
                    puReporte = puBasePorcentual ?? 0m;
                }
                else
                {
                    // Cantidad inferida = ImporteAcumulado / PU_catálogo
                    puReporte = src.PuFijo;
                    cantidadReporte = puReporte > 0m
                        ? src.ImporteAcumulado / puReporte
                        : src.CantidadFisicaAcumulada;  // fallback si PU es 0
                }

                // Nota sobre campos de ExplosionInsumoAccumulated:
                //   Cantidad      → IMPORTE acumulado (lo que usa la UI como total)
                //   CantidadFisica → CANTIDAD del reporte:
                //                    • Normal:     INFERIDA = ImporteAcumulado / PU_catálogo
                //                    • Porcentual: FÍSICA acumulada (PU es inferido)
                destino[kvp.Key] = new ExplosionInsumoAccumulated
                {
                    Clave          = src.Clave,
                    Descripcion    = src.Descripcion,
                    Unidad         = src.Unidad,
                    Cantidad       = src.ImporteAcumulado,   // importe total
                    CantidadFisica = cantidadReporte,         // inferida (normal) o física (%MO)
                    PrecioUnitario = puReporte,               // fijo (normal) o inferido (%MO)
                    EsPorcentual   = src.EsPorcentual
                };
            }
        }

        // ════════════════════════════════════════════════════════════════════════
        // CONSTRUCCIÓN DE FILAS DE DISPLAY
        // ════════════════════════════════════════════════════════════════════════

        private static List<ExplosionRowDisplay> BuildRows(
            ExplosionCalculationResult data,
            string filtro,
            SoproCalculationEngine engine,
            Func<decimal, string> formatCantidad,
            Func<decimal, string> formatImporte)
        {
            var rows = new List<ExplosionRowDisplay>();

            if (filtro == "Todos" || filtro == "Materiales")
                AddSection(rows, "MATERIALES",   data.Materiales,   data.CostoDirectoTotal, engine, formatCantidad, formatImporte);
            if (filtro == "Todos" || filtro == "Mano de Obra")
                AddSection(rows, "MANO DE OBRA", data.ManoObra,     data.CostoDirectoTotal, engine, formatCantidad, formatImporte);
            if (filtro == "Todos" || filtro == "Herramientas")
                AddSection(rows, "HERRAMIENTAS", data.Herramientas, data.CostoDirectoTotal, engine, formatCantidad, formatImporte);
            if (filtro == "Todos" || filtro == "Maquinaria")
                AddSection(rows, "MAQUINARIA",   data.Maquinaria,   data.CostoDirectoTotal, engine, formatCantidad, formatImporte);

            if (filtro == "Todos")
            {
                rows.Add(new ExplosionRowDisplay { Kind = ExplosionRowKind.Vacia });
                rows.Add(new ExplosionRowDisplay
                {
                    Kind         = ExplosionRowKind.TotalGeneral,
                    Descripcion  = "TOTAL DEL REPORTE",
                    ImporteTexto = formatImporte(data.CostoDirectoTotal),
                    Porcentaje   = 1.0m
                });

                decimal diferencia = data.CostoDirectoTotal - data.CostoDirectoPresupuesto;
                decimal pctDif = data.CostoDirectoPresupuesto > 0m
                    ? diferencia / data.CostoDirectoPresupuesto
                    : 0m;

                rows.Add(new ExplosionRowDisplay
                {
                    Kind         = ExplosionRowKind.Referencia,
                    Descripcion  = "Costo Directo (Presupuesto)",
                    ImporteTexto = formatImporte(data.CostoDirectoPresupuesto),
                    Porcentaje   = 0m
                });
                rows.Add(new ExplosionRowDisplay
                {
                    Kind         = ExplosionRowKind.Referencia,
                    Descripcion  = "Diferencia por redondeo",
                    ImporteTexto = formatImporte(diferencia),
                    Porcentaje   = pctDif
                });
            }

            return rows;
        }

        private static void AddSection(
            List<ExplosionRowDisplay> rows,
            string titulo,
            Dictionary<int, ExplosionInsumoAccumulated> dic,
            decimal costoDirectoTotal,
            SoproCalculationEngine engine,
            Func<decimal, string> formatCantidad,
            Func<decimal, string> formatImporte)
        {
            if (dic.Count == 0) return;

            rows.Add(new ExplosionRowDisplay
            {
                Kind        = ExplosionRowKind.Encabezado,
                Descripcion = titulo
            });

            foreach (var kvp in dic.OrderBy(x => x.Value.Clave))
            {
                var ins     = kvp.Value;
                // ins.Cantidad = ImporteAcumulado (campo reutilizado para compatibilidad)
                decimal importe = ins.Cantidad;
                decimal pct     = costoDirectoTotal > 0m ? importe / costoDirectoTotal : 0m;

                // Usar el flag EsPorcentual del modelo — no re-detectar por heurísticas
                bool esPct = ins.EsPorcentual;

                // Normal:     CantidadFisica = inferida, PrecioUnitario = fijo del catálogo
                // Porcentual: CantidadFisica = física acumulada, PrecioUnitario = inferido
                string cantTexto = esPct ? "—" : formatCantidad(ins.CantidadFisica);
                string puTexto   = esPct
                    ? (ins.PrecioUnitario > 0m ? formatImporte(ins.PrecioUnitario) : "—")
                    : formatImporte(ins.PrecioUnitario);

                rows.Add(new ExplosionRowDisplay
                {
                    Kind                = ExplosionRowKind.Detalle,
                    Clave               = ins.Clave,
                    Descripcion         = ins.Descripcion,
                    Unidad              = ins.Unidad,
                    CantidadTexto       = cantTexto,
                    PrecioUnitarioTexto = puTexto,
                    ImporteTexto        = formatImporte(importe),
                    Porcentaje          = pct
                });
            }

            decimal totalSeccion = engine.SumAmounts(dic.Values.Select(i => i.Cantidad));
            rows.Add(new ExplosionRowDisplay
            {
                Kind         = ExplosionRowKind.Total,
                Descripcion  = $"TOTAL {titulo}",
                ImporteTexto = formatImporte(totalSeccion),
                Porcentaje   = costoDirectoTotal > 0m ? totalSeccion / costoDirectoTotal : 0m
            });
            rows.Add(new ExplosionRowDisplay { Kind = ExplosionRowKind.Vacia });
        }

        // ════════════════════════════════════════════════════════════════════════
        // CLASE INTERNA DE ACUMULACIÓN (privada, solo en este servicio)
        // ════════════════════════════════════════════════════════════════════════

        private sealed class InsumoAcum
        {
            public string  Clave                   { get; set; } = string.Empty;
            public string  Descripcion             { get; set; } = string.Empty;
            public string  Unidad                  { get; set; } = string.Empty;
            public decimal ImporteAcumulado         { get; set; }
            public decimal CantidadFisicaAcumulada  { get; set; }
            public decimal PuFijo                   { get; set; }
            public bool    EsPorcentual             { get; set; }
        }
    }
}
