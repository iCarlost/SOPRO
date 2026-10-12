using ClosedXML.Excel;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SOPRO.Application.Models.Reporting.Fsr;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using Sopro.Calculation.Labor;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera reportes Excel del Factor de Salario Real.
    /// AE-2(A): Tabla de cálculo detallada (parámetros y fórmulas).
    /// AE-2(C): Tabulador desglosado por cada insumo de mano de obra.
    ///
    /// Ambas rutas consumen el snapshot neutral <see cref="FsrReportSnapshotBuilder"/>
    /// (compartido con PDF): los encabezados, anchos, alineaciones y formatos salen
    /// del contrato, no de literales '$'/'0.00000'. Las filas viajan como modelos
    /// neutrales (<see cref="FsrRow"/> y <see cref="FsrTabuladorRow"/>), extraídos
    /// de los generadores.
    /// </summary>
    public static class GeneradorExcelFSR
    {
        private const string ColorEncabezado  = "#33334C";
        private const string ColorSeccion     = "#4A4A6A";
        private const string ColorSubtotal    = "#E8EAF6";
        private const string ColorTotal       = "#1A237E";
        private const string ColorFilaAlterna = "#F5F5F5";
        private const string ColorTituloTabla = "#C5CAE9";

        // ─────────────────────────────────────────────────────────────────────
        // Tabla de cálculo del FSR (parámetros + fórmulas)
        // ─────────────────────────────────────────────────────────────────────
        public static void GenerarAE2A(XLWorkbook wb, Proyecto proyecto,
            PlantillaReporte plantilla, ReporteService svc, ReportColumnSnapshot snapshot, ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (string.IsNullOrEmpty(proyecto.ParametrosFSR)) return;

            var p = JsonSerializer.Deserialize<Dictionary<string, string>>(proyecto.ParametrosFSR);
            if (p == null) return;

            decimal Get(string key, decimal def = 0)
                => p.TryGetValue(key, out var v) && decimal.TryParse(v,
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : def;

            // Reconstruir cálculo completo para tener todas las variables intermedias
            var c = RecalcularCompleto(p);

            var cols = snapshot.Columnas.Where(x => x.Visible).OrderBy(x => x.Orden).ToList();
            if (cols.Count == 0)
                cols = FsrReportSnapshotBuilder.DefaultColumnsAE2A().Where(x => x.Visible).OrderBy(x => x.Orden).ToList();
            var valorCol = cols.FirstOrDefault(x => string.Equals(x.Identificador, FsrReportSnapshotBuilder.ColValor, StringComparison.OrdinalIgnoreCase))
                           ?? FsrReportSnapshotBuilder.DefaultColumnsAE2A()
                               .Single(x => string.Equals(x.Identificador, FsrReportSnapshotBuilder.ColValor, StringComparison.OrdinalIgnoreCase));
            string formatoValor = ReportColumnGridFormat.ResolveExcelFormat(valorCol, snapshot);
            int totalCols = cols.Count;

            var ws = wb.Worksheets.Add("Cálculo FSR");

            // Columnas: anchos desde el contrato neutral (px → unidades Excel)
            for (int i = 0; i < totalCols; i++)
                ws.Column(i + 1).Width = ReportColumnWidthConverter.PxToExcelWidth(cols[i].Ancho);

            int f = 1;
            f = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, totalCols, f, svc);

            // Título tabla
            Merge(ws, f, 1, f, totalCols, string.Empty,
                ColorEncabezado, "#FFFFFF", 11, bold: true, height: 22);
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(ws.Range(f, 1, f, totalCols), tituloCfg, "TABLA DE CALCULO DEL FACTOR DE SALARIO REAL", ColorEncabezado);
            f++;

            // Encabezado columnas (desde el snapshot neutral)
            for (int i = 0; i < totalCols; i++)
            {
                var enc = cols[i].EstiloEncabezado;
                var hc = ws.Cell(f, i + 1);
                hc.Value = cols[i].Encabezado ?? string.Empty;
                hc.Style.Font.Bold = enc.Negrita;
                hc.Style.Font.Italic = enc.Cursiva;
                if (!string.IsNullOrEmpty(enc.Fuente))
                    hc.Style.Font.FontName = enc.Fuente;
                if (enc.Tamano > 0)
                    hc.Style.Font.FontSize = enc.Tamano;
                hc.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(enc.ColorFondo ?? ColorSeccion);
                hc.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(enc.ColorFuente, "#FFFFFF");
                hc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hc.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            ws.Row(f).Height = 16;
            f++;

            int primeraFilaDatos = f;
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, f - 1);

            // Clave / Descripción general
            FilaInfoFSR(ws, f++, "Clave : JOR8HR");
            FilaInfoFSR(ws, f++, "Descripción : Factor de Salario Real FSR");
            f++; // espacio

            // ── DATOS BASICOS ─────────────────────────────────────────────────
            SeccionFSR(ws, f++, "DATOS BASICOS");

            SubseccionFSR(ws, f++, "Para el cálculo de días pagados");
            FilaFSR(ws, f++, "Días Calendario (DC)",          "", "días", Get("DiasCalendario", 365));
            FilaFSR(ws, f++, "Días Aguinaldo",                "", "días", Get("DiasAguinaldo", 15));
            FilaFSR(ws, f++, "Días de vacaciones para calcular prima vacacional", "", "días", Get("DiasVacaciones", 12));
            FilaFSR(ws, f++, "Prima vacacional",              "", "%",    Get("PrimaVacacional", 25));
            FilaFSR(ws, f++, "Otros días",                    "", "días", Get("OtrosDiasPagados", 0));
            FilaFSR(ws, f++, "Días de Descanso (Ley Federal del Trabajo)", "", "días", Get("DiasDescanso", 52));
            FilaFSR(ws, f++, "Festivos oficiales (Ley Federal del Trabajo)", "", "días", Get("DiasFestivos", 7));
            FilaFSR(ws, f++, "Días no laborables según contrato colectivo", "", "días", Get("DiasContrato", 0));
            FilaFSR(ws, f++, "Días Sindicato",                "", "días", Get("DiasSindicato", 0));
            FilaFSR(ws, f++, "Enfermedad no profesional",     "", "días", Get("DiasEnfermedad", 0));
            FilaFSR(ws, f++, "Condiciones Climat. (Lluvias y otros) Contr. Colec", "", "días", Get("DiasClima", 0));
            FilaFSR(ws, f++, "Otros Días no trabajados por costumbre", "", "días", Get("OtrosDiasNL", 0));

            SubseccionFSR(ws, f++, "Para el calculo de cuotas del IMSS");
            FilaFSR(ws, f++, "Guarderías",         "", "%", Get("PctGuarderias", 1));
            FilaFSR(ws, f++, "Retiro",             "", "%", Get("PctRetiro", 2));
            FilaFSR(ws, f++, "Riesgos de trabajo", "", "%", Get("PctRiesgos", 4.58875m));
            FilaFSR(ws, f++, "Impuesto INFONAVIT", "", "%", Get("PctINFONAVIT", 5));
            FilaFSR(ws, f++, "Impuesto Nómina",    "", "%", Get("PctNomina", 2.4m));
            FilaFSR(ws, f++, "Otros impuestos",    "", "%", Get("OtrosImpuestos", 0));

            // ── CALCULO ───────────────────────────────────────────────────────
            f++;
            SeccionFSR(ws, f++, "CALCULO");

            SubseccionFSR(ws, f++, "De datos básicos a utilizar");
            FilaFSR(ws, f++, "Salario Mínimo General (D.F.)", "", "", c.FSR_SAMI);
            FilaFSR(ws, f++, "Salario Nominal por jornada (SND)", "", "", c.FSR_SACAL);

            SubseccionFSR(ws, f++, "De días realmente pagados y SBC");
            FilaFSR(ws, f++, "Vacaciones",    "", "días", c.FSR_DVAC);
            FilaFSR(ws, f++, "Prima vacacional", "", "días", c.FSR_DPPVA);
            FilaFSR(ws, f++, "Prima Dominical",  "", "días", c.FSR_DPPDO);
            FilaFSR(ws, f++, "Días equivalentes por horas extras al año", "", "días", c.FSR_DPHEX);
            FilaFSR(ws, f++, "SUMA de días pagados",    "", "días", c.FSR_DPA);
            FilaFSR(ws, f++, "SUMA de días no laborados", "", "días", c.FSR_DNLA);
            FilaFSROperacion(ws, f++, "Días realmente laborados  (TL = DC - DNLA)",
                $"{c.FSR_DPCAL:N6}días-{c.FSR_DNLA:N6}días", "días", c.FSR_DLA);
            FilaFSR(ws, f++, "TP/TL", "", "", c.FSR_FSI);
            FilaFSROperacion(ws, f++, "(FSBC = DPA/DPCAL)",
                $"{c.FSR_DPA:N6}días/{c.FSR_DPCAL:N6}días", "", c.FSR_FSBC);
            FilaFSROperacion(ws, f++, "Salario Base de Cotización (SB = FSBC * SN)",
                $"{c.FSR_SACAL:N6} * {c.FSR_FSBC:N6}", "", c.FSR_SABC);

            SubseccionFSR(ws, f++, "De cuotas del IMSS");
            FilaFSR(ws, f++, "Porcentaje sobre salario mínimo para cuota fija", "", "%", c.AA);
            FilaFSR(ws, f++, "Porcentaje para Excedente a 3 SMGDF", "", "%", c.AB);
            FilaFSR(ws, f++, "Excedente de 3 SMGDF", "", "", c.AU);
            FilaFSROperacion(ws, f++, "Prestaciones en dinero (Patron+obrero)",
                $".7+IIF({c.FSR_SACAL:N6}>1.000000,0,0.25)", "%", c.FSR_IMPE_p);
            FilaFSROperacion(ws, f++, "Gastos medicos. Pensionados (Patrón-Obrero)",
                $"1.05+IIF({c.FSR_SACAL:N6}>1.000000,0,0.375)", "%", c.FSR_IMGM_p);
            FilaFSROperacion(ws, f++, "Invalidez y vida",
                $"1.75+IIF({c.FSR_SACAL:N6}>1.000000,0,0.625)", "%", c.FSR_IMINV_p);
            FilaFSROperacion(ws, f++, "Cesantía en edad avanzada y vejez",
                $"3.15+IIF({c.FSR_SACAL:N6}>1.000000,0,1.125)", "%", c.FSR_IMCE_p);
            FilaFSR(ws, f++, "Límite de prest. Inv., vida, cesantía y vejez", "", "", c.AS_lim);
            FilaFSR(ws, f++, "Enfermedad y maternidad. Cuota fija especie", "", "", c.AC);
            FilaFSR(ws, f++, "Enferm.-matern. Exc. a 3 S.M.D.F. especie",    "", "", c.AD);

            // Página 2 — continuación
            FilaFSR(ws, f++, "Enfermedad y maternidad. Prestaciones en dinero", "", "", c.AE);
            FilaFSR(ws, f++, "Enfermedad y maternidad gastos médicos pensionados", "", "", c.AF);
            FilaFSR(ws, f++, "Invalidez y vida",  "", "", c.AG);
            FilaFSR(ws, f++, "Guarderías",         "", "", c.AH);
            FilaFSR(ws, f++, "Retiro",              "", "", c.AI);
            FilaFSR(ws, f++, "Cesantía en edad avanzada y vejez", "", "", c.AJ);
            FilaFSR(ws, f++, "Riesgos de trabajo", "", "", c.AK);
            FilaFSR(ws, f++, "Cuota patronal del IMSS", "", "", c.AL);
            FilaFSROperacion(ws, f++, "Factor de cuota patronal del IMSS = IMSS/SND",
                $"{c.AL:N6}/{c.FSR_SACAL:N6}", "factor", c.FSR_IMIMS);

            SubseccionFSR(ws, f++, "De INFONAVIT y otras cuotas");
            FilaFSRSinVal(ws, f++, "Limite de Aportaciones INFONAVIT", "", "", $"{c.AZ:N0}");
            FilaFSR(ws, f++, "INFONAVIT",              "", "", c.AM);
            FilaFSR(ws, f++, "Impuesto sobre Nómina",  "", "", c.AN);
            FilaFSR(ws, f++, "Otros impuestos",         "", "", c.AO);
            FilaFSR(ws, f++, "Obligaciones patronales (IOP)", "", "", c.AP);
            FilaFSROperacion(ws, f++, "Obligaciones patronales entre SN",
                $"{c.AP:N6}/{c.FSR_SACAL:N6}", "", c.AQ);

            SubseccionFSR(ws, f++, "Del TP/TL y del FSR");
            FilaFSROperacion(ws, f++, "FSR = Ps (Tp/Tl) + Tp/Tl",
                $"{c.BH:N6}+{c.FSR_FSI:N6}", "", c.FSR_FSR);

            // Fila total destacada
            f++;
            var rFinal = ws.Range(f, 1, f, 3);
            rFinal.Merge();
            rFinal.Value = "FACTOR DE SALARIO REAL";
            rFinal.Style.Font.Bold = true;
            rFinal.Style.Font.FontSize = 12;
            rFinal.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorTotal);
            rFinal.Style.Font.FontColor = XLColor.White;
            rFinal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rFinal.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            var vFinal = ws.Cell(f, 4);
            vFinal.Value = c.FSR_FSR;
            vFinal.Style.NumberFormat.Format = formatoValor;
            vFinal.Style.Font.Bold = true;
            vFinal.Style.Font.FontSize = 12;
            vFinal.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorTotal);
            vFinal.Style.Font.FontColor = XLColor.White;
            vFinal.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            vFinal.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
            ws.Row(f).Height = 22;

            // Paridad de formato del valor: todas las celdas numéricas de la columna
            // Valor usan el formato del contrato neutral (cantidad → decimales de
            // cantidad), eliminando el N5/`$#,##0.00` hardcodeado.
            for (int r = primeraFilaDatos; r <= f; r++)
            {
                var celdaValor = ws.Cell(r, 4);
                if (celdaValor.DataType == XLDataType.Number)
                    celdaValor.Style.NumberFormat.Format = formatoValor;
            }

            // Bordes generales
            AplicarBordes(ws, 1, f);

            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.SetRowsToRepeatAtTop(1, 4);
            ws.PageSetup.CenterHorizontally = true;
            ws.PageSetup.Margins.Top = 0.60;
            ws.PageSetup.Margins.Bottom = 0.45;
            ws.PageSetup.Margins.Left = 0.30;
            ws.PageSetup.Margins.Right = 0.30;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Tabulador desglosado por insumo de mano de obra
        // ─────────────────────────────────────────────────────────────────────
        public static void GenerarAE2C(XLWorkbook wb, Proyecto proyecto,
            List<ManoDeObra> manoDeObras, PlantillaReporte plantilla, ReporteService svc, ReportColumnSnapshot snapshot)
        {
            if (string.IsNullOrEmpty(proyecto.ParametrosFSR)) return;

            var p = JsonSerializer.Deserialize<Dictionary<string, string>>(proyecto.ParametrosFSR);
            if (p == null) return;

            var cols = snapshot.Columnas.Where(x => x.Visible).OrderBy(x => x.Orden).ToList();
            if (cols.Count == 0)
                cols = FsrReportSnapshotBuilder.DefaultColumnsAE2C().Where(x => x.Visible).OrderBy(x => x.Orden).ToList();
            int totalCols = cols.Count;

            var ws = wb.Worksheets.Add("Tabulador FSR");

            for (int i = 0; i < totalCols; i++)
                ws.Column(i + 1).Width = ReportColumnWidthConverter.PxToExcelWidth(cols[i].Ancho);

            int f = 1;
            f = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, totalCols, f, svc);

            // Título
            Merge(ws, f, 1, f, totalCols, "TABLA DE CÁLCULO DEL FACTOR DE SALARIO REAL",
                ColorEncabezado, "#FFFFFF", 11, bold: true, height: 22);
            f++;

            // Encabezados desde el snapshot neutral
            for (int i = 0; i < totalCols; i++)
            {
                var enc = cols[i].EstiloEncabezado;
                var hc = ws.Cell(f, i + 1);
                hc.Value = cols[i].Encabezado ?? string.Empty;
                hc.Style.Font.Bold = enc.Negrita;
                hc.Style.Font.Italic = enc.Cursiva;
                if (!string.IsNullOrEmpty(enc.Fuente))
                    hc.Style.Font.FontName = enc.Fuente;
                if (enc.Tamano > 0)
                    hc.Style.Font.FontSize = enc.Tamano;
                hc.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(enc.ColorFondo ?? ColorSeccion);
                hc.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(enc.ColorFuente, "#FFFFFF");
                hc.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                hc.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                hc.Style.Alignment.WrapText = true;
                hc.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            }
            ws.Row(f).Height = 46;
            f++;

            int filaDatos = f;
            int filasEncabezado = filaDatos - 1;

            // Datos — FSR individual por insumo; valores crudos formateados por el contrato
            var filas = CalcularFilasAE2C(proyecto, manoDeObras);
            bool alt = false;
            foreach (var r in filas)
            {
                string fondo = alt ? ColorFilaAlterna : "#FFFFFF";
                alt = !alt;

                for (int i = 0; i < totalCols; i++)
                {
                    var def = cols[i];
                    var cell = ws.Cell(f, i + 1);
                    cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(fondo);
                    cell.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    cell.Style.Border.OutsideBorderColor = XLColor.FromHtml("#BDBDBD");
                    cell.Style.Font.FontSize = 8;
                    cell.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;

                    if (i == 0)
                    {
                        cell.Value = r.Clave ?? string.Empty;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                    }
                    else if (i == 1)
                    {
                        cell.Value = r.Descripcion ?? string.Empty;
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                    }
                    else if (!r.SinCalculo && r.Valores.Count > i - 2)
                    {
                        cell.Value = r.Valores[i - 2];
                        cell.Style.NumberFormat.Format = ReportColumnGridFormat.ResolveExcelFormat(def, snapshot);
                        cell.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
                    }
                }
                ws.Row(f).Height = 15;
                f++;
            }

            // Ajuste final
            ReporteEncabezadoHelper.ConfigurarFilasRepetidas(ws, 1, filasEncabezado);
            ws.SheetView.FreezeRows(filasEncabezado);
        }

        /// <summary>
        /// Calcula las filas del Tabulador FSR (AE-2C) como modelos neutrales: clave
        /// y descripción textuales más los 19 valores numéricos crudos alineados con
        /// las columnas del snapshot (los renderizadores aplican el formato del
        /// contrato). Sustituye al DTO preformateado <c>AE2CRowData</c>.
        /// </summary>
        public static List<FsrTabuladorRow> CalcularFilasAE2C(Proyecto proyecto, List<ManoDeObra> manoDeObras)
        {
            var resultado = new List<FsrTabuladorRow>();
            if (proyecto == null || string.IsNullOrEmpty(proyecto.ParametrosFSR) || manoDeObras == null)
                return resultado;

            var p = JsonSerializer.Deserialize<Dictionary<string, string>>(proyecto.ParametrosFSR);
            if (p == null) return resultado;

            foreach (var mo in manoDeObras.Where(m => m.SalarioBase > 0).OrderBy(m => m.Clave))
            {
                var c = RecalcularCompleto(p, mo.SalarioBase);
                resultado.Add(new FsrTabuladorRow
                {
                    Clave = mo.Clave ?? string.Empty,
                    Descripcion = mo.Descripcion ?? string.Empty,
                    Valores = new[]
                    {
                        mo.SalarioBase,   // Sal. Base M.N.
                        c.FSR_SACAL,      // Salario Nominal
                        c.FSR_FSBC,       // Factor SBC
                        c.FSR_SABC,       // Salario Base de Cotización
                        c.AC,             // Cuota fija
                        c.AD,             // Excedente a 3 SMGDF
                        c.AE + c.AF,      // Prestaciones en especie (gastos médicos)
                        c.AE,             // Prestaciones en dinero
                        c.AG,             // Invalidez y vida
                        c.AH,             // Guarderías
                        c.AJ,             // Cesantía y vejez
                        c.AI,             // Retiro
                        c.AL,             // Suma cuotas IMSS
                        c.AM,             // INFONAVIT
                        c.AP,             // Suma prest. patronales IMSS+INFONAVIT
                        c.AQ,             // Obligaciones obrero patronales PS
                        c.FSR_FSI,        // Factor empresa TP/TL
                        mo.FactorSalarioReal, // FSR (guardado en BD)
                        mo.SalarioReal        // Salario Real
                    }
                });
            }

            foreach (var mo in manoDeObras.Where(m => m.SalarioBase <= 0).OrderBy(m => m.Clave))
            {
                resultado.Add(new FsrTabuladorRow
                {
                    Clave = mo.Clave ?? string.Empty,
                    Descripcion = mo.Descripcion ?? string.Empty,
                    SinCalculo = true
                });
            }

            return resultado;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Recálculo completo — devuelve struct con todas las variables
        // ─────────────────────────────────────────────────────────────────────
        private static FSRCalc RecalcularCompleto(Dictionary<string, string> p, decimal? overrideSN = null)
        {
            decimal SN = overrideSN ?? (p.TryGetValue("SalarioNominal", out var snV)
                && decimal.TryParse(snV, System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var snD) ? snD : 0m);

            var input = FsrInputAdapter.FromDictionary(p, SN);
            var b = RealSalaryFactorCalculator.Calculate(input);

            return new FSRCalc
            {
                FSR_SAMI  = b.MinimumSalaryUnit,
                FSR_SACAL = b.AdjustedNominalSalaryInMinimumSalaryUnits,
                FSR_DPCAL = b.CalendarDays,
                FSR_DVAC  = b.VacationDays,
                FSR_DPPVA = b.VacationPremiumDays,
                FSR_DPPDO = b.SundayPremiumEquivalentDays,
                FSR_DPHEX = b.OvertimeEquivalentDays,
                FSR_DPA   = b.PaidDays,
                FSR_DNLA  = b.NonWorkingDays,
                FSR_DLA   = b.WorkedDays,
                FSR_FSI   = b.PaidToWorkedDaysFactor,
                FSR_FSBC  = b.ContributionBaseFactor,
                FSR_SABC  = b.ContributionBaseSalaryInMinimumSalaryUnits,
                AA        = b.FixedFeePercentage,
                AB        = b.ExcessPercentage,
                AU        = b.ThreeMinimumSalaryExcess,
                AS_lim    = b.LifeAndRetirementCap,
                AZ        = b.InfonavitSalaryLimit,
                FSR_IMPE_p  = b.CashBenefitsPercentage,
                FSR_IMGM_p  = b.PensionerMedicalExpensesPercentage,
                FSR_IMINV_p = b.DisabilityAndLifePercentage,
                FSR_IMCE_p  = b.SeveranceAndOldAgePercentage,
                AC        = b.FixedFee,
                AD        = b.ThreeMinimumSalaryExcessContribution,
                AE        = b.CashBenefitsContribution,
                AF        = b.PensionerMedicalExpensesContribution,
                AG        = b.DisabilityAndLifeContribution,
                AH        = b.DaycareContribution,
                AI        = b.RetirementContribution,
                AJ        = b.SeveranceAndOldAgeContribution,
                AK        = b.OccupationalRiskContribution,
                AL        = b.EmployerImssTotal,
                FSR_IMIMS = b.EmployerImssFactor,
                AM        = b.InfonavitContribution,
                AN        = b.PayrollTax,
                AO        = b.OtherTaxes,
                AP        = b.EmployerObligations,
                AQ        = b.EmployerObligationsFactor,
                BH        = b.EmployerObligationsWeightedByPaidToWorkedDays,
                FSR_FSR   = b.Factor
            };
        }

        private class FSRCalc
        {
            public decimal FSR_SAMI, FSR_SACAL, FSR_DPCAL;
            public decimal FSR_DVAC, FSR_DPPVA, FSR_DPPDO, FSR_DPHEX;
            public decimal FSR_DPA, FSR_DNLA, FSR_DLA;
            public decimal FSR_FSI, FSR_FSBC, FSR_SABC;
            public decimal AA, AB, AU, AS_lim, AZ;
            public decimal FSR_IMPE_p, FSR_IMGM_p, FSR_IMINV_p, FSR_IMCE_p;
            public decimal AC, AD, AE, AF, AG, AH, AI, AJ, AK, AL;
            public decimal FSR_IMIMS;
            public decimal AM, AN, AO, AP, AQ;
            public decimal BH, FSR_FSR;
        }

        // ─────────────────────────────────────────────────────────────────────
        // Helpers de estilo
        // ─────────────────────────────────────────────────────────────────────
        private static void Merge(IXLWorksheet ws, int r1, int c1, int r2, int c2, string texto,
            string bgHex, string fgHex, double fontSize = 10, bool bold = false, double height = 18)
        {
            var rng = ws.Range(r1, c1, r2, c2);
            rng.Merge();
            rng.Value = texto;
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(bgHex);
            rng.Style.Font.FontColor = ExcelColorHelper.SafeFromHtml(fgHex);
            rng.Style.Font.Bold = bold;
            rng.Style.Font.FontSize = fontSize;
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            rng.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
            rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Row(r1).Height = height;
        }

        private static void SeccionFSR(IXLWorksheet ws, int f, string titulo)
        {
            var rng = ws.Range(f, 1, f, 4);
            rng.Merge();
            rng.Value = titulo;
            rng.Style.Font.Bold = true;
            rng.Style.Font.FontSize = 10;
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSeccion);
            rng.Style.Font.FontColor = XLColor.White;
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Row(f).Height = 16;
        }

        private static void SubseccionFSR(IXLWorksheet ws, int f, string titulo)
        {
            var rng = ws.Range(f, 1, f, 4);
            rng.Merge();
            rng.Value = titulo;
            rng.Style.Font.Bold = true;
            rng.Style.Font.FontSize = 9;
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorTituloTabla);
            rng.Style.Font.FontColor = XLColor.FromHtml("#1A237E");
            rng.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
            rng.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
            ws.Row(f).Height = 15;
        }

        // Fila info (solo texto, sin valor)
        private static void FilaInfoFSR(IXLWorksheet ws, int f, string texto)
        {
            var rng = ws.Range(f, 1, f, 4);
            rng.Merge();
            rng.Value = texto;
            rng.Style.Font.Italic = true;
            rng.Style.Font.FontSize = 9;
            ws.Row(f).Height = 14;
        }

        // Fila con descripción + unidad + valor decimal
        private static void FilaFSR(IXLWorksheet ws, int f, string desc, string op, string unidad, decimal valor)
        {
            ws.Cell(f, 1).Value = desc;
            ws.Cell(f, 2).Value = op;
            ws.Cell(f, 3).Value = unidad;
            ws.Cell(f, 4).Value = valor;
            ws.Cell(f, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            EstiloFilaFSR(ws, f);
        }

        // Fila con descripción + operación + unidad + valor decimal
        private static void FilaFSROperacion(IXLWorksheet ws, int f, string desc, string op, string unidad, decimal valor)
        {
            ws.Cell(f, 1).Value = desc;
            ws.Cell(f, 2).Value = op;
            ws.Cell(f, 3).Value = unidad;
            ws.Cell(f, 4).Value = valor;
            ws.Cell(f, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(f, 2).Style.Font.Italic = true;
            ws.Cell(f, 2).Style.Font.FontSize = 8;
            EstiloFilaFSR(ws, f);
        }

        // Fila con valor como texto (para casos especiales con IIF)
        private static void FilaFSRSinVal(IXLWorksheet ws, int f, string desc, string op, string unidad, string valorTexto)
        {
            ws.Cell(f, 1).Value = desc;
            ws.Cell(f, 2).Value = op;
            ws.Cell(f, 3).Value = unidad;
            ws.Cell(f, 4).Value = valorTexto;
            ws.Cell(f, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            if (!string.IsNullOrEmpty(op))
            {
                ws.Cell(f, 2).Style.Font.Italic = true;
                ws.Cell(f, 2).Style.Font.FontSize = 8;
            }
            EstiloFilaFSR(ws, f);
        }

        private static void EstiloFilaFSR(IXLWorksheet ws, int f)
        {
            ws.Cell(f, 1).Style.Font.FontSize = 9;
            ws.Cell(f, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
            ws.Cell(f, 3).Style.Font.FontSize = 9;
            ws.Cell(f, 4).Style.Font.FontSize = 9;
            for (int col = 1; col <= 4; col++)
                ws.Cell(f, col).Style.Border.BottomBorder = XLBorderStyleValues.Hair;
            ws.Row(f).Height = 14;
        }

        private static void AplicarBordes(IXLWorksheet ws, int filaInicio, int filaFin)
        {
            var rng = ws.Range(filaInicio, 1, filaFin, 4);
            rng.Style.Border.OutsideBorder = XLBorderStyleValues.Medium;
        }
    }
}
