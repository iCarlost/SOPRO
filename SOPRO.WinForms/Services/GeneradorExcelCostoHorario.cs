using ClosedXML.Excel;
using ClosedXML.Excel.Drawings;
using SOPRO.Data.Context;
using SOPRO.Core.Entities;
using Sopro.Calculation.Equipment;
using System;
using System.Collections.Generic;
using System.Drawing;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera un reporte Excel de Análisis de Costo Horario.
    /// Una hoja por máquina, con el formato estándar de la industria.
    /// </summary>
    public static class GeneradorExcelCostoHorario
    {
        // Colores del encabezado estándar SOPRO
        private const string ColorEncabezado = "#33334C";
        private const string ColorSeccion = "#4A4A6A";
        private const string ColorSubtotal = "#E8EAF6";
        private const string ColorTotal = "#1A237E";
        private const string ColorFilaAlterna = "#F5F5F5";

        public static void Generar(XLWorkbook wb, IEnumerable<Maquinaria> lista, Proyecto proyecto, PlantillaReporte plantilla, ReporteService svc, ConfiguracionTituloReporte? tituloCfg = null)
        {
            foreach (var maq in lista)
                GenerarHoja(wb, maq, proyecto, plantilla, svc, tituloCfg);
        }

        private static void GenerarHoja(XLWorkbook wb, Maquinaria maq, Proyecto proyecto, PlantillaReporte plantilla, ReporteService svc, ConfiguracionTituloReporte? tituloCfg)
        {
            // Nombre de hoja: máximo 31 chars, sin caracteres inválidos
            string nombreHoja = LimpiarNombreHoja(maq.Clave ?? maq.Descripcion ?? "MAQ");
            var ws = wb.Worksheets.Add(nombreHoja);

            // Calcular valores derivados con la única implementación del motor.
            var result = HourlyCostCalculator.Calculate(new HourlyCostInput
            {
                AcquisitionValue = maq.ValorAdquisicion,
                TireValue = maq.ValorLlantas,
                SpecialPartsValue = maq.ValorPiezasEspeciales,
                SalvageFactor = maq.FactorRescate,
                EconomicLifeHours = maq.VidaEconomica,
                InterestRatePercentage = maq.TasaInteres,
                EffectiveHoursPerYear = maq.HorasEfectivasAnio,
                InsuranceRatePercentage = maq.PrimaSeguro,
                MaintenanceFactor = maq.FactorMantenimiento,
                FuelQuantity = maq.CantidadCombustible,
                FuelPrice = maq.PrecioCombustible,
                OilQuantity = maq.CantidadAceite,
                OilPrice = maq.PrecioAceite,
                TireLifeHours = maq.VidaEconomicaLlantas,
                SpecialPartsLifeHours = maq.VidaPiezasEspeciales,
                OperatorSalary = maq.SalarioOperador,
                RealSalaryFactor = maq.FactorSalarioReal,
                EffectiveHoursPerShift = maq.HorasEfectivasTurno
            });
            decimal valorNeto = result.NetValue;
            decimal valorRescate = result.SalvageValue;
            decimal depreciacion = result.Depreciation;
            decimal inversion = result.Investment;
            decimal seguros = result.Insurance;
            decimal mantenimiento = result.Maintenance;
            decimal totalCargosFijos = result.FixedChargesTotal;
            decimal combustibles = result.Fuel;
            decimal lubricantes = result.Lubricants;
            decimal llantas = result.Tires;
            decimal piezasEsp = result.SpecialParts;
            decimal totalConsumos = result.ConsumptionTotal;
            decimal salarioReal = result.RealSalary;
            decimal operacion = result.Operation;
            decimal costoHorario = result.HourlyCost;

            // ── Layout: columnas A-E ──────────────────────────────────────────
            ws.Column(1).Width = 22;  // Concepto / Clave
            ws.Column(2).Width = 32;  // Descripción / Fórmula
            ws.Column(3).Width = 18;  // Operaciones
            ws.Column(4).Width = 13;  // Resultado
            ws.Column(5).Width = 11;  // Total

            int f = 1;

            // ── Encabezado estándar SOPRO ─────────────────────────────────────
            f = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, 5, f, svc);

            // ── Título del análisis ───────────────────────────────────────────
            MergeEstilo(ws, f, 1, f, 5, ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "ANÁLISIS DE COSTO HORARIO DE MAQUINARIA Y EQUIPO"),
                ColorEncabezado, string.IsNullOrWhiteSpace(tituloCfg?.ColorTexto) ? "#FFFFFF" : tituloCfg.ColorTexto,
                tituloCfg?.TamanoFuente > 0 ? tituloCfg.TamanoFuente : 13,
                bold: tituloCfg?.Negrita ?? true, height: 26,
                fontName: string.IsNullOrWhiteSpace(tituloCfg?.NombreFuente) ? "Segoe UI" : tituloCfg.NombreFuente,
                italic: tituloCfg?.Cursiva ?? false);
            f++;

            // ── Identificación ────────────────────────────────────────────────
            FilaDato(ws, f++, "Descripción:", maq.Descripcion ?? "", height: 18);
            FilaDatosDos(ws, f++,
                "Clave:", maq.Clave ?? "",
                "Unidad:", "hora");
            FilaDatosDos(ws, f++,
                "Tipo de combustible:", maq.TipoCombustible.ToString(),
                "Potencia nominal:", $"{maq.PotenciaNominal:N2} hp");
            f++; // espacio

            // ── DATOS GENERALES ───────────────────────────────────────────────
            EncabezadoSeccion(ws, f++, "DATOS GENERALES");

            FilaDatosDos(ws, f++,
                "Vad = Valor de adquisición =", $"{maq.ValorAdquisicion:N2} $",
                "Ve = Vida económica =", $"{maq.VidaEconomica:N2} hrs");
            FilaDatosDos(ws, f++,
                "Pn = Valor de llantas =", $"{maq.ValorLlantas:N2} $",
                "Vn = Vida econ. llantas =", $"{maq.VidaEconomicaLlantas:N2} hrs");
            FilaDatosDos(ws, f++,
                "Pa = Valor piezas especiales =", $"{maq.ValorPiezasEspeciales:N2} $",
                "Va = Vida econ. piezas esp. =", $"{maq.VidaPiezasEspeciales:N2} hrs");
            FilaDatosDos(ws, f++,
                "Vm = Valor neto = Vad-Pn-Pa =", $"{valorNeto:N2} $",
                "Hea = Tiempo trabajado/año =", $"{maq.HorasEfectivasAnio:N2} hrs");
            FilaDatosDos(ws, f++,
                "r = Factor de rescate =", $"{maq.FactorRescate:N5}",
                "Gh = Cantidad combustible =", $"{maq.CantidadCombustible:N5} lts/hr");
            FilaDatosDos(ws, f++,
                "Vr = Valor de rescate = Vm*r =", $"{valorRescate:N2} $",
                "Ah = Cantidad de aceite =", $"{maq.CantidadAceite:N5} lts/hr");
            FilaDatosDos(ws, f++,
                "i = Tasa de interés =", $"{maq.TasaInteres:N4} % anual",
                "Pc = Precio combustible =", $"{maq.PrecioCombustible:N2} $/litro");
            FilaDatosDos(ws, f++,
                "s = Prima de seguros =", $"{maq.PrimaSeguro:N4} % anual",
                "Pac = Precio del aceite =", $"{maq.PrecioAceite:N2} $/litro");
            FilaDato(ws, f++,
                "Ko = Factor de mantenimiento =", $"{maq.FactorMantenimiento:N5}");
            f++; // espacio

            // ── Tabla de cálculos ─────────────────────────────────────────────
            // Encabezado tabla
            var hdr = ws.Range(f, 1, f, 5);
            hdr.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSeccion);
            hdr.Style.Font.FontColor = XLColor.White;
            hdr.Style.Font.Bold = true;
            hdr.Style.Font.FontSize = 9;
            ws.Cell(f, 1).Value = "Clave";
            ws.Cell(f, 2).Value = "Fórmula";
            ws.Cell(f, 3).Value = "Operaciones";
            ws.Cell(f, 4).Value = "Resultado";
            ws.Cell(f, 5).Value = "Total";
            foreach (var ci in new[] { 1, 2, 3, 4, 5 })
                ws.Cell(f, ci).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Row(f).Height = 18;
            f++;

            // ── A. CARGOS FIJOS ───────────────────────────────────────────────
            FilaSeccion(ws, f++, "Cargos Fijos");

            FilaCalculo(ws, f++, "D",
                "Depreciación: D = (Vm-Vr)/Ve",
                $"({valorNeto:N2}-{valorRescate:N2})/{maq.VidaEconomica:N2}",
                depreciacion);
            FilaCalculo(ws, f++, "Im",
                "Inversión: Im = [(Vm+Vr)/2Hea]·i",
                $"[({valorNeto:N2}+{valorRescate:N2})/2·{maq.HorasEfectivasAnio:N2}]·{maq.TasaInteres / 100:N6}",
                inversion);
            FilaCalculo(ws, f++, "Sm",
                "Seguros: Sm = [(Vm+Vr)/2Hea]·s",
                $"[({valorNeto:N2}+{valorRescate:N2})/2·{maq.HorasEfectivasAnio:N2}]·{maq.PrimaSeguro / 100:N6}",
                seguros);
            FilaCalculo(ws, f++, "Mn",
                "Mantenimiento: Mn = Ko·D",
                $"{maq.FactorMantenimiento:N5}·{depreciacion:N2}",
                mantenimiento);

            FilaSubtotal(ws, f++, "Total de Cargos Fijos", totalCargosFijos);

            // ── B. CONSUMOS ───────────────────────────────────────────────────
            FilaSeccion(ws, f++, "Consumos");

            FilaCalculo(ws, f++, "Co",
                "Combustibles: Co = Gh·Pc",
                $"{maq.CantidadCombustible:N5}·{maq.PrecioCombustible:N2}",
                combustibles);
            FilaCalculo(ws, f++, "Lb",
                "Lubricantes: Lb = Ah·Pac",
                $"{maq.CantidadAceite:N5}·{maq.PrecioAceite:N2}",
                lubricantes);

            if (maq.ValorLlantas > 0)
                FilaCalculo(ws, f++, "N",
                    "Llantas: N = Pn/Vn",
                    $"{maq.ValorLlantas:N2}/{maq.VidaEconomicaLlantas:N2}",
                    llantas);
            if (maq.ValorPiezasEspeciales > 0)
                FilaCalculo(ws, f++, "Pe",
                    "Piezas especiales: Pe = Pa/Va",
                    $"{maq.ValorPiezasEspeciales:N2}/{maq.VidaPiezasEspeciales:N2}",
                    piezasEsp);

            FilaSubtotal(ws, f++, "Total de Consumos", totalConsumos);

            // ── C. OPERACIÓN ──────────────────────────────────────────────────
            FilaSeccion(ws, f++, "Operación");

            FilaDato(ws, f++, "Sn = Salario tabulado =", $"${maq.SalarioOperador:N2}");
            FilaDato(ws, f++, "Fsr = Factor de salario real =", $"{maq.FactorSalarioReal:N5}");
            FilaDato(ws, f++, "Sr = Salario real = Sn·Fsr =", $"${salarioReal:N2}");
            FilaDato(ws, f++, "Ht = Horas efectivas/turno =", $"{maq.HorasEfectivasTurno:N2}");

            FilaCalculo(ws, f++, maq.Clave ?? "",
                "Po = Sr/Ht",
                $"{salarioReal:N2}/{maq.HorasEfectivasTurno:N2}",
                operacion);
            FilaSubtotal(ws, f++, "Total de Operación", operacion);

            // ── COSTO HORARIO TOTAL ───────────────────────────────────────────
            f++; // espacio
            var rTotal = ws.Range(f, 1, f, 4);
            rTotal.Merge();
            rTotal.Value = "COSTO HORARIO";
            rTotal.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorTotal);
            rTotal.Style.Font.FontColor = XLColor.White;
            rTotal.Style.Font.Bold = true;
            rTotal.Style.Font.FontSize = 11;
            rTotal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            rTotal.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

            var cTotal = ws.Cell(f, 5);
            cTotal.Value = costoHorario;
            cTotal.Style.NumberFormat.Format = "#,##0.00";
            cTotal.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorTotal);
            cTotal.Style.Font.FontColor = XLColor.White;
            cTotal.Style.Font.Bold = true;
            cTotal.Style.Font.FontSize = 11;
            cTotal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Row(f).Height = 22;

            // Borde general de la tabla de cálculos
            int filaInicioTabla = f - 1; // approximado, suficiente para el borde exterior
            ws.Range(f, 1, f, 5).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            // Notas al pie
            if (!string.IsNullOrWhiteSpace(maq.Notas))
            {
                f += 2;
                ws.Cell(f, 1).Value = "Notas:";
                ws.Cell(f, 1).Style.Font.Bold = true;
                ws.Range(f, 2, f, 5).Merge().Value = maq.Notas;
                ws.Range(f, 2, f, 5).Style.Alignment.WrapText = true;
                ws.Range(f, 2, f, 5).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                ws.Row(f).Height = CalcularAlturaFilaMerge(maq.Notas, 4, 9f, 18);
            }


            // Ajuste de impresión: una hoja vertical por equipo
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.CenterHorizontally = true;
            ws.PageSetup.Margins.Left = 0.30;
            ws.PageSetup.Margins.Right = 0.30;
            ws.PageSetup.Margins.Top = 0.35;
            ws.PageSetup.Margins.Bottom = 0.35;
            ws.PageSetup.Margins.Header = 0.15;
            ws.PageSetup.Margins.Footer = 0.15;
            ws.PageSetup.SetRowsToRepeatAtTop(1, Math.Max(1, f));
            ws.ShowGridLines = false;

            // Freeze encabezado
            ws.SheetView.FreezeRows(3);
        }

        // ── Helpers de formato ────────────────────────────────────────────────

        private static void MergeEstilo(IXLWorksheet ws, int r1, int c1, int r2, int c2,
            string texto, string bgColor, string fgColor, double fontSize,
            bool bold, double height, string fontName = "Segoe UI", bool italic = false)
        {
            var rng = ws.Range(r1, c1, r2, c2);
            rng.Merge();
            rng.Value = texto;
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(bgColor);
            rng.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(fgColor);
            rng.Style.Font.Bold = bold;
            rng.Style.Font.Italic = italic;
            rng.Style.Font.FontName = fontName;
            rng.Style.Font.FontSize = fontSize;
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rng.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(r1).Height = height;
        }

        private static void EncabezadoSeccion(IXLWorksheet ws, int f, string texto)
        {
            var rng = ws.Range(f, 1, f, 5);
            rng.Merge();
            rng.Value = texto;
            rng.Style.Fill.BackgroundColor = XLColor.FromHtml("#E3F2FD");
            rng.Style.Font.Bold = true;
            rng.Style.Font.FontSize = 9;
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Row(f).Height = 16;
        }

        private static void FilaDato(IXLWorksheet ws, int f, string etiqueta, string valor, double height = 16)
        {
            ws.Cell(f, 1).Value = etiqueta;
            ws.Cell(f, 1).Style.Font.Bold = true;
            ws.Cell(f, 1).Style.Font.FontSize = 9;
            var rngValor = ws.Range(f, 2, f, 5);
            rngValor.Merge();
            rngValor.Value = valor;
            rngValor.Style.Font.FontSize = 9;
            rngValor.Style.Alignment.WrapText = true;
            rngValor.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(f).Height = CalcularAlturaFilaMerge(valor, 4, 9f, height);
            ws.Range(f, 1, f, 5).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            ws.Range(f, 1, f, 5).Style.Border.BottomBorderColor = XLColor.LightGray;
        }

        private static void FilaDatosDos(IXLWorksheet ws, int f,
            string lbl1, string val1, string lbl2, string val2)
        {
            ws.Cell(f, 1).Value = lbl1;
            ws.Cell(f, 1).Style.Font.Bold = true;
            ws.Cell(f, 1).Style.Font.FontSize = 9;
            ws.Cell(f, 2).Value = val1;
            ws.Cell(f, 2).Style.Font.FontSize = 9;
            ws.Cell(f, 3).Value = lbl2;
            ws.Cell(f, 3).Style.Font.Bold = true;
            ws.Cell(f, 3).Style.Font.FontSize = 9;
            ws.Range(f, 4, f, 5).Merge().Value = val2;
            ws.Range(f, 4, f, 5).Style.Font.FontSize = 9;
            ws.Range(f, 4, f, 5).Style.Alignment.WrapText = true;
            ws.Range(f, 4, f, 5).Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            ws.Row(f).Height = Math.Max(CalcularAlturaFilaMerge(val1, 1, 9f, 16), CalcularAlturaFilaMerge(val2, 2, 9f, 16));
            ws.Range(f, 1, f, 5).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            ws.Range(f, 1, f, 5).Style.Border.BottomBorderColor = XLColor.LightGray;
        }

        private static void FilaSeccion(IXLWorksheet ws, int f, string texto)
        {
            var rng = ws.Range(f, 1, f, 5);
            rng.Merge();
            rng.Value = texto;
            rng.Style.Fill.BackgroundColor = XLColor.FromHtml("#ECEFF1");
            rng.Style.Font.Bold = true;
            rng.Style.Font.FontSize = 9;
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            ws.Row(f).Height = 16;
        }

        private static void FilaCalculo(IXLWorksheet ws, int f,
            string clave, string formula, string operaciones, decimal resultado)
        {
            ws.Cell(f, 1).Value = clave;
            ws.Cell(f, 1).Style.Font.FontSize = 9;
            ws.Cell(f, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(f, 2).Value = formula;
            ws.Cell(f, 2).Style.Font.FontSize = 9;

            ws.Cell(f, 3).Value = operaciones;
            ws.Cell(f, 3).Style.Font.FontSize = 9;
            ws.Cell(f, 3).Style.Font.Italic = true;
            ws.Cell(f, 3).Style.Font.FontColor = XLColor.FromHtml("#546E7A");

            ws.Cell(f, 4).Value = resultado;
            ws.Cell(f, 4).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(f, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(f, 4).Style.Font.FontSize = 9;

            ws.Row(f).Height = 15;
            ws.Range(f, 1, f, 5).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            ws.Range(f, 1, f, 5).Style.Border.BottomBorderColor = XLColor.LightGray;
        }

        private static void FilaSubtotal(IXLWorksheet ws, int f, string texto, decimal valor)
        {
            var rng = ws.Range(f, 1, f, 4);
            rng.Merge();
            rng.Value = texto;
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSubtotal);
            rng.Style.Font.Bold = true;
            rng.Style.Font.FontSize = 9;
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(f, 5).Value = valor;
            ws.Cell(f, 5).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(f, 5).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSubtotal);
            ws.Cell(f, 5).Style.Font.Bold = true;
            ws.Cell(f, 5).Style.Font.FontSize = 9;
            ws.Cell(f, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Row(f).Height = 17;
            ws.Range(f, 1, f, 5).Style.Border.TopBorder = XLBorderStyleValues.Thin;
            ws.Range(f, 1, f, 5).Style.Border.BottomBorder = XLBorderStyleValues.Thin;
        }

        private static string LimpiarNombreHoja(string nombre)
        {
            foreach (char c in new[] { '/', '\\', '?', '*', '[', ']', ':' })
                nombre = nombre.Replace(c, '_');
            if (nombre.Length > 31) nombre = nombre.Substring(0, 31);
            return nombre.Trim();
        }

        private static double CalcularAlturaFilaMerge(string texto, int columnasEquivalentes, float fontSize, double alturaBase)
        {
            if (string.IsNullOrWhiteSpace(texto)) return alturaBase;

            using var font = new System.Drawing.Font("Segoe UI", Math.Max(8f, fontSize), System.Drawing.FontStyle.Regular);
            int anchoPx = Math.Max(40, columnasEquivalentes * 64);
            var proposed = new System.Drawing.Size(anchoPx, int.MaxValue);
            var flags = System.Windows.Forms.TextFormatFlags.WordBreak | System.Windows.Forms.TextFormatFlags.TextBoxControl;
            var measured = System.Windows.Forms.TextRenderer.MeasureText(texto, font, proposed, flags);
            return Math.Max(alturaBase, measured.Height * 72.0 / 96.0 + 6);
        }

    }
}
