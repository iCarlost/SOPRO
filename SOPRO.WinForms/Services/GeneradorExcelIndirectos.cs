using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using ClosedXML.Excel;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte de Cálculo de Indirectos en formato .xlsx. Consume el
    /// MISMO <see cref="ReportColumnSnapshot"/> neutral que la ruta PDF
    /// (visibilidad, orden, encabezado, ancho, alineación, wrap, estilo y formato
    /// numérico) y resuelve los valores del dominio con
    /// <see cref="IndirectosExportResolver"/>. Incluye encabezado/pie de
    /// PlantillaReporte, dos secciones (Oficina Central y Campo) y resumen final
    /// con porcentajes; preserva la jerarquía grupo/concepto y los subtotales.
    ///
    /// Los importes usan el símbolo '$' + decimales de importe del proyecto y los
    /// porcentajes los decimales de porcentaje, vía <see cref="ReportColumnGridFormat"/>,
    /// corrigiendo la divergencia histórica entre PDF y Excel.
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
            ReportColumnSnapshot snapshot,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));

            if (string.IsNullOrEmpty(rutaDestino))
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"Indirectos_{Sanitizar(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            var cols = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (cols.Count == 0)
                cols = IndirectosExportResolver.DefaultColumns().Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            int N = cols.Count;

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Indirectos");

            int fila = 1;

            // ── ENCABEZADO ────────────────────────────────────────────────────
            fila = EscribirEncabezado(ws, plantilla, proyecto, N, fila);

            // ── TÍTULO ────────────────────────────────────────────────────────
            fila = EscribirTitulo(ws, proyecto, N, fila, tituloCfg);

            // ── DATOS GENERALES ───────────────────────────────────────────────
            fila = EscribirDatosGenerales(ws, config, proyecto, N, fila, snapshot);

            // ── ENCABEZADOS DE COLUMNAS ───────────────────────────────────────
            fila = EscribirTitulosColumnas(ws, cols, fila);
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, fila - 1);
            ws.SheetView.FreezeRows(fila - 1);

            // ── SECCIÓN OFICINA CENTRAL ───────────────────────────────────────
            fila = EscribirSeccion(ws, "OFICINA CENTRAL", gruposOC, cols, false, fila, snapshot);

            // Resumen OC
            fila = EscribirResumenSeccion(ws,
                "Subtotal Oficina Central Anual",
                config.TotalOficinaCentralAnual,
                config.PorcentajeOficinaCentral,
                "% sobre Volumen Anual",
                N, fila, snapshot);

            fila++; // espacio

            // ── SECCIÓN CAMPO ─────────────────────────────────────────────────
            fila = EscribirSeccion(ws, "GASTOS DE CAMPO", gruposCampo, cols, true, fila, snapshot);

            // Resumen Campo
            fila = EscribirResumenSeccion(ws,
                "Subtotal Campo",
                config.TotalCampo,
                config.PorcentajeCampo,
                "% sobre Costo Directo",
                N, fila, snapshot);

            fila++; // espacio

            // ── RESUMEN FINAL ─────────────────────────────────────────────────
            fila = EscribirResumenFinal(ws, config, N, fila, snapshot);

            // ── PIE ───────────────────────────────────────────────────────────
            fila += 2;
            EscribirPie(ws, plantilla, proyecto, N, fila);

            // ── ANCHOS ────────────────────────────────────────────────────────
            for (int i = 0; i < cols.Count; i++)
                ws.Column(i + 1).Width = ReportColumnWidthConverter.PxToExcelWidth(cols[i].Ancho);

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
                                            Proyecto proyecto, int N, int fila, ReportColumnSnapshot snapshot)
        {
            fila++;
            int mid = N / 2;

            void FilaValor(string etiqueta, Action<IXLRange> escribirValor)
            {
                var rE = ws.Range(fila, 1, fila, mid); rE.Merge();
                rE.FirstCell().Value = etiqueta;
                rE.Style.Font.Bold = true;
                rE.Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");

                var rV = ws.Range(fila, mid + 1, fila, N); rV.Merge();
                rV.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                escribirValor(rV);
                ws.Row(fila).Height = 14;
            }

            FilaValor("Costo Directo de Obra:", rV =>
            {
                rV.FirstCell().Value = config.CostoDirectoObra;
                rV.Style.NumberFormat.Format = ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);
            });
            fila++;
            FilaValor("Volumen Anual de Obra:", rV =>
            {
                rV.FirstCell().Value = config.VolumenAnualObra;
                rV.Style.NumberFormat.Format = ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);
            });
            fila++;
            int durMeses = proyecto.PlazoEjecucion > 0 ? (int)Math.Ceiling(proyecto.PlazoEjecucion / 30.0) : 1;
            FilaValor("Duración de la Obra:", rV => rV.FirstCell().Value = $"{durMeses} meses ({proyecto.PlazoEjecucion} días)");
            fila++;
            fila++;
            return fila;
        }

        private int EscribirTitulosColumnas(IXLWorksheet ws, IReadOnlyList<ReportColumnDefinition> cols, int fila)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var enc = col.EstiloEncabezado;
                var cell = ws.Cell(fila, i + 1);
                cell.Value = col.Encabezado ?? string.Empty;
                cell.Style.Font.Bold     = enc.Negrita;
                cell.Style.Font.Italic   = enc.Cursiva;
                cell.Style.Font.FontName = enc.Fuente ?? "Segoe UI";
                cell.Style.Font.FontSize = enc.Tamano > 0 ? enc.Tamano : 9;
                cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(enc.ColorFondo ?? "#4A4A6A");
                cell.Style.Font.FontColor       = ExcelColorHelper.SafeFromHtml(enc.ColorFuente, "#FFFFFF");
                cell.Style.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                cell.Style.Border.BottomBorder  = XLBorderStyleValues.Medium;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");
            }
            ws.Row(fila).Height = 18;
            return fila + 1;
        }

        // ── SECCIÓN (OFICINA CENTRAL o CAMPO) ────────────────────────────────
        private int EscribirSeccion(IXLWorksheet ws, string titulo,
            List<GrupoIndirecto> grupos, IReadOnlyList<ReportColumnDefinition> cols,
            bool mostrarDuracion, int fila, ReportColumnSnapshot snapshot)
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
                    var cont = col.EstiloContenido;
                    var cell = ws.Cell(fila, i + 1);

                    if (IndirectosExportResolver.TryGetValorGrupo(grupo, col, out var valorGrupo))
                    {
                        cell.Value = valorGrupo;
                        cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(col, snapshot);
                    }
                    else
                    {
                        cell.Value = IndirectosExportResolver.ResolveTextoGrupo(grupo, col);
                    }

                    AplicarEstiloCelda(cell, cont);
                    cell.Style.Font.Bold            = true;
                    cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#E8E8E8");
                    cell.Style.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                    cell.Style.Border.BottomBorder      = XLBorderStyleValues.Thin;
                    cell.Style.Border.BottomBorderColor = XLColor.Gray;
                }
                ws.Row(fila).Height = 15; fila++;

                // Filas de conceptos
                foreach (var concepto in grupo.Conceptos.Where(c => c.Activo).OrderBy(c => c.Orden))
                {
                    for (int i = 0; i < cols.Count; i++)
                    {
                        var col  = cols[i];
                        var cont = col.EstiloContenido;
                        var cell = ws.Cell(fila, i + 1);

                        if (IndirectosExportResolver.TryGetValorConcepto(concepto, col, out var valorConcepto))
                        {
                            cell.Value = valorConcepto;
                            cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(col, snapshot);
                        }
                        else
                        {
                            cell.Value = IndirectosExportResolver.ResolveTextoConcepto(concepto, col, mostrarDuracion);
                        }

                        AplicarEstiloCelda(cell, cont);
                        cell.Style.Alignment.Horizontal = AlineacionXL(col.Alineacion);
                        cell.Style.Border.BottomBorder      = XLBorderStyleValues.Hair;
                        cell.Style.Border.BottomBorderColor = XLColor.LightGray;
                    }
                    ws.Row(fila).Height = 13; fila++;
                }
            }
            return fila;
        }

        private static void AplicarEstiloCelda(IXLCell cell, ReportTextStyle cont)
        {
            cell.Style.Font.Bold   = cont.Negrita;
            cell.Style.Font.Italic = cont.Cursiva;
            cell.Style.Font.FontName = cont.Fuente ?? "Segoe UI";
            cell.Style.Font.FontSize = cont.Tamano > 0 ? cont.Tamano : 9;
            cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(cont.ColorFondo ?? "#FFFFFF");
            cell.Style.Font.FontColor       = ExcelColorHelper.SafeFromHtml(cont.ColorFuente, "#000000");
        }

        private int EscribirResumenSeccion(IXLWorksheet ws, string etiquetaTotal,
            decimal total, decimal porcentaje, string labelPorc, int N, int fila, ReportColumnSnapshot snapshot)
        {
            // Línea separadora
            ws.Range(fila, 1, fila, N).Style.Border.TopBorder      = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, N).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

            var rEtiq = ws.Range(fila, 1, fila, N - 1); rEtiq.Merge();
            rEtiq.FirstCell().Value = etiquetaTotal;
            rEtiq.Style.Font.Bold = true;
            rEtiq.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            rEtiq.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(fila, N).Value = total;
            ws.Cell(fila, N).Style.NumberFormat.Format = ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);
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
            ws.Cell(fila, N).Style.NumberFormat.Format = ReportColumnGridFormat.FormatoPorcentajeExcel(snapshot.DecimalesPorcentaje);
            ws.Cell(fila, N).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
            ws.Cell(fila, N).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Row(fila).Height = 13;
            return fila + 1;
        }

        private int EscribirResumenFinal(IXLWorksheet ws, ConfiguracionIndirectos config,
                                          int N, int fila, ReportColumnSnapshot snapshot)
        {
            // Título resumen
            var rTit = ws.Range(fila, 1, fila, N); rTit.Merge();
            rTit.FirstCell().Value = "RESUMEN DE INDIRECTOS";
            rTit.Style.Font.Bold = true; rTit.Style.Font.FontSize = 11;
            rTit.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
            rTit.Style.Font.FontColor = XLColor.White;
            rTit.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(fila).Height = 18; fila++;

            void FilaResumen(string label, decimal porcentaje, string fondo, bool bold = false)
            {
                var rL = ws.Range(fila, 1, fila, N - 1); rL.Merge();
                rL.FirstCell().Value = label;
                rL.Style.Font.Bold = bold;
                rL.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                rL.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Cell(fila, N).Value = porcentaje / 100m;
                ws.Cell(fila, N).Style.NumberFormat.Format = ReportColumnGridFormat.FormatoPorcentajeExcel(snapshot.DecimalesPorcentaje);
                ws.Cell(fila, N).Style.Font.Bold = bold;
                ws.Cell(fila, N).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                ws.Cell(fila, N).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Row(fila).Height = 14; fila++;
            }

            FilaResumen("% Oficina Central:", config.PorcentajeOficinaCentral, "#F5F5F5");
            FilaResumen("% Gastos de Campo:", config.PorcentajeCampo, "#F5F5F5");

            // Separador antes del total
            ws.Range(fila, 1, fila, N).Style.Border.TopBorder      = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, N).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

            FilaResumen("% TOTAL INDIRECTOS:", config.PorcentajeTotal, "#BBDEFB", bold: true);

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

        private XLAlignmentHorizontalValues AlineacionXL(ReportTextAlignment a) => a switch
        {
            ReportTextAlignment.Centro      => XLAlignmentHorizontalValues.Center,
            ReportTextAlignment.Derecha     => XLAlignmentHorizontalValues.Right,
            ReportTextAlignment.Justificado => XLAlignmentHorizontalValues.Left,
            _                               => XLAlignmentHorizontalValues.Left,
        };

        private string Sanitizar(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre[..40] : nombre;
        }
    }
}
