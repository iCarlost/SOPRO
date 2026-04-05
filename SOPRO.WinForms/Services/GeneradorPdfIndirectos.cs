using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Core.Entities;
using DrawingColor = System.Drawing.Color;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
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
            List<ColumnaIndirectos> columnas,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (columnas == null) throw new ArgumentNullException(nameof(columnas));

            var cols = columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
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

            var headerHeightCm = Math.Max(1.8, plantilla.EncabezadoAltura / 28.0);
            var footerHeightCm = Math.Max(1.2, plantilla.PiePaginaAltura / 28.0);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + 1.35);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + 0.8);

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, gruposOC ?? new List<GrupoIndirecto>(), gruposCampo ?? new List<GrupoIndirecto>(), config, cols, tituloCfg);

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
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
            ConstruirTablaPlantilla(section.Headers.Primary.AddTable(), proyecto, plantilla, headerHeightCm, false);
            ConstruirTablaPlantilla(section.Headers.FirstPage.AddTable(), proyecto, plantilla, headerHeightCm, false);
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
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
            ConfiguracionIndirectos config, List<ColumnaIndirectos> cols, ConfiguracionTituloReporte? tituloCfg)
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

            ConstruirDatosGenerales(section, proyecto, config);
            ConstruirTablaColumnas(section, cols, gruposOC, gruposCampo, config);
        }

        private void ConstruirDatosGenerales(Section section, Proyecto proyecto, ConfiguracionIndirectos config)
        {
            var table = section.AddTable();
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(8.8));
            table.AddColumn(Unit.FromCentimeter(9.0));
            table.Rows.LeftIndent = 0;

            int durMeses = proyecto.PlazoEjecucion > 0 ? (int)Math.Ceiling(proyecto.PlazoEjecucion / 30.0) : 1;
            AgregarDatoGeneral(table, "Costo Directo de Obra:", "$" + config.CostoDirectoObra.ToString("N2"));
            AgregarDatoGeneral(table, "Volumen Anual de Obra:", "$" + config.VolumenAnualObra.ToString("N2"));
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

        private void ConstruirTablaColumnas(Section section, List<ColumnaIndirectos> cols, List<GrupoIndirecto> gruposOC,
            List<GrupoIndirecto> gruposCampo, ConfiguracionIndirectos config)
        {
            var table = section.AddTable();
            table.Rows.LeftIndent = 0;
            table.Borders.Visible = false;
            table.Format.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            table.Format.Font.Size = 8.5;

            double availableCm = 19.59;
            double totalPx = Math.Max(1, cols.Sum(c => Math.Max(24, c.AnchoColumna)));
            foreach (var col in cols)
            {
                double widthCm = availableCm * Math.Max(24, col.AnchoColumna) / totalPx;
                var pdfCol = table.AddColumn(Unit.FromCentimeter(widthCm));
                pdfCol.Format.Alignment = ConvertirAlineacion(col.Alineacion);
            }

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.HeightRule = RowHeightRule.AtLeast;
            header.Height = Unit.FromCentimeter(0.65);
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = header.Cells[i];
                cell.Shading.Color = ParseColor(!string.IsNullOrWhiteSpace(col.ColorFondo) && !EsBlanco(col.ColorFondo) ? col.ColorFondo : "#33334C");
                cell.VerticalAlignment = VerticalAlignment.Center;
                var p = cell.AddParagraph(col.Nombre ?? string.Empty);
                p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                PdfFontHelper.ApplyFont(p.Format.Font, col.NombreFuente, col.TamanoFuente > 0 ? col.TamanoFuente : 9, true, col.Cursiva);
                p.Format.Font.Color = ParseColor(!string.IsNullOrWhiteSpace(col.ColorFuente) && !EsNegro(col.ColorFuente) ? col.ColorFuente : "#FFFFFF");
                AplicarBordeInferior(cell, "#1565C0", 0.75);
            }

            AgregarFilaEncabezadoSeccion(table, cols.Count, "OFICINA CENTRAL");
            foreach (var grupo in gruposOC.OrderBy(g => g.Orden))
                AgregarGrupo(table, cols, grupo, false);

            AgregarResumenSeccion(table, cols, "Subtotal Oficina Central Anual", config.TotalOficinaCentralAnual, "% sobre Volumen Anual", config.PorcentajeOficinaCentral);
            AgregarFilaEspaciadora(table, cols.Count, 0.15);

            AgregarFilaEncabezadoSeccion(table, cols.Count, "GASTOS DE CAMPO");
            foreach (var grupo in gruposCampo.OrderBy(g => g.Orden))
                AgregarGrupo(table, cols, grupo, true);

            AgregarResumenSeccion(table, cols, "Subtotal Campo", config.TotalCampo, "% sobre Costo Directo", config.PorcentajeCampo);
            AgregarFilaEspaciadora(table, cols.Count, 0.15);

            AgregarResumenFinal(table, cols, config);
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
            row.Cells[0].MergeRight = colCount - 1;
            row.Cells[0].Format.Alignment = MParagraphAlignment.Left;
        }

        private void AgregarGrupo(Table table, List<ColumnaIndirectos> cols, GrupoIndirecto grupo, bool mostrarDuracion)
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
                PdfFontHelper.ApplyFont(p.Format.Font, col.NombreFuente, col.TamanoFuente > 0 ? col.TamanoFuente : 9, true, false);
                p.Format.Alignment = col.NombreInterno == "Grupo" ? MParagraphAlignment.Left : ConvertirAlineacion(col.Alineacion);
                if (col.NombreInterno == "Grupo")
                    p.AddText(grupo.Nombre ?? string.Empty);
                else if (col.NombreInterno == "ImporteTotal")
                    p.AddText("$" + grupo.Total.ToString("N2"));
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
                    var cell = row.Cells[i];
                    cell.Shading.Color = ParseColor(col.ColorFondo);
                    var p = cell.AddParagraph();
                    PdfFontHelper.ApplyFont(p.Format.Font, col.NombreFuente, col.TamanoFuente > 0 ? col.TamanoFuente : 9, col.Negrita, col.Cursiva);
                    p.Format.Font.Color = ParseColor(col.ColorFuente);
                    p.Format.Alignment = col.NombreInterno == "Grupo" ? MParagraphAlignment.Left : ConvertirAlineacion(col.Alineacion);
                    p.Format.SpaceAfter = 0;
                    p.Format.SpaceBefore = 0;

                    switch (col.NombreInterno)
                    {
                        case "Grupo":
                            p.AddText("    " + (concepto.Concepto ?? string.Empty));
                            break;
                        case "ImporteMensual":
                            p.AddText("$" + concepto.ImporteMensual.ToString("N2"));
                            break;
                        case "Duracion":
                            p.AddText(mostrarDuracion ? concepto.DuracionMeses.ToString() : string.Empty);
                            break;
                        case "ImporteTotal":
                            p.AddText("$" + concepto.ImporteTotal.ToString("N2"));
                            break;
                    }
                    AplicarBordeInferior(cell, "#D8D8D8", 0.20);
                }
            }
        }

        private void AgregarResumenSeccion(Table table, List<ColumnaIndirectos> cols, string etiquetaTotal, decimal total, string labelPorc, decimal porcentaje)
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
            rowTotal.Cells[0].MergeRight = last - 1;
            var pTot = rowTotal.Cells[0].AddParagraph(etiquetaTotal);
            pTot.Format.Alignment = MParagraphAlignment.Right;
            pTot.Format.Font.Bold = true;
            rowTotal.Cells[last].AddParagraph("$" + total.ToString("N2")).Format.Alignment = MParagraphAlignment.Right;
            rowTotal.Cells[last].Format.Font.Bold = true;

            var rowPct = table.AddRow();
            rowPct.HeightRule = RowHeightRule.AtLeast;
            rowPct.Height = Unit.FromCentimeter(0.48);
            for (int i = 0; i < cols.Count; i++)
                rowPct.Cells[i].Shading.Color = ParseColor("#F5F5F5");
            rowPct.Cells[0].MergeRight = last - 1;
            rowPct.Cells[0].AddParagraph(labelPorc).Format.Alignment = MParagraphAlignment.Right;
            rowPct.Cells[last].AddParagraph((porcentaje / 100m).ToString("P4")).Format.Alignment = MParagraphAlignment.Right;
        }

        private void AgregarResumenFinal(Table table, List<ColumnaIndirectos> cols, ConfiguracionIndirectos config)
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

            AgregarFilaResumenFinal(table, cols.Count, "% Oficina Central:", config.PorcentajeOficinaCentral.ToString("N4") + "%", "#F5F5F5", false);
            AgregarFilaResumenFinal(table, cols.Count, "% Gastos de Campo:", config.PorcentajeCampo.ToString("N4") + "%", "#F5F5F5", false);

            var spacerTop = table.AddRow();
            spacerTop.HeightRule = RowHeightRule.Exactly;
            spacerTop.Height = Unit.FromCentimeter(0.01);
            for (int i = 0; i < cols.Count; i++)
                AplicarBordeSuperior(spacerTop.Cells[i], "#1565C0", 0.75);

            AgregarFilaResumenFinal(table, cols.Count, "% TOTAL INDIRECTOS:", config.PorcentajeTotal.ToString("N4") + "%", "#BBDEFB", true);
        }

        private void AgregarFilaResumenFinal(Table table, int colCount, string etiqueta, string valor, string fondo, bool bold)
        {
            int last = colCount - 1;
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.50);
            for (int i = 0; i < colCount; i++)
                row.Cells[i].Shading.Color = ParseColor(fondo);
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

        private static bool EsBlanco(string color) => string.Equals(NormalizarColor(color), "#FFFFFF", StringComparison.OrdinalIgnoreCase);
        private static bool EsNegro(string color) => string.Equals(NormalizarColor(color), "#000000", StringComparison.OrdinalIgnoreCase);

        private static string NormalizarColor(string value)
        {
            var c = DrawingColorTranslator(value);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private static MParagraphAlignment ConvertirAlineacion(AlineacionColumna alineacion)
        {
            return alineacion switch
            {
                AlineacionColumna.Centro => MParagraphAlignment.Center,
                AlineacionColumna.Derecha => MParagraphAlignment.Right,
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
