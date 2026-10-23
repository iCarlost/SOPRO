using SOPRO.Application.Models.Presupuesto;
using System.Globalization;

namespace SOPRO.Application.Services
{
    public static class BudgetClipboardPasteService
    {
        private static readonly string[] AllowedColumns = { "Clave", "Descripcion", "Unidad", "Cantidad" };

        public static BudgetClipboardPastePlan BuildPlan(string? startColumnInternalName, string? clipboardText)
        {
            var plan = new BudgetClipboardPastePlan();

            if (string.IsNullOrWhiteSpace(startColumnInternalName))
            {
                plan.ErrorMessage = "Seleccione una celda válida para pegar.";
                return plan;
            }

            int startIndex = Array.FindIndex(AllowedColumns, c => string.Equals(c, startColumnInternalName, StringComparison.OrdinalIgnoreCase));
            if (startIndex < 0)
            {
                plan.ErrorMessage = "Solo se permite pegar en Clave, Descripción, Unidad o Cantidad.";
                return plan;
            }

            var text = clipboardText ?? string.Empty;
            if (string.IsNullOrWhiteSpace(text))
            {
                plan.ErrorMessage = "El portapapeles no contiene texto para pegar.";
                return plan;
            }

            var lines = text.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
            for (int rowOffset = 0; rowOffset < lines.Length; rowOffset++)
            {
                var line = lines[rowOffset];
                if (rowOffset == lines.Length - 1 && string.IsNullOrWhiteSpace(line))
                    continue;

                var cells = line.Split('	');
                var row = new BudgetClipboardPasteRow { RowOffset = rowOffset };

                for (int i = 0; i < cells.Length && (startIndex + i) < AllowedColumns.Length; i++)
                {
                    row.ValuesByColumn[AllowedColumns[startIndex + i]] = cells[i] ?? string.Empty;
                }

                if (row.ValuesByColumn.Count > 0)
                    plan.Rows.Add(row);
            }

            if (plan.Rows.Count == 0)
            {
                plan.ErrorMessage = "No se detectaron celdas válidas para pegar.";
                return plan;
            }

            plan.IsValid = true;
            return plan;
        }

        public static bool CanPasteIntoColumn(string? tipo, string? columnInternalName, bool hasKey, bool rowWillHaveKeyAfterPaste)
        {
            if (string.IsNullOrWhiteSpace(columnInternalName)) return false;
            if (!AllowedColumns.Any(c => string.Equals(c, columnInternalName, StringComparison.OrdinalIgnoreCase))) return false;
            if (string.IsNullOrWhiteSpace(tipo)) return false;

            if (IsAggregatorType(tipo))
            {
                return string.Equals(columnInternalName, "Clave", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(columnInternalName, "Descripcion", StringComparison.OrdinalIgnoreCase);
            }

            if (!string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase))
                return false;

            if (string.Equals(columnInternalName, "Clave", StringComparison.OrdinalIgnoreCase))
                return true;

            return hasKey || rowWillHaveKeyAfterPaste;
        }

        public static bool TryNormalizeValue(string? columnInternalName, string? rawValue, out string normalizedValue)
        {
            normalizedValue = string.Empty;
            var value = rawValue ?? string.Empty;

            if (string.Equals(columnInternalName, "Cantidad", StringComparison.OrdinalIgnoreCase))
            {
                if (!TryParseDecimal(value, out var quantity) || quantity <= 0m)
                    return false;

                normalizedValue = quantity.ToString(CultureInfo.CurrentCulture);
                return true;
            }

            if (string.Equals(columnInternalName, "Clave", StringComparison.OrdinalIgnoreCase)
                || string.Equals(columnInternalName, "Unidad", StringComparison.OrdinalIgnoreCase))
            {
                normalizedValue = value.Trim();
                return true;
            }

            normalizedValue = value.TrimEnd();
            return true;
        }

        public static IReadOnlyList<string> GetColumnOrder() => AllowedColumns;

