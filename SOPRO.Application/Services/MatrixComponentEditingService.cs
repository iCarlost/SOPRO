using Sopro.Calculation;
using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;
using System.Globalization;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  MatrixComponentEditingService — [N5-8]                                 ║
    // ║  UpdateUnitPrice migró su única operación del motor de MotorCalculo-     ║
    // ║  Sopro a SoproCalculationEngine.Multiply (SOPRO.Calculation).            ║
    // ║  SyncRendimiento delega en Maquinaria.CalcularRendimiento:              ║
    // ║  precisión fija 5 por contrato de captura, no es operación del motor.    ║
    // ║  El resto de métodos no tienen aritmética de motor. Sin cambios de API.  ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

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
            component.Rendimiento = component.TipoComponente == TipoComponenteMatriz.Maquinaria
                ? Maquinaria.CalcularRendimiento(component.Cantidad)
                : 0m;
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
            var engine = new SoproCalculationEngine(decimalesImporte, decimalesImporte, 4);
            component.Importe = engine.Multiply(component.Cantidad, unitPrice);
            return MatrixComponentEditResult.Ok();
        }

        /// <summary>
        /// Valida que cada componente referencie exactamente la entidad que exige su
        /// <see cref="ComponenteMatriz.TipoComponente"/>: Material→MaterialId,
        /// ManoDeObra→ManoDeObraId, Maquinaria→MaquinariaId,
        /// Herramienta→HerramientaId y Auxiliar→AuxiliarId. Una referencia ausente,
        /// no positiva o acompañada de otra columna de referencia se rechaza con un
        /// mensaje en español: guardar o recalcular ese componente produciría una
        /// navegación nula y un costo incorrecto.
        /// </summary>
        public static MatrixComponentEditResult ValidateReferences(IEnumerable<ComponenteMatriz>? components)
        {
            if (components == null) throw new ArgumentNullException(nameof(components));

            return TryFindInvalidReference(components, out _, out _, out var message)
                ? MatrixComponentEditResult.Fail(message)
                : MatrixComponentEditResult.Ok(requiresRecalculation: false);
        }

        /// <summary>
        /// Localiza el primer componente con una referencia inválida para su tipo.
        /// La posición es 1-based (la que ve el usuario en la lista). Devuelve
        /// <c>false</c> cuando todos los componentes son consistentes.
        /// </summary>
        internal static bool TryFindInvalidReference(
            IEnumerable<ComponenteMatriz>? components,
            out ComponenteMatriz? invalidComponent,
            out int position,
            out string message)
        {
            invalidComponent = null;
            position = 0;
            message = string.Empty;

            if (components == null)
            {
                message = "La lista de componentes es nula.";
                return true;
            }

            int index = 0;
            foreach (var component in components)
            {
                index++;

                if (component == null)
                {
                    position = index;
                    message = $"El componente en la posición {index} es nulo.";
                    return true;
                }

                string? requiredName = NombreReferenciaRequerida(component.TipoComponente);
                if (requiredName == null)
                {
                    invalidComponent = component;
                    position = index;
                    message =
                        $"El componente {IdentificarComponente(component, index)} tiene un tipo no soportado " +
                        $"({(int)component.TipoComponente}).";
                    return true;
                }

                int? requiredId = IdReferenciaRequerida(component);
                if (!requiredId.HasValue || requiredId.Value <= 0)
                {
                    invalidComponent = component;
                    position = index;
                    var adicionalesAusente = NombresReferenciasAdicionales(component).ToList();
                    var sufijo = adicionalesAusente.Count == 0
                        ? string.Empty
                        : $" y además tiene {string.Join(", ", adicionalesAusente)}";
                    message =
                        $"El componente {component.TipoComponente} {IdentificarComponente(component, index)} " +
                        $"no tiene la referencia {requiredName} requerida por su tipo{sufijo}.";
                    return true;
                }

                var referenciasAdicionales = NombresReferenciasAdicionales(component).ToList();
                if (referenciasAdicionales.Count > 0)
                {
                    invalidComponent = component;
                    position = index;
                    message =
                        $"El componente {component.TipoComponente} {IdentificarComponente(component, index)} " +
                        $"tiene referencias inconsistentes con su tipo: use solo {requiredName} " +
                        $"(sobran {string.Join(", ", referenciasAdicionales)}).";
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// Localiza el primer componente cuya navegación requerida por su tipo no
        /// está cargada. Es la validación del recálculo: un componente temporal
        /// (aún sin Id) puede ser evaluable si su navegación está presente, mientras
        /// que una fila persistida con la referencia nula (el defecto real) no lo es.
        /// La posición es 1-based.
        /// </summary>
        internal static bool TryFindInvalidNavigation(
            IEnumerable<ComponenteMatriz>? components,
            out ComponenteMatriz? invalidComponent,
            out int position,
            out string message)
        {
            invalidComponent = null;
            position = 0;
            message = string.Empty;

            if (components == null)
            {
                message = "La lista de componentes es nula.";
                return true;
            }

            int index = 0;
            foreach (var component in components)
            {
                index++;

                if (component == null)
                {
                    position = index;
                    message = $"El componente en la posición {index} es nulo.";
                    return true;
                }

                string? requiredName = NombreReferenciaRequerida(component.TipoComponente);
                if (requiredName == null)
                {
                    invalidComponent = component;
                    position = index;
                    message =
                        $"El componente {IdentificarComponente(component, index)} tiene un tipo no soportado " +
                        $"({(int)component.TipoComponente}).";
                    return true;
                }

                if (TieneNavegacionRequerida(component))
                    continue;

                invalidComponent = component;
                position = index;

                int? referenceId = IdReferenciaRequerida(component);
                message = referenceId.HasValue && referenceId.Value > 0
                    ? $"El componente {component.TipoComponente} {IdentificarComponente(component, index)} " +
                      $"tiene {requiredName} = {referenceId.Value}, pero la navegación " +
                      $"{NombreNavegacion(component.TipoComponente)} no está cargada."
                    : $"El componente {component.TipoComponente} {IdentificarComponente(component, index)} " +
                      $"no tiene la referencia {requiredName} requerida por su tipo.";
                return true;
            }

            return false;
        }

        private static bool TieneNavegacionRequerida(ComponenteMatriz component)
            => component.TipoComponente switch
            {
                TipoComponenteMatriz.Material => component.Material != null,
                TipoComponenteMatriz.ManoDeObra => component.ManoDeObra != null,
                TipoComponenteMatriz.Maquinaria => component.Maquinaria != null,
                TipoComponenteMatriz.Herramienta => component.Herramienta != null,
                TipoComponenteMatriz.Auxiliar => component.Auxiliar != null,
                _ => false
            };

        private static string NombreNavegacion(TipoComponenteMatriz tipo)
            => tipo switch
            {
                TipoComponenteMatriz.Material => nameof(ComponenteMatriz.Material),
                TipoComponenteMatriz.ManoDeObra => nameof(ComponenteMatriz.ManoDeObra),
                TipoComponenteMatriz.Maquinaria => nameof(ComponenteMatriz.Maquinaria),
                TipoComponenteMatriz.Herramienta => nameof(ComponenteMatriz.Herramienta),
                _ => nameof(ComponenteMatriz.Auxiliar)
            };

        private static string? NombreReferenciaRequerida(TipoComponenteMatriz tipo)
            => tipo switch
            {
                TipoComponenteMatriz.Material => nameof(ComponenteMatriz.MaterialId),
                TipoComponenteMatriz.ManoDeObra => nameof(ComponenteMatriz.ManoDeObraId),
                TipoComponenteMatriz.Maquinaria => nameof(ComponenteMatriz.MaquinariaId),
                TipoComponenteMatriz.Herramienta => nameof(ComponenteMatriz.HerramientaId),
                TipoComponenteMatriz.Auxiliar => nameof(ComponenteMatriz.AuxiliarId),
                _ => null
            };

        private static int? IdReferenciaRequerida(ComponenteMatriz component)
            => component.TipoComponente switch
            {
                TipoComponenteMatriz.Material => component.MaterialId,
                TipoComponenteMatriz.ManoDeObra => component.ManoDeObraId,
                TipoComponenteMatriz.Maquinaria => component.MaquinariaId,
                TipoComponenteMatriz.Herramienta => component.HerramientaId,
                TipoComponenteMatriz.Auxiliar => component.AuxiliarId,
                _ => null
            };

        private static IEnumerable<string> NombresReferenciasAdicionales(ComponenteMatriz component)
        {
            if (component.TipoComponente != TipoComponenteMatriz.Material && component.MaterialId.HasValue)
                yield return nameof(ComponenteMatriz.MaterialId);
            if (component.TipoComponente != TipoComponenteMatriz.ManoDeObra && component.ManoDeObraId.HasValue)
                yield return nameof(ComponenteMatriz.ManoDeObraId);
            if (component.TipoComponente != TipoComponenteMatriz.Maquinaria && component.MaquinariaId.HasValue)
                yield return nameof(ComponenteMatriz.MaquinariaId);
            if (component.TipoComponente != TipoComponenteMatriz.Herramienta && component.HerramientaId.HasValue)
                yield return nameof(ComponenteMatriz.HerramientaId);
            if (component.TipoComponente != TipoComponenteMatriz.Auxiliar && component.AuxiliarId.HasValue)
                yield return nameof(ComponenteMatriz.AuxiliarId);
        }

        private static string IdentificarComponente(ComponenteMatriz component, int position)
            => component.Id > 0
                ? $"(Id {component.Id}, posición {position})"
                : $"(posición {position})";

        private static bool TryParseDecimal(string? rawValue, out decimal value)
        {
            var clean = (rawValue ?? string.Empty)
                .Replace("$", string.Empty)
                .Replace(",", string.Empty)
                .Trim();

            return decimal.TryParse(clean, NumberStyles.Any, CultureInfo.InvariantCulture, out value)
                || decimal.TryParse(clean, NumberStyles.Any, CultureInfo.CurrentCulture, out value);
        }

    }
}
