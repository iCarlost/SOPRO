using ClosedXML.Excel;
using SOPRO.Core.Entities;
using System;
using System.Collections.Generic;
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
    /// </summary>
    public class GeneradorExcelAPU
    {
        private readonly ReporteService _svc;
        private readonly SOPROContext _ctx;
        private ConfigColumnaReporte? _estiloDescripcionPresupuesto;

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
            List<ConfigColumnaReporte> columnas,
            string rutaDestino,
            ConfigColumnaReporte? estiloDescripcionPresupuesto = null)
        {
            _estiloDescripcionPresupuesto = estiloDescripcionPresupuesto;

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

            // Columnas visibles ordenadas (para la tabla de componentes)
            var colsVisibles = columnas
                .Where(c => c.Visible)
                .OrderBy(c => c.Orden)
                .ToList();

            using var wb = new XLWorkbook();

            // Índice general (primera hoja)
            var wsIndice = wb.AddWorksheet("Índice");
            EscribirIndice(wsIndice, conceptosConAPU, proyecto, plantilla, colsVisibles);

            // Una hoja por concepto
            int numero = 1;
            foreach (var concepto in conceptosConAPU)
            {
                if (!matrices.TryGetValue(concepto.MatrizId.Value, out var matriz))
                    continue;

                // Nombre de hoja: número + clave (máx 31 chars, sin caracteres inválidos)
                string nombreHoja = LimpiarNombreHoja($"{numero:D3}-{concepto.Clave ?? concepto.Descripcion}");
                var ws = wb.AddWorksheet(nombreHoja);

                EscribirAPU(ws, concepto, matriz, proyecto, plantilla, colsVisibles, numero);
                numero++;
            }

            wb.SaveAs(rutaDestino);
            return rutaDestino;
        }

        // ── ÍNDICE ────────────────────────────────────────────────────────────
        private void EscribirIndice(IXLWorksheet ws, List<ConceptoPresupuesto> conceptos,
            Proyecto proyecto, PlantillaReporte plantilla, List<ConfigColumnaReporte> cols)
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
                ws.Cell(fila, 5).Style.NumberFormat.Format = "#,##0.00";
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

        private ConfigColumnaReporte ObtenerEstiloBaseApu()
        {
            return _estiloDescripcionPresupuesto ?? new ConfigColumnaReporte
            {
                ConFuente = "Segoe UI",
                ConTamaño = 9f,
                ConNegrita = false,
                ConCursiva = false,
                ConColorTexto = "#000000",
                ConColorFondo = "#FFFFFF",
                ConAlineacion = "Izquierda"
            };
        }

        private void AplicarFuenteBase(IXLStyle style, bool boldOverride = false, string? colorOverride = null)
        {
            var baseStyle = ObtenerEstiloBaseApu();
            style.Font.FontName = string.IsNullOrWhiteSpace(baseStyle.ConFuente) ? "Segoe UI" : baseStyle.ConFuente;
            style.Font.FontSize = baseStyle.ConTamaño > 0 ? baseStyle.ConTamaño : 9f;
            style.Font.Bold = boldOverride || baseStyle.ConNegrita;
            style.Font.Italic = baseStyle.ConCursiva;
            style.Font.FontColor = ObtenerColorXL(string.IsNullOrWhiteSpace(colorOverride) ? (string.IsNullOrWhiteSpace(baseStyle.ConColorTexto) ? "#000000" : baseStyle.ConColorTexto) : colorOverride, "#000000");
        }

        // ── APU INDIVIDUAL ────────────────────────────────────────────────────
        private void EscribirAPU(IXLWorksheet ws, ConceptoPresupuesto concepto,
            Matriz matriz, Proyecto proyecto, PlantillaReporte plantilla,
            List<ConfigColumnaReporte> cols, int numero)
        {
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);

            // Anchos de columna fijos para APU (A-G)
            ws.Column(1).Width = 12;  // Tipo
            ws.Column(2).Width = 14;  // Clave
            ws.Column(3).Width = 50;  // Descripción
            ws.Column(4).Width = 10;  // Unidad
            ws.Column(5).Width = 14;  // Cantidad
            ws.Column(6).Width = 16;  // P.U. / Costo Unit.
            ws.Column(7).Width = 16;  // Importe

            int fila = 1;
            const int COLS = 7;

            // ── ENCABEZADO DEL PROYECTO ──────────────────────────────────────
            fila = EscribirEncabezadoProyecto(ws, proyecto, plantilla, numero, fila, COLS);

            // ── ENCABEZADO DEL APU ───────────────────────────────────────────
            fila = EscribirEncabezadoAPU(ws, concepto, fila, COLS);

            // ── TÍTULOS DE COLUMNAS ──────────────────────────────────────────
            fila = EscribirTitulosColumnas(ws, fila, COLS);
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
                fila = EscribirTituloSeccion(ws, fila, grupo.Titulo, grupo.Color, COLS);

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

                    // Fila del componente
                    ws.Cell(fila, 1).Value = grupo.Titulo[..Math.Min(3, grupo.Titulo.Length)]; // abreviatura
                    ws.Cell(fila, 2).Value = clave;
                    ws.Cell(fila, 3).Value = desc;
                    ws.Cell(fila, 4).Value = unidad;
                    ws.Cell(fila, 5).Value = cantidad;
                    ws.Cell(fila, 6).Value = pu;
                    ws.Cell(fila, 7).Value = importeComp;

                    AplicarEstiloFilaDato(ws, fila, COLS, grupo.Color, numComp);
                    fila++;
                    numComp++;
                }

                // Subtotal de sección
                fila = EscribirSubtotal(ws, fila, $"Subtotal {grupo.Titulo}", subtotal, COLS);
                costoDirecto += subtotal;
            }

            // ── RESUMEN DE INTEGRACIÓN DEL PU ────────────────────────────────
            fila++;
            fila = EscribirResumenIntegracion(ws, fila, concepto, proyecto, costoDirecto, COLS);

            // Congelar hasta títulos de columna
            ws.SheetView.FreezeRows(filaCongelar - 1);
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
            rngNum.Style.Font.FontSize = Math.Max(10, ObtenerEstiloBaseApu().ConTamaño);
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

            if (tipo == "Imagen" && System.IO.File.Exists(contenido))
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
            int fila, int COLS)
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
            rngCantLbl.Value = $"Cantidad: {concepto.Cantidad:N3} {concepto.Unidad}";
            rngCantLbl.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var rngPULbl = ws.Range(fila, midCol + 1, fila, midCol * 2); rngPULbl.Merge();
            rngPULbl.Value = $"P.U.: ${puConcepto:N2}";
            rngPULbl.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var rngTotLbl = ws.Range(fila, midCol * 2 + 1, fila, COLS); rngTotLbl.Merge();
            rngTotLbl.Value = $"Total: ${totalConcepto:N2}";
            rngTotLbl.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            AplicarEstiloEncabezadoInfo(ws, fila, COLS);
            fila++;

            return fila;
        }

        // ── TÍTULOS DE COLUMNAS ───────────────────────────────────────────────
        private int EscribirTitulosColumnas(IXLWorksheet ws, int fila, int COLS)
        {
            var titulos = new[] { "TIPO", "CLAVE", "DESCRIPCIÓN", "UNIDAD", "CANTIDAD", "COSTO UNIT.", "IMPORTE" };
            for (int c = 0; c < titulos.Length; c++)
            {
                var cell = ws.Cell(fila, c + 1);
                cell.Value = titulos[c];
                AplicarFuenteBase(cell.Style, boldOverride: true, colorOverride: "#FFFFFF");
                cell.Style.Fill.BackgroundColor = XLColor.FromHtml("#1565C0");
                cell.Style.Alignment.Horizontal = (c == 2) ? XLAlignmentHorizontalValues.Left : XLAlignmentHorizontalValues.Center;
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

        // ── FILA DE DATO ──────────────────────────────────────────────────────
        private void AplicarEstiloFilaDato(IXLWorksheet ws, int fila, int COLS, string colorSeccion, int numComp)
        {
            // Alternas: color de sección vs blanco
            string fondo = (numComp % 2 == 0) ? colorSeccion : "#FFFFFF";

            for (int c = 1; c <= COLS; c++)
            {
                var cell = ws.Cell(fila, c);
                cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                AplicarFuenteBase(cell.Style);
                cell.Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                cell.Style.Border.RightBorder = XLBorderStyleValues.Hair;
            }

            ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(fila, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(fila, 5).Style.NumberFormat.Format = "#,##0.00000";
            ws.Cell(fila, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(fila, 6).Style.NumberFormat.Format = "#,##0.0000";
            ws.Cell(fila, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(fila, 7).Style.NumberFormat.Format = "#,##0.0000";
            ws.Cell(fila, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
        }

        // ── SUBTOTAL ──────────────────────────────────────────────────────────
        private int EscribirSubtotal(IXLWorksheet ws, int fila, string etiqueta, decimal valor, int COLS)
        {
            var rng = ws.Range(fila, 1, fila, COLS - 1);
            rng.Merge();
            rng.Value = etiqueta;
            AplicarFuenteBase(rng.Style, boldOverride: true);
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSubtotal);
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(fila, COLS).Value = valor;
            ws.Cell(fila, COLS).Style.NumberFormat.Format = "#,##0.0000";
            AplicarFuenteBase(ws.Cell(fila, COLS).Style, boldOverride: true);
            ws.Cell(fila, COLS).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSubtotal);
            ws.Cell(fila, COLS).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Range(fila, 1, fila, COLS).Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            return fila + 1;
        }

        // ── RESUMEN DE INTEGRACIÓN ─────────────────────────────────────────────
        private int EscribirResumenIntegracion(IXLWorksheet ws, int fila,
            ConceptoPresupuesto concepto, Proyecto proyecto, decimal costoDirecto, int COLS)
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

            foreach (var linea in integracion.Lineas)
            {
                var rngE = ws.Range(fila, 1, fila, 4);
                rngE.Merge();
                rngE.Value = linea.Etiqueta;
                rngE.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                rngE.Style.Alignment.Indent = 2;

                ws.Cell(fila, 5).Value = linea.PorcentajeTexto;
                ws.Cell(fila, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                var rngM = ws.Range(fila, 6, fila, COLS);
                rngM.Merge();
                rngM.Value = linea.Monto;
                rngM.Style.NumberFormat.Format = "#,##0.0000";
                rngM.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

                ws.Range(fila, 1, fila, COLS).Style.Fill.BackgroundColor = XLColor.FromHtml("#FAFAFA");
                ws.Range(fila, 1, fila, COLS).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
                for (int c = 1; c <= COLS; c++) AplicarFuenteBase(ws.Cell(fila, c).Style);
                fila++;
            }

            // Precio Unitario Final
            var rngPU = ws.Range(fila, 1, fila, 5);
            rngPU.Merge();
            rngPU.Value = $"PRECIO UNITARIO  (Unidad: {concepto.Unidad})";
            AplicarFuenteBase(rngPU.Style, boldOverride: true);
            rngPU.Style.Font.FontSize = Math.Max(11, ObtenerEstiloBaseApu().ConTamaño);
            rngPU.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            rngPU.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorPU);

            var rngPUV = ws.Range(fila, 6, fila, COLS);
            rngPUV.Merge();
            rngPUV.Value = integracion.PrecioUnitario;
            rngPUV.Style.NumberFormat.Format = "#,##0.00";
            AplicarFuenteBase(rngPUV.Style, boldOverride: true);
            rngPUV.Style.Font.FontSize = Math.Max(11, ObtenerEstiloBaseApu().ConTamaño);
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
