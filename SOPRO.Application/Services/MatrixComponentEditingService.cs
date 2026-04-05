using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;
using System.Globalization;

namespace SOPRO.Application.Services
{
    public static class MatrixComponentEditingService
    {
        public static MatrixComponentEditResult UpdateDescription(ComponenteMatriz component, string value)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            var text = value?.Trim() ?? string.Empty;
            var changed = false;

            if (component.Material != null) { component.Material.Descripcion = text; changed = true; }
            if (component.ManoDeObra != null) { component.ManoDeObra.Descripcion = text; changed = true; }
            if (component.Maquinaria != null) { component.Maquinaria.Descripcion = text; changed = true; }
            if (component.Auxiliar != null) { component.Auxiliar.Descripcion = text; changed = true; }
            if (component.Herramienta != null) { component.Herramienta.Descripcion = text; changed = true; }

            return changed ? MatrixComponentEditResult.Ok(requiresRecalculation: false) : MatrixComponentEditResult.Fail();
        }

        public static MatrixComponentEditResult UpdateUnit(ComponenteMatriz component, string value)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            var text = value?.Trim() ?? string.Empty;
            var changed = false;

            if (component.Material != null) { component.Material.Unidad = text; changed = true; }
            if (component.ManoDeObra != null) { component.ManoDeObra.Unidad = text; changed = true; }
            if (component.Auxiliar != null) { component.Auxiliar.Unidad = text; changed = true; }
            if (component.Herramienta != null) { component.Herramienta.Unidad = text; changed = true; }

            return changed ? MatrixComponentEditResult.Ok() : MatrixComponentEditResult.Fail();
        }

        public static MatrixComponentEditResult UpdateQuantity(ComponenteMatriz component, string rawValue)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));

            if (!TryParseDecimal(rawValue, out var quantity) || quantity <= 0)
            {
                return MatrixComponentEditResult.Fail("La cantidad debe ser un número mayor que cero.");
            }

            component.Cantidad = quantity;
            SyncRendimiento(component);
            return MatrixComponentEditResult.Ok();
        }

        public static MatrixComponentEditResult UpdateQuantityFromDialog(ComponenteMatriz component, decimal quantity)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (quantity <= 0)
            {
                return MatrixComponentEditResult.Fail("La cantidad debe ser mayor que cero.");
            }

            component.Cantidad = quantity;
            SyncRendimiento(component);
            return MatrixComponentEditResult.Ok();
        }

        /// <summary>
        /// Sincroniza Rendimiento = 1/Cantidad para componentes de maquinaria.
        /// Para otros tipos el rendimiento no aplica y se deja en cero.
        /// </summary>
        public static void SyncRendimiento(ComponenteMatriz component)
        {
            if (component == null) return;
            if (component.TipoComponente == TipoComponenteMatriz.Maquinaria && component.Cantidad > 0)
                component.Rendimiento = Math.Round(1m / component.Cantidad, 5, MidpointRounding.AwayFromZero);
            else
                component.Rendimiento = 0m;
        }

        public static MatrixComponentEditResult UpdateUnitPrice(ComponenteMatriz component, string rawValue, int decimalesImporte)
        {
            if (component == null) throw new ArgumentNullException(nameof(component));
            if (component.TipoComponente != TipoComponenteMatriz.Material || component.Material == null)
            {
                return MatrixComponentEditResult.Fail("Solo los materiales permiten edición directa del precio unitario.");
            }

            if (!TryParseDecimal(rawValue, out var unitPrice) || unitPrice < 0)
            {
                return MatrixComponentEditResult.Fail("El precio unitario debe ser un número mayor o igual a cero.");
            }

            component.Material.PrecioUnitario = unitPrice;
            component.Importe = Round(component.Cantidad * Round(unitPrice, decimalesImporte), decimalesImporte);
            return MatrixComponentEditResult.Ok();
        }

        private static bool TryParseDecimal(string? rawValue, out decimal value)
        {
            var clean = (rawValue ?? string.Empty)
                .Replace("$", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                || decimal.TryParse(clean, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
        }

        private static decimal Round(decimal value, int decimals)
        {
            return Math.Round(value, decimals, MidpointRounding.AwayFromZero);
        }
    }
}
