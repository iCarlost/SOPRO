using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;

namespace SOPRO.Reporting.Pdf;

/// <summary>
/// Renderer PDF del "Catálogo de Matrices" sobre un modelo neutral.
///
/// Port fiel del mapa de producción de
/// <c>GeneradorPdfCatalogoMatrices</c> (SOPRO.WinForms.Services): orientación
/// automática, sección letter con encabezado/pie clásico SOPRO o libre, y
/// cuerpo con título y tabla de 7 columnas.
///
/// Los valores (importes, costos, totales, claves) ya vienen resueltos en el
/// documento; este renderer solo presenta. Los tokens {{pagina}}/{{total_paginas}}
/// se convierten en campos MigraDoc en <see cref="PlantillaLibrePdfRenderer"/>.
/// </summary>
public sealed class CatalogoMatricesPdfRenderer
{
    private const string StandardBackgroundHex = "#1F4E79";
    private const string StandardTextHex = "#FFFFFF";

    private static readonly double[] BaseColumnCm = { 1.2, 3.0, 12.4, 2.0, 2.6, 3.0, 3.0 };

    /// <summary>
    /// Cultura usada por el render en este render. Por defecto la cultura
    /// ambiente (comportamiento legacy: CurrentCulture en los ToString de
    /// formato); se hace explícita y observable para que la API no dependa de
    /// forma implícita del hilo (dictamen Oracle N7-18c).
    /// </summary>
    private CultureInfo _culture = CultureInfo.CurrentCulture;

    /// <summary>
    /// Renderiza el documento a bytes PDF. El parámetro <paramref name="culture"/>
    /// controla el formateo numérico (Cantidad "0.00000", Costo/Importe "#,##0.00");
    /// si es null se usa <see cref="CultureInfo.CurrentCulture"/> (paridad legacy).
    /// </summary>
    public byte[] Render(MatrixCatalogReportDocument document, CultureInfo? culture = null)
    {
        if (document == null) throw new ArgumentNullException(nameof(document));
        _culture = culture ?? CultureInfo.CurrentCulture;

        var doc = new Document();
        doc.Info.Title = document.Title ?? "Catálogo de Matrices";
        DefinirEstilos(doc);

        var orientation = ReportPageLayoutHelper.DetermineAutoOrientation(
            new[]
            {
                ("Tipo", 70),
                ("Clave", 160),
                ("Descripcion", 420),
                ("Unidad", 100),
                ("Cantidad", 120),
                ("CostoUnitario", 140),
                ("Total", 140)
            },
            1.0, 1.0);

        var section = doc.AddSection();
        section.PageSetup.PageFormat = PageFormat.Letter;
        section.PageSetup.Orientation = orientation;
        section.PageSetup.DifferentFirstPageHeaderFooter = true;

        bool headerLibre = document.HeaderElements.Count > 0;
        bool footerLibre = document.FooterElements.Count > 0;

        double headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(document.PageHeights, headerLibre);
        double footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(document.PageHeights, footerLibre);

        section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
        section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
        section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
        section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
        section.PageSetup.TopMargin = Unit.FromCentimeter(
            headerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoSuperiorCm(headerLibre, 1.0));
        section.PageSetup.BottomMargin = Unit.FromCentimeter(
            footerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoInferiorCm(footerLibre, 0.8));

        EscribirHeader(section, document, headerLibre, headerHeightCm);
        EscribirFooter(section, document, footerLibre, footerHeightCm);
        EscribirCuerpo(section, document);

        using var ms = new MemoryStream();
        var renderer = new PdfDocumentRenderer { Document = doc };
        renderer.RenderDocument();
        renderer.PdfDocument.Save(ms, false);
        return ms.ToArray();
    }

    private static void DefinirEstilos(Document doc)
    {
        var normal = doc.Styles["Normal"]!;
        normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
        normal.Font.Size = 9;

        var title = doc.Styles.AddStyle("CatalogoMatricesTitle", "Normal");
        title.Font.Bold = true;
        title.Font.Size = 12;
        title.ParagraphFormat.Alignment = MParagraphAlignment.Center;

        var project = doc.Styles.AddStyle("CatalogoMatricesProject", "Normal");
        project.Font.Size = 10;
        project.ParagraphFormat.Alignment = MParagraphAlignment.Center;
    }

