using System.Globalization;
using ClosedXML.Excel;

namespace SOPRO.WinForms.Tests.TestInfrastructure;

/// <summary>
/// Snapshot semántico del XLSX de catálogo de matrices generado por el generador legacy.
/// Congela lo que importa para caracterizar el comportamiento, NO los bytes del archivo:
///   - nombre/orden de hojas
///   - configuración de página y anchos de columna
///   - rangos combinados
///   - celdas con contenido o estilo (dirección, tipo, valor invariante, formato, fuente,
///     alineación, relleno)
///
/// Valores: los numéricos se serializan con "R" para conservar el decimal exacto almacenado
/// (p.ej. 30.015 de la fila 23 de N0-TABLA) e independientemente del número de decimales del
/// formato. La comparación es entre JSON exactos (orden de celdas y propiedades) sin
/// normalizar nada, para que cualquier cambio en el generador rompa el golden.
/// </summary>
internal sealed partial class XlsxSemanticSnapshot
{
    public string Schema { get; set; } = Snapshots.SchemaExcel;
    public string Escenario { get; set; } = "";
    public string FuenteDatos { get; set; } = "";
    public string Guardian { get; set; } = "sin-golden";
    public string Generador { get; set; } = "GeneradorExcelCatalogoMatrices";
    public string ClosedXml { get; set; } = "0.102.2";
    public List<string> Hojas { get; set; } = new();
    public ConfigPaginaData? ConfigPagina { get; set; }
    public List<double> AnchosColumna { get; set; } = new();
    public List<string> RangosCombinados { get; set; } = new();
    public List<string> Matrices { get; set; } = new();
    public List<CeldaData> Celdas { get; set; } = new();

    public static XlsxSemanticSnapshot Of(string archivoXlsx, string escenario, string fuenteDatos,
        IEnumerable<string> matricesEsperadas, string guardian)
    {
        using var wb = new XLWorkbook(archivoXlsx);
        var ws = wb.Worksheet(1);
        var snap = new XlsxSemanticSnapshot
        {
            Escenario = escenario,
            FuenteDatos = fuenteDatos,
            Guardian = guardian,
            Hojas = wb.Worksheets.Select(h => h.Name).ToList(),
            Matrices = matricesEsperadas.ToList(),
        };

        var ps = ws.PageSetup;
        snap.ConfigPagina = new ConfigPaginaData
        {
            Orientacion = ps.PageOrientation.ToString(),
            Papel = ps.PaperSize.ToString(),
            AnchoPaginas = ps.PagesWide,
            AltoPaginas = ps.PagesTall,
            MargenSuperior = ps.Margins.Top,
            MargenInferior = ps.Margins.Bottom,
            MargenIzquierdo = ps.Margins.Left,
            MargenDerecho = ps.Margins.Right,
        };
        snap.AnchosColumna = Enumerable.Range(1, 7).Select(c => ws.Column(c).Width).ToList();
        snap.RangosCombinados = ws.MergedRanges
            .Select(r => r.RangeAddress.ToString() ?? "")
            .OrderBy(x => x, StringComparer.Ordinal)
            .ToList();

        var used = ws.RangeUsed();
        if (used is null)
            return snap;

        var ultimaFila = used.RangeAddress.LastAddress.RowNumber;
        for (int f = 1; f <= ultimaFila; f++)
        {
            for (int c = 1; c <= 7; c++)
            {
                var cell = ws.Cell(f, c);
                var data = CeldaData.De(cell);
                if (data != null)
                    snap.Celdas.Add(data);
            }
        }

        return snap;
    }

    public sealed class ConfigPaginaData
    {
        public string Orientacion { get; set; } = "";
        public string Papel { get; set; } = "";
        public int AnchoPaginas { get; set; }
        public int AltoPaginas { get; set; }
        public double MargenSuperior { get; set; }
        public double MargenInferior { get; set; }
        public double MargenIzquierdo { get; set; }
        public double MargenDerecho { get; set; }
    }

    public sealed class CeldaData
    {
        public int F { get; set; }
        public int C { get; set; }
        public string? T { get; set; }
        public string? V { get; set; }
        public string? Fmt { get; set; }
        public bool B { get; set; }
        public double Fz { get; set; }
        public string? Hal { get; set; }
        public string? Fill { get; set; }
        public bool Wrap { get; set; }

        public static CeldaData? De(IXLCell cell)
        {
            bool tieneValor = !cell.IsEmpty();
            string? t = null, v = null;
            if (tieneValor)
            {
                switch (cell.DataType)
                {
                    case XLDataType.Text:
                        t = "txt"; v = cell.GetString(); break;
                    case XLDataType.Number:
                        t = "num"; v = cell.GetDouble().ToString("R", CultureInfo.InvariantCulture); break;
                    case XLDataType.DateTime:
                        t = "fecha"; v = cell.GetDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture); break;
                    default:
                        t = "otro"; v = cell.GetFormattedString(); break;
                }
            }

            string? fmt = null;
            var nf = cell.Style.NumberFormat.Format;
            if (!string.IsNullOrWhiteSpace(nf) && !string.Equals(nf, "General", StringComparison.Ordinal))
                fmt = nf;

            bool bold = cell.Style.Font.Bold;
            double fz = cell.Style.Font.FontSize;
            string? hal = null;
            switch (cell.Style.Alignment.Horizontal)
            {
                case XLAlignmentHorizontalValues.Center: hal = "C"; break;
                case XLAlignmentHorizontalValues.Right: hal = "D"; break;
                case XLAlignmentHorizontalValues.Left: hal = "I"; break;
            }
            bool wrap = cell.Style.Alignment.WrapText;

            string? fill = null;
            try
            {
                var argb = cell.Style.Fill.BackgroundColor.Color.ToArgb();
                if (argb != 0 && (argb & 0xFF000000) == 0xFF000000)
                    fill = argb.ToString("X8");
            }
            catch
            {
                fill = null;
            }

            var data = new CeldaData { F = cell.Address.RowNumber, C = cell.Address.ColumnNumber };
            bool relevante = tieneValor || fmt != null || bold || hal != null || fill != null || wrap;
            if (tieneValor) { data.T = t; data.V = v; }
            if (fmt != null) data.Fmt = fmt;
            data.B = bold;
            data.Fz = fz;
            if (hal != null) data.Hal = hal;
            if (fill != null) data.Fill = fill;
            data.Wrap = wrap;
            return relevante ? data : null;
        }
    }
}