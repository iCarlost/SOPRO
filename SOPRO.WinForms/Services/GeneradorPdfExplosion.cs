using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfExplosion
    {
        private readonly ReporteService _svc;

        public GeneradorPdfExplosion(ReporteService svc) => _svc = svc;

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            List<ColumnaExplosion> columnas,
            string filtro,
            Dictionary<int, DatosInsumo> materiales,
            Dictionary<int, DatosInsumo> manoObra,
            Dictionary<int, DatosInsumo> maquinaria,
            Dictionary<int, DatosInsumo> herramientas,
            decimal costoDirectoTotal,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            var cols = columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (!cols.Any())
                throw new InvalidOperationException("No hay columnas visibles para exportar.");

            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"ExplosionInsumos_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            var doc = new Document();
            DefinirEstilos(doc);

            var orientation = DeterminarOrientacionExplosion(cols, 1.0, 1.0);

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = orientation;

            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(plantilla, elementosPdf);
            var footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(plantilla, elementosPdf);

            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoSuperiorCm(plantilla, elementosPdf, 0.8));
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoInferiorCm(plantilla, elementosPdf, 0.8));

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, cols, filtro, materiales, manoObra, maquinaria, herramientas, costoDirectoTotal, tituloCfg);

            var renderer = new PdfDocumentRenderer() { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = "Segoe UI";
            normal.Font.Size = 8.5;

            var title = doc.Styles.AddStyle("ExplosionTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 12;
            title.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var project = doc.Styles.AddStyle("ExplosionProject", "Normal");
            project.Font.Size = 10;
            project.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var header = doc.Styles.AddStyle("ExplosionHeader", "Normal");
            header.Font.Bold = true;
            header.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var section = doc.Styles.AddStyle("ExplosionSection", "Normal");
            section.Font.Bold = true;
            section.Font.Size = 10;

            var subtotal = doc.Styles.AddStyle("ExplosionSubtotal", "Normal");
            subtotal.Font.Bold = true;

            var total = doc.Styles.AddStyle("ExplosionTotal", "Normal");
            total.Font.Bold = true;
            total.Font.Size = 11;
        }

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
            {
                PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.FirstPage, section, proyecto, plantilla, elementosPdf, _svc);
                return;
            }
            var primary = section.Headers.Primary.AddTable();
            primary.Borders.Visible = false;
            ReportPageLayoutHelper.AddHeaderFooterColumns(primary, section);
            var row = primary.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(headerHeightCm);

            EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño, plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                plantilla.EncabezadoIzqAlineacion, false);
            EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño, plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                plantilla.EncabezadoCenAlineacion, false);
            EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño, plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                plantilla.EncabezadoDerAlineacion, false);

            var first = section.Headers.FirstPage.AddTable();
            first.Borders.Visible = false;
            ReportPageLayoutHelper.AddHeaderFooterColumns(first, section);
            var firstRow = first.AddRow();
            firstRow.HeightRule = RowHeightRule.AtLeast;
            firstRow.Height = Unit.FromCentimeter(headerHeightCm);

            EscribirCeldaPlantilla(firstRow.Cells[0], proyecto, plantilla, plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño, plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                plantilla.EncabezadoIzqAlineacion, false);
            EscribirCeldaPlantilla(firstRow.Cells[1], proyecto, plantilla, plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño, plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                plantilla.EncabezadoCenAlineacion, false);
            EscribirCeldaPlantilla(firstRow.Cells[2], proyecto, plantilla, plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño, plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                plantilla.EncabezadoDerAlineacion, false);
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
            {
                PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.FirstPage, section, proyecto, plantilla, elementosPdf, _svc);
                return;
            }
            var primary = section.Footers.Primary.AddTable();
            primary.Borders.Visible = false;
            ReportPageLayoutHelper.AddHeaderFooterColumns(primary, section);
            var row = primary.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(footerHeightCm);

            EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.PiePaginaIzqTipo, plantilla.PiePaginaIzqContenido,
                plantilla.PiePaginaIzqFuente, plantilla.PiePaginaIzqTamaño, plantilla.PiePaginaIzqNegrita, plantilla.PiePaginaIzqCursiva,
                plantilla.PiePaginaIzqAlineacion, true);
            EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.PiePaginaCenTipo, plantilla.PiePaginaCenContenido,
                plantilla.PiePaginaCenFuente, plantilla.PiePaginaCenTamaño, plantilla.PiePaginaCenNegrita, plantilla.PiePaginaCenCursiva,
                plantilla.PiePaginaCenAlineacion, true);
            EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.PiePaginaDerTipo, plantilla.PiePaginaDerContenido,
                plantilla.PiePaginaDerFuente, plantilla.PiePaginaDerTamaño, plantilla.PiePaginaDerNegrita, plantilla.PiePaginaDerCursiva,
                plantilla.PiePaginaDerAlineacion, true);

            var first = section.Footers.FirstPage.AddTable();
            first.Borders.Visible = false;
            ReportPageLayoutHelper.AddHeaderFooterColumns(first, section);
            var firstRow = first.AddRow();
            firstRow.HeightRule = RowHeightRule.AtLeast;
            firstRow.Height = Unit.FromCentimeter(footerHeightCm);

            EscribirCeldaPlantilla(firstRow.Cells[0], proyecto, plantilla, plantilla.PiePaginaIzqTipo, plantilla.PiePaginaIzqContenido,
                plantilla.PiePaginaIzqFuente, plantilla.PiePaginaIzqTamaño, plantilla.PiePaginaIzqNegrita, plantilla.PiePaginaIzqCursiva,
                plantilla.PiePaginaIzqAlineacion, true);
            EscribirCeldaPlantilla(firstRow.Cells[1], proyecto, plantilla, plantilla.PiePaginaCenTipo, plantilla.PiePaginaCenContenido,
                plantilla.PiePaginaCenFuente, plantilla.PiePaginaCenTamaño, plantilla.PiePaginaCenNegrita, plantilla.PiePaginaCenCursiva,
                plantilla.PiePaginaCenAlineacion, true);
            EscribirCeldaPlantilla(firstRow.Cells[2], proyecto, plantilla, plantilla.PiePaginaDerTipo, plantilla.PiePaginaDerContenido,
                plantilla.PiePaginaDerFuente, plantilla.PiePaginaDerTamaño, plantilla.PiePaginaDerNegrita, plantilla.PiePaginaDerCursiva,
                plantilla.PiePaginaDerAlineacion, true);
        }

        private void ConstruirCuerpo(
            Section section,
            Proyecto proyecto,
            List<ColumnaExplosion> cols,
            string filtro,
            Dictionary<int, DatosInsumo> materiales,
            Dictionary<int, DatosInsumo> manoObra,
            Dictionary<int, DatosInsumo> maquinaria,
            Dictionary<int, DatosInsumo> herramientas,
            decimal costoDirectoTotal,
            ConfiguracionTituloReporte? tituloCfg)
        {
            var tituloTable = section.AddTable();
            tituloTable.Borders.Visible = false;
            tituloTable.AddColumn(Unit.FromCentimeter(ReportPageLayoutHelper.GetLetterContentWidthCm(section)));
            var rowTitulo = tituloTable.AddRow();
            rowTitulo.Shading.Color = MColor.Parse(ReportTitleStyleHelper.StandardBackgroundHex);
            rowTitulo.HeightRule = RowHeightRule.AtLeast;
            rowTitulo.Height = Unit.FromCentimeter(0.75);
            var textoTitulo = string.Equals(filtro, "Todos", StringComparison.OrdinalIgnoreCase)
                ? ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "EXPLOSIÓN DE INSUMOS")
                : ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "EXPLOSIÓN DE INSUMOS") + " — " + (filtro ?? string.Empty).ToUpperInvariant();
            var pTitulo = rowTitulo.Cells[0].AddParagraph(textoTitulo);
            pTitulo.Style = "ExplosionTitle";
            ReportTitleStyleHelper.ApplyToParagraph(pTitulo, tituloCfg, textoTitulo);
            rowTitulo.Cells[0].Format.Alignment = MParagraphAlignment.Center;
            rowTitulo.Cells[0].VerticalAlignment = VerticalAlignment.Center;

            var proyectoTable = section.AddTable();
            proyectoTable.Borders.Visible = false;
            proyectoTable.AddColumn(Unit.FromCentimeter(ReportPageLayoutHelper.GetLetterContentWidthCm(section)));
            var rowProyecto = proyectoTable.AddRow();
            rowProyecto.Shading.Color = MColor.Parse("#E3F2FD");
            rowProyecto.HeightRule = RowHeightRule.AtLeast;
            rowProyecto.Height = Unit.FromCentimeter(0.58);
            var pProyecto = rowProyecto.Cells[0].AddParagraph(proyecto.Nombre ?? string.Empty);
            pProyecto.Style = "ExplosionProject";
            rowProyecto.Cells[0].VerticalAlignment = VerticalAlignment.Center;

            var spacer = section.AddParagraph();
            spacer.Format.SpaceBefore = 0;
            spacer.Format.SpaceAfter = Unit.FromCentimeter(0.06);

            if (DebeIncluirSeccion(filtro, "Materiales"))
                AgregarSeccion(section, "MATERIALES", materiales, cols, costoDirectoTotal, proyecto);
            if (DebeIncluirSeccion(filtro, "Mano de Obra"))
                AgregarSeccion(section, "MANO DE OBRA", manoObra, cols, costoDirectoTotal, proyecto);
            if (DebeIncluirSeccion(filtro, "Herramientas"))
                AgregarSeccion(section, "HERRAMIENTAS", herramientas, cols, costoDirectoTotal, proyecto);
            if (DebeIncluirSeccion(filtro, "Maquinaria"))
                AgregarSeccion(section, "MAQUINARIA", maquinaria, cols, costoDirectoTotal, proyecto);

            if (string.Equals(filtro, "Todos", StringComparison.OrdinalIgnoreCase))
                AgregarTotalGeneral(section, cols, costoDirectoTotal, proyecto);
        }

        private static bool DebeIncluirSeccion(string filtro, string seccion)
        {
            return string.Equals(filtro, "Todos", StringComparison.OrdinalIgnoreCase)
                || string.Equals(filtro, seccion, StringComparison.OrdinalIgnoreCase);
        }

        private void AgregarSeccion(Section section, string titulo, Dictionary<int, DatosInsumo> datos, List<ColumnaExplosion> cols, decimal costoTotal, Proyecto proyecto)
        {
            if (datos == null || datos.Count == 0)
                return;

            var table = section.AddTable();
            table.Format.Font.Name = "Segoe UI";
            table.Format.Font.Size = 8.5;
            table.Rows.LeftIndent = 0;
            table.Borders.Visible = false;

            AgregarColumnas(table, cols, section);

            var rowSeccion = table.AddRow();
            rowSeccion.HeadingFormat = true;
            rowSeccion.HeightRule = RowHeightRule.AtLeast;
            rowSeccion.Height = Unit.FromCentimeter(0.60);
            rowSeccion.Shading.Color = MColor.Parse("#37474F");
            rowSeccion.Cells[0].MergeRight = cols.Count - 1;
            var pSec = rowSeccion.Cells[0].AddParagraph(titulo);
            pSec.Style = "ExplosionSection";
            pSec.Format.Font.Color = MColor.Parse(ReportTitleStyleHelper.StandardTextHex);
            rowSeccion.Cells[0].VerticalAlignment = VerticalAlignment.Center;
            AplicarBordeInferior(rowSeccion.Cells[0], "#37474F", 0.01);

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.HeightRule = RowHeightRule.AtLeast;
            header.Height = Unit.FromCentimeter(0.62);
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = header.Cells[i];
                cell.Shading.Color = ParseColorSafe(
                    !string.IsNullOrWhiteSpace(col.ColorFondo) && !string.Equals(col.ColorFondo, "#FFFFFF", StringComparison.OrdinalIgnoreCase)
                        ? col.ColorFondo
                        : "#33334C",
                    "#33334C");
                cell.VerticalAlignment = VerticalAlignment.Center;
                var p = cell.AddParagraph(col.Nombre ?? string.Empty);
                p.Style = "ExplosionHeader";
                p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                p.Format.Font.Name = NormalizarFuentePdf(col.NombreFuente);
                p.Format.Font.Size = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
                p.Format.Font.Color = ParseColorSafe(
                    !string.IsNullOrWhiteSpace(col.ColorFuente) && !string.Equals(col.ColorFuente, "#000000", StringComparison.OrdinalIgnoreCase)
                        ? col.ColorFuente
                        : "#FFFFFF",
                    "#FFFFFF");
                cell.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                AplicarBordeInferior(cell, "#1565C0", 0.45);
            }

            decimal subtotal = 0m;
            foreach (var kvp in datos.OrderBy(x => x.Value.Clave))
            {
                var ins = kvp.Value;
                decimal importe = ins.Cantidad;
                decimal porcentaje = costoTotal > 0m ? importe / costoTotal : 0m;
                subtotal += importe;

                var row = table.AddRow();
                row.HeightRule = RowHeightRule.AtLeast;
                row.Height = Unit.FromCentimeter(CalcularAlturaFilaPdf(ins, cols));

                for (int i = 0; i < cols.Count; i++)
                {
                    var col = cols[i];
                    var cell = row.Cells[i];
                    cell.VerticalAlignment = VerticalAlignment.Center;
                    cell.Shading.Color = ParseColorSafe(string.IsNullOrWhiteSpace(col.ColorFondo) ? "#FFFFFF" : col.ColorFondo, "#FFFFFF");
                    cell.Format.Alignment = ConvertirAlineacion(col.Alineacion);

                    var p = cell.AddParagraph();
                    p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                    p.Format.Font.Name = NormalizarFuentePdf(col.NombreFuente);
                    p.Format.Font.Size = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
                    p.Format.Font.Bold = col.Negrita;
                    p.Format.Font.Italic = col.Cursiva;
                    p.Format.Font.Color = ParseColorSafe(string.IsNullOrWhiteSpace(col.ColorFuente) ? "#000000" : col.ColorFuente, "#000000");
                    p.AddText(ResolverValor(col.NombreInterno, ins, importe, porcentaje, proyecto));
                    AplicarBordeInferior(cell, "#D9DEE3", 0.20);
                }
            }

            var subtotalRow = table.AddRow();
            subtotalRow.HeightRule = RowHeightRule.AtLeast;
            subtotalRow.Height = Unit.FromCentimeter(0.56);
            subtotalRow.Shading.Color = MColor.Parse("#E3F2FD");
            subtotalRow.Format.Font.Bold = true;

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = subtotalRow.Cells[i];
                cell.VerticalAlignment = VerticalAlignment.Center;
                var p = cell.AddParagraph();
                p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                p.Format.Font.Bold = true;

                if (string.Equals(col.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase))
                    p.AddText("SUBTOTAL " + titulo + ":");
                else if (string.Equals(col.NombreInterno, "Importe", StringComparison.OrdinalIgnoreCase))
                    p.AddText(FormatearDecimal(subtotal, FormatoC(proyecto.DecimalesImporte)));
                else if (string.Equals(col.NombreInterno, "Porcentaje", StringComparison.OrdinalIgnoreCase))
                    p.AddText(FormatearPorcentaje(costoTotal > 0m ? subtotal / costoTotal : 0m));
                else
                    p.AddText(string.Empty);

                AplicarBordeSuperior(cell, "#1565C0", 0.30);
                AplicarBordeInferior(cell, "#E3F2FD", 0.01);
            }

            var spacer = section.AddParagraph();
            spacer.Format.SpaceBefore = 0;
            spacer.Format.SpaceAfter = Unit.FromCentimeter(0.10);
        }

        private void AgregarTotalGeneral(Section section, List<ColumnaExplosion> cols, decimal costoDirectoTotal, Proyecto proyecto)
        {
            var table = section.AddTable();
            table.Format.Font.Name = "Segoe UI";
            table.Format.Font.Size = 8.8;
            table.Rows.LeftIndent = 0;
            table.Borders.Visible = false;
            AgregarColumnas(table, cols, section);

            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.62);
            row.Shading.Color = MColor.Parse("#BBDEFB");
            row.Format.Font.Bold = true;

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = row.Cells[i];
                cell.VerticalAlignment = VerticalAlignment.Center;
                var p = cell.AddParagraph();
                p.Style = "ExplosionTotal";
                p.Format.Alignment = ConvertirAlineacion(col.Alineacion);

                if (string.Equals(col.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase))
                    p.AddText("TOTAL COSTO DIRECTO:");
                else if (string.Equals(col.NombreInterno, "Importe", StringComparison.OrdinalIgnoreCase))
                    p.AddText(FormatearDecimal(costoDirectoTotal, FormatoC(proyecto.DecimalesImporte)));
                else if (string.Equals(col.NombreInterno, "Porcentaje", StringComparison.OrdinalIgnoreCase))
                    p.AddText(FormatearPorcentaje(1m));
                else
                    p.AddText(string.Empty);

                AplicarBordeSuperior(cell, "#1565C0", 0.45);
            }
        }

        private static MOrientation DeterminarOrientacionExplosion(List<ColumnaExplosion> cols, double leftMarginCm, double rightMarginCm)
        {
            if (cols == null || cols.Count == 0)
                return MOrientation.Portrait;

            const double pxPerCm = 37.7952755906;
            const double letterPortraitWidthCm = 21.59;

            var portraitContentPx = Math.Max(1.0, (letterPortraitWidthCm - leftMarginCm - rightMarginCm) * pxPerCm);
            var requiredPx = cols
                .Where(c => c.Visible)
                .Sum(c => (double)Math.Max(12, c.AnchoColumna));

            return requiredPx <= portraitContentPx * 1.02
                ? MOrientation.Portrait
                : MOrientation.Landscape;
        }

        private static void AgregarColumnas(Table table, List<ColumnaExplosion> cols, Section section)
        {
            double availableCm = ReportPageLayoutHelper.GetLetterContentWidthCm(section);
            double totalPx = Math.Max(1, cols.Sum(c => Math.Max(24, c.AnchoColumna)));
            foreach (var col in cols)
            {
                double widthCm = availableCm * Math.Max(24, col.AnchoColumna) / totalPx;
                table.AddColumn(Unit.FromCentimeter(widthCm));
            }
        }

        private void EscribirCeldaPlantilla(Cell cell, Proyecto proyecto, PlantillaReporte plantilla,
            string tipo, string contenido, string fuente, float tamano, bool negrita, bool cursiva,
            string alineacion, bool soportaCamposPagina)
        {
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Format.Alignment = ConvertirAlineacion(alineacion);
            cell.Borders.Visible = false;

            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase) && File.Exists(contenido))
            {
                var img = cell.AddImage(contenido);
                img.LockAspectRatio = true;
                img.Height = Unit.FromCentimeter(1.5);
                return;
            }

            var p = cell.AddParagraph();
            p.Format.Alignment = ConvertirAlineacion(alineacion);
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
            p.Format.Font.Name = NormalizarFuentePdf(fuente);
            p.Format.Font.Size = tamano <= 0 ? 9 : tamano;
            p.Format.Font.Bold = negrita;
            p.Format.Font.Italic = cursiva;

            var texto = _svc.ResolverCampos(contenido ?? string.Empty, proyecto, plantilla);
            if (!soportaCamposPagina)
            {
                p.AddText(texto);
                return;
            }

            AgregarTextoConCamposPagina(p, texto);
        }


        private static string NormalizarFuentePdf(string fuente)
        {
            if (string.IsNullOrWhiteSpace(fuente))
                return "Segoe UI";

            var valor = fuente.Trim();
            return valor.ToLowerInvariant() switch
            {
                "cambria" => "Segoe UI",
                "cambria math" => "Segoe UI",
                "calibri" => "Segoe UI",
                "calibri light" => "Segoe UI",
                "aptos" => "Segoe UI",
                "aptos display" => "Segoe UI",
                "aptos narrow" => "Segoe UI",
                _ => valor
            };
        }

        private static void AgregarTextoConCamposPagina(Paragraph p, string texto)
        {
            texto ??= string.Empty;
            int index = 0;
            while (index < texto.Length)
            {
                int posPagina = texto.IndexOf("{pagina}", index, StringComparison.OrdinalIgnoreCase);
                int posTotal = texto.IndexOf("{total_paginas}", index, StringComparison.OrdinalIgnoreCase);
                int next = new[] { posPagina, posTotal }.Where(x => x >= 0).DefaultIfEmpty(-1).Min();
                if (next < 0)
                {
                    p.AddText(texto.Substring(index));
                    break;
                }

                if (next > index)
                    p.AddText(texto.Substring(index, next - index));

                if (next == posPagina)
                {
                    p.AddPageField();
                    index = posPagina + "{pagina}".Length;
                }
                else
                {
                    p.AddNumPagesField();
                    index = posTotal + "{total_paginas}".Length;
                }
            }
        }

        private static string ResolverValor(string nombreInterno, DatosInsumo ins, decimal importe, decimal porcentaje, Proyecto proyecto)
        {
            switch (nombreInterno)
            {
                case "Clave":
                    return ins.Clave ?? string.Empty;
                case "Descripcion":
                    return ins.Descripcion ?? string.Empty;
                case "Unidad":
                    return ins.Unidad ?? string.Empty;
                case "Cantidad":
                    return ins.EsPorcentual ? "—" : FormatearDecimal(ins.CantidadFisica, FormatoN(proyecto.DecimalesCantidad));
                case "PrecioUnitario":
                    return (ins.EsPorcentual && ins.PrecioUnitario == 0m) ? "—" : FormatearDecimal(ins.PrecioUnitario, FormatoC(proyecto.DecimalesImporte));
                case "Importe":
                    return FormatearDecimal(importe, FormatoC(proyecto.DecimalesImporte));
                case "Porcentaje":
                    return FormatearPorcentaje(porcentaje);
                default:
                    return string.Empty;
            }
        }

        private static double CalcularAlturaFilaPdf(DatosInsumo ins, List<ColumnaExplosion> cols)
        {
            int maxLineas = 1;
            foreach (var col in cols)
            {
                if (!col.WrapTexto)
                    continue;

                string texto = col.NombreInterno switch
                {
                    "Clave" => ins.Clave ?? string.Empty,
                    "Descripcion" => ins.Descripcion ?? string.Empty,
                    "Unidad" => ins.Unidad ?? string.Empty,
                    _ => string.Empty
                };

                if (string.IsNullOrWhiteSpace(texto))
                    continue;

                double ancho = Math.Max(24, col.AnchoColumna);
                int estimadas = (int)Math.Ceiling((texto.Length * 7.0) / ancho);
                if (estimadas > maxLineas)
                    maxLineas = estimadas;
            }

            return Math.Max(0.44, Math.Min(1.20, 0.22 * maxLineas));
        }

        private static string FormatearDecimal(decimal valor, string formatoNumero)
        {
            string formato = formatoNumero switch
            {
                "N0" => "#,##0",
                "N2" => "#,##0.00",
                "N3" => "#,##0.000",
                "N4" => "#,##0.0000",
                "N5" => "#,##0.00000",
                "C2" => "$#,##0.00",
                "C4" => "$#,##0.0000",
                _ => string.IsNullOrWhiteSpace(formatoNumero) ? "#,##0.00" : formatoNumero
            };
            return valor.ToString(formato, CultureInfo.InvariantCulture);
        }

        private static string FormatearPorcentaje(decimal valor)
        {
            return valor.ToString("0.00%", CultureInfo.InvariantCulture);
        }

        private static MParagraphAlignment ConvertirAlineacion(AlineacionColumna alineacion)
        {
            switch (alineacion)
            {
                case AlineacionColumna.Centro:
                    return MParagraphAlignment.Center;
                case AlineacionColumna.Derecha:
                    return MParagraphAlignment.Right;
                default:
                    return MParagraphAlignment.Left;
            }
        }

        private static MParagraphAlignment ConvertirAlineacion(string alineacion)
        {
            switch (alineacion)
            {
                case "Centro":
                    return MParagraphAlignment.Center;
                case "Derecha":
                    return MParagraphAlignment.Right;
                default:
                    return MParagraphAlignment.Left;
            }
        }

        private static MColor ParseColorSafe(string html, string fallback)
        {
            try { return MColor.Parse(string.IsNullOrWhiteSpace(html) ? fallback : html); }
            catch { return MColor.Parse(fallback); }
        }

        private static void AplicarBordeInferior(Cell cell, string colorHtml, double width)
        {
            cell.Borders.Visible = false;
            cell.Borders.Bottom.Visible = true;
            cell.Borders.Bottom.Color = MColor.Parse(colorHtml);
            cell.Borders.Bottom.Width = width;
        }

        private static void AplicarBordeSuperior(Cell cell, string colorHtml, double width)
        {
            cell.Borders.Top.Visible = true;
            cell.Borders.Top.Color = MColor.Parse(colorHtml);
            cell.Borders.Top.Width = width;
        }

        private static string FormatoN(int decimales)
        {
            string ceros = decimales > 0 ? "." + new string('0', decimales) : "";
            return $"#,##0{ceros}";
        }

        private static string FormatoC(int decimales)
        {
            string ceros = decimales > 0 ? "." + new string('0', decimales) : "";
            return $"$#,##0{ceros}";
        }

        private static string SanitizarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre))
                return "Proyecto";

            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');

            return nombre.Length > 50 ? nombre.Substring(0, 50) : nombre;
        }
    }
}
