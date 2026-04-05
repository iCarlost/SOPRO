using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Core.Entities;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
    public sealed class GeneradorPdfFSR
    {
        public enum FsrPdfRowType
        {
            Seccion,
            Subseccion,
            Dato,
            Final
        }

        public sealed class FsrPdfRow
        {
            public FsrPdfRowType Tipo { get; set; }
            public string Descripcion { get; set; } = string.Empty;
            public string Operacion { get; set; } = string.Empty;
            public string Unidad { get; set; } = string.Empty;
            public string Valor { get; set; } = string.Empty;

            public static FsrPdfRow Seccion(string descripcion) => new() { Tipo = FsrPdfRowType.Seccion, Descripcion = descripcion };
            public static FsrPdfRow Subseccion(string descripcion) => new() { Tipo = FsrPdfRowType.Subseccion, Descripcion = descripcion };
            public static FsrPdfRow Numero(string descripcion, string operacion, string unidad, decimal valor) => new()
            {
                Tipo = FsrPdfRowType.Dato,
                Descripcion = descripcion,
                Operacion = operacion ?? string.Empty,
                Unidad = unidad ?? string.Empty,
                Valor = valor.ToString("N5")
            };
            public static FsrPdfRow Texto(string descripcion, string operacion, string unidad, string valor) => new()
            {
                Tipo = FsrPdfRowType.Dato,
                Descripcion = descripcion,
                Operacion = operacion ?? string.Empty,
                Unidad = unidad ?? string.Empty,
                Valor = valor ?? string.Empty
            };
        }

        private readonly ReporteService _svc;

        public GeneradorPdfFSR(ReporteService svc)
        {
            _svc = svc;
        }

        public string Generar(Proyecto proyecto, PlantillaReporte plantilla, List<FsrPdfRow> filas, decimal factorFsr, string rutaDestino = null, ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (filas == null || filas.Count == 0) throw new InvalidOperationException("No hay datos de FSR para exportar.");

            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta, $"CalculoFSR_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            var doc = new Document();
            DefinirEstilos(doc);

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = MOrientation.Landscape;

            var headerHeightCm = Math.Max(1.8, plantilla.EncabezadoAltura / 28.0);
            var footerHeightCm = Math.Max(1.2, plantilla.PiePaginaAltura / 28.0);

            section.PageSetup.LeftMargin = Unit.FromCentimeter(0.9);
            section.PageSetup.RightMargin = Unit.FromCentimeter(0.9);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + 0.95);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + 0.65);

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, filas, factorFsr, tituloCfg);

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            normal.Font.Size = 8;

            var title = doc.Styles.AddStyle("FsrTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 11;
            title.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var head = doc.Styles.AddStyle("FsrHead", "Normal");
            head.Font.Bold = true;
            head.ParagraphFormat.Alignment = MParagraphAlignment.Center;
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
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));

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

        private static MParagraphAlignment ConvertirAlineacionTexto(string alineacion)
        {
            return (alineacion ?? string.Empty).Trim().ToLowerInvariant() switch
            {
                "centro" or "center" => MParagraphAlignment.Center,
                "derecha" or "right" => MParagraphAlignment.Right,
                "justificado" or "justify" => MParagraphAlignment.Justify,
                _ => MParagraphAlignment.Left
            };
        }

        private void ConstruirCuerpo(Section section, Proyecto proyecto, List<FsrPdfRow> filas, decimal factorFsr, ConfiguracionTituloReporte? tituloCfg)
        {
            var titulo = section.AddParagraph();
            ReportTitleStyleHelper.ApplyToParagraph(titulo, tituloCfg, "TABLA DE CÁLCULO DEL FACTOR DE SALARIO REAL");
            titulo.Format.Alignment = MParagraphAlignment.Center;
            titulo.Format.SpaceAfter = Unit.FromCentimeter(0.20);

            var meta = section.AddTable();
            meta.Borders.Visible = false;
            meta.AddColumn(Unit.FromCentimeter(7.0));
            meta.AddColumn(Unit.FromCentimeter(18.0));
            var mr1 = meta.AddRow();
            mr1.Cells[0].AddParagraph("Clave:").Format.Font.Bold = true;
            mr1.Cells[1].AddParagraph("JOR8HR");
            var mr2 = meta.AddRow();
            mr2.Cells[0].AddParagraph("Descripción:").Format.Font.Bold = true;
            mr2.Cells[1].AddParagraph("Factor de Salario Real FSR");
            meta.Format.SpaceAfter = Unit.FromCentimeter(0.20);

            var t = section.AddTable();
            t.Borders.Width = 0.25;
            t.Rows.LeftIndent = 0;
            t.Format.SpaceAfter = Unit.FromCentimeter(0.12);
            t.AddColumn(Unit.FromCentimeter(9.6));
            t.AddColumn(Unit.FromCentimeter(9.0));
            t.AddColumn(Unit.FromCentimeter(2.0));
            t.AddColumn(Unit.FromCentimeter(4.2));

            var h = t.AddRow();
            h.HeadingFormat = true;
            h.Shading.Color = ParseColor("#1F4E78");
            h.Height = Unit.FromCentimeter(0.65);
            h.HeightRule = RowHeightRule.AtLeast;
            AgregarHeader(h.Cells[0], "Descripción");
            AgregarHeader(h.Cells[1], "Operación");
            AgregarHeader(h.Cells[2], "Unidad");
            AgregarHeader(h.Cells[3], "Valor");

            foreach (var fila in filas)
            {
                if (fila.Tipo == FsrPdfRowType.Seccion)
                {
                    var r = t.AddRow();
                    r.Cells[0].MergeRight = 3;
                    r.Cells[0].Shading.Color = ParseColor("#D9E2F3");
                    r.Cells[0].VerticalAlignment = VerticalAlignment.Center;
                    var p = r.Cells[0].AddParagraph(fila.Descripcion);
                    p.Format.Font.Bold = true;
                    p.Format.SpaceAfter = 0;
                    p.Format.SpaceBefore = 0;
                    continue;
                }
                if (fila.Tipo == FsrPdfRowType.Subseccion)
                {
                    var r = t.AddRow();
                    r.Cells[0].MergeRight = 3;
                    r.Cells[0].Shading.Color = ParseColor("#EDEDED");
                    var p = r.Cells[0].AddParagraph(fila.Descripcion);
                    p.Format.Font.Bold = true;
                    p.Format.Font.Italic = true;
                    p.Format.SpaceAfter = 0;
                    p.Format.SpaceBefore = 0;
                    continue;
                }

                var row = t.AddRow();
                row.VerticalAlignment = VerticalAlignment.Center;
                row.Cells[0].AddParagraph(fila.Descripcion);
                var op = row.Cells[1].AddParagraph(fila.Operacion ?? string.Empty);
                op.Format.Font.Italic = !string.IsNullOrWhiteSpace(fila.Operacion);
                op.Format.Font.Size = 7.5;
                row.Cells[2].AddParagraph(fila.Unidad ?? string.Empty).Format.Alignment = MParagraphAlignment.Center;
                row.Cells[3].AddParagraph(fila.Valor ?? string.Empty).Format.Alignment = MParagraphAlignment.Right;
            }

            var fin = t.AddRow();
            fin.Height = Unit.FromCentimeter(0.75);
            fin.Cells[0].MergeRight = 2;
            fin.Cells[0].Shading.Color = ParseColor("#C00000");
            fin.Cells[3].Shading.Color = ParseColor("#C00000");
            var pfin = fin.Cells[0].AddParagraph("FACTOR DE SALARIO REAL");
            pfin.Format.Font.Bold = true;
            pfin.Format.Font.Color = ParseColor("#FFFFFF");
            pfin.Format.Alignment = MParagraphAlignment.Center;
            var vfin = fin.Cells[3].AddParagraph(factorFsr.ToString("N5"));
            vfin.Format.Font.Bold = true;
            vfin.Format.Font.Color = ParseColor("#FFFFFF");
            vfin.Format.Alignment = MParagraphAlignment.Center;
        }

        private static void AgregarHeader(Cell cell, string texto)
        {
            cell.VerticalAlignment = VerticalAlignment.Center;
            var p = cell.AddParagraph(texto);
            p.Style = "FsrHead";
            p.Format.Font.Color = ParseColor("#FFFFFF");
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
        }

        private static MColor ParseColor(string hex)
        {
            if (string.IsNullOrWhiteSpace(hex)) return Colors.Black;
            try { return MColor.Parse(hex); }
            catch { return Colors.Black; }
        }
    }
}
