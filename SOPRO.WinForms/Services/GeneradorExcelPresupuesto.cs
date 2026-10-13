using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Services;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Reporting;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Reporting.Columns;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte de Presupuesto en formato .xlsx usando ClosedXML.
    /// Aplica la PlantillaReporte del proyecto (encabezado/pie) y el snapshot
    /// neutral de columnas (<see cref="ReportColumnSnapshot"/>: anchos, fuentes,
    /// alineaciones, colores). No depende del grid de la UI.
    /// </summary>
    public class GeneradorExcelPresupuesto
    {
        private readonly ReporteService _svc;

        public GeneradorExcelPresupuesto(ReporteService svc) => _svc = svc;

        /// <summary>
        /// Genera el .xlsx y devuelve la ruta del archivo generado.
        /// </summary>
        public string Generar(
            Proyecto proyecto,
            List<ConceptoPresupuesto> conceptos,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            string rutaDestino = null,
            decimal factorPU = 1m,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            // Defaults NEUTRALES por rol de fila (única fuente de verdad, compartida
            // con grid/PDF) combinados con la config persistida del usuario. El
            // override de celda del snapshot sigue teniendo la máxima precedencia.
            snapshot = snapshot with
            {
                EstilosFila = EstilosFilaPredeterminados.CombinarPresupuesto(snapshot.EstilosFila)
            };

            // Columnas visibles ordenadas (definición neutral persistida, no el grid).
            var cols = snapshot.Columnas
                .Where(c => c.Visible)
                .OrderBy(c => c.Orden)
                .ToList();

            // Paridad por defecto con el grid legacy: la columna sintética de
            // numeración "#" se antepone cuando el snapshot no la define.
            cols = ReportColumnDefaults.ConPrefijoNumeroPresupuesto(cols);

            if (string.IsNullOrEmpty(rutaDestino))
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"Presupuesto_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Presupuesto");

            int fila = 1;

            // ── ENCABEZADO ────────────────────────────────────────────────────
            fila = EscribirEncabezado(ws, plantilla, proyecto, cols.Count, fila);

            var titulo = ws.Range(fila, 1, fila, cols.Count);
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(titulo, tituloCfg, "PRESUPUESTO");
            ws.Row(fila).Height = 20;
            fila++;

            // ── TÍTULOS DE COLUMNAS ───────────────────────────────────────────
            fila = EscribirTitulosColumnas(ws, cols, snapshot.EstiloTabla, fila);
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, fila - 1);

            // ── DATOS ─────────────────────────────────────────────────────────
            int filaInicioDatos = fila;
            var conceptosOrdenados = OrdenarJerarquia(conceptos);
            int consecutivo = 0;
            foreach (var c in conceptosOrdenados)
            {
                if (!c.EsAgrupador) consecutivo++;
                fila = EscribirConcepto(ws, proyecto, c, cols, snapshot, fila, consecutivo, factorPU);
            }

            // ── TOTALES ───────────────────────────────────────────────────────
            fila = EscribirTotales(ws, proyecto, conceptos, cols, snapshot, fila);

            // ── PIE DE PÁGINA ─────────────────────────────────────────────────
            fila++;
            EscribirPie(ws, plantilla, proyecto, cols.Count, fila);

            // ── ANCHOS DE COLUMNA ─────────────────────────────────────────────
            AplicarAnchos(ws, cols);

            // ── CONFIGURACIÓN DE IMPRESIÓN ────────────────────────────────────
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);   // 1 página de ancho, alto libre
            ws.PageSetup.Margins.Left = 0.5;
            ws.PageSetup.Margins.Right = 0.5;
            ws.PageSetup.Margins.Top = 0.75;
            ws.PageSetup.Margins.Bottom = 0.75;
            ws.SheetView.FreezeRows(filaInicioDatos - 1); // Congelar encabezado

            wb.SaveAs(rutaDestino);
            return rutaDestino;
        }

        // ── ENCABEZADO ────────────────────────────────────────────────────────
        private int EscribirEncabezado(IXLWorksheet ws, PlantillaReporte p,
                                        Proyecto proyecto, int numCols, int fila)
        {
            int colTotal = numCols;
            int c1 = 1, c2 = colTotal / 3 + 1, c3 = colTotal * 2 / 3 + 1;

            // Zona izquierda
            EscribirZona(ws, fila, c1, c2 - 1,
                p.EncabezadoIzqTipo, p.EncabezadoIzqContenido,
                p.EncabezadoIzqFuente, p.EncabezadoIzqTamaño,
                p.EncabezadoIzqNegrita, p.EncabezadoIzqCursiva,
                p.EncabezadoIzqAlineacion, proyecto, p);

            // Zona central
            EscribirZona(ws, fila, c2, c3 - 1,
                p.EncabezadoCenTipo, p.EncabezadoCenContenido,
                p.EncabezadoCenFuente, p.EncabezadoCenTamaño,
                p.EncabezadoCenNegrita, p.EncabezadoCenCursiva,
                p.EncabezadoCenAlineacion, proyecto, p);

            // Zona derecha
            EscribirZona(ws, fila, c3, colTotal,
                p.EncabezadoDerTipo, p.EncabezadoDerContenido,
                p.EncabezadoDerFuente, p.EncabezadoDerTamaño,
                p.EncabezadoDerNegrita, p.EncabezadoDerCursiva,
                p.EncabezadoDerAlineacion, proyecto, p);

            // Altura de fila (pts → aprox 0.75 pts/unidad Excel)
            ws.Row(fila).Height = p.EncabezadoAltura * 0.75;

            // Línea separadora
            fila++;
            var rango = ws.Range(fila, 1, fila, colTotal);
            rango.Style.Border.TopBorder = XLBorderStyleValues.Medium;
            rango.Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

            return fila + 1;
        }

        private void EscribirZona(IXLWorksheet ws, int fila, int colIni, int colFin,
                                   string tipo, string contenido,
                                   string fuente, float tamaño, bool negrita, bool cursiva,
                                   string alineacion, Proyecto proyecto, PlantillaReporte plantilla)
        {
            if (colIni > colFin) return;

            var rango = ws.Range(fila, colIni, fila, colFin);
            rango.Merge();

            if (tipo == "Imagen" && File.Exists(contenido))
            {
                // Insertar imagen en la celda
                try
                {
                    var img = ws.AddPicture(contenido)
                        .MoveTo(ws.Cell(fila, colIni))
                        .WithSize(120, 50);
                }
                catch { /* Si falla la imagen, dejar vacío */ }
            }
            else
            {
                var texto = _svc.ResolverCampos(contenido, proyecto, plantilla);
                rango.FirstCell().Value = texto;
            }

            var estilo = rango.Style;
            estilo.Font.FontName = fuente;
            estilo.Font.FontSize = tamaño;
            estilo.Font.Bold = negrita;
            estilo.Font.Italic = cursiva;
            estilo.Alignment.WrapText = true;
            estilo.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            estilo.Alignment.Horizontal = alineacion switch
            {
                "Centro" => XLAlignmentHorizontalValues.Center,
                "Derecha" => XLAlignmentHorizontalValues.Right,
                _ => XLAlignmentHorizontalValues.Left,
            };
        }

        // ── TÍTULOS DE COLUMNAS ───────────────────────────────────────────────
        private int EscribirTitulosColumnas(IXLWorksheet ws,
                                             List<ReportColumnDefinition> cols,
                                             ReportTableStyle estiloTabla, int fila)
        {
            var encabezadoDefault = estiloTabla.EstiloEncabezado;

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var enc = col.EstiloEncabezado;
                var cell = ws.Cell(fila, i + 1);
                cell.Value = col.Encabezado;

                var estilo = cell.Style;
                estilo.Font.FontName = enc.Fuente;
                estilo.Font.FontSize = enc.Tamano;
                estilo.Font.Bold = enc.Negrita;
                estilo.Fill.BackgroundColor = ObtenerColorXL(enc.ColorFondo, encabezadoDefault.ColorFondo ?? "#1565C0");
                estilo.Font.FontColor = ObtenerColorXL(enc.ColorFuente, encabezadoDefault.ColorFuente);
                estilo.Font.Italic = enc.Cursiva;
                estilo.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                estilo.Border.BottomBorder = XLBorderStyleValues.Medium;
                estilo.Border.BottomBorderColor = ObtenerColorXL(enc.ColorFondo, encabezadoDefault.ColorFondo ?? "#1565C0");
            }

            ws.Row(fila).Height = 18;
            return fila + 1;
        }

        // ── CONCEPTO ─────────────────────────────────────────────────────────
        private int EscribirConcepto(IXLWorksheet ws, Proyecto proyecto, ConceptoPresupuesto c,
                                      List<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, int fila,
                                      int consecutivo, decimal factorPU = 1m)
        {
            // Snapshot sin overrides: línea base de la cascada tabla/columna para
            // aislar QUÉ propiedad aportó realmente un override (sin estilos, el
            // delta es vacío y el render replica el comportamiento legacy).
            var snapshotBase = snapshot with { EstilosCelda = null, EstilosFila = null };
            var claveFila = PresupuestoReportSnapshotBuilder.ClaveFila(c);
            var rolFila = PresupuestoReportSnapshotBuilder.RolFila(c);

            // ── SUBTOTAL DE AGRUPADOR ─────────────────────────────────────────
            if (c.Notas == PresupuestoReportSnapshotBuilder.MarcaSubtotalSintetico)
            {
                string indent = new string(' ', c.Nivel * 2);

                for (int i = 0; i < cols.Count; i++)
                {
                    var col = cols[i];
                    var cont = col.EstiloContenido;
                    var cell = ws.Cell(fila, i + 1);
                    var overrideEfectivo = OverrideEfectivo(snapshot, snapshotBase, claveFila, col.Identificador, rolFila);

                    if (col.Identificador == "Descripcion")
                        cell.Value = indent + $"Total {c.Descripcion}:";
                    else if (col.Identificador is "ImporteTotal" or "Importe" or "Subtotal" or "Total")
                    {
                        cell.Value = c.ImporteTotal;
                        cell.Style.NumberFormat.Format = FormatoNumericoExcel(overrideEfectivo, col, snapshot);
                    }
                    else
                        cell.Value = "";

                    // Adoptar exactamente el mismo estilo que la columna configurada
                    var estilo = cell.Style;
                    estilo.Font.FontName = cont.Fuente;
                    estilo.Font.FontSize = cont.Tamano;
                    estilo.Font.Bold = true;
                    estilo.Font.Italic = cont.Cursiva;
                    estilo.Font.FontColor = ObtenerColorXL(cont.ColorFuente, "#000000");
                    estilo.Alignment.Horizontal = ConvertirAlineacionXL(col.Alineacion);
                    estilo.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    estilo.Alignment.WrapText = col.Wrap;
                    if (col.Identificador == "Descripcion")
                        estilo.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    // Fondo del subtotal sintético: DEFAULT del rol Subtotal vía overlay.
                    AplicarOverlayExcel(estilo, overrideEfectivo);

                    estilo.Border.TopBorder = XLBorderStyleValues.Hair;
                    estilo.Border.TopBorderColor = XLColor.Gray;
                }

                ws.Row(fila).Height = CalcularAlturaFila(cols, fila, ws, 15, esSubtotal: true);
                return fila + 1;
            }

            bool esAgrupador = c.EsAgrupador;
            string indent2 = new string(' ', c.Nivel * 2);
            var rowDisplay = BudgetLoadService.BuildRowDisplay(proyecto, c);

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cont = col.EstiloContenido;
                var cell = ws.Cell(fila, i + 1);
                var overrideEfectivo = OverrideEfectivo(snapshot, snapshotBase, claveFila, col.Identificador, rolFila);

                object valor = ResolverValorConcepto(c, col.Identificador, consecutivo, factorPU, rowDisplay, indent2);

                if (valor is decimal d)
                {
                    cell.Value = d;
                    string? formato = !string.IsNullOrWhiteSpace(overrideEfectivo.FormatoNumerico)
                        ? ReportNumberFormatMapper.ToExcelFormat(overrideEfectivo.FormatoNumerico)
                        : (!string.IsNullOrEmpty(col.FormatoNumerico)
                            ? ReportColumnGridFormat.ResolveExcelFormat(col, snapshot)
                            : null);
                    if (formato != null)
                        cell.Style.NumberFormat.Format = formato;
                }
                else
                {
                    cell.Value = valor?.ToString() ?? "";
                }

                var estilo = cell.Style;
                estilo.Font.FontName = cont.Fuente;
                estilo.Font.FontSize = cont.Tamano;
                estilo.Font.Bold = esAgrupador || cont.Negrita;
                estilo.Font.Italic = cont.Cursiva;
                estilo.Alignment.Horizontal = ConvertirAlineacionXL(col.Alineacion);
                estilo.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                estilo.Alignment.WrapText = col.Wrap;

                // Fondo base de la columna; las filas agrupador lo heredan del
                // DEFAULT del rol vía el overlay (ya no hay colores hardcodeados).
                estilo.Fill.BackgroundColor = ObtenerColorXL(cont.ColorFondo, "#FFFFFF");

                estilo.Font.FontColor = ObtenerColorXL(cont.ColorFuente, "#000000");

                AplicarOverlayExcel(estilo, overrideEfectivo);

                estilo.Border.BottomBorder = XLBorderStyleValues.Hair;
                estilo.Border.BottomBorderColor = XLColor.Gray;
            }

            ws.Row(fila).Height = CalcularAlturaFila(cols, fila, ws, esAgrupador ? 16 : 14);
            return fila + 1;
        }

        private static object ResolverValorConcepto(
            ConceptoPresupuesto concepto,
            string nombreInterno,
            int consecutivo,
            decimal factorPU,
            SOPRO.Application.Models.Presupuesto.BudgetGridRowDisplay rowDisplay,
            string indent)
        {
            bool esAgrupador = concepto.EsAgrupador;

            return nombreInterno switch
            {
                "Numero" => esAgrupador ? (object)string.Empty : consecutivo.ToString(),
                "Clave" => concepto.Clave ?? string.Empty,
                "Descripcion" => indent + (concepto.Descripcion ?? string.Empty),
                "Unidad" => esAgrupador ? (object)string.Empty : (concepto.Unidad ?? string.Empty),
                "Cantidad" => esAgrupador ? (object)string.Empty : concepto.Cantidad,
                "PrecioUnitario" => esAgrupador ? (object)string.Empty : (concepto.PrecioUnitario > 0 ? concepto.PrecioUnitario : concepto.CostoDirectoUnitario * factorPU),
                "ImporteTotal" or "Importe" => esAgrupador
                    ? (concepto.ImporteTotal > 0 ? (object)concepto.ImporteTotal : string.Empty)
                    : (concepto.ImporteTotal > 0 ? (object)concepto.ImporteTotal : concepto.Cantidad * concepto.CostoDirectoUnitario * factorPU),
                _ => ObtenerValorDesdeDisplay(rowDisplay, nombreInterno)
            };
        }

        private static object ObtenerValorDesdeDisplay(SOPRO.Application.Models.Presupuesto.BudgetGridRowDisplay rowDisplay, string nombreInterno)
        {
            if (rowDisplay?.ValuesByInternalName == null)
                return string.Empty;

            if (!rowDisplay.ValuesByInternalName.TryGetValue(nombreInterno, out var valor) || valor == null)
                return string.Empty;

            return valor;
        }

        // ── TOTALES ───────────────────────────────────────────────────────────
        private int EscribirTotales(IXLWorksheet ws, Proyecto proyecto,
                                     List<ConceptoPresupuesto> conceptos,
                                     List<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, int fila)
        {
            // Solo conceptos terminales (no agrupadores)
            var terminales = conceptos.Where(c => !c.EsAgrupador).ToList();

            decimal totalImporte = terminales.Sum(c => c.ImporteTotal);
            decimal totalIVA = totalImporte * proyecto.PorcentajeIVA / 100m;
            decimal totalConIVA = totalImporte + totalIVA;

            // Índice de columna ImporteTotal
            int colImporte = cols.FindIndex(c => c.Identificador == "ImporteTotal") + 1;
            var colImporteDef = colImporte > 0 ? cols[colImporte - 1] : null;
            int colDesc = cols.FindIndex(c => c.Identificador == "Descripcion") + 1;
            if (colDesc < 1) colDesc = 1;
            var colDescDef = cols[colDesc - 1];
            var snapshotBase = snapshot with { EstilosCelda = null, EstilosFila = null };

            void FilaTotal(string etiqueta, decimal monto, bool negrita, string claveFila, string rolFila)
            {
                fila++;

                // Fondo de toda la fila desde el DEFAULT del rol (Subtotal/Total) vía
                // el resolver; ya no hay colores hardcodeados.
                var estiloFila = ReportCellStyleResolver.Resolve(
                    snapshot,
                    PresupuestoReportSnapshotBuilder.ClaveVista,
                    claveFila,
                    colDescDef.Identificador,
                    rolFila,
                    false);
                if (!string.IsNullOrWhiteSpace(estiloFila.ColorFondo))
                    ws.Range(fila, 1, fila, cols.Count).Style.Fill.BackgroundColor =
                        ObtenerColorXL(estiloFila.ColorFondo, "#FFFFFF");

                // Etiqueta en columna Descripcion (o columna 1 si no existe)
                var overrideEtiqueta = OverrideEfectivo(snapshot, snapshotBase, claveFila, colDescDef.Identificador, rolFila);
                var cEtiq = ws.Cell(fila, colDesc);
                cEtiq.Value = etiqueta;
                cEtiq.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                AplicarOverlayExcel(cEtiq.Style, overrideEtiqueta);
                // La negrita es un rasgo de la fila de cierre (IVA no va en negrita
                // aunque comparta el rol Total): se reafirma tras el default del rol.
                cEtiq.Style.Font.Bold = negrita;

                // Monto en columna ImporteTotal (misma semántica que el grid para el importe).
                if (colImporte > 0 && colImporteDef != null)
                {
                    var overrideMonto = OverrideEfectivo(snapshot, snapshotBase, claveFila, colImporteDef.Identificador, rolFila);
                    var cMonto = ws.Cell(fila, colImporte);
                    cMonto.Value = monto;
                    cMonto.Style.NumberFormat.Format = FormatoNumericoExcel(overrideMonto, colImporteDef, snapshot);
                    cMonto.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    AplicarOverlayExcel(cMonto.Style, overrideMonto);
                    cMonto.Style.Font.Bold = negrita;
                }
                ws.Row(fila).Height = CalcularAlturaFila(cols, fila, ws, 15, esSubtotal: true);
            }

            // Línea separadora antes de totales
            var sep = ws.Range(fila, 1, fila, cols.Count);
            sep.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            sep.Style.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");

            // Claves estables de las filas de cierre (coinciden con las que persiste
            // el grid): SUB:{clave} para el subtotal global, IVA y TOTAL.
            FilaTotal("SUBTOTAL:", totalImporte, true, ClaveFilaReporte.Subtotal(string.Empty), RolFilaReporte.Subtotal);
            FilaTotal($"IVA ({proyecto.PorcentajeIVA:N0}%):", totalIVA, false, ClaveFilaReporte.Iva, RolFilaReporte.Total);
            FilaTotal("TOTAL:", totalConIVA, true, ClaveFilaReporte.Total, RolFilaReporte.Total);

            return fila + 1;
        }

        // ── PIE DE PÁGINA ─────────────────────────────────────────────────────
        private void EscribirPie(IXLWorksheet ws, PlantillaReporte p,
                                  Proyecto proyecto, int numCols, int fila)
        {
            fila++; // Espacio
            int colTotal = numCols;
            int c1 = 1, c2 = colTotal / 3 + 1, c3 = colTotal * 2 / 3 + 1;

            // Línea separadora
            var sep = ws.Range(fila, 1, fila, colTotal);
            sep.Style.Border.TopBorder = XLBorderStyleValues.Thin;
            sep.Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

            fila++;
            EscribirZona(ws, fila, c1, c2 - 1,
                p.PiePaginaIzqTipo, p.PiePaginaIzqContenido,
                p.PiePaginaIzqFuente, p.PiePaginaIzqTamaño,
                p.PiePaginaIzqNegrita, p.PiePaginaIzqCursiva,
                p.PiePaginaIzqAlineacion, proyecto, p);

            EscribirZona(ws, fila, c2, c3 - 1,
                p.PiePaginaCenTipo, p.PiePaginaCenContenido,
                p.PiePaginaCenFuente, p.PiePaginaCenTamaño,
                p.PiePaginaCenNegrita, p.PiePaginaCenCursiva,
                p.PiePaginaCenAlineacion, proyecto, p);

            EscribirZona(ws, fila, c3, colTotal,
                p.PiePaginaDerTipo, p.PiePaginaDerContenido,
                p.PiePaginaDerFuente, p.PiePaginaDerTamaño,
                p.PiePaginaDerNegrita, p.PiePaginaDerCursiva,
                p.PiePaginaDerAlineacion, proyecto, p);

            ws.Row(fila).Height = p.PiePaginaAltura * 0.75;
        }

        private double CalcularAlturaFila(List<ReportColumnDefinition> cols, int fila, IXLWorksheet ws, double alturaBase, bool esSubtotal = false)
        {
            double altura = alturaBase;
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cont = col.EstiloContenido;
                if (!col.Wrap) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                using var font = new Font(
                    string.IsNullOrWhiteSpace(cont.Fuente) ? "Segoe UI" : cont.Fuente,
                    Math.Max(8f, cont.Tamano),
                    (esSubtotal || cont.Negrita ? FontStyle.Bold : FontStyle.Regular));

                int anchoPx = Math.Max(24, col.Ancho - 8);
                var proposed = new Size(anchoPx, int.MaxValue);
                var flags = TextFormatFlags.WordBreak | TextFormatFlags.TextBoxControl;
                var measured = TextRenderer.MeasureText(valor, font, proposed, flags);
                double alturaPts = Math.Max(alturaBase, PixelsToPoints(measured.Height + 6));
                if (alturaPts > altura)
                    altura = alturaPts;
            }
            return altura;
        }

        private static double PixelsToPoints(int pixels) => pixels * 72.0 / 96.0;

        // ── ANCHOS ────────────────────────────────────────────────────────────
        private void AplicarAnchos(IXLWorksheet ws, List<ReportColumnDefinition> cols)
        {
            double[] anchos = ReportColumnWidthConverter.PxToExcelWidths(
                cols.Select(c => c.Ancho).ToArray());
            for (int i = 0; i < cols.Count; i++)
                ws.Column(i + 1).Width = anchos[i];
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private List<ConceptoPresupuesto> OrdenarJerarquia(List<ConceptoPresupuesto> conceptos)
        {
            var resultado = new List<ConceptoPresupuesto>();
            // Orden es la fuente de verdad — define la posición visual en el grid.
            // AgregarConSubtotales infiere la jerarquía por Clave o por Nivel.
            var ordenados = conceptos.OrderBy(c => c.Orden).ToList();
            AgregarConSubtotales(ordenados, resultado);
            return resultado;
        }

        /// <summary>
        /// Recorre la lista ordenada por Orden e inserta filas de subtotal
        /// después de los últimos hijos de cada agrupador.
        /// Usa la Clave para inferir jerarquía (PadreId puede ser null en BD).
        /// </summary>
        private void AgregarConSubtotales(List<ConceptoPresupuesto> ordenados,
                                           List<ConceptoPresupuesto> resultado)
        {
            // Lista de agrupadores activos: clave, descripcion, nivel, importe acumulado
            var pilaAgrup = new List<(string clave, string desc, int nivel, decimal importe)>();

            foreach (var c in ordenados)
            {
                string claveC = (c.Clave ?? "").Trim();

                // Cerrar agrupadores que ya no son ancestros del concepto actual
                // (de más profundo a más superficial)
                for (int k = pilaAgrup.Count - 1; k >= 0; k--)
                {
                    var agr = pilaAgrup[k];
                    bool esAncestro = (agr.clave != "" && claveC != "")
                        ? (claveC.StartsWith(agr.clave + ".") || claveC == agr.clave)
                        : c.Nivel > agr.nivel;  // sin clave: hijo si tiene nivel más profundo
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
                    pilaAgrup.Add((claveC, c.Descripcion ?? "", c.Nivel, 0m));
                }
                else
                {
                    // Acumular importe en todos los agrupadores ancestros activos
                    for (int k = 0; k < pilaAgrup.Count; k++)
                    {
                        var agr = pilaAgrup[k];
                        pilaAgrup[k] = (agr.clave, agr.desc, agr.nivel, agr.importe + c.ImporteTotal);
                    }
                }
            }

            // Cerrar agrupadores que quedaron abiertos (de más profundo a más superficial)
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

        private void AgregarRecursivo(List<ConceptoPresupuesto> nodos,
                                       List<ConceptoPresupuesto> todos,
                                       List<ConceptoPresupuesto> resultado)
        {
            foreach (var n in nodos)
            {
                resultado.Add(n);
                var hijos = todos.Where(c => c.PadreId == n.Id).OrderBy(c => c.Orden).ToList();
                if (hijos.Any())
                    AgregarRecursivo(hijos, todos, resultado);
            }
        }

        private bool EsDescendiente(int conceptoId, int agrupadorId,
                                     List<ConceptoPresupuesto> todos)
        {
            var concepto = todos.FirstOrDefault(c => c.Id == conceptoId);
            if (concepto == null) return false;
            if (concepto.PadreId == agrupadorId) return true;
            if (concepto.PadreId == null) return false;
            return EsDescendiente(concepto.PadreId.Value, agrupadorId, todos);
        }

        private static XLAlignmentHorizontalValues ConvertirAlineacionXL(ReportTextAlignment alineacion) => alineacion switch
        {
            ReportTextAlignment.Derecha => XLAlignmentHorizontalValues.Right,
            ReportTextAlignment.Centro => XLAlignmentHorizontalValues.Center,
            _ => XLAlignmentHorizontalValues.Left,
        };

        private static XLAlignmentVerticalValues ConvertirAlineacionVerticalXL(ReportVerticalAlignment alineacion) => alineacion switch
        {
            ReportVerticalAlignment.Superior => XLAlignmentVerticalValues.Top,
            ReportVerticalAlignment.Inferior => XLAlignmentVerticalValues.Bottom,
            _ => XLAlignmentVerticalValues.Center,
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
        /// Aplica al estilo SOLO las propiedades no nulas del override (fuente, tamaño,
        /// negrita, cursiva, colores de texto/fondo, alineaciones y wrap). No toca el
        /// formato numérico (lo resuelve <see cref="FormatoNumericoExcel"/>).
        /// </summary>
        private static void AplicarOverlayExcel(IXLStyle estilo, ReportStyleOverlay overrideEfectivo)
        {
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.Fuente))
                estilo.Font.FontName = overrideEfectivo.Fuente;
            if (overrideEfectivo.TamanoFuente is float tamano && tamano > 0)
                estilo.Font.FontSize = tamano;
            if (overrideEfectivo.Negrita is bool negrita)
                estilo.Font.Bold = negrita;
            if (overrideEfectivo.Cursiva is bool cursiva)
                estilo.Font.Italic = cursiva;
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.ColorFuente))
                estilo.Font.FontColor = ObtenerColorXL(overrideEfectivo.ColorFuente, "#000000");
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.ColorFondo))
                estilo.Fill.BackgroundColor = ObtenerColorXL(overrideEfectivo.ColorFondo, "#FFFFFF");
            if (overrideEfectivo.AlineacionHorizontal is ReportTextAlignment alineacion)
                estilo.Alignment.Horizontal = ConvertirAlineacionXL(alineacion);
            if (overrideEfectivo.AlineacionVertical is ReportVerticalAlignment alineacionV)
                estilo.Alignment.Vertical = ConvertirAlineacionVerticalXL(alineacionV);
            if (overrideEfectivo.Wrap is bool wrap)
                estilo.Alignment.WrapText = wrap;
        }

        /// <summary>
        /// Formato de celda Excel: si el override aporta un <c>FormatoNumerico</c>
        /// explícito se respeta (ClosedXML lo soporta sin renderizar); en otro caso se
        /// conserva el formato numérico por columna de <see cref="ReportColumnGridFormat"/>
        /// (autoridad de cantidades/importes).
        /// </summary>
        private static string FormatoNumericoExcel(
            ReportStyleOverlay overrideEfectivo,
            ReportColumnDefinition columna,
            ReportColumnSnapshot snapshot)
        {
            if (!string.IsNullOrWhiteSpace(overrideEfectivo.FormatoNumerico))
                return ReportNumberFormatMapper.ToExcelFormat(overrideEfectivo.FormatoNumerico);

            return ReportColumnGridFormat.ResolveExcelFormat(columna, snapshot);
        }

        private string SanitizarNombre(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre[..40] : nombre;
        }

        private static XLColor ObtenerColorXL(string? valor, string fallbackHex)
        {
            string normalizado = NormalizarColorHex(valor, fallbackHex);
            try { return ExcelColorHelper.SafeFromHtml(normalizado); }
            catch { return ExcelColorHelper.SafeFromHtml(fallbackHex); }
        }

        private static string NormalizarColorHex(string? valor, string fallbackHex)
        {
            if (string.IsNullOrWhiteSpace(valor)) return fallbackHex;
            valor = valor.Trim();
            try
            {
                var c = ColorTranslator.FromHtml(valor);
                return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
            }
            catch { return fallbackHex; }
        }

    }
}

