using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    [System.Flags]
    public enum MatrixAdjustmentScopes
    {
        None = 0,
        Materiales = 1,
        ManoDeObra = 2,
        Maquinaria = 4,
        Herramienta = 8
    }

    public sealed class MatrixCostAdjustmentResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public decimal CurrentCost { get; set; }
        public decimal TargetCost { get; set; }
        public decimal AchievedCost { get; set; }
        public decimal FactorApplied { get; set; }
    }


    public sealed class MatrixAdjustmentBasis
    {
        public decimal CurrentCost { get; set; }
        public decimal AdjustableCost { get; set; }
        public decimal MinCost { get; set; }
        public decimal FixedCost => CurrentCost - AdjustableCost;
        public bool HasAdjustableScope => AdjustableCost > 0m;
    }

    public static class MatrixCostAdjustmentService
    {

        public static MatrixAdjustmentBasis? GetAdjustmentBasis(
            Matriz matrix,
            Proyecto proyecto,
            MatrixAdjustmentScopes scopes)
        {
            if (matrix == null) throw new ArgumentNullException(nameof(matrix));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
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

            var currentCost = EvaluateCost(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
            var adjustableCurrentAmount = ajustables.Sum(c => c.Importe);
            var minCost = EvaluateCost(componentes, originales, 0m, motor, proyecto.DecimalesImporte, scopes);
            RestoreOriginalQuantities(componentes, originales);
            MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);

            return new MatrixAdjustmentBasis
            {
                CurrentCost = currentCost,
                AdjustableCost = adjustableCurrentAmount,
                MinCost = minCost
            };
        }

        public static MatrixCostAdjustmentResult AdjustMatrixByFactor(
            Matriz matrix,
            Proyecto proyecto,
            MatrixAdjustmentScopes scopes,
            decimal factor)
        {
            if (matrix == null) throw new ArgumentNullException(nameof(matrix));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (matrix.Componentes == null || matrix.Componentes.Count == 0)
            {
                return Fail("La matriz no tiene componentes para reajustar.");
            }

            if (scopes == MatrixAdjustmentScopes.None)
                return Fail("Seleccione al menos un rubro a reajustar.");

            if (factor < 0m)
                return Fail("El factor de reajuste no puede ser negativo.");

            var componentes = matrix.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
            var motor = new MotorCalculoSopro(proyecto);
            var originales = componentes.ToDictionary(c => c, c => c.Cantidad);
            var ajustables = componentes.Where(c => IsAdjustable(c, scopes)).ToList();
            if (ajustables.Count == 0)
            {
                RestoreOriginalQuantities(componentes, originales);
                MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
                return Fail("La matriz no contiene componentes ajustables para los rubros seleccionados.");
            }

            var currentCost = EvaluateCost(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
            ApplyFactor(componentes, originales, factor, motor, proyecto.DecimalesImporte, scopes);
            var achieved = componentes.Sum(c => c.Importe);
            matrix.CostoDirecto = achieved;
            matrix.FechaUltimoCalculo = DateTime.Now;
            matrix.FechaModificacion = DateTime.Now;
            return new MatrixCostAdjustmentResult
            {
                Success = true,
                CurrentCost = currentCost,
                TargetCost = 0m,
                AchievedCost = achieved,
                FactorApplied = factor
            };
        }

        public static MatrixCostAdjustmentResult AdjustMatrixByTargetCost(
            Matriz matrix,
            Proyecto proyecto,
            MatrixAdjustmentScopes scopes,
            decimal targetCost)
        {
            if (matrix == null) throw new ArgumentNullException(nameof(matrix));
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (matrix.Componentes == null || matrix.Componentes.Count == 0)
            {
                return Fail("La matriz no tiene componentes para reajustar.");
            }

            if (scopes == MatrixAdjustmentScopes.None)
                return Fail("Seleccione al menos un rubro a reajustar.");

            if (targetCost <= 0m)
                return Fail("El monto objetivo debe ser mayor que cero.");

            var componentes = matrix.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
            var motor = new MotorCalculoSopro(proyecto);
            var originales = componentes.ToDictionary(c => c, c => c.Cantidad);
            var ajustables = componentes.Where(c => IsAdjustable(c, scopes)).ToList();

            if (ajustables.Count == 0)
            {
                RestoreOriginalQuantities(componentes, originales);
                MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
                return Fail("La matriz no contiene componentes ajustables para los rubros seleccionados.");
            }

            var currentCost = EvaluateCost(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
            var adjustableCurrentAmount = ajustables.Sum(c => c.Importe);
            if (adjustableCurrentAmount <= 0m)
            {
                RestoreOriginalQuantities(componentes, originales);
                MatrixComponentCalculationService.Recalculate(componentes, proyecto.DecimalesImporte);
                return Fail("Los rubros seleccionados no tienen importe ajustable dentro de la matriz.", currentCost, targetCost, currentCost);
            }

            var minCost = EvaluateCost(componentes, originales, 0m, motor, proyecto.DecimalesImporte, scopes);

            if (Math.Abs(targetCost - currentCost) <= 0.01m)
            {
                ApplyFactor(componentes, originales, 1m, motor, proyecto.DecimalesImporte, scopes);
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
                return Fail($"No es posible bajar la matriz al monto solicitado con los rubros seleccionados. El mínimo alcanzable es {minCost:C2}.", currentCost, targetCost, minCost);
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
                var highCost = EvaluateCost(componentes, originales, high, motor, proyecto.DecimalesImporte, scopes);
                int guard = 0;
                while (highCost < targetCost && guard < 20)
                {
                    high *= 2m;
                    highCost = EvaluateCost(componentes, originales, high, motor, proyecto.DecimalesImporte, scopes);
                    guard++;
                }
            }

            decimal bestFactor = 1m;
            decimal bestCost = currentCost;
            decimal bestDiff = Math.Abs(bestCost - targetCost);

            for (int i = 0; i < 40; i++)
            {
                var mid = (low + high) / 2m;
                var midCost = EvaluateCost(componentes, originales, mid, motor, proyecto.DecimalesImporte, scopes);
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

            ApplyFactor(componentes, originales, bestFactor, motor, proyecto.DecimalesImporte, scopes);
            bestCost = componentes.Sum(c => c.Importe);
            matrix.CostoDirecto = bestCost;
            matrix.FechaUltimoCalculo = DateTime.Now;
            matrix.FechaModificacion = DateTime.Now;

            if (Math.Abs(bestCost - currentCost) <= 0.01m && Math.Abs(targetCost - currentCost) > 0.01m)
            {
                return Fail("No se realizó un ajuste efectivo con los rubros seleccionados.", currentCost, targetCost, bestCost);
            }

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

        private static MatrixCostAdjustmentResult Fail(string message, decimal current = 0m, decimal target = 0m, decimal achieved = 0m)
            => new()
            {
                Success = false,
                Message = message,
                CurrentCost = current,
                TargetCost = target,
                AchievedCost = achieved
            };

        private static decimal EvaluateCost(
            IList<ComponenteMatriz> componentes,
            IReadOnlyDictionary<ComponenteMatriz, decimal> originales,
            decimal factor,
            MotorCalculoSopro motor,
            int decimalesImporte,
            MatrixAdjustmentScopes scopes)
        {
            ApplyFactor(componentes, originales, factor, motor, decimalesImporte, scopes);
            return componentes.Count == 0 ? 0m : componentes.Sum(c => c.Importe);
        }

        private static void ApplyFactor(
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

            var totals = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte);
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
    }
}
