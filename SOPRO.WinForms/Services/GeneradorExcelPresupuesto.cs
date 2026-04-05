using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte de Presupuesto en formato .xlsx usando ClosedXML.
    /// Aplica la PlantillaReporte del proyecto (encabezado/pie) y la
    /// ConfigColumnaReporte (anchos, fuentes, alineaciones, colores).
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
            List<ConfigColumnaReporte> columnas,
            string rutaDestino = null,
            decimal factorPU = 1m,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            // Columnas visibles ordenadas
            var cols = columnas
                .Where(c => c.Visible)
                .OrderBy(c => c.Orden)
                .ToList();

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
            fila = EscribirTitulosColumnas(ws, cols, fila);
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, fila - 1);

            // ── DATOS ─────────────────────────────────────────────────────────
            int filaInicioDatos = fila;
            var conceptosOrdenados = OrdenarJerarquia(conceptos);
            int consecutivo = 0;
            foreach (var c in conceptosOrdenados)
            {
                if (!c.EsAgrupador) consecutivo++;
                fila = EscribirConcepto(ws, proyecto, c, cols, fila, consecutivo, factorPU);
            }

            // ── TOTALES ───────────────────────────────────────────────────────
            fila = EscribirTotales(ws, proyecto, conceptos, cols, fila);

            // ── PIE DE PÁGINA ─────────────────────────────────────────────────
            fila++;
            EscribirPie(ws, plantilla, proyecto, cols.Count, fila);

            // ── ANCHOS DE COLUMNA ─────────────────────────────────────────────
            AplicarAnchos(ws, cols);

            // ── CONFIGURACIÓN DE IMPRESIÓN ────────────────────────────────────
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);   // 1 página de ancho, alto libre
            ws.PageSetup.Margins.Left    = 0.5;
            ws.PageSetup.Margins.Right   = 0.5;
            ws.PageSetup.Margins.Top     = 0.75;
            ws.PageSetup.Margins.Bottom  = 0.75;
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
            estilo.Font.FontName  = fuente;
            estilo.Font.FontSize  = tamaño;
            estilo.Font.Bold      = negrita;
            estilo.Font.Italic    = cursiva;
            estilo.Alignment.WrapText   = true;
            estilo.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            estilo.Alignment.Horizontal = alineacion switch
            {
                "Centro"  => XLAlignmentHorizontalValues.Center,
                "Derecha" => XLAlignmentHorizontalValues.Right,
                _         => XLAlignmentHorizontalValues.Left,
            };
        }

        // ── TÍTULOS DE COLUMNAS ───────────────────────────────────────────────
        private int EscribirTitulosColumnas(IXLWorksheet ws,
                                             List<ConfigColumnaReporte> cols, int fila)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var cell = ws.Cell(fila, i + 1);
                cell.Value = col.Encabezado;

                var estilo = cell.Style;
                estilo.Font.FontName   = col.EncFuente;
                estilo.Font.FontSize   = col.EncTamaño;
                estilo.Font.Bold       = col.EncNegrita;
                estilo.Fill.BackgroundColor = ObtenerColorXL(col.EncColorFondo, "#1565C0");
                estilo.Font.FontColor       = ObtenerColorXL(col.EncColorTexto, "#000000");
                estilo.Font.Italic          = col.EncCursiva;
                estilo.Alignment.Horizontal = col.EncAlineacion switch
                {
                    "Derecha"   => XLAlignmentHorizontalValues.Right,
                    "Izquierda" => XLAlignmentHorizontalValues.Left,
                    _           => XLAlignmentHorizontalValues.Center,
                };
                estilo.Border.BottomBorder = XLBorderStyleValues.Medium;
                estilo.Border.BottomBorderColor = ObtenerColorXL(col.EncColorFondo, "#1565C0");
            }

            ws.Row(fila).Height = 18;
            return fila + 1;
        }

        // ── CONCEPTO ─────────────────────────────────────────────────────────
        private int EscribirConcepto(IXLWorksheet ws, Proyecto proyecto, ConceptoPresupuesto c,
                                      List<ConfigColumnaReporte> cols, int fila, int consecutivo,
                                      decimal factorPU = 1m)
        {
            // ── SUBTOTAL DE AGRUPADOR ─────────────────────────────────────────
            if (c.Notas == "__subtotal__")
            {
                string indent = new string(' ', c.Nivel * 2);

                for (int i = 0; i < cols.Count; i++)
                {
                    var col  = cols[i];
                    var cell = ws.Cell(fila, i + 1);

                    if (col.NombreInterno == "Descripcion")
                        cell.Value = indent + $"Total {c.Descripcion}:";
                    else if (col.NombreInterno is "ImporteTotal" or "Importe" or "Subtotal" or "Total")
                    {
                        cell.Value = c.ImporteTotal;
                        cell.Style.NumberFormat.Format = ConvertirFormato(
                            string.IsNullOrEmpty(col.FormatoNumero) ? "N2" : col.FormatoNumero);
                    }
                    else
                        cell.Value = "";

                    // Adoptar exactamente el mismo estilo que la columna configurada
                    var estilo = cell.Style;
                    estilo.Font.FontName = col.ConFuente;
                    estilo.Font.FontSize = col.ConTamaño;
                    estilo.Font.Bold     = true;
                    estilo.Font.Italic   = col.ConCursiva;
                    estilo.Font.FontColor = ObtenerColorXL(col.ConColorTexto, "#000000");
                    estilo.Alignment.Horizontal = col.ConAlineacion switch
                    {
                        "Derecha" => XLAlignmentHorizontalValues.Right,
                        "Centro"  => XLAlignmentHorizontalValues.Center,
                        _         => XLAlignmentHorizontalValues.Left,
                    };
                    estilo.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    estilo.Alignment.WrapText = col.WrapTexto;
                    if (col.NombreInterno == "Descripcion")
                        estilo.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                    estilo.Fill.BackgroundColor = c.Nivel switch
                    {
                        0 => XLColor.FromHtml("#E3F2FD"),
                        1 => XLColor.FromHtml("#F3F3F3"),
                        _ => XLColor.FromHtml("#FAFAFA"),
                    };

                    estilo.Border.TopBorder      = XLBorderStyleValues.Hair;
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
                var col  = cols[i];
                var cell = ws.Cell(fila, i + 1);

                object valor = ResolverValorConcepto(c, col.NombreInterno, consecutivo, factorPU, rowDisplay, indent2);

                if (valor is decimal d)
                {
                    cell.Value = d;
                    if (!string.IsNullOrEmpty(col.FormatoNumero))
                        cell.Style.NumberFormat.Format = ConvertirFormato(col.FormatoNumero);
                }
                else
                {
                    cell.Value = valor?.ToString() ?? "";
                }

                var estilo = cell.Style;
                estilo.Font.FontName = col.ConFuente;
                estilo.Font.FontSize = col.ConTamaño;
                estilo.Font.Bold     = esAgrupador || col.ConNegrita;
                estilo.Font.Italic   = col.ConCursiva;
                estilo.Alignment.Horizontal = col.ConAlineacion switch
                {
                    "Derecha" => XLAlignmentHorizontalValues.Right,
                    "Centro"  => XLAlignmentHorizontalValues.Center,
                    _         => XLAlignmentHorizontalValues.Left,
                };
                estilo.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                estilo.Alignment.WrapText = col.WrapTexto;

                if (esAgrupador)
                {
                    estilo.Fill.BackgroundColor = c.Nivel switch
                    {
                        0 => XLColor.FromHtml("#E3F2FD"),
                        1 => XLColor.FromHtml("#F3F3F3"),
                        _ => XLColor.FromHtml("#FAFAFA"),
                    };
                }
                else
                    estilo.Fill.BackgroundColor = ObtenerColorXL(col.ConColorFondo, "#FFFFFF");

                estilo.Font.FontColor = ObtenerColorXL(col.ConColorTexto, "#000000");
                estilo.Border.BottomBorder      = XLBorderStyleValues.Hair;
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
                                     List<ConfigColumnaReporte> cols, int fila)
        {
            // Solo conceptos terminales (no agrupadores)
            var terminales = conceptos.Where(c => !c.EsAgrupador).ToList();

            decimal totalImporte = terminales.Sum(c => c.ImporteTotal);
            decimal totalIVA     = totalImporte * proyecto.PorcentajeIVA / 100m;
            decimal totalConIVA  = totalImporte + totalIVA;

            // Índice de columna ImporteTotal
            int colImporte = cols.FindIndex(c => c.NombreInterno == "ImporteTotal") + 1;
            int colDesc    = cols.FindIndex(c => c.NombreInterno == "Descripcion") + 1;
            if (colDesc < 1) colDesc = 1;

            void FilaTotal(string etiqueta, decimal monto, bool negrita, string fondo)
            {
                fila++;

                // Aplicar fondo a toda la fila
                ws.Range(fila, 1, fila, cols.Count).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);

                // Etiqueta en columna Descripcion (o columna 1 si no existe)
                var cEtiq = ws.Cell(fila, colDesc);
                cEtiq.Value = etiqueta;
                cEtiq.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                cEtiq.Style.Font.Bold = negrita;

                // Monto en columna ImporteTotal
                if (colImporte > 0)
                {
                    var cMonto = ws.Cell(fila, colImporte);
                    cMonto.Value = monto;
                    cMonto.Style.NumberFormat.Format = "#,##0.00";
                    cMonto.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    cMonto.Style.Font.Bold = negrita;
                }
                ws.Row(fila).Height = CalcularAlturaFila(cols, fila, ws, 15, esSubtotal: true);
            }

            // Línea separadora antes de totales
            var sep = ws.Range(fila, 1, fila, cols.Count);
            sep.Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            sep.Style.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");

            FilaTotal("SUBTOTAL:", totalImporte, true,  "#E3F2FD");
            FilaTotal($"IVA ({proyecto.PorcentajeIVA:N0}%):", totalIVA, false, "#F5F5F5");
            FilaTotal("TOTAL:",    totalConIVA,  true,  "#BBDEFB");

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

        private double CalcularAlturaFila(List<ConfigColumnaReporte> cols, int fila, IXLWorksheet ws, double alturaBase, bool esSubtotal = false)
        {
            double altura = alturaBase;
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                if (!col.WrapTexto) continue;

                var valor = ws.Cell(fila, i + 1).GetFormattedString();
                if (string.IsNullOrWhiteSpace(valor)) continue;

                using var font = new Font(
                    string.IsNullOrWhiteSpace(col.ConFuente) ? "Segoe UI" : col.ConFuente,
                    Math.Max(8f, col.ConTamaño),
                    (esSubtotal || col.ConNegrita ? FontStyle.Bold : FontStyle.Regular));

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
        private void AplicarAnchos(IXLWorksheet ws, List<ConfigColumnaReporte> cols)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                // Convertir px → caracteres Excel (aprox 7px por caracter)
                double ancho = cols[i].Ancho / 7.0;
                ws.Column(i + 1).Width = Math.Max(ancho, 4);
            }
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
                            Descripcion  = agr.desc,
                            Clave        = agr.clave,
                            EsAgrupador  = true,
                            Notas        = "__subtotal__",
                            ImporteTotal = agr.importe,
                            Nivel        = agr.nivel
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
                    Descripcion  = agr.desc,
                    Clave        = agr.clave,
                    EsAgrupador  = true,
                    Notas        = "__subtotal__",
                    ImporteTotal = agr.importe,
                    Nivel        = agr.nivel
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

        private string ConvertirFormato(string formato) => formato switch
        {
            "N0" => "#,##0",
            "N2" => "#,##0.00",
            "N3" => "#,##0.000",
            "N4" => "#,##0.0000",
            "N5" => "#,##0.00000",
            "C2" => "$#,##0.00",
            "P2" => "0.00%",
            _    => "#,##0.00"
        };

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

