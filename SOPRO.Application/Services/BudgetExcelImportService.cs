using ClosedXML.Excel;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using System.Data;
using System.Globalization;

namespace SOPRO.Application.Services
{
    public class BudgetExcelImportService
    {
        public List<string> ObtenerHojas(string rutaArchivo)
        {
            using var wb = new XLWorkbook(rutaArchivo);
            return wb.Worksheets.Select(x => x.Name).ToList();
        }

        public DataTable LeerVistaPrevia(string rutaArchivo, string hoja, int maxFilas = 25, bool primeraFilaEsEncabezado = true)
        {
            using var wb = new XLWorkbook(rutaArchivo);
            var ws = wb.Worksheet(hoja);
            ValidarSinCeldasCombinadas(ws);
            var rango = ws.RangeUsed();

            var dt = new DataTable();
            if (rango == null)
                return dt;

            int firstRow = rango.RangeAddress.FirstAddress.RowNumber;
            int lastRow = rango.RangeAddress.LastAddress.RowNumber;
            int firstCol = rango.RangeAddress.FirstAddress.ColumnNumber;
            int lastCol = rango.RangeAddress.LastAddress.ColumnNumber;

            var nombres = new List<string>();
            for (int c = firstCol; c <= lastCol; c++)
            {
                string header = primeraFilaEsEncabezado
                    ? (ws.Cell(firstRow, c).GetFormattedString() ?? string.Empty).Trim()
                    : string.Empty;

                if (string.IsNullOrWhiteSpace(header))
                    header = $"Columna {c - firstCol + 1}";

                string original = header;
                int dup = 2;
                while (nombres.Contains(header, StringComparer.OrdinalIgnoreCase))
                {
                    header = $"{original} ({dup++})";
                }
                nombres.Add(header);
                dt.Columns.Add(header);
            }

            int startRow = primeraFilaEsEncabezado ? firstRow + 1 : firstRow;
            int added = 0;
            for (int r = startRow; r <= lastRow && added < maxFilas; r++)
            {
                var row = dt.NewRow();
                bool hasData = false;
                for (int c = firstCol; c <= lastCol; c++)
                {
                    string value = ws.Cell(r, c).GetFormattedString();
                    row[c - firstCol] = value;
                    if (!string.IsNullOrWhiteSpace(value))
                        hasData = true;
                }

                if (!hasData)
                    continue;

                dt.Rows.Add(row);
                added++;
            }

            return dt;
        }

        public List<BudgetExcelImportRowDto> ImportarFilas(string rutaArchivo, string hoja, BudgetExcelImportColumnMapping mapping)
        {
            using var wb = new XLWorkbook(rutaArchivo);
            var ws = wb.Worksheet(hoja);
            ValidarSinCeldasCombinadas(ws);
            var rango = ws.RangeUsed();
            var resultado = new List<BudgetExcelImportRowDto>();

            if (rango == null)
                return resultado;

            int firstRow = rango.RangeAddress.FirstAddress.RowNumber;
            int lastRow = rango.RangeAddress.LastAddress.RowNumber;
            int startRow = mapping.PrimeraFilaEsEncabezado ? firstRow + 1 : firstRow;

            for (int r = startRow; r <= lastRow; r++)
            {
                string descripcion = LeerCelda(ws, r, mapping.ColumnaDescripcion);
                string clave = LeerCelda(ws, r, mapping.ColumnaClave);
                string unidad = LeerCelda(ws, r, mapping.ColumnaUnidad);
                string cantidadRaw = LeerCelda(ws, r, mapping.ColumnaCantidad);
                string tipo = LeerCelda(ws, r, mapping.ColumnaTipo);

                bool rowHasAny = !string.IsNullOrWhiteSpace(descripcion)
                    || !string.IsNullOrWhiteSpace(clave)
                    || !string.IsNullOrWhiteSpace(unidad)
                    || !string.IsNullOrWhiteSpace(cantidadRaw)
                    || !string.IsNullOrWhiteSpace(tipo);

                if (!rowHasAny)
                    continue;

                if (string.IsNullOrWhiteSpace(descripcion))
                    continue;

                resultado.Add(new BudgetExcelImportRowDto
                {
                    SourceRowNumber = r,
                    Clave = clave.Trim(),
                    Descripcion = descripcion.Trim(),
                    Unidad = unidad.Trim(),
                    Cantidad = TryParseDecimal(cantidadRaw),
                    TipoTexto = tipo.Trim()
                });
            }

            return resultado;
        }


        private static void ValidarSinCeldasCombinadas(IXLWorksheet ws)
        {
            if (ws.MergedRanges != null && ws.MergedRanges.Any())
            {
                throw new InvalidOperationException("Las celdas combinadas en la hoja de calculo no pueden ser procesadas correctamente. Se recomienda quitar la combinacion e intentarlo de nuevo.");
            }
        }

        private static string LeerCelda(IXLWorksheet ws, int row, int? col)
        {
            if (!col.HasValue || col.Value < 0)
                return string.Empty;

            return ws.Cell(row, col.Value + 1).GetFormattedString() ?? string.Empty;
        }

        private static decimal? TryParseDecimal(string? raw)
        {
            var clean = (raw ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(clean))
                return null;

            clean = clean.Replace("$", string.Empty).Trim();

            if (decimal.TryParse(clean, NumberStyles.Any, CultureInfo.CurrentCulture, out var value))
                return value;

            var invariantCandidate = clean.Replace(",", string.Empty);
            if (decimal.TryParse(invariantCandidate, NumberStyles.Any, CultureInfo.InvariantCulture, out value))
                return value;

            return null;
        }
    }
}
