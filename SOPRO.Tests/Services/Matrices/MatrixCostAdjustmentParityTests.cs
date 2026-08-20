using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Matrices;

[TestClass]
public class MatrixCostAdjustmentParityTests
{
    // ── N5-9: única operación del motor del servicio (RedondearCantidad en
    //    ApplyFactor) migró a SoproCalculationEngine.RoundQuantity.
    //    Paridad exacta contra referencias que replican el flujo completo de
    //    los tres métodos públicos con la fachada (oráculo diferencial) +
    //    comparación profunda del estado de los componentes.

    [TestMethod]
    public void GetAdjustmentBasis_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(987654321);

        for (int iter = 0; iter < 400; iter++)
        {
            var proyecto = CrearProyecto(rnd.Next(0, 5), rnd.Next(0, 7), rnd.Next(0, 8));
            var specs = CrearSpecs(rnd);
            var matriz = ConstruirMatriz(specs);
            var matrizRef = ConstruirMatriz(specs);
            var scopes = ScopeAleatorio(rnd);

            var servicio = MatrixCostAdjustmentService.GetAdjustmentBasis(matriz, proyecto, scopes);
            var referencia = GetAdjustmentBasisConFachada(matrizRef, proyecto, scopes);

            Assert.AreEqual(referencia == null, servicio == null, $"iter={iter} null");
            if (referencia == null)
                continue;

            Assert.AreEqual(referencia.CurrentCost, servicio!.CurrentCost, $"iter={iter} CurrentCost");
            Assert.AreEqual(referencia.AdjustableCost, servicio.AdjustableCost, $"iter={iter} AdjustableCost");
            Assert.AreEqual(referencia.MinCost, servicio.MinCost, $"iter={iter} MinCost");
            Assert.AreEqual(referencia.FixedCost, servicio.FixedCost, $"iter={iter} FixedCost");
        }
    }

    [TestMethod]
    public void AdjustMatrixByFactor_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(13572468);

        for (int iter = 0; iter < 300; iter++)
        {
            var proyecto = CrearProyecto(rnd.Next(0, 5), rnd.Next(0, 7), rnd.Next(0, 8));
            var specs = CrearSpecs(rnd);
            var matriz = ConstruirMatriz(specs);
            var matrizRef = ConstruirMatriz(specs);
            var scopes = ScopeAleatorio(rnd);
            decimal factor = iter % 10 == 0 ? -rnd.Next(1, 5) / 100m : rnd.Next(0, 300) / 100m;

            var servicio = MatrixCostAdjustmentService.AdjustMatrixByFactor(matriz, proyecto, scopes, factor);
            var referencia = AdjustMatrixByFactorConFachada(matrizRef, proyecto, scopes, factor);

            CompararResultados(servicio, referencia, $"iter={iter}");
            Assert.AreEqual(matrizRef.CostoDirecto, matriz.CostoDirecto, $"iter={iter} CostoDirecto");
            CompararComponentes(matrizRef.Componentes, matriz.Componentes, $"iter={iter}");
        }
    }

    [TestMethod]
    public void AdjustMatrixByTargetCost_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(1123581321);

        for (int iter = 0; iter < 300; iter++)
        {
            var proyecto = CrearProyecto(rnd.Next(0, 5), rnd.Next(0, 7), rnd.Next(0, 8));
            var specs = CrearSpecs(rnd);
            var matriz = ConstruirMatriz(specs);
            var matrizRef = ConstruirMatriz(specs);
            var scopes = ScopeAleatorio(rnd);

            decimal actual = matriz.Componentes.Sum(c => c.Importe);
            decimal target = actual * (0.3m + (decimal)rnd.NextDouble() * 1.4m);

            var servicio = MatrixCostAdjustmentService.AdjustMatrixByTargetCost(matriz, proyecto, scopes, target);
            var referencia = AdjustMatrixByTargetCostConFachada(matrizRef, proyecto, scopes, target);

            CompararResultados(servicio, referencia, $"iter={iter}");
            Assert.AreEqual(matrizRef.CostoDirecto, matriz.CostoDirecto, $"iter={iter} CostoDirecto");
            CompararComponentes(matrizRef.Componentes, matriz.Componentes, $"iter={iter}");
        }
    }

    // ── Referencias compuestas con la fachada (código pre-migración) ──────────

    private static MatrixAdjustmentBasis? GetAdjustmentBasisConFachada(
        Matriz matrix, Proyecto proyecto, MatrixAdjustmentScopes scopes)
    {
        if (matrix.Componentes == null || matrix.Componentes.Count == 0) return null;
        if (scopes == MatrixAdjustmentScopes.None) return null;

        var componentes = matrix.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
        var motor = new MotorCalculoSopro(proyecto);
        var originales = componentes.ToDictionary(c => c, c => c.Cantidad);
        var ajustables = componentes.Where(c => IsAdjustable(c, scopes)).ToList();
        if (ajustables.Count == 0)
        {
            RestoreOriginalQuantities(componentes, originales);
            MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
            return null;
        }

        var currentCost = EvaluateCostConFachada(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
        var adjustableCurrentAmount = ajustables.Sum(c => c.Importe);
        var minCost = EvaluateCostConFachada(componentes, originales, 0m, motor, proyecto.DecimalesImporte, scopes);
        RestoreOriginalQuantities(componentes, originales);
        MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);

        return new MatrixAdjustmentBasis
        {
            CurrentCost = currentCost,
            AdjustableCost = adjustableCurrentAmount,
            MinCost = minCost
        };
    }

    private static MatrixCostAdjustmentResult AdjustMatrixByFactorConFachada(
        Matriz matrix, Proyecto proyecto, MatrixAdjustmentScopes scopes, decimal factor)
    {
        if (matrix.Componentes == null || matrix.Componentes.Count == 0)
            return FailConFachada("La matriz no tiene componentes para reajustar.");

        if (scopes == MatrixAdjustmentScopes.None)
            return FailConFachada("Seleccione al menos un rubro a reajustar.");

        if (factor < 0m)
            return FailConFachada("El factor de reajuste no puede ser negativo.");

        var componentes = matrix.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
        var motor = new MotorCalculoSopro(proyecto);
        var originales = componentes.ToDictionary(c => c, c => c.Cantidad);
        var ajustables = componentes.Where(c => IsAdjustable(c, scopes)).ToList();
        if (ajustables.Count == 0)
        {
            RestoreOriginalQuantities(componentes, originales);
            MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
            return FailConFachada("La matriz no contiene componentes ajustables para los rubros seleccionados.");
        }

        var currentCost = EvaluateCostConFachada(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
        ApplyFactorConFachada(componentes, originales, factor, motor, proyecto.DecimalesImporte, scopes);
        var achieved = componentes.Sum(c => c.Importe);
        matrix.CostoDirecto = achieved;
        return new MatrixCostAdjustmentResult
        {
            Success = true,
            CurrentCost = currentCost,
            TargetCost = 0m,
            AchievedCost = achieved,
            FactorApplied = factor
        };
    }

    private static MatrixCostAdjustmentResult AdjustMatrixByTargetCostConFachada(
        Matriz matrix, Proyecto proyecto, MatrixAdjustmentScopes scopes, decimal targetCost)
    {
        if (matrix.Componentes == null || matrix.Componentes.Count == 0)
            return FailConFachada("La matriz no tiene componentes para reajustar.");

        if (scopes == MatrixAdjustmentScopes.None)
            return FailConFachada("Seleccione al menos un rubro a reajustar.");

        if (targetCost <= 0m)
            return FailConFachada("El monto objetivo debe ser mayor que cero.");

        var componentes = matrix.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
        var motor = new MotorCalculoSopro(proyecto);
        var originales = componentes.ToDictionary(c => c, c => c.Cantidad);
        var ajustables = componentes.Where(c => IsAdjustable(c, scopes)).ToList();

        if (ajustables.Count == 0)
        {
            RestoreOriginalQuantities(componentes, originales);
            MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
            return FailConFachada("La matriz no contiene componentes ajustables para los rubros seleccionados.");
        }

        var currentCost = EvaluateCostConFachada(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
        var adjustableCurrentAmount = ajustables.Sum(c => c.Importe);
        if (adjustableCurrentAmount <= 0m)
        {
            RestoreOriginalQuantities(componentes, originales);
            MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
            return FailConFachada("Los rubros seleccionados no tienen importe ajustable dentro de la matriz.", currentCost, targetCost, currentCost);
        }

        var minCost = EvaluateCostConFachada(componentes, originales, 0m, motor, proyecto.DecimalesImporte, scopes);

        if (Math.Abs(targetCost - currentCost) <= 0.01m)
        {
            ApplyFactorConFachada(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
            matrix.CostoDirecto = currentCost;
            return new MatrixCostAdjustmentResult
            {
                Success = true,
                CurrentCost = currentCost,
                TargetCost = targetCost,
                AchievedCost = currentCost,
                FactorApplied = 1m,
                Message = "La matriz ya se encuentra en el monto solicitado."
            };
        }

        if (targetCost < minCost - 0.01m)
        {
            RestoreOriginalQuantities(componentes, originales);
            MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
            return FailConFachada($"No es posible bajar la matriz al monto solicitado con los rubros seleccionados. El mínimo alcanzable es {minCost:C2}.", currentCost, targetCost, minCost);
        }

        decimal low, high;
        if (targetCost < currentCost)
        {
            low = 0m;
            high = 1m;
        }
        else
        {
            low = 1m;
            high = 2m;
            var highCost = EvaluateCostConFachada(componentes, originales, high, motor, proyecto.DecimalesImporte, scopes);
            int guard = 0;
            while (highCost < targetCost && guard < 20)
            {
                high *= 2m;
                highCost = EvaluateCostConFachada(componentes, originales, high, motor, proyecto.DecimalesImporte, scopes);
                guard++;
            }
        }

        decimal bestFactor = 1m;
        decimal bestCost = currentCost;
        decimal bestDiff = Math.Abs(bestCost - targetCost);

        for (int i = 0; i < 40; i++)
        {
            var mid = (low + high) / 2m;
            var midCost = EvaluateCostConFachada(componentes, originales, mid, motor, proyecto.DecimalesImporte, scopes);
            var diff = Math.Abs(midCost - targetCost);
            if (diff < bestDiff)
            {
                bestDiff = diff;
                bestCost = midCost;
                bestFactor = mid;
            }

            if (midCost == targetCost)
                break;

            if (midCost < targetCost)
                low = mid;
            else
                high = mid;
        }

        ApplyFactorConFachada(componentes, originales, bestFactor, motor, proyecto.DecimalesImporte, scopes);
        bestCost = componentes.Sum(c => c.Importe);
        matrix.CostoDirecto = bestCost;

        if (Math.Abs(bestCost - currentCost) <= 0.01m && Math.Abs(targetCost - currentCost) > 0.01m)
            return FailConFachada("No se realizó un ajuste efectivo con los rubros seleccionados.", currentCost, targetCost, bestCost);

        return new MatrixCostAdjustmentResult
        {
            Success = true,
            CurrentCost = currentCost,
            TargetCost = targetCost,
            AchievedCost = bestCost,
            FactorApplied = bestFactor,
            Message = string.Empty
        };
    }

    private static MatrixCostAdjustmentResult FailConFachada(string message, decimal current = 0m, decimal target = 0m, decimal achieved = 0m)
        => new()
        {
            Success = false,
            Message = message,
            CurrentCost = current,
            TargetCost = target,
            AchievedCost = achieved
        };

    private static decimal EvaluateCostConFachada(
        IList<ComponenteMatriz> componentes,
        IReadOnlyDictionary<ComponenteMatriz, decimal> originales,
        decimal factor,
        MotorCalculoSopro motor,
        int decimalesImporte,
        MatrixAdjustmentScopes scopes)
    {
        ApplyFactorConFachada(componentes, originales, factor, motor, decimalesImporte, scopes);
        return componentes.Count == 0 ? 0m : componentes.Sum(c => c.Importe);
    }

    private static void ApplyFactorConFachada(
        IList<ComponenteMatriz> componentes,
        IReadOnlyDictionary<ComponenteMatriz, decimal> originales,
        decimal factor,
        MotorCalculoSopro motor,
        int decimalesImporte,
        MatrixAdjustmentScopes scopes)
    {
        foreach (var kv in originales)
        {
            var componente = kv.Key;
            var original = kv.Value;
            if (IsAdjustable(componente, scopes))
            {
                var nuevaCantidad = motor.RedondearCantidad(original * factor);
                if (nuevaCantidad < 0m) nuevaCantidad = 0m;
                componente.Cantidad = nuevaCantidad;
                MatrixComponentEditingService.SyncRendimiento(componente);
            }
            else
            {
                componente.Cantidad = original;
                MatrixComponentEditingService.SyncRendimiento(componente);
            }
        }

        MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte);
    }

    private static void RestoreOriginalQuantities(IList<ComponenteMatriz> componentes, IReadOnlyDictionary<ComponenteMatriz, decimal> originales)
    {
        foreach (var kv in originales)
        {
            kv.Key.Cantidad = kv.Value;
            MatrixComponentEditingService.SyncRendimiento(kv.Key);
        }
    }

    private static bool IsAdjustable(ComponenteMatriz componente, MatrixAdjustmentScopes scopes)
    {
        if (componente == null) return false;
        return componente.TipoComponente switch
        {
            TipoComponenteMatriz.Material => scopes.HasFlag(MatrixAdjustmentScopes.Materiales),
            TipoComponenteMatriz.ManoDeObra => scopes.HasFlag(MatrixAdjustmentScopes.ManoDeObra),
            TipoComponenteMatriz.Maquinaria => scopes.HasFlag(MatrixAdjustmentScopes.Maquinaria),
            TipoComponenteMatriz.Herramienta => scopes.HasFlag(MatrixAdjustmentScopes.Herramienta),
            TipoComponenteMatriz.Auxiliar when componente.Auxiliar?.Tipo == TipoMatriz.Cuadrilla => scopes.HasFlag(MatrixAdjustmentScopes.ManoDeObra),
            _ => false
        };
    }

    // ── Comparaciones ─────────────────────────────────────────────────────────

    private static void CompararResultados(
        MatrixCostAdjustmentResult servicio, MatrixCostAdjustmentResult referencia, string tag)
    {
        Assert.AreEqual(referencia.Success, servicio.Success, $"{tag} Success");
        Assert.AreEqual(referencia.Message, servicio.Message, $"{tag} Message");
        Assert.AreEqual(referencia.CurrentCost, servicio.CurrentCost, $"{tag} CurrentCost");
        Assert.AreEqual(referencia.TargetCost, servicio.TargetCost, $"{tag} TargetCost");
        Assert.AreEqual(referencia.AchievedCost, servicio.AchievedCost, $"{tag} AchievedCost");
        Assert.AreEqual(referencia.FactorApplied, servicio.FactorApplied, $"{tag} FactorApplied");
    }

    private static void CompararComponentes(
        ICollection<ComponenteMatriz> referencia, ICollection<ComponenteMatriz> servicio, string tag)
    {
        Assert.AreEqual(referencia.Count, servicio.Count, $"{tag} cantidad de componentes");
        var refList = referencia.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
        var serList = servicio.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
        for (int i = 0; i < refList.Count; i++)
        {
            Assert.AreEqual(refList[i].Cantidad, serList[i].Cantidad, $"{tag} comp[{i}] Cantidad");
            Assert.AreEqual(refList[i].Importe, serList[i].Importe, $"{tag} comp[{i}] Importe");
            Assert.AreEqual(refList[i].Rendimiento, serList[i].Rendimiento, $"{tag} comp[{i}] Rendimiento");
        }
    }

    // ── Generador determinista ─────────────────────────────────────────────────

    private sealed record Spec(TipoComponenteMatriz Tipo, decimal Cantidad, decimal Precio, bool EsPorcentaje, bool EsCuadrilla);

    private static List<Spec> CrearSpecs(Random rnd)
    {
        var specs = new List<Spec>();
        int n = rnd.Next(3, 9);
        for (int i = 0; i < n; i++)
        {
            int tipo = rnd.Next(0, 5);
            bool esPorcentaje = rnd.Next(0, 3) == 0;
            specs.Add(tipo switch
            {
                0 => new Spec(TipoComponenteMatriz.Material, Valor(rnd), Valor(rnd), false, false),
                1 => new Spec(TipoComponenteMatriz.ManoDeObra,
                    esPorcentaje ? Porcentaje(rnd) : Valor(rnd), Valor(rnd), esPorcentaje, false),
                2 => new Spec(TipoComponenteMatriz.Maquinaria, Valor(rnd), Valor(rnd), false, false),
                3 => new Spec(TipoComponenteMatriz.Herramienta,
                    esPorcentaje ? Porcentaje(rnd) : Valor(rnd), Valor(rnd), esPorcentaje, false),
                _ => new Spec(TipoComponenteMatriz.Auxiliar, Valor(rnd), Valor(rnd), false, rnd.Next(0, 2) == 0)
            });
        }
        return specs;
    }

    private static Matriz ConstruirMatriz(List<Spec> specs)
    {
        var matriz = new Matriz
        {
            Clave = "APU-PAR",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };

        int orden = 1;
        foreach (var spec in specs)
        {
            var componente = new ComponenteMatriz
            {
                TipoComponente = spec.Tipo,
                Cantidad = spec.Cantidad,
                Orden = orden++,
                Notas = string.Empty
            };

            switch (spec.Tipo)
            {
                case TipoComponenteMatriz.Material:
                    componente.Material = new Material { Clave = "MAT", PrecioUnitario = spec.Precio };
                    break;
                case TipoComponenteMatriz.ManoDeObra:
                    componente.ManoDeObra = new ManoDeObra
                    {
                        Clave = "MO",
                        Unidad = spec.EsPorcentaje ? "%MO" : "jor",
                        SalarioReal = spec.EsPorcentaje ? 0m : spec.Precio
                    };
                    break;
                case TipoComponenteMatriz.Maquinaria:
                    componente.Maquinaria = new Maquinaria { Clave = "MAQ", CostoHorario = spec.Precio };
                    break;
                case TipoComponenteMatriz.Herramienta:
                    componente.Herramienta = new Herramienta
                    {
                        Clave = "HER",
                        Unidad = spec.EsPorcentaje ? "%MO" : "pza",
                        PrecioUnitario = spec.EsPorcentaje ? 0m : spec.Precio
                    };
                    break;
                case TipoComponenteMatriz.Auxiliar:
                    componente.Auxiliar = new Matriz
                    {
                        Clave = "AUX",
                        Tipo = spec.EsCuadrilla ? TipoMatriz.Cuadrilla : TipoMatriz.Basico,
                        CostoDirecto = spec.Precio
                    };
                    break;
            }

            matriz.Componentes.Add(componente);
        }

        MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), 2);
        return matriz;
    }

    private static MatrixAdjustmentScopes ScopeAleatorio(Random rnd)
        => rnd.Next(0, 7) switch
        {
            0 => MatrixAdjustmentScopes.Materiales,
            1 => MatrixAdjustmentScopes.ManoDeObra,
            2 => MatrixAdjustmentScopes.Maquinaria,
            3 => MatrixAdjustmentScopes.Herramienta,
            4 => MatrixAdjustmentScopes.Materiales | MatrixAdjustmentScopes.ManoDeObra,
            5 => MatrixAdjustmentScopes.Materiales | MatrixAdjustmentScopes.Maquinaria,
            _ => MatrixAdjustmentScopes.Materiales | MatrixAdjustmentScopes.ManoDeObra
                 | MatrixAdjustmentScopes.Maquinaria | MatrixAdjustmentScopes.Herramienta
        };

    private static Proyecto CrearProyecto(int decQty, int decAmt, int decPct) => new()
    {
        Nombre = "Proyecto reajuste paridad",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 1, 31),
        PlazoEjecucion = 31,
        DecimalesCantidad = decQty,
        DecimalesImporte = decAmt,
        DecimalesPorcentaje = decPct
    };

    private static decimal Valor(Random rnd)
        => rnd.Next(1, 1_000_000) / 1000m;

    private static decimal Porcentaje(Random rnd)
        => rnd.Next(1, 20_000) / 100m;
}