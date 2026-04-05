using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte de Cálculo de Indirectos en formato .xlsx.
    /// Incluye encabezado/pie de PlantillaReporte, dos secciones
    /// (Oficina Central y Campo) y resumen final con porcentajes.
    /// </summary>
    public class GeneradorExcelIndirectos
    {
        private readonly ReporteService _svc;

        public GeneradorExcelIndirectos(ReporteService svc) => _svc = svc;

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
            if (string.IsNullOrEmpty(rutaDestino))
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"Indirectos_{Sanitizar(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            var cols = columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            int N = cols.Count;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Indirectos");

            int fila = 1;

            // ── ENCABEZADO ────────────────────────────────────────────────────
            fila = EscribirEncabezado(ws, plantilla, proyecto, N, fila);

            // ── TÍTULO ────────────────────────────────────────────────────────
            fila = EscribirTitulo(ws, proyecto, N, fila, tituloCfg);

            // ── DATOS GENERALES ───────────────────────────────────────────────
            fila = EscribirDatosGenerales(ws, config, proyecto, N, fila);

            // ── ENCABEZADOS DE COLUMNAS ───────────────────────────────────────
            int filaCols = fila;
            fila = EscribirTitulosColumnas(ws, cols, fila);
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, fila - 1);
            ws.SheetView.FreezeRows(fila - 1);

            // ── SECCIÓN OFICINA CENTRAL ───────────────────────────────────────
            fila = EscribirSeccion(ws, "OFICINA CENTRAL", gruposOC, cols, false, fila);

            // Resumen OC
            fila = EscribirResumenSeccion(ws,
                "Subtotal Oficina Central Anual",
                config.TotalOficinaCentralAnual,
                config.PorcentajeOficinaCentral,
                "% sobre Volumen Anual",
                N, fila);

            fila++; // espacio

            // ── SECCIÓN CAMPO ─────────────────────────────────────────────────
            fila = EscribirSeccion(ws, "GASTOS DE CAMPO", gruposCampo, cols, true, fila);

            // Resumen Campo
            fila = EscribirResumenSeccion(ws,
                "Subtotal Campo",
                config.TotalCampo,
                config.PorcentajeCampo,
                "% sobre Costo Directo",
                N, fila);

            fila++; // espacio

            // ── RESUMEN FINAL ─────────────────────────────────────────────────
            fila = EscribirResumenFinal(ws, config, N, fila);

            // ── PIE ───────────────────────────────────────────────────────────
            fila += 2;
            EscribirPie(ws, plantilla, proyecto, N, fila);

            // ── ANCHOS ────────────────────────────────────────────────────────
            for (int i = 0; i < cols.Count; i++)
                ws.Column(i + 1).Width = Math.Max(cols[i].AnchoColumna / 7.0, 4);

            // ── IMPRESIÓN ─────────────────────────────────────────────────────
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Left    = 0.5;
            ws.PageSetup.Margins.Right   = 0.5;
            ws.PageSetup.Margins.Top     = 0.75;
            ws.PageSetup.Margins.Bottom  = 0.75;

            wb.SaveAs(rutaDestino);
            return rutaDestino;
        }

        // ── ENCABEZADO ────────────────────────────────────────────────────────
        private int EscribirEncabezado(IXLWorksheet ws, PlantillaReporte p,
                                        Proyecto proyecto, int N, int fila)
        {
            int c1 = 1, c2 = N / 3 + 1, c3 = N * 2 / 3 + 1;

            EscribirZona(ws, fila, c1, c2 - 1, p.EncabezadoIzqTipo, p.EncabezadoIzqContenido,
                p.EncabezadoIzqFuente, p.EncabezadoIzqTamaño, p.EncabezadoIzqNegrita,
                p.EncabezadoIzqCursiva, p.EncabezadoIzqAlineacion, proyecto, p);
            EscribirZona(ws, fila, c2, c3 - 1, p.EncabezadoCenTipo, p.EncabezadoCenContenido,
                p.EncabezadoCenFuente, p.EncabezadoCenTamaño, p.EncabezadoCenNegrita,
                p.EncabezadoCenCursiva, p.EncabezadoCenAlineacion, proyecto, p);
            EscribirZona(ws, fila, c3, N, p.EncabezadoDerTipo, p.EncabezadoDerContenido,
                p.EncabezadoDerFuente, p.EncabezadoDerTamaño, p.EncabezadoDerNegrita,
                p.EncabezadoDerCursiva, p.EncabezadoDerAlineacion, proyecto, p);

            ws.Row(fila).Height = p.EncabezadoAltura * 0.75;
            fila++;
            ws.Range(fila, 1, fila, N).Style.Border.TopBorder      = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, N).Style.Border.TopBorderColor  = XLColor.FromHtml("#1565C0");
            return fila + 1;
        }

        private int EscribirTitulo(IXLWorksheet ws, Proyecto proyecto, int N, int fila, ConfiguracionTituloReporte? tituloCfg)
        {
            var r = ws.Range(fila, 1, fila, N);
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(r, tituloCfg, "ANÁLISIS DE COSTOS INDIRECTOS", "#1565C0");
            ws.Row(fila).Height = 20; fila++;

            var r2 = ws.Range(fila, 1, fila, N); r2.Merge();
            r2.FirstCell().Value = proyecto.Nombre;
            r2.Style.Font.FontSize = 10;
            r2.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            r2.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            ws.Row(fila).Height = 15;
            return fila + 1;
        }

        private int EscribirDatosGenerales(IXLWorksheet ws, ConfiguracionIndirectos config,
                                            Proyecto proyecto, int N, int fila)
        {
            fila++;
            void FilaDato(string etiqueta, string valor)
            {
                int mid = N / 2;
                var rE = ws.Range(fila, 1, fila, mid); rE.Merge();
                rE.FirstCell().Value = etiqueta;
                rE.Style.Font.Bold = true;
                rE.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");

                var rV = ws.Range(fila, mid + 1, fila, N); rV.Merge();
                rV.FirstCell().Value = valor;
                rV.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Row(fila).Height = 14;
            }

            FilaDato("Costo Directo de Obra:", $"${config.CostoDirectoObra:N2}"); fila++;
            FilaDato("Volumen Anual de Obra:", $"${config.VolumenAnualObra:N2}"); fila++;
            int durMeses = proyecto.PlazoEjecucion > 0 ? (int)Math.Ceiling(proyecto.PlazoEjecucion / 30.0) : 1;
            FilaDato("Duración de la Obra:", $"{durMeses} meses ({proyecto.PlazoEjecucion} días)"); fila++;
            fila++;
            return fila;
        }

        private int EscribirTitulosColumnas(IXLWorksheet ws, List<ColumnaIndirectos> cols, int fila)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                var col  = cols[i];
                var cell = ws.Cell(fila, i + 1);
                cell.Value = col.Nombre;
                cell.Style.Font.Bold            = true;
                cell.Style.Font.FontName        = col.NombreFuente ?? "Segoe UI";
                cell.Style.Font.FontSize        = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
                cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(
                    !string.IsNullOrEmpty(col.ColorFondo) && col.ColorFondo != "#FFFFFF"
                    ? col.ColorFondo : "#33334C");
                cell.Style.Font.FontColor       = ExcelColorHelper.SafeFromHtml(
                    !string.IsNullOrEmpty(col.ColorFuente) && col.ColorFuente != "#000000"
                    ? col.ColorFuente : "#FFFFFF");
                cell.Style.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                cell.Style.Border.BottomBorder  = XLBorderStyleValues.Medium;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");
            }
            ws.Row(fila).Height = 18;
            return fila + 1;
        }

        // ── SECCIÓN (OFICINA CENTRAL o CAMPO) ────────────────────────────────
        private int EscribirSeccion(IXLWorksheet ws, string titulo,
            List<GrupoIndirecto> grupos, List<ColumnaIndirectos> cols,
            bool mostrarDuracion, int fila)
        {
            // Encabezado de sección
            var rEnc = ws.Range(fila, 1, fila, cols.Count); rEnc.Merge();
            rEnc.FirstCell().Value = titulo;
            rEnc.Style.Font.Bold = true; rEnc.Style.Font.FontSize = 10;
            rEnc.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
            rEnc.Style.Font.FontColor = XLColor.White;
            ws.Row(fila).Height = 16; fila++;

            foreach (var grupo in grupos)
            {
                // Fila de grupo
                for (int i = 0; i < cols.Count; i++)
                {
                    var col  = cols[i];
                    var cell = ws.Cell(fila, i + 1);
                    if      (col.NombreInterno == "Grupo")        cell.Value = grupo.Nombre;
                    else if (col.NombreInterno == "ImporteTotal") { cell.Value = grupo.Total; cell.Style.NumberFormat.Format = "$#,##0.00"; }
                    cell.Style.Font.Bold            = true;
                    cell.Style.Font.FontName        = col.NombreFuente ?? "Segoe UI";
                    cell.Style.Font.FontSize        = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8E8E8");
                    cell.Style.Alignment.Horizontal = col.NombreInterno == "Grupo"
                        ? XLAlignmentHorizontalValues.Left : AlineacionXL(col.Alineacion);
                    cell.Style.Border.BottomBorder      = XLBorderStyleValues.Thin;
                    cell.Style.Border.BottomBorderColor = XLColor.Gray;
                }
                ws.Row(fila).Height = 15; fila++;

                // Filas de conceptos
                foreach (var concepto in grupo.Conceptos.OrderBy(c => c.Orden))
                {
                    for (int i = 0; i < cols.Count; i++)
                    {
                        var col  = cols[i];
                        var cell = ws.Cell(fila, i + 1);
                        switch (col.NombreInterno)
                        {
                            case "Grupo":
                                cell.Value = "    " + concepto.Concepto;
                                break;
                            case "ImporteMensual":
                                cell.Value = concepto.ImporteMensual;
                                cell.Style.NumberFormat.Format = "$#,##0.00";
                                break;
                            case "Duracion":
                                if (mostrarDuracion) { cell.Value = concepto.DuracionMeses; cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; }
                                break;
                            case "ImporteTotal":
                                cell.Value = concepto.ImporteTotal;
                                cell.Style.NumberFormat.Format = "$#,##0.00";
                                break;
                        }
                        cell.Style.Font.FontName        = col.NombreFuente ?? "Segoe UI";
                        cell.Style.Font.FontSize        = col.TamanoFuente > 0 ? col.TamanoFuente : 9;
                        cell.Style.Font.Bold            = col.Negrita;
                        cell.Style.Font.Italic          = col.Cursiva;
                        cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(!string.IsNullOrEmpty(col.ColorFondo) ? col.ColorFondo : "#FFFFFF");
                        cell.Style.Font.FontColor       = ExcelColorHelper.SafeFromHtml(!string.IsNullOrEmpty(col.ColorFuente) ? col.ColorFuente : "#000000");
                        if (col.NombreInterno != "Grupo" && col.NombreInterno != "Duracion")
                            cell.Style.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                        cell.Style.Border.BottomBorder      = XLBorderStyleValues.Hair;
                        cell.Style.Border.BottomBorderColor = XLColor.LightGray;
                    }
                    ws.Row(fila).Height = 13; fila++;
                }
            }
            return fila;
        }

        private int EscribirResumenSeccion(IXLWorksheet ws, string etiquetaTotal,
            decimal total, decimal porcentaje, string labelPorc, int N, int fila)
        {
            // Línea separadora
            ws.Range(fila, 1, fila, N).Style.Border.TopBorder      = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, N).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

            int colImporte = 0, colPorcCol = 0;
            // Encontrar columnas por posición (ImporteTotal es siempre la última visible importante)
            // Usamos N directamente
            var rEtiq = ws.Range(fila, 1, fila, N - 1); rEtiq.Merge();
            rEtiq.FirstCell().Value = etiquetaTotal;
            rEtiq.Style.Font.Bold = true;
            rEtiq.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            rEtiq.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(fila, N).Value = total;
            ws.Cell(fila, N).Style.NumberFormat.Format = "$#,##0.00";
            ws.Cell(fila, N).Style.Font.Bold = true;
            ws.Cell(fila, N).Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            ws.Cell(fila, N).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Row(fila).Height = 15; fila++;

            // Porcentaje
            var rP = ws.Range(fila, 1, fila, N - 1); rP.Merge();
            rP.FirstCell().Value = labelPorc;
            rP.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
            rP.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(fila, N).Value = porcentaje / 100m;
            ws.Cell(fila, N).Style.NumberFormat.Format = "0.0000%";
            ws.Cell(fila, N).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
            ws.Cell(fila, N).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Row(fila).Height = 13;
            return fila + 1;
        }

        private int EscribirResumenFinal(IXLWorksheet ws, ConfiguracionIndirectos config,
                                          int N, int fila)
        {
            // Título resumen
            var rTit = ws.Range(fila, 1, fila, N); rTit.Merge();
            rTit.FirstCell().Value = "RESUMEN DE INDIRECTOS";
            rTit.Style.Font.Bold = true; rTit.Style.Font.FontSize = 11;
            rTit.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            rTit.Style.Font.FontColor = XLColor.White;
            rTit.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(fila).Height = 18; fila++;

            void FilaResumen(string label, string valor, string fondo, bool bold = false)
            {
                var rL = ws.Range(fila, 1, fila, N - 1); rL.Merge();
                rL.FirstCell().Value = label;
                rL.Style.Font.Bold = bold;
                rL.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                rL.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(fila, N).Value = valor;
                ws.Cell(fila, N).Style.Font.Bold = bold;
                ws.Cell(fila, N).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                ws.Cell(fila, N).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Row(fila).Height = 14; fila++;
            }

            FilaResumen("% Oficina Central:",
                $"{config.PorcentajeOficinaCentral:N4}%", "#F5F5F5");
            FilaResumen("% Gastos de Campo:",
                $"{config.PorcentajeCampo:N4}%", "#F5F5F5");

            // Separador antes del total
            ws.Range(fila, 1, fila, N).Style.Border.TopBorder      = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, N).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

            FilaResumen("% TOTAL INDIRECTOS:",
                $"{config.PorcentajeTotal:N4}%", "#BBDEFB", bold: true);

            return fila;
        }

        // ── PIE ───────────────────────────────────────────────────────────────
        private void EscribirPie(IXLWorksheet ws, PlantillaReporte p,
                                  Proyecto proyecto, int N, int fila)
        {
            ws.Range(fila, 1, fila, N).Style.Border.TopBorder      = XLBorderStyleValues.Thin;
            ws.Range(fila, 1, fila, N).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");
            fila++;

            int c1 = 1, c2 = N / 3 + 1, c3 = N * 2 / 3 + 1;
            EscribirZona(ws, fila, c1, c2 - 1, p.PiePaginaIzqTipo, p.PiePaginaIzqContenido,
                p.PiePaginaIzqFuente, p.PiePaginaIzqTamaño, p.PiePaginaIzqNegrita,
                p.PiePaginaIzqCursiva, p.PiePaginaIzqAlineacion, proyecto, p);
            EscribirZona(ws, fila, c2, c3 - 1, p.PiePaginaCenTipo, p.PiePaginaCenContenido,
                p.PiePaginaCenFuente, p.PiePaginaCenTamaño, p.PiePaginaCenNegrita,
                p.PiePaginaCenCursiva, p.PiePaginaCenAlineacion, proyecto, p);
            EscribirZona(ws, fila, c3, N, p.PiePaginaDerTipo, p.PiePaginaDerContenido,
                p.PiePaginaDerFuente, p.PiePaginaDerTamaño, p.PiePaginaDerNegrita,
                p.PiePaginaDerCursiva, p.PiePaginaDerAlineacion, proyecto, p);
            ws.Row(fila).Height = p.PiePaginaAltura * 0.75;
        }

        // ── ZONA ─────────────────────────────────────────────────────────────
        private void EscribirZona(IXLWorksheet ws, int fila, int colIni, int colFin,
                                   string tipo, string contenido, string fuente, float tamaño,
                                   bool negrita, bool cursiva, string alineacion,
                                   Proyecto proyecto, PlantillaReporte plantilla)
        {
            if (colIni > colFin) return;
            var rango = ws.Range(fila, colIni, fila, colFin);
            rango.Merge();
            if (tipo == "Imagen" && File.Exists(contenido))
            { try { ws.AddPicture(contenido).MoveTo(ws.Cell(fila, colIni)).WithSize(120, 50); } catch { } }
            else
                rango.FirstCell().Value = _svc.ResolverCampos(contenido, proyecto, plantilla);

            var est = rango.Style;
            est.Font.FontName  = fuente; est.Font.FontSize  = tamaño;
            est.Font.Bold      = negrita; est.Font.Italic   = cursiva;
            est.Alignment.WrapText = true;
            est.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            est.Alignment.Horizontal = alineacion switch
            {
                "Centro"  => XLAlignmentHorizontalValues.Center,
                "Derecha" => XLAlignmentHorizontalValues.Right,
                _         => XLAlignmentHorizontalValues.Left,
            };
        }

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
            foreach (var c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre[..40] : nombre;
        }
    }
}
