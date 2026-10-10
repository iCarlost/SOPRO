using ClosedXML.Excel;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing;
using Microsoft.EntityFrameworkCore;
using SOPRO.Data.Context;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera un archivo Excel con los Análisis de Precios Unitarios (APU).
    /// Una hoja por concepto, con secciones por tipo de insumo y resumen de integración.
    ///
    /// La tabla de componentes consume el MISMO <see cref="ReportColumnSnapshot"/>
    /// neutral que la ruta PDF (visibilidad, orden, encabezado, ancho, alineación,
    /// wrap, estilo y formato numérico), garantizando paridad de cantidades e
    /// importes con el PDF.
    /// </summary>
    public class GeneradorExcelAPU
    {
        private readonly ReporteService _svc;
        private readonly SOPROContext _ctx;

        /// <summary>Estilo de contenido neutral vigente (snapshot o default de catálogo).</summary>
        private ReportTextStyle _contenido = ReportTableStyle.LegacyCatalogo().EstiloContenido;

        // Colores de sección por tipo de insumo
        private static readonly string ColorSeccionMaterial    = "#E3F2FD"; // Azul muy claro
        private static readonly string ColorSeccionManoObra    = "#F3E5F5"; // Morado muy claro
        private static readonly string ColorSeccionMaquinaria  = "#FFF3E0"; // Naranja muy claro
        private static readonly string ColorSeccionHerramienta = "#E8F5E9"; // Verde muy claro
        private static readonly string ColorSeccionAuxiliar    = "#FFF9C4"; // Amarillo muy claro
        private static readonly string ColorSubtotal           = "#ECEFF1"; // Gris claro
        private static readonly string ColorResumen            = "#1565C0"; // Azul oscuro (encabezado)
        private static readonly string ColorPU                 = "#FFF176"; // Amarillo PU final

        public GeneradorExcelAPU(ReporteService svc, SOPROContext ctx)
        {
            _svc = svc;
            _ctx = ctx;
        }

        /// <summary>
        /// Genera el Excel de APUs para los conceptos indicados (solo los que tienen Matriz).
        /// </summary>
        public string Generar(
            Proyecto proyecto,
            List<ConceptoPresupuesto> conceptos,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            string rutaDestino)
        {
            ArgumentNullException.ThrowIfNull(proyecto);
            ArgumentNullException.ThrowIfNull(conceptos);
            ArgumentNullException.ThrowIfNull(plantilla);
            ArgumentNullException.ThrowIfNull(snapshot);
            _contenido = snapshot.EstiloTabla.EstiloContenido;

            var cols = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (cols.Count == 0)
                cols = ApuExportResolver.DefaultColumns().Where(c => c.Visible).OrderBy(c => c.Orden).ToList();

            // Solo conceptos con Matriz APU
            var conceptosConAPU = conceptos
                .Where(c => !c.EsAgrupador && c.MatrizId.HasValue)
                .ToList();

            if (!conceptosConAPU.Any())
                throw new InvalidOperationException("No hay conceptos con APU vinculado en este presupuesto.");

            // Cargar matrices con todos sus componentes
            var matrizIds = conceptosConAPU.Select(c => c.MatrizId.Value).Distinct().ToList();
            var matrices = _ctx.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrizIds.Contains(m.Id))
                .ToDictionary(m => m.Id);

            using var wb = new XLWorkbook();

            // Índice general (primera hoja)
            var wsIndice = wb.AddWorksheet("Índice");
            EscribirIndice(wsIndice, conceptosConAPU, proyecto, snapshot);

            // Una hoja por concepto
            int numero = 1;
            foreach (var concepto in conceptosConAPU)
            {
                if (!matrices.TryGetValue(concepto.MatrizId.Value, out var matriz))
                    continue;

                // Nombre de hoja: número + clave (máx 31 chars, sin caracteres inválidos)
                string nombreHoja = LimpiarNombreHoja($"{numero:D3}-{concepto.Clave ?? concepto.Descripcion}");
                var ws = wb.AddWorksheet(nombreHoja);

                EscribirAPU(ws, concepto, matriz, proyecto, plantilla, cols, snapshot, numero);
                numero++;
            }

            wb.SaveAs(rutaDestino);
            return rutaDestino;
        }

        // ── ÍNDICE ────────────────────────────────────────────────────────────
        private void EscribirIndice(IXLWorksheet ws, List<ConceptoPresupuesto> conceptos,
            Proyecto proyecto, ReportColumnSnapshot snapshot)
        {
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);

            int fila = 1;

            // Título
            var rngTitulo = ws.Range(fila, 1, fila, 5);
            rngTitulo.Merge();
            rngTitulo.Value = $"ANÁLISIS DE PRECIOS UNITARIOS — {proyecto.Nombre?.ToUpper()}";
            rngTitulo.Style.Font.Bold = true;
            rngTitulo.Style.Font.FontSize = 12;
            rngTitulo.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rngTitulo.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorResumen);
            rngTitulo.Style.Font.FontColor = XLColor.White;
            fila++;

            // Encabezados del índice
            string[] encabezados = { "No.", "Clave", "Descripción", "Unidad", "P.U." };
            int[] anchos         = {   5,     14,        60,           10,      16    };
            for (int c = 0; c < encabezados.Length; c++)
            {
                var cell = ws.Cell(fila, c + 1);
                cell.Value = encabezados[c];
                AplicarFuenteBase(cell.Style, boldOverride: true, colorOverride: "#FFFFFF");
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
                cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Column(c + 1).Width = anchos[c];
            }
            fila++;

            // Filas del índice
            int num = 1;
            foreach (var c in conceptos)
            {
                ws.Cell(fila, 1).Value = num;
                ws.Cell(fila, 2).Value = c.Clave;
                ws.Cell(fila, 3).Value = c.Descripcion;
                ws.Cell(fila, 4).Value = c.Unidad;
                ws.Cell(fila, 5).Value = c.PrecioUnitario;
                AplicarFuenteBase(ws.Cell(fila, 1).Style);
                AplicarFuenteBase(ws.Cell(fila, 2).Style);
                AplicarFuenteBase(ws.Cell(fila, 3).Style);
                AplicarFuenteBase(ws.Cell(fila, 4).Style);
                AplicarFuenteBase(ws.Cell(fila, 5).Style);
                ws.Cell(fila, 5).Style.NumberFormat.Format =
                    ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);
                ws.Cell(fila, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(fila, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                if (num % 2 == 0)
                {
                    for (int col = 1; col <= 5; col++)
                        ws.Cell(fila, col).Style.Fill.BackgroundColor = XLColor.FromHtml("#F5F5F5");
                }
                fila++;
                num++;
            }

            // Borde general
            ws.Range(2, 1, fila - 1, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Range(2, 1, fila - 1, 5).Style.Border.InsideBorder  = XLBorderStyleValues.Hair;
        }

        private void AplicarFuenteBase(IXLStyle style, bool boldOverride = false, string? colorOverride = null)
        {
            var estilo = _contenido;
            style.Font.FontName = string.IsNullOrWhiteSpace(estilo.Fuente) ? "Segoe UI" : estilo.Fuente;
            style.Font.FontSize = estilo.Tamano > 0 ? estilo.Tamano : 9f;
            style.Font.Bold = boldOverride || estilo.Negrita;
            style.Font.Italic = estilo.Cursiva;
            style.Font.FontColor = ObtenerColorXL(
                string.IsNullOrWhiteSpace(colorOverride) ? (string.IsNullOrWhiteSpace(estilo.ColorFuente) ? "#000000" : estilo.ColorFuente) : colorOverride,
                "#000000");
        }

        // ── APU INDIVIDUAL ────────────────────────────────────────────────────
        private void EscribirAPU(IXLWorksheet ws, ConceptoPresupuesto concepto,
            Matriz matriz, Proyecto proyecto, PlantillaReporte plantilla,
            IReadOnlyList<ReportColumnDefinition> cols, ReportColumnSnapshot snapshot, int numero)
        {
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);

            int nCols = cols.Count;
            for (int i = 0; i < nCols; i++)
                ws.Column(i + 1).Width = ReportColumnWidthConverter.PxToExcelWidth(cols[i].Ancho);

            int fila = 1;

            // ── ENCABEZADO DEL PROYECTO ──────────────────────────────────────
            fila = EscribirEncabezadoProyecto(ws, proyecto, plantilla, numero, fila, nCols);

            // ── ENCABEZADO DEL APU ───────────────────────────────────────────
            fila = EscribirEncabezadoAPU(ws, concepto, fila, nCols, snapshot);

            // ── TÍTULOS DE COLUMNAS ──────────────────────────────────────────
            fila = EscribirTitulosColumnas(ws, fila, cols);
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, fila - 1);
            int filaCongelar = fila;

            // ── SECCIONES POR TIPO DE INSUMO ────────────────────────────────
            var grupos = new[]
            {
                (Tipo: TipoComponenteMatriz.Material,    Titulo: "MATERIALES",       Color: ColorSeccionMaterial),
                (Tipo: TipoComponenteMatriz.ManoDeObra,  Titulo: "MANO DE OBRA",     Color: ColorSeccionManoObra),
                (Tipo: TipoComponenteMatriz.Maquinaria,  Titulo: "MAQUINARIA Y EQUIPO", Color: ColorSeccionMaquinaria),
                (Tipo: TipoComponenteMatriz.Herramienta, Titulo: "HERRAMIENTA MENOR",Color: ColorSeccionHerramienta),
                (Tipo: TipoComponenteMatriz.Auxiliar,    Titulo: "BÁSICOS / AUXILIARES", Color: ColorSeccionAuxiliar),
            };

            decimal costoDirecto = 0;

            foreach (var grupo in grupos)
            {
                var componentes = matriz.Componentes
                    .Where(c => ApuComponenteClasificacionHelper.ObtenerTipoSeccion(c) == grupo.Tipo)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!componentes.Any()) continue;

                // Título de sección
                fila = EscribirTituloSeccion(ws, fila, grupo.Titulo, grupo.Color, nCols);

                // Calcular totalMO para herramientas y MO con %MO
                decimal totalMO = CalcularTotalMO(matriz);

                decimal subtotal = 0;
                int numComp = 1;
                foreach (var comp in componentes)
                {
                    var (clave, desc, unidad, pu) = ApuComponenteClasificacionHelper.ObtenerDatosInsumo(comp);
                    decimal cantidad = comp.Cantidad;

                    // Para %MO: la cantidad es el porcentaje, PU es totalMO
                    decimal importeComp;
                    if ((grupo.Tipo == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra?.EsPorcentajeMO == true) ||
                        (grupo.Tipo == TipoComponenteMatriz.Herramienta && comp.Herramienta?.EsPorcentajeMO == true))
                    {
                        importeComp = totalMO * cantidad;
                        pu = totalMO;
                    }
                    else
                    {
                        importeComp = cantidad * pu;
                    }

                    subtotal += importeComp;

                    string abrev = ApuExportResolver.AbreviarTipo(grupo.Titulo);
                    string fondo = (numComp % 2 == 0) ? grupo.Color : "#FFFFFF";

                    // Fila del componente, columna a columna desde el snapshot neutral
                    for (int i = 0; i < nCols; i++)
                    {
                        var col = cols[i];
                        var cell = ws.Cell(fila, i + 1);
                        cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                        AplicarFuenteBase(cell.Style);
                        cell.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                        cell.Style.Border.RightBorder = XLBorderStyleValues.Hair;
                        cell.Style.Alignment.WrapText = col.Wrap;
                        cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                        cell.Style.Alignment.Horizontal = ConvertirAlineacion(col.Alineacion);
                        EscribirValorColumnaApu(cell, col, abrev, clave, desc, unidad, cantidad, pu, importeComp, snapshot);
                    }
                    fila++;
                    numComp++;
                }

                // Subtotal de sección
                fila = EscribirSubtotal(ws, fila, $"Subtotal {grupo.Titulo}", subtotal, nCols, snapshot);
                costoDirecto += subtotal;
            }

            // ── RESUMEN DE INTEGRACIÓN DEL PU ────────────────────────────────
            fila++;
            fila = EscribirResumenIntegracion(ws, fila, concepto, proyecto, costoDirecto, nCols, snapshot);

            // Congelar hasta títulos de columna
            ws.SheetView.FreezeRows(filaCongelar - 1);
        }

        private static void EscribirValorColumnaApu(IXLCell cell, ReportColumnDefinition col,
            string abrev, string clave, string desc, string unidad,
            decimal cantidad, decimal pu, decimal importe, ReportColumnSnapshot snapshot)
        {
            if (ApuExportResolver.EsCantidad(col))
            {
                cell.Value = cantidad;
                cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(col, snapshot);
            }
            else if (ApuExportResolver.EsPrecioUnitario(col))
            {
                cell.Value = pu;
                cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(col, snapshot);
            }
            else if (ApuExportResolver.EsImporte(col))
            {
                cell.Value = importe;
                cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(col, snapshot);
            }
            else if (ApuExportResolver.EsTipo(col))
            {
                cell.Value = abrev;
            }
            else
            {
                cell.Value = ApuExportResolver.ResolveTexto(col, clave, desc, unidad);
            }
        }

        // ── ENCABEZADO DEL PROYECTO ───────────────────────────────────────────
        private int EscribirEncabezadoProyecto(IXLWorksheet ws, Proyecto proyecto,
            PlantillaReporte plantilla, int numero, int fila, int COLS)
        {
            // ── Encabezado desde PlantillaReporte (igual que Presupuesto y Explosión) ──
            int c1 = 1, c2 = COLS / 3 + 1, c3 = COLS * 2 / 3 + 1;

            EscribirZona(ws, fila, c1, c2 - 1,
                plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño,
                plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                plantilla.EncabezadoIzqAlineacion, proyecto, plantilla);

            EscribirZona(ws, fila, c2, c3 - 1,
                plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño,
                plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                plantilla.EncabezadoCenAlineacion, proyecto, plantilla);

            EscribirZona(ws, fila, c3, COLS,
                plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño,
                plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                plantilla.EncabezadoDerAlineacion, proyecto, plantilla);

            ws.Row(fila).Height = plantilla.EncabezadoAltura * 0.75;
            fila++;

            // Línea separadora
            ws.Range(fila, 1, fila, COLS).Style.Border.TopBorder = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, COLS).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");
            fila++;

            // APU No. en línea propia (info específica del APU)
            var rngNum = ws.Range(fila, 1, fila, COLS);
            rngNum.Merge();
            rngNum.Value = $"APU No. {numero:D3}  —  {proyecto.Nombre}";
            AplicarFuenteBase(rngNum.Style, boldOverride: true, colorOverride: "#FFFFFF");
            rngNum.Style.Font.FontSize = Math.Max(10, _contenido.Tamano);
            rngNum.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rngNum.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorResumen);
            rngNum.Style.Font.FontColor = XLColor.White;
            ws.Row(fila).Height = 16;
            fila++;

            return fila;
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
                try { ws.AddPicture(contenido).MoveTo(ws.Cell(fila, colIni)).WithSize(120, 50); }
                catch { }
            }
            else
            {
                rango.FirstCell().Value = _svc.ResolverCampos(contenido, proyecto, plantilla);
            }

            var est = rango.Style;
            est.Font.FontName  = fuente;
            est.Font.FontSize  = tamaño;
            est.Font.Bold      = negrita;
            est.Font.Italic    = cursiva;
            est.Alignment.WrapText   = true;
            est.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            est.Alignment.Horizontal = alineacion switch
            {
                "Centro"  => XLAlignmentHorizontalValues.Center,
                "Derecha" => XLAlignmentHorizontalValues.Right,
                _         => XLAlignmentHorizontalValues.Left,
            };
        }

        // ── ENCABEZADO DEL APU ────────────────────────────────────────────────
        private int EscribirEncabezadoAPU(IXLWorksheet ws, ConceptoPresupuesto concepto,
            int fila, int COLS, ReportColumnSnapshot snapshot)
        {
            var colorAPU = "#37474F";

            // Etiquetas
            ws.Cell(fila, 1).Value = "CLAVE";
            ws.Cell(fila, 2).Value = concepto.Clave;
            var rngDesc = ws.Range(fila, 3, fila, COLS - 1);
            rngDesc.Merge();
            rngDesc.Value = concepto.Descripcion;
            ws.Cell(fila, COLS).Value = concepto.Unidad;

            // Estilo fila clave/desc
            foreach (var col in Enumerable.Range(1, COLS))
            {
                var cell = ws.Cell(fila, col);
                cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(colorAPU);
                AplicarFuenteBase(cell.Style, boldOverride: true, colorOverride: "#FFFFFF");
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(fila, COLS).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            fila++;

            // Cantidad / P.U. / Total del concepto en el presupuesto
            decimal puConcepto    = concepto.PrecioUnitario; // se muestra el guardado en BD
            decimal totalConcepto = concepto.Cantidad * puConcepto;
            int midCol = COLS / 3;

            var rngCantLbl = ws.Range(fila, 1, fila, midCol); rngCantLbl.Merge();
            rngCantLbl.Value = $"Cantidad: {concepto.Cantidad.ToString($"N{snapshot.DecimalesCantidad}", CultureInfo.CurrentCulture)} {concepto.Unidad}";
            rngCantLbl.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var rngPULbl = ws.Range(fila, midCol + 1, fila, midCol * 2); rngPULbl.Merge();
            rngPULbl.Value = $"P.U.: {FormatearMonedaTexto(puConcepto, snapshot)}";
            rngPULbl.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var rngTotLbl = ws.Range(fila, midCol * 2 + 1, fila, COLS); rngTotLbl.Merge();
            rngTotLbl.Value = $"Total: {FormatearMonedaTexto(totalConcepto, snapshot)}";
            rngTotLbl.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            AplicarEstiloEncabezadoInfo(ws, fila, COLS);
            fila++;

            return fila;
        }

        // ── TÍTULOS DE COLUMNAS ───────────────────────────────────────────────
        private static int EscribirTitulosColumnas(IXLWorksheet ws, int fila, IReadOnlyList<ReportColumnDefinition> cols)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var enc = col.EstiloEncabezado;
                var cell = ws.Cell(fila, i + 1);
                cell.Value = col.Encabezado ?? string.Empty;
                cell.Style.Font.Bold = enc.Negrita;
                cell.Style.Font.Italic = enc.Cursiva;
                if (!string.IsNullOrEmpty(enc.Fuente)) cell.Style.Font.FontName = enc.Fuente;
                if (enc.Tamano > 0) cell.Style.Font.FontSize = enc.Tamano;
                cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(enc.ColorFondo ?? "#4A4A6A");
                cell.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(enc.ColorFuente, "#FFFFFF");
                cell.Style.Alignment.Horizontal = ConvertirAlineacion(col.Alineacion);
                cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            return fila + 1;
        }

        // ── TÍTULO DE SECCIÓN ─────────────────────────────────────────────────
        private int EscribirTituloSeccion(IXLWorksheet ws, int fila, string titulo, string color, int COLS)
        {
            var rng = ws.Range(fila, 1, fila, COLS);
            rng.Merge();
            rng.Value = titulo;
            AplicarFuenteBase(rng.Style, boldOverride: true);
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(color);
            rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            rng.Style.Alignment.Indent = 1;
            return fila + 1;
        }

        // ── SUBTOTAL ──────────────────────────────────────────────────────────
        private int EscribirSubtotal(IXLWorksheet ws, int fila, string etiqueta, decimal valor, int COLS, ReportColumnSnapshot snapshot)
        {
            var rng = ws.Range(fila, 1, fila, COLS - 1);
            rng.Merge();
            rng.Value = etiqueta;
            AplicarFuenteBase(rng.Style, boldOverride: true);
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSubtotal);
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(fila, COLS).Value = valor;
            ws.Cell(fila, COLS).Style.NumberFormat.Format =
                ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);
            AplicarFuenteBase(ws.Cell(fila, COLS).Style, boldOverride: true);
            ws.Cell(fila, COLS).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSubtotal);
            ws.Cell(fila, COLS).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Range(fila, 1, fila, COLS).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            return fila + 1;
        }

        // ── RESUMEN DE INTEGRACIÓN ─────────────────────────────────────────────
        private int EscribirResumenIntegracion(IXLWorksheet ws, int fila,
            ConceptoPresupuesto concepto, Proyecto proyecto, decimal costoDirecto, int COLS, ReportColumnSnapshot snapshot)
        {
            // Título del resumen
            var rngTit = ws.Range(fila, 1, fila, COLS);
            rngTit.Merge();
            rngTit.Value = "INTEGRACIÓN DEL PRECIO UNITARIO";
            AplicarFuenteBase(rngTit.Style, boldOverride: true, colorOverride: "#FFFFFF");
            rngTit.Style.Fill.BackgroundColor = XLColor.FromHtml("#37474F");
            rngTit.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            fila++;

            var integracion = ApuPrecioUnitarioIntegracionHelper.Calcular(proyecto, concepto, costoDirecto);

            int idxPct = Math.Max(1, COLS - 2);
            for (int idx = 0; idx < integracion.Lineas.Count; idx++)
            {
                var linea = integracion.Lineas[idx];
                var rngE = ws.Range(fila, 1, fila, idxPct - 1);
                rngE.Merge();
                rngE.Value = linea.Etiqueta;
                rngE.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                rngE.Style.Alignment.Indent = 2;

                ws.Cell(fila, idxPct).Value = linea.PorcentajeTexto;
                ws.Cell(fila, idxPct).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var rngM = ws.Range(fila, idxPct + 1, fila, COLS);
                rngM.Merge();
                rngM.Value = linea.Monto;
                rngM.Style.NumberFormat.Format =
                    ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);
                rngM.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Range(fila, 1, fila, COLS).Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");
                ws.Range(fila, 1, fila, COLS).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                for (int c = 1; c <= COLS; c++) AplicarFuenteBase(ws.Cell(fila, c).Style);
                fila++;
            }

            // Precio Unitario Final
            var rngPU = ws.Range(fila, 1, fila, COLS - 2);
            rngPU.Merge();
            rngPU.Value = $"PRECIO UNITARIO  (Unidad: {concepto.Unidad})";
            AplicarFuenteBase(rngPU.Style, boldOverride: true);
            rngPU.Style.Font.FontSize = Math.Max(11, _contenido.Tamano);
            rngPU.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            rngPU.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorPU);

            var rngPUV = ws.Range(fila, COLS - 1, fila, COLS);
            rngPUV.Merge();
            rngPUV.Value = integracion.PrecioUnitario;
            rngPUV.Style.NumberFormat.Format =
                ReportColumnGridFormat.FormatoMonedaExcel(snapshot.DecimalesImporte, CultureInfo.CurrentCulture);
            AplicarFuenteBase(rngPUV.Style, boldOverride: true);
            rngPUV.Style.Font.FontSize = Math.Max(11, _contenido.Tamano);
            rngPUV.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            rngPUV.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorPU);

            ws.Range(fila, 1, fila, COLS).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            fila++;

            return fila;
        }

        // ── HELPERS ───────────────────────────────────────────────────────────
        private void AplicarEstiloEncabezadoInfo(IXLWorksheet ws, int fila, int COLS)
        {
            ws.Range(fila, 1, fila, COLS).Style.Fill.BackgroundColor = XLColor.FromHtml("#ECEFF1");
            ws.Range(fila, 1, fila, COLS).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            for (int c = 1; c <= COLS; c++) AplicarFuenteBase(ws.Cell(fila, c).Style, boldOverride: true);
        }

        private static string FormatearMonedaTexto(decimal valor, ReportColumnSnapshot snapshot)
            => valor.ToString(ReportColumnGridFormat.FormatoMonedaPdf(snapshot.DecimalesImporte), CultureInfo.CurrentCulture);

        private static XLAlignmentHorizontalValues ConvertirAlineacion(ReportTextAlignment a) => a switch
        {
            ReportTextAlignment.Centro => XLAlignmentHorizontalValues.Center,
            ReportTextAlignment.Derecha => XLAlignmentHorizontalValues.Right,
            ReportTextAlignment.Justificado => XLAlignmentHorizontalValues.Justify,
            _ => XLAlignmentHorizontalValues.Left,
        };

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

        private decimal CalcularTotalMO(Matriz matriz)
        {
            decimal total = 0;
            foreach (var comp in matriz.Componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra &&
                    comp.ManoDeObra != null && !comp.ManoDeObra.EsPorcentajeMO)
                    total += comp.Cantidad * comp.ManoDeObra.SalarioReal;
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar &&
                         comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                    total += comp.Cantidad * comp.Auxiliar.CostoDirecto;
            }
            return total;
        }

        private string LimpiarNombreHoja(string nombre)
        {
            var invalidos = new[] { ':', '\\', '/', '?', '*', '[', ']' };
            foreach (var c in invalidos)
                nombre = nombre.Replace(c, '-');
            return nombre.Length > 31 ? nombre[..31] : nombre;
        }
    }
}