    // ── Encabezado / pie ────────────────────────────────────────────────────
    private static void EscribirHeader(Section section, MatrixCatalogReportDocument document,
        bool headerLibre, double headerHeightCm)
    {
        if (headerLibre)
        {
            PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.Primary, section,
                document.HeaderElements, document.PageHeights.HeaderHeightDmm);
            PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.FirstPage, section,
                document.HeaderElements, document.PageHeights.HeaderHeightDmm);
            return;
        }

        var primary = section.Headers.Primary.AddTable();
        primary.Borders.Visible = false;
        primary.Rows.LeftIndent = 0;
        ReportPageLayoutHelper.AddHeaderFooterColumns(primary, section);
        var row = primary.AddRow();
        row.HeightRule = RowHeightRule.AtLeast;
        row.Height = Unit.FromCentimeter(headerHeightCm);
        EscribirCeldaPlantilla(row.Cells[0], document.Header.Left, false);
        EscribirCeldaPlantilla(row.Cells[1], document.Header.Center, false);
        EscribirCeldaPlantilla(row.Cells[2], document.Header.Right, false);

        var first = section.Headers.FirstPage.AddTable();
        first.Borders.Visible = false;
        first.Rows.LeftIndent = 0;
        ReportPageLayoutHelper.AddHeaderFooterColumns(first, section);
        var firstRow = first.AddRow();
        firstRow.HeightRule = RowHeightRule.AtLeast;
        firstRow.Height = Unit.FromCentimeter(headerHeightCm);
        EscribirCeldaPlantilla(firstRow.Cells[0], document.Header.Left, false);
        EscribirCeldaPlantilla(firstRow.Cells[1], document.Header.Center, false);
        EscribirCeldaPlantilla(firstRow.Cells[2], document.Header.Right, false);
    }

    private static void EscribirFooter(Section section, MatrixCatalogReportDocument document,
        bool footerLibre, double footerHeightCm)
    {
        if (footerLibre)
        {
            PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.Primary, section,
                document.FooterElements, document.PageHeights.FooterHeightDmm);
            PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.FirstPage, section,
                document.FooterElements, document.PageHeights.FooterHeightDmm);
            return;
        }

        var primary = section.Footers.Primary.AddTable();
        primary.Borders.Visible = false;
        primary.Rows.LeftIndent = 0;
        ReportPageLayoutHelper.AddHeaderFooterColumns(primary, section);
        var row = primary.AddRow();
        row.HeightRule = RowHeightRule.AtLeast;
        row.Height = Unit.FromCentimeter(footerHeightCm);
        EscribirCeldaPlantilla(row.Cells[0], document.Footer.Left, true);
        EscribirCeldaPlantilla(row.Cells[1], document.Footer.Center, true);
        EscribirCeldaPlantilla(row.Cells[2], document.Footer.Right, true);

        var first = section.Footers.FirstPage.AddTable();
        first.Borders.Visible = false;
        first.Rows.LeftIndent = 0;
        ReportPageLayoutHelper.AddHeaderFooterColumns(first, section);
        var firstRow = first.AddRow();
        firstRow.HeightRule = RowHeightRule.AtLeast;
        firstRow.Height = Unit.FromCentimeter(footerHeightCm);
        EscribirCeldaPlantilla(firstRow.Cells[0], document.Footer.Left, true);
        EscribirCeldaPlantilla(firstRow.Cells[1], document.Footer.Center, true);
        EscribirCeldaPlantilla(firstRow.Cells[2], document.Footer.Right, true);
    }

    private static void EscribirCeldaPlantilla(Cell cell, MatrixCatalogZone zone, bool soportaCamposPagina)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        cell.Format.Alignment = ConvertirAlineacionTexto(zone.Style.Alignment);
        cell.Borders.Visible = false;

        if (zone.Kind == MatrixCatalogZoneKind.Imagen && File.Exists(zone.Content))
        {
            var img = cell.AddImage(zone.Content);
            img.LockAspectRatio = true;
            img.Height = Unit.FromCentimeter(1.5);
            return;
        }

        var p = cell.AddParagraph();
        p.Format.Alignment = ConvertirAlineacionTexto(zone.Style.Alignment);
        p.Format.SpaceAfter = 0;
        p.Format.SpaceBefore = 0;
        PdfFontHelper.ApplyFont(p.Format.Font, zone.Style.FontName, zone.Style.Size <= 0 ? 9 : zone.Style.Size,
            zone.Style.Bold, zone.Style.Italic);

        var texto = zone.Content ?? string.Empty;
        if (!soportaCamposPagina)
        {
            p.AddText(texto);
            return;
        }

        PlantillaLibrePdfRenderer.AgregarTextoConCamposPagina(p, texto);
    }

    // ── Cuerpo ──────────────────────────────────────────────────────────────
    private void EscribirCuerpo(Section section, MatrixCatalogReportDocument document)
    {
        var pTitle = section.AddParagraph(document.Title ?? string.Empty, "CatalogoMatricesTitle");
        pTitle.Format.Alignment = MParagraphAlignment.Center;
        pTitle.Format.Font.Name = PdfFontHelper.NormalizeFontName(
            string.IsNullOrWhiteSpace(document.TitleStyle.FontName) ? "Segoe UI" : document.TitleStyle.FontName);
        pTitle.Format.Font.Size = document.TitleStyle.Size > 0 ? document.TitleStyle.Size : 14;
        pTitle.Format.Font.Bold = document.TitleStyle.Bold;
        pTitle.Format.Font.Italic = document.TitleStyle.Italic;
        pTitle.Format.Shading.Color = ParseColor(StandardBackgroundHex);
        pTitle.Format.Font.Color = ParseColor(StandardTextHex);
        pTitle.Format.SpaceAfter = Unit.FromCentimeter(0.12);
        pTitle.Format.KeepWithNext = true;

        var pProject = section.AddParagraph(document.ProjectName ?? string.Empty, "CatalogoMatricesProject");
        pProject.Format.Shading.Color = ParseColor("#E8EAF6");
        pProject.Format.SpaceAfter = Unit.FromCentimeter(0.2);
        pProject.Format.KeepWithNext = true;

        var table = section.AddTable();
        table.Rows.LeftIndent = 0;
        table.Borders.Width = 0.25;
        table.Borders.Color = ParseColor("#DDDDDD");
        table.TopPadding = 1.5;
        table.BottomPadding = 1.5;
        table.LeftPadding = 2;
        table.RightPadding = 2;

        AgregarColumnas(table, section);
        EscribirEncabezadoColumnas(table);

        bool filaAlternada = false;
        foreach (var matriz in document.Matrices)
        {
            EscribirFilaMatriz(table, matriz);

            foreach (var comp in matriz.Components)
            {
                EscribirFilaComponente(table, comp, filaAlternada);
                filaAlternada = !filaAlternada;
            }

            EscribirFilaSuma(table, matriz.DirectCost);
            EscribirFilaSeparacion(table);
            filaAlternada = false;
        }
    }

    private static void AgregarColumnas(Table table, Section section)
    {
        double availableCm = ReportPageLayoutHelper.GetLetterContentWidthCm(section);
        double factor = availableCm / BaseColumnCm.Sum();
        foreach (var width in BaseColumnCm)
            table.AddColumn(Unit.FromCentimeter(width * factor));
    }

    private static void EscribirEncabezadoColumnas(Table table)
    {
        string[] headers = { "", "Clave", "Descripción", "Unidad", "Cantidad", "Costo Unitario", "Total" };
        var head = table.AddRow();
        head.HeadingFormat = true;
        head.HeightRule = RowHeightRule.AtLeast;
        head.Height = Unit.FromPoint(18);
        head.Shading.Color = ParseColor("#4A4A6A");

        for (int i = 0; i < headers.Length; i++)
        {
            var cell = head.Cells[i];
            cell.VerticalAlignment = VerticalAlignment.Center;
            var p = cell.AddParagraph(headers[i]);
            p.Format.Alignment = i >= 4 ? MParagraphAlignment.Right : i == 3 ? MParagraphAlignment.Center : MParagraphAlignment.Left;
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
            PdfFontHelper.ApplyFont(p.Format.Font, "Segoe UI", 9, true, false);
            p.Format.Font.Color = ParseColor(StandardTextHex);
        }
    }

    private static void EscribirFilaMatriz(Table table, MatrixCatalogMatrix matriz)
    {
        var row = table.AddRow();
        row.HeightRule = RowHeightRule.AtLeast;
        row.Height = Unit.FromPoint(18);
        row.Shading.Color = ParseColor("#E8EAF6");
        row.Borders.Bottom.Width = 0.25;
        row.Borders.Bottom.Color = ParseColor("#C9C9D8");
        row.KeepWith = 1;

        AgregarTexto(row.Cells[0], "+", MParagraphAlignment.Center, bold: true);
        AgregarTexto(row.Cells[1], matriz.Key ?? string.Empty, MParagraphAlignment.Left, bold: true);
        AgregarTexto(row.Cells[2], matriz.Description ?? string.Empty, MParagraphAlignment.Left, bold: true);
        AgregarTexto(row.Cells[3], matriz.Unit ?? string.Empty, MParagraphAlignment.Center, bold: true);
        AgregarTexto(row.Cells[4], string.Empty, MParagraphAlignment.Right, bold: false);
        AgregarTexto(row.Cells[5], string.Empty, MParagraphAlignment.Right, bold: false);
        AgregarTexto(row.Cells[6], string.Empty, MParagraphAlignment.Right, bold: false);
    }

    private void EscribirFilaComponente(Table table, MatrixCatalogComponent comp, bool alt)
    {
        var row = table.AddRow();
        row.HeightRule = RowHeightRule.AtLeast;
        row.Height = Unit.FromPoint(15);
        row.Shading.Color = ParseColor(alt ? "#F5F5F5" : "#FFFFFF");
        row.Borders.Bottom.Width = 0.15;
        row.Borders.Bottom.Color = ParseColor("#E0E0E0");

        AgregarTexto(row.Cells[0], comp.Prefix ?? string.Empty, MParagraphAlignment.Center, comp.Prefix == "+");
        AgregarTexto(row.Cells[1], comp.Key ?? string.Empty, MParagraphAlignment.Left, false);
        AgregarTexto(row.Cells[2], comp.Description ?? string.Empty, MParagraphAlignment.Left, false);
        AgregarTexto(row.Cells[3], comp.Unit ?? string.Empty, MParagraphAlignment.Center, false);
        AgregarTexto(row.Cells[4], comp.Quantity.ToString("0.00000", _culture), MParagraphAlignment.Right, false);
        AgregarTexto(row.Cells[5], comp.UnitCost.ToString("#,##0.00", _culture), MParagraphAlignment.Right, false);
        AgregarTexto(row.Cells[6], comp.Amount.ToString("#,##0.00", _culture), MParagraphAlignment.Right, false);
    }

    private void EscribirFilaSuma(Table table, decimal total)
    {
        var row = table.AddRow();
        row.HeightRule = RowHeightRule.AtLeast;
        row.Height = Unit.FromPoint(16);
        row.Shading.Color = ParseColor("#E3F2FD");
        row.Borders.Top.Width = 0.25;
        row.Borders.Bottom.Width = 0.4;
        row.Borders.Bottom.Color = ParseColor("#9DB6D0");
        row.KeepWith = 1;

        row.Cells[0].MergeRight = 5;
        AgregarTexto(row.Cells[0], "Suma", MParagraphAlignment.Right, true);
        AgregarTexto(row.Cells[6], total.ToString("#,##0.00", _culture), MParagraphAlignment.Right, true);
    }

    private static void EscribirFilaSeparacion(Table table)
    {
        var row = table.AddRow();
        row.HeightRule = RowHeightRule.Exactly;
        row.Height = Unit.FromPoint(6);
        row.Borders.Visible = false;
        row.Shading.Color = ParseColor(StandardTextHex);
        for (int i = 0; i < row.Cells.Count; i++)
        {
            row.Cells[i].Borders.Visible = false;
            row.Cells[i].AddParagraph();
        }
    }

    private static void AgregarTexto(Cell cell, string texto, MParagraphAlignment alignment, bool bold)
    {
        cell.VerticalAlignment = VerticalAlignment.Center;
        var p = cell.AddParagraph(texto ?? string.Empty);
        p.Format.Alignment = alignment;
        p.Format.SpaceAfter = 0;
        p.Format.SpaceBefore = 0;
        PdfFontHelper.ApplyFont(p.Format.Font, "Segoe UI", 9, bold, false);
    }

    private static MParagraphAlignment ConvertirAlineacionTexto(MatrixCatalogTextAlignment alineacion)
        => alineacion switch
        {
            MatrixCatalogTextAlignment.Derecha => MParagraphAlignment.Right,
            MatrixCatalogTextAlignment.Centro => MParagraphAlignment.Center,
            _ => MParagraphAlignment.Left,
        };

    private static MColor ParseColor(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return MColor.Parse("#000000");
        try
        {
            var c = System.Drawing.ColorTranslator.FromHtml(value);
            return MColor.FromRgb(c.R, c.G, c.B);
        }
        catch { return MColor.Parse("#000000"); }
    }
}