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
