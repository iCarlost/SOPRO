using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Columns;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfPresupuesto
    {
        private readonly ReporteService _svc;

        public GeneradorPdfPresupuesto(ReporteService svc) => _svc = svc;

        public string Generar(
            Proyecto proyecto,
            List<ConceptoPresupuesto> conceptos,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            string? rutaDestino = null,
            decimal factorPU = 1m,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"Presupuesto_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            var doc = ConstruirDocumento(proyecto, conceptos, plantilla, snapshot, factorPU, tituloCfg);

            var renderer = new PdfDocumentRenderer()
            {
                Document = doc
            };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        /// <summary>
        /// Construye el documento MigraDoc del reporte SIN serializarlo. Es el SEAM
        /// que permite a las pruebas inspeccionar las propiedades de las celdas
        /// (fuentes, colores, shading) antes de generar el PDF. Sólo lee el snapshot
        /// neutral + el dominio; nunca el grid.
        /// </summary>
        public Document ConstruirDocumento(
            Proyecto proyecto,
            List<ConceptoPresupuesto> conceptos,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            decimal factorPU = 1m,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            // Defaults NEUTRALES por rol de fila (única fuente de verdad, compartida
            // con grid/Excel) combinados con la config persistida del usuario. El
            // override de celda del snapshot sigue teniendo la máxima precedencia.
            snapshot = snapshot with
            {
                EstilosFila = EstilosFilaPredeterminados.CombinarPresupuesto(snapshot.EstilosFila)
            };

            var cols = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (!cols.Any())
                throw new InvalidOperationException("No hay columnas visibles para exportar.");

            // Paridad por defecto con el grid legacy: la columna sintética de
            // numeración "#" se antepone cuando el snapshot no la define.
            cols = ReportColumnDefaults.ConPrefijoNumeroPresupuesto(cols);

            var doc = new Document();
            DefinirEstilos(doc);

            var orientation = ReportPageLayoutHelper.DetermineAutoOrientation(
                cols.Select(c => (c.Identificador ?? c.Encabezado ?? string.Empty, Math.Max(24, c.Ancho))),
                1.0,
                1.0);

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
            ConstruirTablaPresupuesto(section, proyecto, conceptos, cols, snapshot, factorPU, tituloCfg);

            return doc;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            normal.Font.Size = 8.5;

            var header = doc.Styles.AddStyle("TableHeader", "Normal");
            header.Font.Bold = true;
            header.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var subtotal = doc.Styles.AddStyle("SubtotalRow", "Normal");
            subtotal.Font.Bold = true;

            var group = doc.Styles.AddStyle("GroupRow", "Normal");
            group.Font.Bold = true;
        }

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderHeader(section.Headers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
                return;

            var table = section.Headers.Primary.AddTable();
            table.Borders.Visible = false;
            ReportPageLayoutHelper.AddHeaderFooterColumns(table, section);
            var row = table.AddRow();
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
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            if (PlantillaLibrePdfRenderer.TryRenderFooter(section.Footers.Primary, section, proyecto, plantilla, elementosPdf, _svc))
                return;

            var table = section.Footers.Primary.AddTable();
            table.Borders.Visible = false;
            ReportPageLayoutHelper.AddHeaderFooterColumns(table, section);
            var row = table.AddRow();
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
            p.Format.Font.Name = PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(fuente) ? "Segoe UI" : fuente);
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
                    p.AddText(texto[index..]);
                    break;
                }

                if (next > index)
                    p.AddText(texto[index..next]);

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

        private void ConstruirTablaPresupuesto(Section section, Proyecto proyecto, List<ConceptoPresupuesto> conceptos, List<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, decimal factorPU, ConfiguracionTituloReporte? tituloCfg = null)
        {
            var estiloTabla = snapshot.EstiloTabla;
            // Snapshot sin overrides: sirve de línea base para aislar QUÉ propiedad
            // aportó realmente un override de celda/rol (delta contra la cascada de
            // tabla/columna). Con snapshots sin estilos el delta es siempre vacío y
            // el render conserva EXACTAMENTE el comportamiento legacy.
            var snapshotBase = snapshot with { EstilosCelda = null, EstilosFila = null };
            var titulo = string.IsNullOrWhiteSpace(tituloCfg?.TextoTitulo) ? "PRESUPUESTO" : tituloCfg!.TextoTitulo;
            var pTitle = section.AddParagraph(titulo);
            ReportTitleStyleHelper.ApplyToParagraph(pTitle, tituloCfg, titulo);
            pTitle.Format.Alignment = MParagraphAlignment.Center;
            pTitle.Format.SpaceAfter = Unit.FromCentimeter(0.20);

            var table = section.AddTable();
            table.Format.Font.Name = PdfFontHelper.NormalizeFontName(estiloTabla.EstiloContenido.Fuente);
            table.Format.Font.Size = estiloTabla.EstiloContenido.Tamano;
            table.Rows.LeftIndent = 0;
            table.Borders.Visible = estiloTabla.Bordes.Visible;

            double availableCm = ReportColumnWidthConverter.GetLetterUsableWidthCm(
                section.PageSetup.Orientation == MOrientation.Landscape);
            int[] anchosPx = cols.Select(c => c.Ancho).ToArray();
            double[] anchosCm = ReportColumnWidthConverter.PxToCm(anchosPx, availableCm);
            for (int i = 0; i < cols.Count; i++)
            {
                var pdfCol = table.AddColumn(Unit.FromCentimeter(anchosCm[i]));
                pdfCol.Format.Alignment = ConvertirAlineacion(cols[i].Alineacion);
            }

            var encabezado = estiloTabla.EstiloEncabezado;
            var header = table.AddRow();
            header.HeadingFormat = true;
            header.HeightRule = RowHeightRule.AtLeast;
            header.Height = Unit.FromCentimeter(0.65);
            header.Shading.Color = ParseColorSafe(encabezado.ColorFondo, "#1565C0");
            header.Format.Font.Color = ParseColorSafe(encabezado.ColorFuente, "#FFFFFF");
            header.Format.Font.Bold = encabezado.Negrita;
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = header.Cells[i];
                cell.AddParagraph(col.Encabezado ?? string.Empty);
                cell.Format.Alignment = MParagraphAlignment.Center;
                cell.Format.Font.Name = PdfFontHelper.NormalizeFontName(col.EstiloEncabezado.Fuente);
                cell.Format.Font.Size = col.EstiloEncabezado.Tamano <= 0 ? 9 : col.EstiloEncabezado.Tamano;
                cell.Format.Font.Bold = col.EstiloEncabezado.Negrita;
                cell.Format.Font.Italic = col.EstiloEncabezado.Cursiva;
                cell.Format.Font.Color = ParseColorSafe(col.EstiloEncabezado.ColorFuente, "#FFFFFF");
                cell.VerticalAlignment = VerticalAlignment.Center;
                AplicarBordeInferior(cell, "#C8D0D8", 0.4);
            }

            var conceptosOrdenados = OrdenarJerarquia(conceptos);
            int consecutivo = 0;
            foreach (var c in conceptosOrdenados)
            {
                if (!c.EsAgrupador) consecutivo++;
                if (c.Notas == SOPRO.Application.UseCases.Reporting.PresupuestoReportSnapshotBuilder.MarcaSubtotalSintetico)
                {
                    AgregarFilaSubtotal(table, c, cols, snapshot, snapshotBase);
                    continue;
                }

                AgregarFilaConcepto(table, proyecto, c, cols, snapshot, snapshotBase, consecutivo, factorPU);
            }

            AgregarTotales(table, proyecto, conceptos, cols, snapshot, snapshotBase);
        }

        private void AgregarFilaConcepto(Table table, Proyecto proyecto, ConceptoPresupuesto c, List<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, ReportColumnSnapshot snapshotBase, int consecutivo, decimal factorPU)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(c.EsAgrupador ? 0.58 : 0.50);
            var rowDisplay = BudgetLoadService.BuildRowDisplay(proyecto, c);
            string indent = new(' ', Math.Max(0, c.Nivel * 2));

            var claveFila = PresupuestoReportSnapshotBuilder.ClaveFila(c);
            var rolFila = PresupuestoReportSnapshotBuilder.RolFila(c);

            // El fondo/estilo de las filas AGRUPADOR lo aporta el DEFAULT del rol
            // (EstilosFilaPredeterminados) a través del resolver, por celda; ya no
            // hay colores hardcodeados aquí. La negrita de los agrupadores también
            // proviene del rol (ver el bucle de celdas).

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = row.Cells[i];
                cell.VerticalAlignment = VerticalAlignment.Center;
                var overrideEfectivo = OverrideEfectivo(snapshot, snapshotBase, claveFila, col.Identificador, rolFila);
                var alignment = overrideEfectivo.AlineacionHorizontal is ReportTextAlignment alineacionOverride
                    ? ConvertirAlineacion(alineacionOverride)
                    : (col.Identificador == "Descripcion" ? MParagraphAlignment.Justify : ConvertirAlineacion(col.Alineacion));
                cell.Format.Alignment = alignment;
                cell.Format.Font.Name = PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(col.EstiloContenido.Fuente) ? "Segoe UI" : col.EstiloContenido.Fuente);
                cell.Format.Font.Size = col.EstiloContenido.Tamano <= 0 ? 8.2f : col.EstiloContenido.Tamano;
                cell.Format.Font.Bold = c.EsAgrupador || col.EstiloContenido.Negrita;
                cell.Format.Font.Italic = col.EstiloContenido.Cursiva;
                cell.Format.Font.Color = ParseColorSafe(col.EstiloContenido.ColorFuente, "#000000");
                if (!c.EsAgrupador)
                    cell.Shading.Color = ParseColorSafe(col.EstiloContenido.ColorFondo, "#FFFFFF");
                AplicarOverlayPdf(cell, overrideEfectivo);
                var p = cell.AddParagraph();
                p.Format.Alignment = alignment;

                object valor = ResolverValorConceptoPdf(c, col.Identificador, consecutivo, factorPU, rowDisplay, indent);
                if (valor is decimal d)
                {
                    p.AddText(ReportColumnGridFormat.FormatearPdf(d, col, snapshot));
                }
                else
                {
                    p.AddText(valor?.ToString() ?? string.Empty);
                }

                AplicarBordeInferior(cell, c.EsAgrupador ? "#D7DEE6" : "#E3E7EB", c.EsAgrupador ? 0.35 : 0.22);
            }
        }

        private void AgregarFilaSubtotal(Table table, ConceptoPresupuesto c, List<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, ReportColumnSnapshot snapshotBase)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.55);
            // Fondo y negrita del subtotal sintético: DEFAULT del rol Subtotal vía resolver.

            var claveFila = PresupuestoReportSnapshotBuilder.ClaveFila(c);
            var rolFila = PresupuestoReportSnapshotBuilder.RolFila(c);

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = row.Cells[i];
                cell.VerticalAlignment = VerticalAlignment.Center;
                var overrideEfectivo = OverrideEfectivo(snapshot, snapshotBase, claveFila, col.Identificador, rolFila);
                cell.Format.Font.Name = PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(col.EstiloContenido.Fuente) ? "Segoe UI" : col.EstiloContenido.Fuente);
                cell.Format.Font.Size = col.EstiloContenido.Tamano <= 0 ? 8.2f : col.EstiloContenido.Tamano;
                cell.Format.Font.Italic = col.EstiloContenido.Cursiva;
                cell.Format.Font.Color = ParseColorSafe(col.EstiloContenido.ColorFuente, "#000000");
                var alignment = overrideEfectivo.AlineacionHorizontal is ReportTextAlignment alineacionOverride
                                ? ConvertirAlineacion(alineacionOverride)
                                : (col.Identificador == "Descripcion"
                                    ? MParagraphAlignment.Justify
                                    : ConvertirAlineacion(col.Alineacion));

                cell.Format.Alignment = alignment;
                AplicarOverlayPdf(cell, overrideEfectivo);

                var p = cell.AddParagraph();
                p.Format.Alignment = alignment;

                if (col.Identificador == "Descripcion")
                    p.AddText($"Total {c.Descripcion}:");
                else if (col.Identificador is "ImporteTotal" or "Importe" or "Subtotal" or "Total")
                    p.AddText(ReportColumnGridFormat.FormatearPdf(c.ImporteTotal, col, snapshot));

                AplicarBordeInferior(cell, "#C7D6E5", 0.35);
            }
        }

        private void AgregarTotales(Table table, Proyecto proyecto, List<ConceptoPresupuesto> conceptos, List<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, ReportColumnSnapshot snapshotBase)
        {
            var terminales = conceptos.Where(c => !c.EsAgrupador).ToList();
            decimal subtotal = terminales.Sum(c => c.ImporteTotal);
            decimal iva = subtotal * proyecto.PorcentajeIVA / 100m;
            decimal total = subtotal + iva;

            // Claves estables de las filas de cierre (coinciden con las que persiste
            // el grid): SUB:{clave} para el subtotal global, IVA y TOTAL. El fondo de
            // cada fila lo aporta el DEFAULT del rol (Subtotal/Total) vía resolver.
            AgregarFilaTotal(table, cols, snapshot, snapshotBase, "SUBTOTAL:", subtotal, true, ClaveFilaReporte.Subtotal(string.Empty), RolFilaReporte.Subtotal);
            AgregarFilaTotal(table, cols, snapshot, snapshotBase, $"IVA ({proyecto.PorcentajeIVA:N0}%):", iva, false, ClaveFilaReporte.Iva, RolFilaReporte.Total);
            AgregarFilaTotal(table, cols, snapshot, snapshotBase, "TOTAL:", total, true, ClaveFilaReporte.Total, RolFilaReporte.Total);
        }

        private void AgregarFilaTotal(Table table, List<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, ReportColumnSnapshot snapshotBase, string etiqueta, decimal monto, bool negrita, string claveFila, string rolFila)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.55);
            row.Format.Font.Bold = negrita;

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = row.Cells[i];
                var overrideEfectivo = OverrideEfectivo(snapshot, snapshotBase, claveFila, col.Identificador, rolFila);
                cell.Format.Font.Name = PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(col.EstiloContenido.Fuente) ? "Segoe UI" : col.EstiloContenido.Fuente);
                cell.Format.Font.Size = col.EstiloContenido.Tamano <= 0 ? 8.2f : col.EstiloContenido.Tamano;
                cell.Format.Font.Italic = col.EstiloContenido.Cursiva;
                cell.Format.Font.Color = ParseColorSafe(col.EstiloContenido.ColorFuente, "#000000");
                var alignment = overrideEfectivo.AlineacionHorizontal is ReportTextAlignment alineacionOverride
                                 ? ConvertirAlineacion(alineacionOverride)
                                 : (col.Identificador == "Descripcion"
                                     ? MParagraphAlignment.Justify
                                     : ConvertirAlineacion(col.Alineacion));

                cell.Format.Alignment = alignment;
                AplicarOverlayPdf(cell, overrideEfectivo);

                // La negrita de la fila de cierre es un rasgo de la fila (IVA no va
                // en negrita aunque comparta el rol Total), por eso se reafirma
                // después de aplicar el default del rol.
                cell.Format.Font.Bold = negrita;

                var p = cell.AddParagraph();
                p.Format.Alignment = alignment;
                if (col.Identificador == "Descripcion")
                    p.AddText(etiqueta);
                else if (col.Identificador is "ImporteTotal" or "Importe" or "Subtotal" or "Total")
                    p.AddText(ReportColumnGridFormat.FormatearPdf(monto, col, snapshot));

                AplicarBordeInferior(cell, "#BCC9D6", 0.4);
            }
        }

        private static object ResolverValorConceptoPdf(ConceptoPresupuesto concepto, string nombreInterno, int consecutivo,
            decimal factorPU, BudgetGridRowDisplay rowDisplay, string indent)
        {
            bool esAgrupador = concepto.EsAgrupador;
            return nombreInterno switch
            {
                "Numero" => esAgrupador ? string.Empty : consecutivo.ToString(),
                "Clave" => concepto.Clave ?? string.Empty,
                "Descripcion" => indent + (concepto.Descripcion ?? string.Empty),
                "Unidad" => esAgrupador ? string.Empty : (concepto.Unidad ?? string.Empty),
                "Cantidad" => esAgrupador ? string.Empty : concepto.Cantidad,
                "PrecioUnitario" => esAgrupador ? string.Empty : (concepto.PrecioUnitario > 0 ? concepto.PrecioUnitario : concepto.CostoDirectoUnitario * factorPU),
                "ImporteTotal" or "Importe" => esAgrupador ? string.Empty : (concepto.ImporteTotal > 0 ? concepto.ImporteTotal : concepto.Cantidad * concepto.CostoDirectoUnitario * factorPU),
                _ => ObtenerValorDesdeDisplay(rowDisplay, nombreInterno)
            };
        }

        private static object ObtenerValorDesdeDisplay(BudgetGridRowDisplay rowDisplay, string nombreInterno)
        {
            if (rowDisplay?.ValuesByInternalName == null)
                return string.Empty;

            if (!rowDisplay.ValuesByInternalName.TryGetValue(nombreInterno, out var valor) || valor == null)
                return string.Empty;

            return valor;
        }

        private static MParagraphAlignment ConvertirAlineacion(string alineacion) => alineacion switch
        {
            "Centro" => MParagraphAlignment.Center,
            "Derecha" => MParagraphAlignment.Right,
            "Justificado" => MParagraphAlignment.Justify,
            _ => MParagraphAlignment.Left,
        };

        private static MParagraphAlignment ConvertirAlineacion(ReportTextAlignment alineacion) => alineacion switch
        {
            ReportTextAlignment.Centro => MParagraphAlignment.Center,
            ReportTextAlignment.Derecha => MParagraphAlignment.Right,
            ReportTextAlignment.Justificado => MParagraphAlignment.Justify,
            _ => MParagraphAlignment.Left,
        };

        /// <summary>
        /// Aísla las propiedades que un override de celda o un estilo de rol
        /// REALMENTE aportaron, comparando el estilo resuelto con el de la cascada
        /// base (tabla + columna, sin celda/rol). Con <paramref name="snapshotBase"/>
        /// sin estilos, TODAS las propiedades quedan <c>null</c> (retrocompatibilidad
        /// exacta con el render legacy).
        /// </summary>
        private static ReportStyleOverlay OverrideEfectivo(
            ReportColumnSnapshot snapshot,
            ReportColumnSnapshot snapshotBase,
            string claveFila,
            string nombreColumna,
            string? rolFila)
        {
            var claveVista = PresupuestoReportSnapshotBuilder.ClaveVista;
            var completo = ReportCellStyleResolver.Resolve(snapshot, claveVista, claveFila, nombreColumna, rolFila, false);
            var baseStyle = ReportCellStyleResolver.Resolve(snapshotBase, claveVista, claveFila, nombreColumna, null, false);

            return new ReportStyleOverlay(
                Fuente: completo.Fuente != baseStyle.Fuente ? completo.Fuente : null,
                TamanoFuente: completo.TamanoFuente != baseStyle.TamanoFuente ? completo.TamanoFuente : null,
                Negrita: completo.Negrita != baseStyle.Negrita ? completo.Negrita : null,
                Cursiva: completo.Cursiva != baseStyle.Cursiva ? completo.Cursiva : null,
                ColorFuente: completo.ColorFuente != baseStyle.ColorFuente ? completo.ColorFuente : null,
                ColorFondo: completo.ColorFondo != baseStyle.ColorFondo ? completo.ColorFondo : null,
                AlineacionHorizontal: completo.AlineacionHorizontal != baseStyle.AlineacionHorizontal ? completo.AlineacionHorizontal : null,
                AlineacionVertical: completo.AlineacionVertical != baseStyle.AlineacionVertical ? completo.AlineacionVertical : null,
                Wrap: completo.Wrap != baseStyle.Wrap ? completo.Wrap : null,
                FormatoNumerico: completo.FormatoNumerico != baseStyle.FormatoNumerico ? completo.FormatoNumerico : null);
        }

        /// <summary>
        /// Aplica al cell SOLO las propiedades no nulas del override (fuente, tamaño,
        /// negrita, cursiva, color de texto, color de fondo). La alineación se aplica
        /// por separado porque debe replicarse también en el párrafo. Un override
        /// numérico (<c>FormatoNumerico</c>) se IGNORA en PDF: el formato numérico se
        /// conserva por columna vía <see cref="ReportColumnGridFormat"/>, que es la
        /// autoridad de cantidades/importes (decisión documentada de 4.2).
        /// </summary>
        private static void AplicarOverlayPdf(Cell cell, ReportStyleOverlay overrideEfectivo)
        {
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.Fuente))
                cell.Format.Font.Name = PdfFontHelper.NormalizeFontName(overrideEfectivo.Fuente);
            if (overrideEfectivo.TamanoFuente is float tamano && tamano > 0)
                cell.Format.Font.Size = tamano;
            if (overrideEfectivo.Negrita is bool negrita)
                cell.Format.Font.Bold = negrita;
            if (overrideEfectivo.Cursiva is bool cursiva)
                cell.Format.Font.Italic = cursiva;
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.ColorFuente))
                cell.Format.Font.Color = ParseColorSafe(overrideEfectivo.ColorFuente, "#000000");
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.ColorFondo))
                cell.Shading.Color = ParseColorSafe(overrideEfectivo.ColorFondo, "#FFFFFF");
        }

        private static MColor ParseColorSafe(string? html, string fallback)
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

        private List<ConceptoPresupuesto> OrdenarJerarquia(List<ConceptoPresupuesto> conceptos)
        {
            var resultado = new List<ConceptoPresupuesto>();
            var ordenados = conceptos.OrderBy(c => c.Orden).ToList();
            AgregarConSubtotales(ordenados, resultado);
            return resultado;
        }

        private void AgregarConSubtotales(List<ConceptoPresupuesto> ordenados, List<ConceptoPresupuesto> resultado)
        {
            var pilaAgrup = new List<(string clave, string desc, int nivel, decimal importe)>();
            foreach (var c in ordenados)
            {
                string claveC = (c.Clave ?? string.Empty).Trim();
                for (int k = pilaAgrup.Count - 1; k >= 0; k--)
                {
                    var agr = pilaAgrup[k];
                    bool esAncestro = (agr.clave != string.Empty && claveC != string.Empty)
                        ? (claveC.StartsWith(agr.clave + ".", StringComparison.OrdinalIgnoreCase) || claveC == agr.clave)
                        : c.Nivel > agr.nivel;
                    if (!esAncestro)
                    {
                        pilaAgrup.RemoveAt(k);
                        resultado.Add(new ConceptoPresupuesto
                        {
                            Descripcion = agr.desc,
                            Clave = agr.clave,
                            EsAgrupador = true,
                            Notas = "__subtotal__",
                            ImporteTotal = agr.importe,
                            Nivel = agr.nivel
                        });
                    }
                }

                resultado.Add(c);
                if (c.EsAgrupador)
                {
                    pilaAgrup.Add((claveC, c.Descripcion ?? string.Empty, c.Nivel, 0m));
                }
                else
                {
                    for (int k = 0; k < pilaAgrup.Count; k++)
                    {
                        var agr = pilaAgrup[k];
                        pilaAgrup[k] = (agr.clave, agr.desc, agr.nivel, agr.importe + c.ImporteTotal);
                    }
                }
            }

            for (int k = pilaAgrup.Count - 1; k >= 0; k--)
            {
                var agr = pilaAgrup[k];
                resultado.Add(new ConceptoPresupuesto
                {
                    Descripcion = agr.desc,
                    Clave = agr.clave,
                    EsAgrupador = true,
                    Notas = "__subtotal__",
                    ImporteTotal = agr.importe,
                    Nivel = agr.nivel
                });
            }
        }

        private static string SanitizarNombre(string? nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre.Length > 50 ? nombre[..50] : nombre;
        }
    }
}
