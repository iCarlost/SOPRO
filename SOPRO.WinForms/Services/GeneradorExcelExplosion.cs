using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte de Explosión de Insumos en formato .xlsx.
    /// Recibe los diccionarios DatosInsumo ya calculados por FormExplosionInsumos
    /// (método OPUS PLANET) — el Excel coincide exactamente con lo que muestra el formulario.
    /// </summary>
    public class GeneradorExcelExplosion
    {
        private readonly ReporteService _svc;

        public GeneradorExcelExplosion(ReporteService svc) => _svc = svc;

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
            if (string.IsNullOrEmpty(rutaDestino))
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"ExplosionInsumos_{Sanitizar(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            var cols    = columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
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
                fila = EscribirSeccion(ws, "MATERIALES",   materiales,   cols, costoDirectoTotal, fila, proyecto);
            if (filtro == "Todos" || filtro == "Mano de Obra")
                fila = EscribirSeccion(ws, "MANO DE OBRA", manoObra,     cols, costoDirectoTotal, fila, proyecto);
            if (filtro == "Todos" || filtro == "Herramientas")
                fila = EscribirSeccion(ws, "HERRAMIENTAS", herramientas, cols, costoDirectoTotal, fila, proyecto);
            if (filtro == "Todos" || filtro == "Maquinaria")
                fila = EscribirSeccion(ws, "MAQUINARIA",   maquinaria,   cols, costoDirectoTotal, fila, proyecto);
            if (filtro == "Todos")
                fila = EscribirTotalGeneral(ws, costoDirectoTotal, cols, fila, proyecto);

            fila += 2;
            EscribirPie(ws, plantilla, proyecto, numCols, fila);

            for (int i = 0; i < cols.Count; i++)
                ws.Column(i + 1).Width = Math.Max(cols[i].AnchoColumna / 7.0, 4);

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

        private int EscribirTitulosColumnas(IXLWorksheet ws, List<ColumnaExplosion> cols, int fila)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i]; var cell = ws.Cell(fila, i + 1);
                cell.Value = col.Nombre;
                var est = cell.Style;
                est.Font.Bold = true;
                est.Font.FontName   = col.NombreFuente ?? "Segoe UI";
                est.Font.FontSize   = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
                est.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                    !string.IsNullOrEmpty(col.ColorFondo) && col.ColorFondo != "#FFFFFF" ? col.ColorFondo : "#33334C");
                est.Font.FontColor = ExcelColorHelper.SafeFromHtml(
                    !string.IsNullOrEmpty(col.ColorFuente) && col.ColorFuente != "#000000" ? col.ColorFuente : "#FFFFFF");
                est.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                est.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                est.Alignment.WrapText = col.WrapTexto;
                est.Border.BottomBorder = XLBorderStyleValues.Medium;
                est.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");
            }
            ws.Row(fila).Height = 18;
            return fila + 1;
        }

        private int EscribirSeccion(IXLWorksheet ws, string titulo,
            Dictionary<int, DatosInsumo> dic,
            List<ColumnaExplosion> cols, decimal costoTotal, int fila,
            Proyecto proyecto)
        {
            if (dic.Count == 0) return fila;

            // Formatos de número según configuración del proyecto
            string fmtCant = DecimalesToFormat(proyecto.DecimalesCantidad, esPrecio: false);
            string fmtImp  = DecimalesToFormat(proyecto.DecimalesImporte,  esPrecio: true);

            var rEnc = ws.Range(fila, 1, fila, cols.Count);
            rEnc.Merge(); rEnc.FirstCell().Value = titulo;
            rEnc.Style.Font.Bold = true; rEnc.Style.Font.FontSize = 10;
            rEnc.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
            rEnc.Style.Font.FontColor = XLColor.White;
            rEnc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Row(fila).Height = 16; fila++;

            decimal subtotal = 0;
            foreach (var kvp in dic.OrderBy(x => x.Value.Clave))
            {
                var ins     = kvp.Value;
                decimal imp = ins.Cantidad;   // Cantidad = importe acumulado (OPUS PLANET)
                decimal pct = costoTotal > 0 ? imp / costoTotal : 0;
                subtotal   += imp;

                for (int i = 0; i < cols.Count; i++)
                {
                    var col  = cols[i];
                    var cell = ws.Cell(fila, i + 1);
                    switch (col.NombreInterno)
                    {
                        case "Clave":       cell.Value = ins.Clave;       break;
                        case "Descripcion": cell.Value = ins.Descripcion; break;
                        case "Unidad":      cell.Value = ins.Unidad;      break;
                        case "Cantidad":
                            if (ins.EsPorcentual) cell.Value = "—";
                            else { cell.Value = ins.CantidadFisica; cell.Style.NumberFormat.Format = fmtCant; }
                            break;
                        case "PrecioUnitario":
                            if (ins.EsPorcentual && ins.PrecioUnitario == 0m) cell.Value = "—";
                            else { cell.Value = ins.PrecioUnitario; cell.Style.NumberFormat.Format = fmtImp; }
                            break;
                        case "Importe":
                            cell.Value = imp; cell.Style.NumberFormat.Format = fmtImp; break;
                        case "Porcentaje":
                            cell.Value = pct; cell.Style.NumberFormat.Format = "0.00%"; break;
                        default: cell.Value = ""; break;
                    }
                    var est = cell.Style;
                    est.Font.FontName   = col.NombreFuente ?? "Segoe UI";
                    est.Font.FontSize   = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
                    est.Font.Bold       = col.Negrita;
                    est.Font.Italic     = col.Cursiva;
                    est.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                        !string.IsNullOrEmpty(col.ColorFondo) ? col.ColorFondo : "#FFFFFF");
                    est.Font.FontColor = ExcelColorHelper.SafeFromHtml(
                        !string.IsNullOrEmpty(col.ColorFuente) ? col.ColorFuente : "#000000");
                    est.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                    est.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    est.Alignment.WrapText = col.WrapTexto;
                    est.Border.BottomBorder = XLBorderStyleValues.Hair;
                    est.Border.BottomBorderColor = XLColor.Gray;
                }
                ws.Row(fila).Height = CalcularAlturaFila(cols, fila, ws, 13);
                fila++;
            }

            // Subtotal
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i]; var cell = ws.Cell(fila, i + 1);
                if      (col.NombreInterno == "Descripcion") { cell.Value = $"SUBTOTAL {titulo}:"; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (col.NombreInterno == "Importe")     { cell.Value = subtotal; cell.Style.NumberFormat.Format = fmtImp; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (col.NombreInterno == "Porcentaje")  { cell.Value = costoTotal > 0 ? subtotal / costoTotal : 0; cell.Style.NumberFormat.Format = "0.00%"; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
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
                                          List<ColumnaExplosion> cols, int fila,
                                          Proyecto proyecto)
        {
            string fmtImp = DecimalesToFormat(proyecto.DecimalesImporte, esPrecio: true);
            var r = ws.Range(fila, 1, fila, cols.Count);
            r.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            r.Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i]; var cell = ws.Cell(fila, i + 1);
                if      (col.NombreInterno == "Descripcion") { cell.Value = "TOTAL COSTO DIRECTO:"; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (col.NombreInterno == "Importe")     { cell.Value = costoTotal; cell.Style.NumberFormat.Format = fmtImp; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                else if (col.NombreInterno == "Porcentaje")  { cell.Value = 1m; cell.Style.NumberFormat.Format = "0.00%"; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right; }
                cell.Style.Font.Bold = true; cell.Style.Font.FontSize = 11;
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#BBDEFB");
            }
            ws.Row(fila).Height = 16;
            return fila + 1;
        }

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


        private double CalcularAlturaFila(List<ColumnaExplosion> cols, int fila, IXLWorksheet ws, double alturaBase)
        {
            double altura = alturaBase;
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                if (!col.WrapTexto) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                using var font = new System.Drawing.Font(
                    string.IsNullOrWhiteSpace(col.NombreFuente) ? "Segoe UI" : col.NombreFuente,
                    Math.Max(8f, col.TamanoFuente > 0 ? col.TamanoFuente : 9f),
                    col.Negrita ? System.Drawing.FontStyle.Bold : System.Drawing.FontStyle.Regular);

                int anchoPx = Math.Max(24, (int)Math.Round(cols[i].AnchoColumna - 8d));
                var proposed = new System.Drawing.Size(anchoPx, int.MaxValue);
                var flags = System.Windows.Forms.TextFormatFlags.WordBreak | System.Windows.Forms.TextFormatFlags.TextBoxControl;
                var measured = System.Windows.Forms.TextRenderer.MeasureText(valor, font, proposed, flags);
                double alturaPts = Math.Max(alturaBase, PixelsToPoints(measured.Height + 6));
                if (alturaPts > altura) altura = alturaPts;
            }
            return altura;
        }

        private static double PixelsToPoints(int pixels) => pixels * 72.0 / 96.0;

                private XLAlignmentHorizontalValues AlineacionXL(AlineacionColumna a) => a switch
        {
            AlineacionColumna.Centro      => XLAlignmentHorizontalValues.Center,
            AlineacionColumna.Derecha     => XLAlignmentHorizontalValues.Right,
            AlineacionColumna.Justificado => XLAlignmentHorizontalValues.Left,
            _                             => XLAlignmentHorizontalValues.Left,
        };

        private string Sanitizar(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre[..40] : nombre;
        }

        private static string DecimalesToFormat(int decimales, bool esPrecio)
        {
            string ceros = decimales > 0 ? "." + new string('0', decimales) : "";
            return esPrecio ? $"$#,##0{ceros}" : $"#,##0{ceros}";
        }
    }
}