        /// <summary>
        /// Simula el pegado sobre las filas de destino SIN tocar la vista: valida que
        /// cada fila del bloque apunte a una fila persistida con Tipo, recorre las
        /// columnas en el orden aceptado (Clave → Descripción → Unidad → Cantidad),
        /// aplica las mismas restricciones por tipo y normaliza los valores. Las celdas
        /// inválidas se omiten como en la ruta heredada; una fila sin ningún valor
        /// aceptado no se representa como operación (evita comandos vacíos que solo
        /// sellarían FechaModificacion). Si alguna fila de destino no es representable,
        /// el lote completo se rechaza con un mensaje accionable antes de cualquier
        /// diálogo o escritura.
        /// </summary>
        public static BudgetClipboardPasteOperationPlan BuildOperations(
            BudgetClipboardPastePlan plan,
            int startRowIndex,
            IReadOnlyDictionary<int, BudgetClipboardPasteTarget?> targets)
        {
            if (plan == null) throw new ArgumentNullException(nameof(plan));
            if (targets == null) throw new ArgumentNullException(nameof(targets));

            var resultado = new BudgetClipboardPasteOperationPlan();

            if (!plan.IsValid)
            {
                resultado.ErrorMessage = plan.ErrorMessage;
                return resultado;
            }

            if (startRowIndex < 0)
            {
                resultado.ErrorMessage = "Seleccione una celda válida para pegar.";
                return resultado;
            }

            var filas = plan.Rows.OrderBy(r => r.RowOffset).ToList();
            var destinosInvalidos = new List<string>();

            foreach (var pasteRow in filas)
            {
                int rowIndex = startRowIndex + pasteRow.RowOffset;
                targets.TryGetValue(rowIndex, out var target);

                if (target?.Concept is not { Id: > 0 } || string.IsNullOrWhiteSpace(target.Tipo))
                {
                    destinosInvalidos.Add(
                        $"fila {rowIndex + 1} del presupuesto (posición {pasteRow.RowOffset + 1} del bloque)");
                }
            }

            if (destinosInvalidos.Count > 0)
            {
                resultado.ErrorMessage =
                    "El pegado solo se permite sobre filas ya guardadas con Tipo asignado. " +
                    "No se modificó nada. Revise " + string.Join(", ", destinosInvalidos) +
                    "; guarde el presupuesto o quite esas filas del bloque antes de pegar.";
                return resultado;
            }

            var operaciones = new List<BudgetClipboardPasteOperation>(filas.Count);

            foreach (var pasteRow in filas)
            {
                int rowIndex = startRowIndex + pasteRow.RowOffset;
                var target = targets[rowIndex]!;
                var operacion = new BudgetClipboardPasteOperation
                {
                    RowIndex = rowIndex,
                    Tipo = target.Tipo,
                    Target = target
                };

                bool hasKey = !string.IsNullOrWhiteSpace(target.CurrentKey);
                bool rowWillHaveKeyAfterPaste = hasKey || pasteRow.ValuesByColumn.ContainsKey("Clave");

                foreach (var columnName in AllowedColumns)
                {
                    if (!pasteRow.ValuesByColumn.TryGetValue(columnName, out var rawValue))
                        continue;

                    if (!CanPasteIntoColumn(target.Tipo, columnName, hasKey, rowWillHaveKeyAfterPaste))
                    {
                        resultado.SkippedCells++;
                        continue;
                    }

                    if (!TryNormalizeValue(columnName, rawValue, out var normalizedValue))
                    {
                        resultado.SkippedCells++;
                        continue;
                    }

                    if (string.Equals(columnName, "Cantidad", StringComparison.OrdinalIgnoreCase))
                    {
                        if (!TryParseNormalizedQuantity(normalizedValue, out decimal cantidad))
                        {
                            resultado.SkippedCells++;
                            continue;
                        }

                        operacion.Cantidad = cantidad;
                    }
                    else if (string.Equals(columnName, "Clave", StringComparison.OrdinalIgnoreCase))
                    {
                        operacion.Clave = normalizedValue;
                        hasKey = !string.IsNullOrWhiteSpace(normalizedValue);
                        rowWillHaveKeyAfterPaste = hasKey;
                    }
                    else if (string.Equals(columnName, "Descripcion", StringComparison.OrdinalIgnoreCase))
                    {
                        operacion.Descripcion = normalizedValue;
                    }
                    else
                    {
                        operacion.Unidad = normalizedValue;
                    }
                }

                if (operacion.HasValues)
                    operaciones.Add(operacion);
            }

            resultado.IsValid = true;
            resultado.Operations = operaciones;
            return resultado;
        }

        private static bool TryParseNormalizedQuantity(string normalizedValue, out decimal cantidad)
        {
            return decimal.TryParse(normalizedValue, NumberStyles.Any, CultureInfo.CurrentCulture, out cantidad)
                || decimal.TryParse(normalizedValue, NumberStyles.Any, CultureInfo.InvariantCulture, out cantidad);
        }

        private static bool IsAggregatorType(string tipo)
        {
            return string.Equals(tipo, "Capitulo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo, "Subcapitulo", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo, "Nivel 1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo, "Nivel 2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(tipo, "Nivel 3", StringComparison.OrdinalIgnoreCase);
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
    }
}
