using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte de Explosión de Insumos en formato .xlsx.
    /// Recibe los diccionarios DatosInsumo ya calculados por FormExplosionInsumos
    /// (método OPUS PLANET) y el snapshot neutral de columnas
    /// (<see cref="ReportColumnSnapshot"/>) compartido con la ruta PDF: mismas
    /// columnas, orden, anchos, estilos y la MISMA resolución de formatos numéricos
    /// (<see cref="ReportColumnGridFormat"/>). Cubre las cuatro familias de insumo
    /// (Materiales, Mano de Obra, Herramientas y Maquinaria) y sus totales.
    /// </summary>
    public class GeneradorExcelExplosion
    {
        private readonly ReporteService _svc;

        public GeneradorExcelExplosion(ReporteService svc) => _svc = svc;

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            string filtro,
            Dictionary<int, DatosInsumo> materiales,
            Dictionary<int, DatosInsumo> manoObra,
            Dictionary<int, DatosInsumo> maquinaria,
            Dictionary<int, DatosInsumo> herramientas,
            decimal costoDirectoTotal,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            ArgumentNullException.ThrowIfNull(snapshot);

            if (string.IsNullOrEmpty(rutaDestino))
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"ExplosionInsumos_{Sanitizar(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            var cols    = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            int numCols = cols.Count;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Explosión de Insumos");

            int fila = 1;
            fila = EscribirEncabezado(ws, plantilla, proyecto, numCols, fila);
            fila = EscribirTituloReporte(ws, proyecto, filtro, numCols, fila, tituloCfg);
            fila = EscribirTitulosColumnas(ws, cols, fila);
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, fila - 1);
            ws.SheetView.FreezeRows(fila - 1);

            if (filtro == "Todos" || filtro == "Materiales")
                fila = EscribirSeccion(ws, "MATERIALES",   materiales,   cols, snapshot, costoDirectoTotal, fila);
            if (filtro == "Todos" || filtro == "Mano de Obra")
                fila = EscribirSeccion(ws, "MANO DE OBRA", manoObra,     cols, snapshot, costoDirectoTotal, fila);
            if (filtro == "Todos" || filtro == "Herramientas")
                fila = EscribirSeccion(ws, "HERRAMIENTAS", herramientas, cols, snapshot, costoDirectoTotal, fila);
            if (filtro == "Todos" || filtro == "Maquinaria")
                fila = EscribirSeccion(ws, "MAQUINARIA",   maquinaria,   cols, snapshot, costoDirectoTotal, fila);
            if (filtro == "Todos")
                fila = EscribirTotalGeneral(ws, costoDirectoTotal, cols, snapshot, fila);

            fila += 2;
            EscribirPie(ws, plantilla, proyecto, numCols, fila);

            for (int i = 0; i < cols.Count; i++)
                ws.Column(i + 1).Width = Math.Max(cols[i].Ancho / 7.0, 4);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Left    = 0.5;
            ws.PageSetup.Margins.Right   = 0.5;
            ws.PageSetup.Margins.Top     = 0.75;
            ws.PageSetup.Margins.Bottom  = 0.75;

            wb.SaveAs(rutaDestino);
            return rutaDestino;
        }

        private int EscribirEncabezado(IXLWorksheet ws, PlantillaReporte p,
                                        Proyecto proyecto, int numCols, int fila)
        {
            int c1 = 1, c2 = numCols / 3 + 1, c3 = numCols * 2 / 3 + 1;
            EscribirZona(ws, fila, c1, c2-1, p.EncabezadoIzqTipo, p.EncabezadoIzqContenido,
                p.EncabezadoIzqFuente, p.EncabezadoIzqTamaño, p.EncabezadoIzqNegrita,
                p.EncabezadoIzqCursiva, p.EncabezadoIzqAlineacion, proyecto, p);
            EscribirZona(ws, fila, c2, c3-1, p.EncabezadoCenTipo, p.EncabezadoCenContenido,
                p.EncabezadoCenFuente, p.EncabezadoCenTamaño, p.EncabezadoCenNegrita,
                p.EncabezadoCenCursiva, p.EncabezadoCenAlineacion, proyecto, p);
            EscribirZona(ws, fila, c3, numCols, p.EncabezadoDerTipo, p.EncabezadoDerContenido,
                p.EncabezadoDerFuente, p.EncabezadoDerTamaño, p.EncabezadoDerNegrita,
                p.EncabezadoDerCursiva, p.EncabezadoDerAlineacion, proyecto, p);
            ws.Row(fila).Height = p.EncabezadoAltura * 0.75;
            fila++;
            ws.Range(fila, 1, fila, numCols).Style.Border.TopBorder = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, numCols).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");
            return fila + 1;
        }

        private int EscribirTituloReporte(IXLWorksheet ws, Proyecto proyecto,
                                           string filtro, int numCols, int fila, ConfiguracionTituloReporte? tituloCfg)
        {
            var r = ws.Range(fila, 1, fila, numCols);
            var titulo = filtro == "Todos"
                ? ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "EXPLOSIÓN DE INSUMOS")
                : $"{ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "EXPLOSIÓN DE INSUMOS")} — {filtro.ToUpper()}";
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(r, tituloCfg, titulo, "#1565C0");
            ws.Row(fila).Height = 20; fila++;

            var r2 = ws.Range(fila, 1, fila, numCols);
            r2.Merge(); r2.FirstCell().Value = proyecto.Nombre;
            r2.Style.Font.FontSize = 10;
            r2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            r2.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            ws.Row(fila).Height = 15;
            return fila + 1;
        }

        private int EscribirTitulosColumnas(IXLWorksheet ws, IReadOnlyList<ReportColumnDefinition> cols, int fila)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var enc = col.EstiloEncabezado;
                var cell = ws.Cell(fila, i + 1);
                cell.Value = col.Encabezado ?? string.Empty;
                var est = cell.Style;
                est.Font.Bold = enc.Negrita;
                est.Font.Italic = enc.Cursiva;
                est.Font.FontName   = string.IsNullOrWhiteSpace(enc.Fuente) ? "Segoe UI" : enc.Fuente;
                est.Font.FontSize   = enc.Tamano > 0 ? enc.Tamano : 9;
                est.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(enc.ColorFondo ?? "#4A4A6A");
                est.Font.FontColor = ExcelColorHelper.SafeFromHtml(enc.ColorFuente, "#FFFFFF");
                est.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                est.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                est.Alignment.WrapText = col.Wrap;
                est.Border.BottomBorder = XLBorderStyleValues.Medium;
                est.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");
            }
            ws.Row(fila).Height = 18;
            return fila + 1;
        }

        private int EscribirSeccion(IXLWorksheet ws, string titulo,
            Dictionary<int, DatosInsumo> dic,
            IReadOnlyList<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot,
            decimal costoTotal, int fila)
        {
            if (dic.Count == 0) return fila;

            var rEnc = ws.Range(fila, 1, fila, cols.Count);
            rEnc.Merge(); rEnc.FirstCell().Value = titulo;
            rEnc.Style.Font.Bold = true; rEnc.Style.Font.FontSize = 10;
            rEnc.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
            rEnc.Style.Font.FontColor = XLColor.White;
            rEnc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Row(fila).Height = 16; fila++;

            decimal subtotal = 0;
            bool alt = false;
            foreach (var kvp in dic.OrderBy(x => x.Value.Clave))
            {
                var ins     = kvp.Value;
                decimal imp = ins.Cantidad;   // Cantidad = importe acumulado (OPUS PLANET)
                decimal pct = costoTotal > 0 ? imp / costoTotal : 0;
                subtotal   += imp;

                var row = new ExplosionReportRow(
                    ins.Clave ?? string.Empty,
                    ins.Descripcion ?? string.Empty,
                    ins.Unidad ?? string.Empty,
                    ins.CantidadFisica,
                    ins.PrecioUnitario,
                    imp,
                    pct,
                    ins.EsPorcentual);

                for (int i = 0; i < cols.Count; i++)
                {
                    var col  = cols[i];
                    var cont = col.EstiloContenido;
                    var cell = ws.Cell(fila, i + 1);

                    if (col.EsNumerica && ExplosionExportResolver.TryResolveNumber(row, col, out var valor))
                        EscribirNumerico(cell, valor, col, snapshot);
                    else
                        cell.Value = ExplosionExportResolver.ResolveValue(row, col);

                    var est = cell.Style;
                    est.Font.FontName   = string.IsNullOrWhiteSpace(cont.Fuente) ? "Segoe UI" : cont.Fuente;
                    est.Font.FontSize   = cont.Tamano > 0 ? cont.Tamano : 9;
                    est.Font.Bold       = cont.Negrita;
                    est.Font.Italic     = cont.Cursiva;
                    est.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                        ExplosionExportResolver.ResolveCellBackground(col, snapshot.EstiloTabla, alt));
                    est.Font.FontColor = ExcelColorHelper.SafeFromHtml(cont.ColorFuente, "#000000");
                    est.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                    est.Alignment.Vertical = ConvertirAlineacionVertical(col.AlineacionVertical);
                    est.Alignment.WrapText = col.Wrap;
                    est.Border.BottomBorder = XLBorderStyleValues.Hair;
                    est.Border.BottomBorderColor = XLColor.Gray;
                }
                ws.Row(fila).Height = CalcularAlturaFila(cols, fila, ws, 13);
                fila++;
                alt = !alt;
            }

            // Subtotal
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i]; var cell = ws.Cell(fila, i + 1);
                if      (string.Equals(col.Identificador, "Descripcion", StringComparison.OrdinalIgnoreCase)) { cell.Value = $"SUBTOTAL {titulo}:"; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (EsColumnaImporte(col))     { EscribirNumerico(cell, subtotal, col, snapshot); cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (string.Equals(col.Identificador, "Porcentaje", StringComparison.OrdinalIgnoreCase))  { EscribirNumerico(cell, costoTotal > 0 ? subtotal / costoTotal : 0, col, snapshot); cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                cell.Style.Font.Bold = true;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
                cell.Style.Border.TopBorder = XLBorderStyleValues.Thin;
                cell.Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");
            }
            ws.Row(fila).Height = 14;
            fila += 2;
            return fila;
        }

        private int EscribirTotalGeneral(IXLWorksheet ws, decimal costoTotal,
                                          IReadOnlyList<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot,
                                          int fila)
        {
            var r = ws.Range(fila, 1, fila, cols.Count);
            r.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            r.Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i]; var cell = ws.Cell(fila, i + 1);
                if      (string.Equals(col.Identificador, "Descripcion", StringComparison.OrdinalIgnoreCase)) { cell.Value = "TOTAL COSTO DIRECTO:"; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (EsColumnaImporte(col))     { EscribirNumerico(cell, costoTotal, col, snapshot); cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (string.Equals(col.Identificador, "Porcentaje", StringComparison.OrdinalIgnoreCase))  { EscribirNumerico(cell, 1m, col, snapshot); cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                cell.Style.Font.Bold = true; cell.Style.Font.FontSize = 11;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#BBDEFB");
            }
            ws.Row(fila).Height = 16;
            return fila + 1;
        }

        /// <summary>
        /// Escribe una celda numérica aplicando la MISMA regla de formato que la ruta
        /// PDF (<see cref="ReportColumnGridFormat"/>). El porcentaje se escribe como
        /// texto con el signo '%' (magnitud, fracción × 100) para paridad byte a byte
        /// con el PDF; el resto se escribe como número con el formato del proyecto.
        /// </summary>
        private static void EscribirNumerico(IXLCell cell, decimal valor, ReportColumnDefinition col, ReportColumnSnapshot snapshot)
        {
            if (ReportColumnGridFormat.EsPorcentajeGrid(col))
            {
                cell.Value = ReportColumnGridFormat.FormatearPorcentajePdf(valor * 100m, snapshot) + "%";
                return;
            }

            cell.Value = valor;
            cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(col, snapshot);
        }

        private static bool EsColumnaImporte(ReportColumnDefinition col)
            => string.Equals(col.Identificador, "ImporteTotal", StringComparison.OrdinalIgnoreCase);

        private void EscribirPie(IXLWorksheet ws, PlantillaReporte p,
                                  Proyecto proyecto, int numCols, int fila)
        {
            var sep = ws.Range(fila, 1, fila, numCols);
            sep.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            sep.Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");
            fila++;
            int c1 = 1, c2 = numCols / 3 + 1, c3 = numCols * 2 / 3 + 1;
            EscribirZona(ws, fila, c1, c2-1, p.PiePaginaIzqTipo, p.PiePaginaIzqContenido,
                p.PiePaginaIzqFuente, p.PiePaginaIzqTamaño, p.PiePaginaIzqNegrita,
                p.PiePaginaIzqCursiva, p.PiePaginaIzqAlineacion, proyecto, p);
            EscribirZona(ws, fila, c2, c3-1, p.PiePaginaCenTipo, p.PiePaginaCenContenido,
                p.PiePaginaCenFuente, p.PiePaginaCenTamaño, p.PiePaginaCenNegrita,
                p.PiePaginaCenCursiva, p.PiePaginaCenAlineacion, proyecto, p);
            EscribirZona(ws, fila, c3, numCols, p.PiePaginaDerTipo, p.PiePaginaDerContenido,
                p.PiePaginaDerFuente, p.PiePaginaDerTamaño, p.PiePaginaDerNegrita,
                p.PiePaginaDerCursiva, p.PiePaginaDerAlineacion, proyecto, p);
            ws.Row(fila).Height = p.PiePaginaAltura * 0.75;
        }

        private void EscribirZona(IXLWorksheet ws, int fila, int colIni, int colFin,
                                   string tipo, string contenido, string fuente, float tamaño,
                                   bool negrita, bool cursiva, string alineacion,
                                   Proyecto proyecto, PlantillaReporte plantilla)
        {
            if (colIni > colFin) return;
            var rango = ws.Range(fila, colIni, fila, colFin);
            rango.Merge();
            if (tipo == "Imagen" && File.Exists(contenido))
                try { ws.AddPicture(contenido).MoveTo(ws.Cell(fila, colIni)).WithSize(120, 50); } catch { }
            else
                rango.FirstCell().Value = _svc.ResolverCampos(contenido, proyecto, plantilla);

            var est = rango.Style;
            est.Font.FontName = fuente; est.Font.FontSize = tamaño;
            est.Font.Bold = negrita; est.Font.Italic = cursiva;
            est.Alignment.WrapText = true;
            est.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            est.Alignment.Horizontal = alineacion switch
            {
                "Centro"  => XLAlignmentHorizontalValues.Center,
                "Derecha" => XLAlignmentHorizontalValues.Right,
                _         => XLAlignmentHorizontalValues.Left,
            };
        }


        private double CalcularAlturaFila(IReadOnlyList<ReportColumnDefinition> cols, int fila, IXLWorksheet ws, double alturaBase)
        {
            double altura = alturaBase;
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                if (!col.Wrap) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                var cont = col.EstiloContenido;
                using var font = new System.Drawing.Font(
                    string.IsNullOrWhiteSpace(cont.Fuente) ? "Segoe UI" : cont.Fuente,
                    Math.Max(8f, cont.Tamano > 0 ? cont.Tamano : 9f),
                    cont.Negrita ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular);

                int anchoPx = Math.Max(24, (int)Math.Round(col.Ancho - 8d));
                var proposed = new System.Drawing.Size(anchoPx, int.MaxValue);
                var flags = System.Windows.Forms.TextFormatFlags.WordBreak | System.Windows.Forms.TextFormatFlags.TextBoxControl;
                var measured = System.Windows.Forms.TextRenderer.MeasureText(valor, font, proposed, flags);
                double alturaPts = Math.Max(alturaBase, PixelsToPoints(measured.Height + 6));
                if (alturaPts > altura) altura = alturaPts;
            }
            return altura;
        }

        private static double PixelsToPoints(int pixels) => pixels * 72.0 / 96.0;

        private XLAlignmentHorizontalValues AlineacionXL(ReportTextAlignment a) => a switch
        {
            ReportTextAlignment.Centro      => XLAlignmentHorizontalValues.Center,
            ReportTextAlignment.Derecha     => XLAlignmentHorizontalValues.Right,
            ReportTextAlignment.Justificado => XLAlignmentHorizontalValues.Left,
            _                               => XLAlignmentHorizontalValues.Left,
        };

        private static XLAlignmentVerticalValues ConvertirAlineacionVertical(ReportVerticalAlignment v) => v switch
        {
            ReportVerticalAlignment.Superior => XLAlignmentVerticalValues.Top,
            ReportVerticalAlignment.Inferior => XLAlignmentVerticalValues.Bottom,
            _                                => XLAlignmentVerticalValues.Center,
        };

        private string Sanitizar(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre[..40] : nombre;
        }
    }
}
