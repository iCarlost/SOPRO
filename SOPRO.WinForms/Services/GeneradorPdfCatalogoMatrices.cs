using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using DrawingFont = System.Drawing.Font;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MColors = MigraDoc.DocumentObjectModel.Colors;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfCatalogoMatrices
    {
        private readonly ReporteService _svc;
        private readonly SOPROContext _ctx;

        public GeneradorPdfCatalogoMatrices(ReporteService svc, SOPROContext ctx)
        {
            _svc = svc;
            _ctx = ctx;
        }

        public string Generar(
            Proyecto proyecto,
            List<Matriz> matrices,
            PlantillaReporte plantilla,
            string filtroTitulo,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (matrices == null) throw new ArgumentNullException(nameof(matrices));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));

            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta, $"CatalogoMatrices_{SanitizarNombre(filtroTitulo)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            var ids = matrices.Select(m => m.Id).Distinct().ToList();
            var matricesCargadas = _ctx.Matrices
                .Include(m => m.Componentes.OrderBy(c => c.Orden))
                    .ThenInclude(c => c.Material)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Auxiliar)
                .Where(m => ids.Contains(m.Id))
                .OrderBy(m => m.Clave)
                .ToList();

            var doc = new Document();
            doc.Info.Title = ObtenerTituloCatalogo(filtroTitulo, tituloCfg);
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
                1.0,
                1.0);

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = orientation;
            section.PageSetup.DifferentFirstPageHeaderFooter = true;

            var headerHeightCm = Math.Max(1.8, plantilla.EncabezadoAltura / 28.0);
            var footerHeightCm = Math.Max(1.2, plantilla.PiePaginaAltura / 28.0);
            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + 1.0);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + 0.8);

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, matricesCargadas, filtroTitulo, tituloCfg);

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
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

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            var primary = section.Headers.Primary.AddTable();
            primary.Borders.Visible = false;
            primary.Rows.LeftIndent = 0;
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
            first.Rows.LeftIndent = 0;
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
            var primary = section.Footers.Primary.AddTable();
            primary.Borders.Visible = false;
            primary.Rows.LeftIndent = 0;
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
            first.Rows.LeftIndent = 0;
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

        private void ConstruirCuerpo(Section section, Proyecto proyecto, List<Matriz> matrices, string filtroTitulo, ConfiguracionTituloReporte? tituloCfg)
        {
            var pTitle = section.AddParagraph(ObtenerTituloCatalogo(filtroTitulo, tituloCfg), "CatalogoMatricesTitle");
            ReportTitleStyleHelper.ApplyToParagraph(pTitle, tituloCfg, ObtenerTituloCatalogo(filtroTitulo, tituloCfg));
            pTitle.Format.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardBackgroundHex);
            pTitle.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            pTitle.Format.SpaceAfter = Unit.FromCentimeter(0.12);
            pTitle.Format.KeepWithNext = true;

            var pProject = section.AddParagraph(proyecto.Nombre ?? string.Empty, "CatalogoMatricesProject");
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
            EscribirEncabezado(table);

            bool filaAlternada = false;
            foreach (var matriz in matrices)
            {
                EscribirFilaMatriz(table, matriz);

                decimal totalMO = CalcularTotalMO(matriz);
                foreach (var comp in matriz.Componentes.OrderBy(c => c.Orden))
                {
                    EscribirFilaComponente(table, comp, totalMO, filaAlternada);
                    filaAlternada = !filaAlternada;
                }

                EscribirFilaSuma(table, matriz.CostoDirecto);
                EscribirFilaSeparacion(table);
                filaAlternada = false;
            }
        }

        private static void AgregarColumnas(Table table, Section section)
        {
            double[] baseCm = { 1.2, 3.0, 12.4, 2.0, 2.6, 3.0, 3.0 };
            double availableCm = ReportPageLayoutHelper.GetLetterContentWidthCm(section);
            double factor = availableCm / baseCm.Sum();
            foreach (var width in baseCm)
                table.AddColumn(Unit.FromCentimeter(width * factor));
        }

        private static void EscribirEncabezado(Table table)
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
                p.Format.Alignment = (i >= 4) ? MParagraphAlignment.Right : (i == 3 ? MParagraphAlignment.Center : MParagraphAlignment.Left);
                p.Format.SpaceAfter = 0;
                p.Format.SpaceBefore = 0;
                PdfFontHelper.ApplyFont(p.Format.Font, "Segoe UI", 9, true, false);
                p.Format.Font.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
            }
        }

        private static void EscribirFilaMatriz(Table table, Matriz matriz)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromPoint(18);
            row.Shading.Color = ParseColor("#E8EAF6");
            row.Borders.Bottom.Width = 0.25;
            row.Borders.Bottom.Color = ParseColor("#C9C9D8");
            row.KeepWith = 1;

            AgregarTexto(row.Cells[0], "+", MParagraphAlignment.Center, bold: true);
            AgregarTexto(row.Cells[1], matriz.Clave ?? string.Empty, MParagraphAlignment.Left, bold: true);
            AgregarTexto(row.Cells[2], matriz.Descripcion ?? string.Empty, MParagraphAlignment.Left, bold: true);
            AgregarTexto(row.Cells[3], matriz.Unidad ?? string.Empty, MParagraphAlignment.Center, bold: true);
            AgregarTexto(row.Cells[4], string.Empty, MParagraphAlignment.Right, bold: false);
            AgregarTexto(row.Cells[5], string.Empty, MParagraphAlignment.Right, bold: false);
            AgregarTexto(row.Cells[6], string.Empty, MParagraphAlignment.Right, bold: false);
        }

        private static void EscribirFilaComponente(Table table, ComponenteMatriz comp, decimal totalMO, bool alt)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromPoint(15);
            row.Shading.Color = ParseColor(alt ? "#F5F5F5" : "#FFFFFF");
            row.Borders.Bottom.Width = 0.15;
            row.Borders.Bottom.Color = ParseColor("#E0E0E0");

            string prefijo = ObtenerPrefijo(comp);
            var datos = ObtenerDatosInsumo(comp);
            decimal costoUnitario = datos.cu;
            decimal importe;

            if ((comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra?.EsPorcentajeMO == true) ||
                (comp.TipoComponente == TipoComponenteMatriz.Herramienta && comp.Herramienta?.EsPorcentajeMO == true))
            {
                importe = totalMO * comp.Cantidad;
                costoUnitario = totalMO;
            }
            else
            {
                importe = comp.Cantidad * costoUnitario;
            }

            AgregarTexto(row.Cells[0], prefijo, MParagraphAlignment.Center, prefijo == "+");
            AgregarTexto(row.Cells[1], datos.clave, MParagraphAlignment.Left, false);
            AgregarTexto(row.Cells[2], datos.desc, MParagraphAlignment.Left, false);
            AgregarTexto(row.Cells[3], datos.unidad, MParagraphAlignment.Center, false);
            AgregarTexto(row.Cells[4], comp.Cantidad.ToString("0.00000"), MParagraphAlignment.Right, false);
            AgregarTexto(row.Cells[5], costoUnitario.ToString("#,##0.00"), MParagraphAlignment.Right, false);
            AgregarTexto(row.Cells[6], importe.ToString("#,##0.00"), MParagraphAlignment.Right, false);
        }

        private static void EscribirFilaSuma(Table table, decimal total)
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
            AgregarTexto(row.Cells[6], total.ToString("#,##0.00"), MParagraphAlignment.Right, true);
        }

        private static void EscribirFilaSeparacion(Table table)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.Exactly;
            row.Height = Unit.FromPoint(6);
            row.Borders.Visible = false;
            row.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardTextHex);
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

        private static decimal CalcularTotalMO(Matriz matriz)
        {
            decimal total = 0;
            foreach (var comp in matriz.Componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null && !comp.ManoDeObra.EsPorcentajeMO)
                    total += comp.Cantidad * comp.ManoDeObra.SalarioReal;
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                    total += comp.Cantidad * comp.Auxiliar.CostoDirecto;
            }
            return total;
        }

        private static string ObtenerPrefijo(ComponenteMatriz comp) => comp.TipoComponente switch
        {
            TipoComponenteMatriz.Material => "M",
            TipoComponenteMatriz.Maquinaria => "H",
            TipoComponenteMatriz.Herramienta => "H",
            TipoComponenteMatriz.Auxiliar => "+",
            TipoComponenteMatriz.ManoDeObra => string.Empty,
            _ => string.Empty
        };

        private static (string clave, string desc, string unidad, decimal cu) ObtenerDatosInsumo(ComponenteMatriz comp) => comp.TipoComponente switch
        {
            TipoComponenteMatriz.Material => (
                comp.Material?.Clave ?? string.Empty,
                comp.Material?.Descripcion ?? string.Empty,
                comp.Material?.Unidad ?? string.Empty,
                comp.Material?.PrecioUnitario ?? 0m),

            TipoComponenteMatriz.ManoDeObra => (
                comp.ManoDeObra?.Clave ?? string.Empty,
                comp.ManoDeObra?.Descripcion ?? string.Empty,
                "jor",
                comp.ManoDeObra?.SalarioReal ?? 0m),

            TipoComponenteMatriz.Maquinaria => (
                comp.Maquinaria?.Clave ?? string.Empty,
                comp.Maquinaria?.Descripcion ?? string.Empty,
                "hora",
                comp.Maquinaria?.CostoHorario ?? 0m),

            TipoComponenteMatriz.Herramienta => (
                comp.Herramienta?.Clave ?? string.Empty,
                comp.Herramienta?.Descripcion ?? string.Empty,
                comp.Herramienta?.Unidad ?? "%",
                comp.Herramienta?.PrecioUnitario ?? 0m),

            TipoComponenteMatriz.Auxiliar => (
                comp.Auxiliar?.Clave ?? string.Empty,
                comp.Auxiliar?.Descripcion ?? string.Empty,
                comp.Auxiliar?.Unidad ?? string.Empty,
                comp.Auxiliar?.CostoDirecto ?? 0m),

            _ => (string.Empty, string.Empty, string.Empty, 0m)
        };

        private static string ObtenerTituloCatalogo(string filtroTitulo, ConfiguracionTituloReporte? tituloCfg)
        {
            var baseTitle = ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "CATÁLOGO DE MATRICES");
            return filtroTitulo switch
            {
                "APU" => baseTitle + " (APU)",
                "Básicos" => baseTitle + " (BÁSICOS)",
                "Cuadrillas" => baseTitle + " (CUADRILLAS)",
                _ => baseTitle
            };
        }

        private static MParagraphAlignment ConvertirAlineacionTexto(string alineacion)
        {
            return (alineacion ?? string.Empty).ToLowerInvariant() switch
            {
                "centrado" or "centro" => MParagraphAlignment.Center,
                "derecha" => MParagraphAlignment.Right,
                "justificado" => MParagraphAlignment.Justify,
                _ => MParagraphAlignment.Left,
            };
        }

        private static string SanitizarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Reporte";
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre.Trim();
        }

        private static MColor ParseColor(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return MColors.Black;
            try
            {
                var c = ColorTranslator.FromHtml(value);
                return MColor.FromRgb(c.R, c.G, c.B);
            }
            catch
            {
                return MColors.Black;
            }
        }
    }
}
