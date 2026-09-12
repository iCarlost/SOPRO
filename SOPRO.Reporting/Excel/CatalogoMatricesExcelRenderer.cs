using ClosedXML.Excel;
using SOPRO.Application.Models.Reporting.MatrixCatalog;

namespace SOPRO.Reporting.Excel;

/// <summary>
/// Renderer Excel del "Catálogo de Matrices" sobre un modelo neutral.
///
/// Port fiel de <c>GeneradorExcelCatalogoMatrices</c> (SOPRO.WinForms.Services):
/// misma hoja, misma configuración de página, mismos estilos, zonas de plantilla
/// e identidad visual. Consume los valores YA resueltos del documento (importes
/// crudos, costos unitarios, totales) y reproduce exactamente el snapshot
/// semántico goldeneado en N7-18a.
/// </summary>
public sealed class CatalogoMatricesExcelRenderer
{
    // ── Colores (misma política que el generador legacy) ───────────────────
    private const string ColorEncabezadoMatriz = "#33334C";
    private const string ColorFilaMatriz        = "#E8EAF6";
    private const string ColorEncabezadoCols    = "#4A4A6A";
    private const string ColorSuma              = "#E3F2FD";
    private const string ColorAlt               = "#F5F5F5";

    /// <summary>
    /// Renderiza el documento a bytes XLSX (sin tocar disco).
    /// </summary>
    /// <returns>Contenido del archivo XLSX (.xlsx).</returns>
    public byte[] Render(MatrixCatalogReportDocument document)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));

        using var wb = new XLWorkbook();
        var ws = wb.Worksheets.Add("Catálogo");

        // Orientación carta horizontal, igual que OPUS
        ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
        ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
        ws.PageSetup.FitToPages(1, 0);
        ws.PageSetup.Margins.Left    = 0.5;
        ws.PageSetup.Margins.Right   = 0.5;
        ws.PageSetup.Margins.Top     = 0.75;
        ws.PageSetup.Margins.Bottom  = 0.75;

        // A=Prefijo, B=Clave, C=Descripción, D=Unidad, E=Cantidad, F=C.U., G=Total
        ws.Column(1).Width =  4;
        ws.Column(2).Width = 16;
        ws.Column(3).Width = 46;
        ws.Column(4).Width =  8;
        ws.Column(5).Width = 12;
        ws.Column(6).Width = 14;
        ws.Column(7).Width = 14;
        int numCols = 7;

        int fila = 1;

        // ── Encabezado estándar SOPRO ────────────────────────────────────
        fila = EscribirEncabezado(ws, document, numCols, fila);

        // ── Título del catálogo ──────────────────────────────────────────
        var rngTitulo = ws.Range(fila, 1, fila, numCols);
        AplicarTitulo(rngTitulo, document);
        ws.Row(fila).Height = 22;
        fila++;

        // ── Encabezado de columnas ───────────────────────────────────────
        EscribirEncabezadoColumnas(ws, fila, numCols);
        fila++;

        // ── Matrices (ya ordenadas por clave en el modelo) ───────────────
        foreach (var m in document.Matrices)
            fila = EscribirMatriz(ws, m, fila, numCols);

        // Borde exterior de toda la tabla
        if (fila > 4)
            ws.Range(3, 1, fila - 1, numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }

    // ── Encabezado estándar SOPRO (port de ReporteEncabezadoHelper) ───────
    private static int EscribirEncabezado(IXLWorksheet ws, MatrixCatalogReportDocument document, int numCols, int fila)
    {
        int c1 = 1;
        int c2 = numCols / 3 + 1;
        int c3 = numCols * 2 / 3 + 1;

        EscribirZona(ws, fila, c1, c2 - 1, document.Header.Left);
        EscribirZona(ws, fila, c2, c3 - 1, document.Header.Center);
        EscribirZona(ws, fila, c3, numCols, document.Header.Right);

        ws.Row(fila).Height = document.PageHeights.HeaderHeight * 0.75;
        fila++;

        // Línea separadora azul igual que en explosión
        ws.Range(fila, 1, fila, numCols).Style.Border.TopBorder      = XLBorderStyleValues.Medium;
        ws.Range(fila, 1, fila, numCols).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

        return fila + 1;
    }

    private static void EscribirZona(IXLWorksheet ws, int fila, int colIni, int colFin, MatrixCatalogZone zone)
    {
        if (colIni > colFin) return;
        var rango = ws.Range(fila, colIni, fila, colFin);
        rango.Merge();

        if (zone.Kind == MatrixCatalogZoneKind.Imagen && File.Exists(zone.Content))
        {
            try { ws.AddPicture(zone.Content).MoveTo(ws.Cell(fila, colIni)).WithSize(120, 50); }
            catch { }
        }
        else
        {
            rango.FirstCell().Value = zone.Content;
        }

        var est = rango.Style;
        est.Font.FontName        = zone.Style.FontName;
        est.Font.FontSize        = zone.Style.Size;
        est.Font.Bold            = zone.Style.Bold;
        est.Font.Italic          = zone.Style.Italic;
        est.Alignment.WrapText   = true;
        est.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
        est.Alignment.Horizontal = zone.Style.Alignment switch
        {
            MatrixCatalogTextAlignment.Centro  => XLAlignmentHorizontalValues.Center,
            MatrixCatalogTextAlignment.Derecha => XLAlignmentHorizontalValues.Right,
            _                                 => XLAlignmentHorizontalValues.Left,
        };
    }

    private static void AplicarTitulo(IXLRange range, MatrixCatalogReportDocument document)
    {
        // Aplica el fondo #33334C (política del renderer Excel) y el estilo
        // semántico del documento para fuente/tamaño/peso/color de texto.
        // El Merge ANTES del estilo es intencional (mismo orden que el legacy):
        // en ClosedXML, Merge() copia el estilo de la celda origen a toda la
        // zona combinada, lo que rompería la paridad del snapshot dorado
        // (el golden solo estiliza la celda A de la fila de título).
        range.Merge();
        var cell = range.FirstCell();
        cell.Value = document.Title;
        cell.Style.Font.Bold = document.TitleStyle.Bold;
        cell.Style.Font.Italic = document.TitleStyle.Italic;
        cell.Style.Font.FontName = string.IsNullOrWhiteSpace(document.TitleStyle.FontName)
            ? "Segoe UI"
            : document.TitleStyle.FontName;
        cell.Style.Font.FontSize = document.TitleStyle.Size;
        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
        cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorEncabezadoMatriz, "#1F4E79");
        cell.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(document.TitleStyle.TextColorHex, "#FFFFFF");
    }

    private static void EscribirEncabezadoColumnas(IXLWorksheet ws, int fila, int numCols)
    {
        string[] headers = { "", "Clave", "Descripción", "Unidad", "Cantidad", "Costo Unitario", "Total" };
        for (int c = 1; c <= numCols; c++)
        {
            var cell = ws.Cell(fila, c);
            cell.Value = headers[c - 1];
            cell.Style.Font.Bold            = true;
            cell.Style.Font.FontSize        = 9;
            cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorEncabezadoCols);
            cell.Style.Font.FontColor       = XLColor.White;
            cell.Style.Alignment.Horizontal = c >= 5
                ? XLAlignmentHorizontalValues.Right
                : c == 4 ? XLAlignmentHorizontalValues.Center
                : XLAlignmentHorizontalValues.Left;
            cell.Style.Border.BottomBorder      = XLBorderStyleValues.Medium;
            cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");
        }
        ws.Row(fila).Height = 18;
    }

    // ── Una matriz completa ─────────────────────────────────────────────────
    private static int EscribirMatriz(IXLWorksheet ws, MatrixCatalogMatrix m, int fila, int numCols)
    {
        // Col A: "+" | Col B: Clave | Col C: Descripción | Col D: Unidad
        ws.Cell(fila, 1).Value = "+";
        ws.Cell(fila, 1).Style.Font.Bold = true;
        ws.Cell(fila, 1).Style.Font.FontSize = 9;

        ws.Cell(fila, 2).Value = m.Key ?? "";
        ws.Cell(fila, 2).Style.Font.Bold = true;
        ws.Cell(fila, 2).Style.Font.FontSize = 9;

        ws.Cell(fila, 3).Value = m.Description ?? "";
        ws.Cell(fila, 3).Style.Font.Bold = true;
        ws.Cell(fila, 3).Style.Font.FontSize = 9;
        ws.Cell(fila, 3).Style.Alignment.WrapText = true;

        ws.Cell(fila, 4).Value = m.Unit ?? "";
        ws.Cell(fila, 4).Style.Font.Bold = true;
        ws.Cell(fila, 4).Style.Font.FontSize = 9;
        ws.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        var rngHeader = ws.Range(fila, 1, fila, numCols);
        rngHeader.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorFilaMatriz);
        rngHeader.Style.Border.BottomBorder  = XLBorderStyleValues.Thin;

        ws.Row(fila).Height = 18;
        fila++;

        // ── Componentes (la aritmética ya viene resuelta en el modelo) ───
        bool alt = false;
        foreach (var comp in m.Components)
        {
            fila = EscribirComponente(ws, comp, fila, alt);
            alt = !alt;
        }

        // ── Fila Suma ─────────────────────────────────────────────────────
        var rngSuma = ws.Range(fila, 1, fila, numCols - 1);
        rngSuma.Merge();
        rngSuma.Value = "Suma";
        rngSuma.Style.Font.Bold               = true;
        rngSuma.Style.Font.FontSize           = 9;
        rngSuma.Style.Alignment.Horizontal    = XLAlignmentHorizontalValues.Right;
        rngSuma.Style.Fill.BackgroundColor    = ExcelColorHelper.SafeFromHtml(ColorSuma);

        ws.Cell(fila, numCols).Value = m.DirectCost;
        ws.Cell(fila, numCols).Style.NumberFormat.Format = "#,##0.00";
        ws.Cell(fila, numCols).Style.Font.Bold    = true;
        ws.Cell(fila, numCols).Style.Font.FontSize = 9;
        ws.Cell(fila, numCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        ws.Cell(fila, numCols).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSuma);

        ws.Range(fila, 1, fila, numCols).Style.Border.TopBorder    = XLBorderStyleValues.Thin;
        ws.Range(fila, 1, fila, numCols).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
        ws.Row(fila).Height = 16;
        fila++;

        // Espacio entre matrices
        ws.Row(fila).Height = 6;
        fila++;

        return fila;
    }

    // ── Un componente ─────────────────────────────────────────────────────
    private static int EscribirComponente(IXLWorksheet ws, MatrixCatalogComponent comp, int fila, bool alt)
    {
        string bgColor = alt ? ColorAlt : "#FFFFFF";

        ws.Cell(fila, 1).Value = comp.Prefix;
        ws.Cell(fila, 1).Style.Font.Bold            = comp.Prefix == "+";
        ws.Cell(fila, 1).Style.Font.FontSize        = 9;
        ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

        ws.Cell(fila, 2).Value = comp.Key;
        ws.Cell(fila, 2).Style.Font.FontSize = 9;

        ws.Cell(fila, 3).Value = comp.Description;
        ws.Cell(fila, 3).Style.Font.FontSize  = 9;
        ws.Cell(fila, 3).Style.Alignment.WrapText = true;

        ws.Cell(fila, 4).Value = comp.Unit;
        ws.Cell(fila, 4).Style.Font.FontSize          = 9;
        ws.Cell(fila, 4).Style.Alignment.Horizontal   = XLAlignmentHorizontalValues.Center;

        ws.Cell(fila, 5).Value = comp.Quantity;
        ws.Cell(fila, 5).Style.NumberFormat.Format    = "0.00000";
        ws.Cell(fila, 5).Style.Font.FontSize          = 9;
        ws.Cell(fila, 5).Style.Alignment.Horizontal   = XLAlignmentHorizontalValues.Right;

        ws.Cell(fila, 6).Value = comp.UnitCost;
        ws.Cell(fila, 6).Style.NumberFormat.Format    = "#,##0.00";
        ws.Cell(fila, 6).Style.Font.FontSize          = 9;
        ws.Cell(fila, 6).Style.Alignment.Horizontal   = XLAlignmentHorizontalValues.Right;

        ws.Cell(fila, 7).Value = comp.Amount;
        ws.Cell(fila, 7).Style.NumberFormat.Format    = "#,##0.00";
        ws.Cell(fila, 7).Style.Font.FontSize          = 9;
        ws.Cell(fila, 7).Style.Alignment.Horizontal   = XLAlignmentHorizontalValues.Right;

        var rng = ws.Range(fila, 1, fila, 7);
        rng.Style.Fill.BackgroundColor        = ExcelColorHelper.SafeFromHtml(bgColor);
        rng.Style.Border.BottomBorder         = XLBorderStyleValues.Hair;
        rng.Style.Border.BottomBorderColor    = XLColor.LightGray;

        ws.Row(fila).Height = 15;
        return fila + 1;
    }
}