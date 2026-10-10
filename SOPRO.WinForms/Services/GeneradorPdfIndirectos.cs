using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using DrawingColor = System.Drawing.Color;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte PDF de Cálculo de Indirectos. Consume el MISMO
    /// <see cref="ReportColumnSnapshot"/> neutral que la ruta Excel (visibilidad,
    /// orden, encabezado, ancho, alineación, wrap, estilo y formato numérico) y
    /// resuelve los valores del dominio con <see cref="IndirectosExportResolver"/>.
    /// NO lee columnas ni estilos del grid y no usa literales "$"/"N2"/"N4": los
    /// importes usan el símbolo '$' + decimales de importe del proyecto y los
    /// porcentajes los decimales de porcentaje, vía <see cref="ReportColumnGridFormat"/>.
    ///
    /// Conserva la plantilla propia del reporte (secciones Oficina Central/Campo,
    /// filas jerárquicas de grupo/concepto y filas de subtotal/resumen final).
    /// </summary>
    public class GeneradorPdfIndirectos
    {
        private readonly ReporteService _svc;

        public GeneradorPdfIndirectos(ReporteService svc) => _svc = svc;

        public string Generar(
            Proyecto proyecto,
            List<GrupoIndirecto> gruposOC,
            List<GrupoIndirecto> gruposCampo,
            ConfiguracionIndirectos config,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            var cols = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (!cols.Any())
            {
                cols = IndirectosExportResolver.DefaultColumns()
                    .Where(c => c.Visible)
                    .OrderBy(c => c.Orden)
                    .ToList();
            }
            if (!cols.Any())
                throw new InvalidOperationException("No hay columnas visibles para exportar.");

            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta, $"Indirectos_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            var doc = new Document();
            DefinirEstilos(doc);

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = MOrientation.Portrait;

            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(plantilla, elementosPdf);
            var footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(plantilla, elementosPdf);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoSuperiorCm(plantilla, elementosPdf, 1.35));
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + PlantillaLibrePdfRenderer.ObtenerSeparacionContenidoInferiorCm(plantilla, elementosPdf, 0.8));

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, gruposOC ?? new List<GrupoIndirecto>(), gruposCampo ?? new List<GrupoIndirecto>(), config, cols, snapshot, tituloCfg);

            var renderer = new PdfDocumentRenderer() { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            normal.Font.Size = 8.5;

            var title = doc.Styles.AddStyle("IndirectosTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 12;
            title.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var project = doc.Styles.AddStyle("IndirectosProject", "Normal");
            project.Font.Size = 10;
            project.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var section = doc.Styles.AddStyle("IndirectosSection", "Normal");
            section.Font.Bold = true;
            section.Font.Size = 10;

            var summary = doc.Styles.AddStyle("IndirectosSummary", "Normal");
            summary.Font.Bold = true;
        }

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
                return;
            ConstruirTablaPlantilla(section.Headers.Primary.AddTable(), proyecto, plantilla, headerHeightCm, false);
            ConstruirTablaPlantilla(section.Headers.FirstPage.AddTable(), proyecto, plantilla, headerHeightCm, false);
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
                return;
            ConstruirTablaPlantilla(section.Footers.Primary.AddTable(), proyecto, plantilla, footerHeightCm, true);
            ConstruirTablaPlantilla(section.Footers.FirstPage.AddTable(), proyecto, plantilla, footerHeightCm, true);
        }

        private void ConstruirTablaPlantilla(Table table, Proyecto proyecto, PlantillaReporte plantilla, double altoCm, bool esPie)
        {
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(6.4));
            table.AddColumn(Unit.FromCentimeter(6.4));
            table.AddColumn(Unit.FromCentimeter(6.4));
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(altoCm);

            if (!esPie)
            {
                EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                    plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño, plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                    plantilla.EncabezadoIzqAlineacion, false);
                EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                    plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño, plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                    plantilla.EncabezadoCenAlineacion, false);
                EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                    plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño, plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                    plantilla.EncabezadoDerAlineacion, false);
            }
            else
            {
                EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.PiePaginaIzqTipo, plantilla.PiePaginaIzqContenido,
                    plantilla.PiePaginaIzqFuente, plantilla.PiePaginaIzqTamaño, plantilla.PiePaginaIzqNegrita, plantilla.PiePaginaIzqCursiva,
                    plantilla.PiePaginaIzqAlineacion, true);
                EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.PiePaginaCenTipo, plantilla.PiePaginaCenContenido,
                    plantilla.PiePaginaCenFuente, plantilla.PiePaginaCenTamaño, plantilla.PiePaginaCenNegrita, plantilla.PiePaginaCenCursiva,
                    plantilla.PiePaginaCenAlineacion, true);
                EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.PiePaginaDerTipo, plantilla.PiePaginaDerContenido,
                    plantilla.PiePaginaDerFuente, plantilla.PiePaginaDerTamaño, plantilla.PiePaginaDerNegrita, plantilla.PiePaginaDerCursiva,
                    plantilla.PiePaginaDerAlineacion, true);
            }
        }

        private void EscribirCeldaPlantilla(Cell cell, Proyecto proyecto, PlantillaReporte plantilla,
            string tipo, string contenido, string fuente, float tamano, bool negrita, bool cursiva,
            string alineacion, bool soportaCamposPagina)
        {
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            cell.Borders.Visible = false;

            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase) && File.Exists(contenido))
            {
                var img = cell.AddImage(contenido);
                img.LockAspectRatio = true;
                img.Height = Unit.FromCentimeter(1.5);
                return;
            }

            var p = cell.AddParagraph();
            p.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
            PdfFontHelper.ApplyFont(p.Format.Font, fuente, tamano <= 0 ? 9 : tamano, negrita, cursiva);

            var texto = _svc.ResolverCampos(contenido ?? string.Empty, proyecto, plantilla);
            if (!soportaCamposPagina)
            {
                p.AddText(texto);
                return;
            }

            AgregarTextoConCamposPagina(p, texto);
        }

        private static void AgregarTextoConCamposPagina(Paragraph p, string texto)
        {
            texto = texto ?? string.Empty;
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

        private void ConstruirCuerpo(Section section, Proyecto proyecto, List<GrupoIndirecto> gruposOC, List<GrupoIndirecto> gruposCampo,
            ConfiguracionIndirectos config, IReadOnlyList<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, ConfiguracionTituloReporte? tituloCfg)
        {
            var pTitle = section.AddParagraph("ANÁLISIS DE COSTOS INDIRECTOS", "IndirectosTitle");
            ReportTitleStyleHelper.ApplyToParagraph(pTitle, tituloCfg, "ANÁLISIS DE COSTOS INDIRECTOS");
            pTitle.Format.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardBackgroundHex);
            pTitle.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            pTitle.Format.SpaceAfter = Unit.FromCentimeter(0.12);
            pTitle.Format.SpaceBefore = 0;
            pTitle.Format.KeepWithNext = true;

            var pProject = section.AddParagraph(proyecto.Nombre ?? string.Empty, "IndirectosProject");
            pProject.Format.Shading.Color = ParseColor("#E3F2FD");
            pProject.Format.SpaceAfter = Unit.FromCentimeter(0.25);
            pProject.Format.KeepWithNext = true;

            ConstruirDatosGenerales(section, proyecto, config, snapshot);
            ConstruirTablaColumnas(section, cols, gruposOC, gruposCampo, config, snapshot);
        }

        private void ConstruirDatosGenerales(Section section, Proyecto proyecto, ConfiguracionIndirectos config, ReportColumnSnapshot snapshot)
        {
            var table = section.AddTable();
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(8.8));
            table.AddColumn(Unit.FromCentimeter(9.0));
            table.Rows.LeftIndent = 0;

            int durMeses = proyecto.PlazoEjecucion > 0 ? (int)Math.Ceiling(proyecto.PlazoEjecucion / 30.0) : 1;
            AgregarDatoGeneral(table, "Costo Directo de Obra:", FormatearMonedaResumen(config.CostoDirectoObra, snapshot));
            AgregarDatoGeneral(table, "Volumen Anual de Obra:", FormatearMonedaResumen(config.VolumenAnualObra, snapshot));
            AgregarDatoGeneral(table, "Duración de la Obra:", durMeses + " meses (" + proyecto.PlazoEjecucion + " días)");

            var spacer = section.AddParagraph();
            spacer.Format.SpaceAfter = Unit.FromCentimeter(0.10);
        }

        private void AgregarDatoGeneral(Table table, string etiqueta, string valor)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.50);
            row.Cells[0].Shading.Color = ParseColor("#F5F5F5");
            row.Cells[1].Shading.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            row.Cells[0].AddParagraph(etiqueta).Format.Font.Bold = true;
            var p = row.Cells[1].AddParagraph(valor);
            p.Format.Alignment = MParagraphAlignment.Right;
        }

        private void ConstruirTablaColumnas(Section section, IReadOnlyList<ReportColumnDefinition> cols, List<GrupoIndirecto> gruposOC,
            List<GrupoIndirecto> gruposCampo, ConfiguracionIndirectos config, ReportColumnSnapshot snapshot)
        {
            var estiloTabla = snapshot.EstiloTabla;

            var table = section.AddTable();
            table.Rows.LeftIndent = 0;
            // Plantilla propia de Indirectos: sin grilla completa; sólo hairlines
            // inferiores por celda y filas de sección/color. El estilo de la tabla
            // del snapshot se conserva para los encabezados de columna.
            table.Borders.Visible = false;
            table.Format.Font.Name = PdfFontHelper.NormalizeFontName(estiloTabla.EstiloContenido.Fuente ?? "Segoe UI");
            table.Format.Font.Size = estiloTabla.EstiloContenido.Tamano > 0 ? estiloTabla.EstiloContenido.Tamano : 8.5f;

            double availableCm = ReportColumnWidthConverter.GetLetterUsableWidthCm(false);
            int[] anchosPx = cols.Select(c => c.Ancho).ToArray();
            double[] anchosCm = ReportColumnWidthConverter.PxToCm(anchosPx, availableCm);
            for (int i = 0; i < cols.Count; i++)
            {
                var pdfCol = table.AddColumn(Unit.FromCentimeter(anchosCm[i]));
                pdfCol.Format.Alignment = ConvertirAlineacion(cols[i].Alineacion);
            }

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.HeightRule = RowHeightRule.AtLeast;
            header.Height = Unit.FromCentimeter(0.65);
            header.Shading.Color = ParseColor(estiloTabla.EstiloEncabezado.ColorFondo ?? "#4A4A6A");
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var enc = col.EstiloEncabezado;
                var cell = header.Cells[i];
                cell.VerticalAlignment = VerticalAlignment.Center;
                var p = cell.AddParagraph(col.Encabezado ?? string.Empty);
                p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                PdfFontHelper.ApplyFont(p.Format.Font, enc.Fuente, enc.Tamano > 0 ? enc.Tamano : 9, enc.Negrita, enc.Cursiva);
                p.Format.Font.Color = ParseColor(enc.ColorFuente);
                AplicarBordeInferior(cell, "#1565C0", 0.75);
            }

            AgregarFilaEncabezadoSeccion(table, cols.Count, "OFICINA CENTRAL");
            foreach (var grupo in gruposOC.OrderBy(g => g.Orden))
                AgregarGrupo(table, cols, grupo, false, snapshot);

            AgregarResumenSeccion(table, cols, "Subtotal Oficina Central Anual", config.TotalOficinaCentralAnual, "% sobre Volumen Anual", config.PorcentajeOficinaCentral, snapshot);
            AgregarFilaEspaciadora(table, cols.Count, 0.15);

            AgregarFilaEncabezadoSeccion(table, cols.Count, "GASTOS DE CAMPO");
            foreach (var grupo in gruposCampo.OrderBy(g => g.Orden))
                AgregarGrupo(table, cols, grupo, true, snapshot);

            AgregarResumenSeccion(table, cols, "Subtotal Campo", config.TotalCampo, "% sobre Costo Directo", config.PorcentajeCampo, snapshot);
            AgregarFilaEspaciadora(table, cols.Count, 0.15);

            AgregarResumenFinal(table, cols, config, snapshot);
        }

        private void AgregarFilaEncabezadoSeccion(Table table, int colCount, string titulo)
        {
            var row = table.AddRow();
            row.HeadingFormat = false;
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.55);
            row.Shading.Color = ParseColor("#37474F");
            row.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            row.Format.Font.Bold = true;
            row.Cells[0].AddParagraph(titulo);
            if (colCount > 1)
                row.Cells[0].MergeRight = colCount - 1;
            row.Cells[0].Format.Alignment = MParagraphAlignment.Left;
        }

        private void AgregarGrupo(Table table, IReadOnlyList<ReportColumnDefinition> cols, GrupoIndirecto grupo, bool mostrarDuracion, ReportColumnSnapshot snapshot)
        {
            var groupRow = table.AddRow();
            groupRow.HeightRule = RowHeightRule.AtLeast;
            groupRow.Height = Unit.FromCentimeter(0.52);
            groupRow.Shading.Color = ParseColor("#E8E8E8");
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = groupRow.Cells[i];
                var p = cell.AddParagraph();
                var cont = col.EstiloContenido;
                PdfFontHelper.ApplyFont(p.Format.Font, cont.Fuente, cont.Tamano > 0 ? cont.Tamano : 9, true, cont.Cursiva);
                p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                p.AddText(ObtenerValorGrupo(grupo, col, snapshot));
                AplicarBordeInferior(cell, "#B0B0B0", 0.35);
            }

            foreach (var concepto in grupo.Conceptos.Where(c => c.Activo).OrderBy(c => c.Orden))
            {
                var row = table.AddRow();
                row.HeightRule = RowHeightRule.AtLeast;
                row.Height = Unit.FromCentimeter(0.48);
                for (int i = 0; i < cols.Count; i++)
                {
                    var col = cols[i];
                    var cont = col.EstiloContenido;
                    var cell = row.Cells[i];
                    cell.Shading.Color = ParseColor(cont.ColorFondo ?? "#FFFFFF");
                    var p = cell.AddParagraph();
                    PdfFontHelper.ApplyFont(p.Format.Font, cont.Fuente, cont.Tamano > 0 ? cont.Tamano : 9, cont.Negrita, cont.Cursiva);
                    p.Format.Font.Color = ParseColor(cont.ColorFuente);
                    p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                    p.Format.SpaceAfter = 0;
                    p.Format.SpaceBefore = 0;
                    p.AddText(ObtenerValorConcepto(concepto, col, mostrarDuracion, snapshot));
                    AplicarBordeInferior(cell, "#D8D8D8", 0.20);
                }
            }
        }

        /// <summary>
        /// Resuelve el valor de una celda de fila de grupo con paridad grid↔PDF↔Excel:
        /// las columnas numéricas usan <see cref="ReportColumnGridFormat"/>
        /// (símbolo de moneda '$' + decimales de importe); las de texto usan el
        /// resolver neutral.
        /// </summary>
        private static string ObtenerValorGrupo(GrupoIndirecto grupo, ReportColumnDefinition col, ReportColumnSnapshot snapshot)
        {
            if (IndirectosExportResolver.TryGetValorGrupo(grupo, col, out var valor))
                return ReportColumnGridFormat.FormatearPdf(valor, col, snapshot);

            return IndirectosExportResolver.ResolveTextoGrupo(grupo, col);
        }

        private static string ObtenerValorConcepto(ConceptoIndirecto concepto, ReportColumnDefinition col, bool mostrarDuracion, ReportColumnSnapshot snapshot)
        {
            if (IndirectosExportResolver.TryGetValorConcepto(concepto, col, out var valor))
                return ReportColumnGridFormat.FormatearPdf(valor, col, snapshot);

            return IndirectosExportResolver.ResolveTextoConcepto(concepto, col, mostrarDuracion);
        }

        private void AgregarResumenSeccion(Table table, IReadOnlyList<ReportColumnDefinition> cols, string etiquetaTotal, decimal total, string labelPorc, decimal porcentaje, ReportColumnSnapshot snapshot)
        {
            int last = cols.Count - 1;

            var rowTotal = table.AddRow();
            rowTotal.HeightRule = RowHeightRule.AtLeast;
            rowTotal.Height = Unit.FromCentimeter(0.52);
            for (int i = 0; i < cols.Count; i++)
            {
                rowTotal.Cells[i].Shading.Color = ParseColor("#E3F2FD");
                AplicarBordeSuperior(rowTotal.Cells[i], "#1565C0", 0.75);
            }
            if (last > 0)
                rowTotal.Cells[0].MergeRight = last - 1;
            var pTot = rowTotal.Cells[0].AddParagraph(etiquetaTotal);
            pTot.Format.Alignment = MParagraphAlignment.Right;
            pTot.Format.Font.Bold = true;
            rowTotal.Cells[last].AddParagraph(FormatearMonedaResumen(total, snapshot)).Format.Alignment = MParagraphAlignment.Right;
            rowTotal.Cells[last].Format.Font.Bold = true;

            var rowPct = table.AddRow();
            rowPct.HeightRule = RowHeightRule.AtLeast;
            rowPct.Height = Unit.FromCentimeter(0.48);
            for (int i = 0; i < cols.Count; i++)
                rowPct.Cells[i].Shading.Color = ParseColor("#F5F5F5");
            if (last > 0)
                rowPct.Cells[0].MergeRight = last - 1;
            rowPct.Cells[0].AddParagraph(labelPorc).Format.Alignment = MParagraphAlignment.Right;
            rowPct.Cells[last].AddParagraph(FormatearPorcentajeResumen(porcentaje, snapshot)).Format.Alignment = MParagraphAlignment.Right;
        }

        private void AgregarResumenFinal(Table table, IReadOnlyList<ReportColumnDefinition> cols, ConfiguracionIndirectos config, ReportColumnSnapshot snapshot)
        {
            int last = cols.Count - 1;

            var title = table.AddRow();
            title.HeightRule = RowHeightRule.AtLeast;
            title.Height = Unit.FromCentimeter(0.60);
            title.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardBackgroundHex);
            title.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            title.Format.Font.Bold = true;
            title.Cells[0].MergeRight = last;
            title.Cells[0].AddParagraph("RESUMEN DE INDIRECTOS").Format.Alignment = MParagraphAlignment.Center;

            AgregarFilaResumenFinal(table, cols.Count, "% Oficina Central:", FormatearPorcentajeResumen(config.PorcentajeOficinaCentral, snapshot), "#F5F5F5", false);
            AgregarFilaResumenFinal(table, cols.Count, "% Gastos de Campo:", FormatearPorcentajeResumen(config.PorcentajeCampo, snapshot), "#F5F5F5", false);

            var spacerTop = table.AddRow();
            spacerTop.HeightRule = RowHeightRule.Exactly;
            spacerTop.Height = Unit.FromCentimeter(0.01);
            for (int i = 0; i < cols.Count; i++)
                AplicarBordeSuperior(spacerTop.Cells[i], "#1565C0", 0.75);

            AgregarFilaResumenFinal(table, cols.Count, "% TOTAL INDIRECTOS:", FormatearPorcentajeResumen(config.PorcentajeTotal, snapshot), "#BBDEFB", true);
        }

        private void AgregarFilaResumenFinal(Table table, int colCount, string etiqueta, string valor, string fondo, bool bold)
        {
            int last = colCount - 1;
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.50);
            for (int i = 0; i < colCount; i++)
                row.Cells[i].Shading.Color = ParseColor(fondo);
            if (last > 0)
                row.Cells[0].MergeRight = last - 1;
            var pL = row.Cells[0].AddParagraph(etiqueta);
            pL.Format.Alignment = MParagraphAlignment.Right;
            pL.Format.Font.Bold = bold;
            var pV = row.Cells[last].AddParagraph(valor);
            pV.Format.Alignment = MParagraphAlignment.Right;
            pV.Format.Font.Bold = bold;
        }

        private static void AgregarFilaEspaciadora(Table table, int colCount, double altoCm)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.Exactly;
            row.Height = Unit.FromCentimeter(altoCm);
            if (colCount > 1)
                row.Cells[0].MergeRight = colCount - 1;
        }

        private static string FormatearMonedaResumen(decimal valor, ReportColumnSnapshot snapshot)
            => valor.ToString(ReportColumnGridFormat.FormatoMonedaPdf(snapshot.DecimalesImporte), CultureInfo.CurrentCulture);

        private static string FormatearPorcentajeResumen(decimal valor, ReportColumnSnapshot snapshot)
            => ReportColumnGridFormat.FormatearPorcentajePdf(valor, snapshot) + "%";

        private static void AplicarBordeInferior(Cell cell, string colorHex, double width)
        {
            cell.Borders.Bottom.Color = ParseColor(colorHex);
            cell.Borders.Bottom.Width = Unit.FromPoint(width);
        }

        private static void AplicarBordeSuperior(Cell cell, string colorHex, double width)
        {
            cell.Borders.Top.Color = ParseColor(colorHex);
            cell.Borders.Top.Width = Unit.FromPoint(width);
        }

        private static MColor ParseColor(string value)
        {
            try
            {
                var c = DrawingColorTranslator(value);
                return new MColor(c.R, c.G, c.B);
            }
            catch
            {
                return MColor.Parse("#000000");
            }
        }

        private static DrawingColor DrawingColorTranslator(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DrawingColor.Black;
            try
            {
                return System.Drawing.ColorTranslator.FromHtml(value);
            }
            catch
            {
                return DrawingColor.Black;
            }
        }

        private static MParagraphAlignment ConvertirAlineacion(ReportTextAlignment alineacion)
        {
            return alineacion switch
            {
                ReportTextAlignment.Centro => MParagraphAlignment.Center,
                ReportTextAlignment.Derecha => MParagraphAlignment.Right,
                ReportTextAlignment.Justificado => MParagraphAlignment.Justify,
                _ => MParagraphAlignment.Left,
            };
        }

        private static MParagraphAlignment ConvertirAlineacionTexto(string alineacion)
        {
            return (alineacion ?? string.Empty) switch
            {
                "Centro" => MParagraphAlignment.Center,
                "Derecha" => MParagraphAlignment.Right,
                _ => MParagraphAlignment.Left,
            };
        }

        private static string SanitizarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre.Substring(0, 40) : nombre;
        }
    }
}
