using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Data;
using System.Globalization;
using System.Linq;
using Sopro.Calculation;

namespace SOPRO.Application.Services
{
    /// <summary>
    /// Calcula el financiamiento del proyecto a partir del flujo de caja del programa de obra.
    ///
    /// Criterio oficial para SOPRO:
    /// - Egresos del periodo = CD programado + CI del periodo
    /// - Ingresos del periodo = Anticipo + Estimaciones cobradas - Amortización del anticipo
    /// - Costo financiero = aplicación de la tasa efectiva al saldo acumulado deficitario
    /// - % Financiamiento = Costo financiero total / Base (CD o CD+CI)
    /// </summary>
    public sealed class FinanciamientoCalculationService
    {
        public ConfiguracionFinanciamiento ObtenerOCrear(SOPROContext context, int proyectoId)
        {
            var config = context.ConfiguracionesFinanciamiento
                .Include(c => c.FilasFlujo)
                .FirstOrDefault(c => c.ProyectoId == proyectoId);

            if (config != null)
                return config;

            try
            {
                config = new ConfiguracionFinanciamiento
                {
                    ProyectoId = proyectoId,
                    TasaTIIE = 11.0m,
                    PuntosAdicionales = 3.0m,
                    PorcentajeAnticipo = 30.0m,
                    PeriodosAmortizacionAnticipo = 1,
                    DesfaseCobro = 1,
                    BaseCalculo = "Acumulable",
                    InteresesNegativos = 0m,
                    InteresesPositivos = 0m,
                    FinanciamientoNeto = 0m,
                    PorcentajeCalculado = 0m,
                    FechaCalculo = null
                };

                context.ConfiguracionesFinanciamiento.Add(config);
                context.SaveChanges();
            }
            catch (DbUpdateException)
            {
                EnsureLegacyCompatibleInsert(context, proyectoId);
                config = context.ConfiguracionesFinanciamiento
                    .Include(c => c.FilasFlujo)
                    .FirstOrDefault(c => c.ProyectoId == proyectoId);

                if (config == null)
                    throw;
            }

            return config;
        }


        private void EnsureLegacyCompatibleInsert(SOPROContext context, int proyectoId)
        {
            var connection = context.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;
            if (shouldClose)
                connection.Open();

            try
            {
                var columns = ReadTableInfo(connection, "ConfiguracionesFinanciamiento");
                if (columns.Count == 0)
                    throw new InvalidOperationException("No se pudo inspeccionar la tabla ConfiguracionesFinanciamiento.");

                var insertable = columns
                    .Where(c => !c.IsPrimaryKey && !string.Equals(c.Name, "Id", StringComparison.OrdinalIgnoreCase))
                    .ToList();

                using var cmd = connection.CreateCommand();
                var colNames = new List<string>();
                var paramNames = new List<string>();

                int i = 0;
                foreach (var col in insertable)
                {
                    colNames.Add(col.Name);
                    var paramName = "@p" + i++;
                    paramNames.Add(paramName);

                    var p = cmd.CreateParameter();
                    p.ParameterName = paramName;
                    p.Value = GetDefaultValueForColumn(col, proyectoId) ?? DBNull.Value;
                    cmd.Parameters.Add(p);
                }

                cmd.CommandText = $"INSERT INTO ConfiguracionesFinanciamiento ({string.Join(", ", colNames)}) VALUES ({string.Join(", ", paramNames)})";
                cmd.ExecuteNonQuery();
            }
            finally
            {
                if (shouldClose)
                    connection.Close();
            }
        }

        private static List<TableColumnInfo> ReadTableInfo(System.Data.Common.DbConnection connection, string tableName)
        {
            var list = new List<TableColumnInfo>();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = $"PRAGMA table_info({tableName})";
            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                list.Add(new TableColumnInfo
                {
                    Name = Convert.ToString(reader[1], CultureInfo.InvariantCulture) ?? string.Empty,
                    Type = Convert.ToString(reader[2], CultureInfo.InvariantCulture) ?? string.Empty,
                    NotNull = Convert.ToInt32(reader[3], CultureInfo.InvariantCulture) == 1,
                    DefaultValueSql = reader.IsDBNull(4) ? null : Convert.ToString(reader[4], CultureInfo.InvariantCulture),
                    IsPrimaryKey = Convert.ToInt32(reader[5], CultureInfo.InvariantCulture) == 1
                });
            }
            return list;
        }

        private static object? GetDefaultValueForColumn(TableColumnInfo col, int proyectoId)
        {
            switch (col.Name)
            {
                case "ProyectoId": return proyectoId;
                case "TasaTIIE": return 11.0m;
                case "PuntosAdicionales": return 3.0m;
                case "PorcentajeAnticipo": return 30.0m;
                case "PeriodosAmortizacionAnticipo": return 1;
                case "DesfaseCobro": return 1;
                case "BaseCalculo": return "Acumulable";
                case "InteresesNegativos": return 0m;
                case "InteresesPositivos": return 0m;
                case "FinanciamientoNeto": return 0m;
                case "PorcentajeCalculado": return 0m;
                case "FechaCalculo": return DBNull.Value;
                // Compatibilidad con esquemas anteriores / importadores
                case "TasaInteresAnual": return 14.0m;
                case "TasaInteresMensual": return decimal.Round(14.0m / 12.0m, 6, MidpointRounding.AwayFromZero);
                case "UsaTasaDiaria": return 0;
                case "DiasCobro": return 0;
                case "PeriodoEntregaAnticipo": return 1;
                case "PeriodosAmortizacion": return 1;
                case "PorcentajeAmortizacion": return 30.0m;
            }

            if (!col.NotNull)
                return DBNull.Value;

            if (!string.IsNullOrWhiteSpace(col.DefaultValueSql))
                return DBNull.Value;

            var type = (col.Type ?? string.Empty).ToUpperInvariant();
            if (type.Contains("CHAR") || type.Contains("TEXT") || type.Contains("CLOB"))
                return string.Empty;
            if (type.Contains("INT"))
                return 0;
            if (type.Contains("REAL") || type.Contains("FLOA") || type.Contains("DOUB"))
                return 0.0;
            if (type.Contains("NUM") || type.Contains("DEC"))
                return 0m;
            if (type.Contains("DATE") || type.Contains("TIME"))
                return DateTime.Now;

            return 0;
        }

        private sealed class TableColumnInfo
        {
            public string Name { get; set; } = string.Empty;
            public string Type { get; set; } = string.Empty;
            public bool NotNull { get; set; }
            public string? DefaultValueSql { get; set; }
            public bool IsPrimaryKey { get; set; }
        }

        public decimal Calcular(SOPROContext context, ConfiguracionFinanciamiento config, Proyecto proyecto, bool modeloDual = false)
        {
            var programa = context.ProgramasObra
                .AsNoTracking()
                .FirstOrDefault(p => p.ProyectoId == proyecto.Id && p.Activo);

            if (programa == null)
                return 0m;

            var periodos = context.PeriodosPrograma
                .AsNoTracking()
                .Where(p => p.ProgramaObraId == programa.Id)
                .OrderBy(p => p.NumeroPeriodo)
                .ToList();

            if (periodos.Count == 0)
                return 0m;

            var engine = CalculationEngineFactory.FromProyecto(proyecto);

            // Traer distribuciones y su concepto de presupuesto para obtener el CD unitario real.
            var distribuciones = context.DistribucionesPeriodo
                .AsNoTracking()
                .Include(d => d.ActividadProgramada)
                    .ThenInclude(a => a.ConceptoPresupuesto)
                .Where(d => d.PeriodoPrograma.ProgramaObraId == programa.Id)
                .ToList();

            decimal tasaAnualNegativa = config.TasaEfectiva / 100m;
            decimal tasaAnualPositiva = config.TasaTIIE / 100m;

            var periodosCalc = BuildPeriodRows(
                proyecto,
                periodos,
                distribuciones,
                proyecto.PorcentajeIndirectosCentral,
                proyecto.PorcentajeIndirectosCampo,
                config.DesfaseCobro,
                engine);
            if (periodosCalc.Count == 0)
                return 0m;

            decimal cdTotal = periodosCalc.Sum(x => x.CostoDirecto);
            decimal ciTotal = periodosCalc.Sum(x => x.CostoIndirecto);
            decimal baseCalculo = config.BaseCalculo == "SobreCD" ? cdTotal : (cdTotal + ciTotal);
            if (baseCalculo <= 0m)
                return 0m;

            decimal totalPresupuesto = BudgetPricingService.RoundImporte(proyecto,
                context.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == proyecto.Id && !c.EsAgrupador && c.MatrizId != null)
                    .AsEnumerable()
                    .Sum(c => c.ImporteTotal));
            decimal anticipoBase = totalPresupuesto > 0m ? totalPresupuesto : baseCalculo;
            decimal anticipoTotal = BudgetPricingService.RoundImporte(proyecto, anticipoBase * (config.PorcentajeAnticipo / 100m));
            decimal amortizacionPendiente = anticipoTotal;
            decimal porcentajeAmortizacion = config.PorcentajeAnticipo / 100m;

            context.FilasFlujoCajaFinanciamiento.RemoveRange(
                context.FilasFlujoCajaFinanciamiento.Where(f => f.ConfiguracionFinanciamientoId == config.Id));

            var filas = new List<FilaFlujoCajaFinanciamiento>();
            decimal saldoAcumulado = 0m;

            for (int i = 0; i < periodosCalc.Count; i++)
            {
                var p = periodosCalc[i];

                decimal anticipoPeriodo = i == 0 ? anticipoTotal : 0m;

                decimal estimacionCobrada = 0m;
                int indiceCobro = i - config.DesfaseCobro;
                if (indiceCobro >= 0 && indiceCobro < periodosCalc.Count)
                {
                    estimacionCobrada = periodosCalc[indiceCobro].EstimacionTotal;
                }

                decimal amortizacion = 0m;
                if (estimacionCobrada > 0m && amortizacionPendiente > 0m)
                {
                    amortizacion = decimal.Round(estimacionCobrada * porcentajeAmortizacion, 6, MidpointRounding.AwayFromZero);
                    amortizacion = Math.Min(amortizacion, amortizacionPendiente);
                    amortizacion = Math.Min(amortizacion, estimacionCobrada);
                    amortizacionPendiente -= amortizacion;
                }

                decimal flujoNeto = anticipoPeriodo + estimacionCobrada - amortizacion - p.EgresoTotal;
                saldoAcumulado += flujoNeto;

                decimal interesPeriodo = 0m;
                if (saldoAcumulado < 0m)
                {
                    decimal baseInteres = Math.Abs(saldoAcumulado);
                    decimal tasaPeriodo = GetTasaPeriodo(tasaAnualNegativa, p.DiasPeriodo);
                    decimal interes = decimal.Round(baseInteres * tasaPeriodo, 4, MidpointRounding.AwayFromZero);
                    interesPeriodo = modeloDual ? -interes : interes;
                }
                else if (modeloDual && saldoAcumulado > 0m)
                {
                    decimal tasaPeriodo = GetTasaPeriodo(tasaAnualPositiva, p.DiasPeriodo);
                    decimal interes = decimal.Round(saldoAcumulado * tasaPeriodo, 4, MidpointRounding.AwayFromZero);
                    interesPeriodo = interes;
                }

                filas.Add(new FilaFlujoCajaFinanciamiento
                {
                    ConfiguracionFinanciamientoId = config.Id,
                    NumeroPeriodo = p.NumeroPeriodo,
                    Etiqueta = p.Etiqueta,
                    FechaInicio = p.FechaInicio,
                    FechaFin = p.FechaFin,
                    Egresos = BudgetPricingService.RoundImporte(proyecto, p.EgresoTotal),
                    AnticipoRecibido = BudgetPricingService.RoundImporte(proyecto, anticipoPeriodo),
                    EstimacionCobrada = BudgetPricingService.RoundImporte(proyecto, estimacionCobrada),
                    AmortizacionAnticipo = BudgetPricingService.RoundImporte(proyecto, amortizacion),
                    FlujoNeto = BudgetPricingService.RoundImporte(proyecto, flujoNeto),
                    SaldoAcumulado = BudgetPricingService.RoundImporte(proyecto, saldoAcumulado),
                    DiasPeriodo = p.DiasPeriodo,
                    InteresPeriodo = interesPeriodo
                });
            }

            context.FilasFlujoCajaFinanciamiento.AddRange(filas);

            decimal interesesCosto;
            decimal interesesBeneficio;
            decimal financiamientoNeto;

            if (modeloDual)
            {
                interesesCosto = decimal.Round(filas.Where(f => f.InteresPeriodo < 0m).Sum(f => Math.Abs(f.InteresPeriodo)), 4, MidpointRounding.AwayFromZero);
                interesesBeneficio = decimal.Round(filas.Where(f => f.InteresPeriodo > 0m).Sum(f => f.InteresPeriodo), 4, MidpointRounding.AwayFromZero);
                financiamientoNeto = decimal.Round(interesesBeneficio - interesesCosto, 4, MidpointRounding.AwayFromZero);
            }
            else
            {
                // En el modelo clásico el interés del renglón se muestra positivo, pero todo corresponde a costo financiero.
                interesesCosto = decimal.Round(filas.Where(f => f.SaldoAcumulado < 0m).Sum(f => Math.Abs(f.InteresPeriodo)), 4, MidpointRounding.AwayFromZero);
                interesesBeneficio = 0m;
                financiamientoNeto = decimal.Round(interesesCosto, 4, MidpointRounding.AwayFromZero);
            }
            decimal porcentajeCalculado = baseCalculo > 0m
                ? decimal.Round(financiamientoNeto / baseCalculo * 100m, 5, MidpointRounding.AwayFromZero)
                : 0m;

            config.InteresesNegativos = interesesCosto;
            config.InteresesPositivos = interesesBeneficio;
            config.FinanciamientoNeto = financiamientoNeto;
            config.PorcentajeCalculado = porcentajeCalculado;
            config.FechaCalculo = DateTime.Now;

            context.SaveChanges();
            return porcentajeCalculado;
        }

        private static decimal GetTasaPeriodo(decimal tasaAnual, int diasPeriodo)
        {
            if (tasaAnual <= 0m || diasPeriodo <= 0)
                return 0m;

            // Tasa equivalente simple prorrateada al periodo del programa.
            return decimal.Round(tasaAnual * diasPeriodo / 365m, 8, MidpointRounding.AwayFromZero);
        }

        private static List<PeriodoFinanciamientoCalc> BuildPeriodRows(
            Proyecto proyecto,
            List<PeriodoPrograma> periodos,
            List<DistribucionPeriodo> distribuciones,
            decimal porcentajeIndirectosCentral,
            decimal porcentajeIndirectosCampo,
            int desfaseCobro,
            SoproCalculationEngine engine)
        {
            var periodOrder = periodos.ToDictionary(p => p.Id, p => p.NumeroPeriodo);
            var acumulados = periodos.ToDictionary(
                p => p.Id,
                p => new PeriodoFinanciamientoCalc
                {
                    NumeroPeriodo = p.NumeroPeriodo,
                    Etiqueta = p.Etiqueta,
                    FechaInicio = p.FechaInicio,
                    FechaFin = p.FechaFin,
                    DiasPeriodo = Math.Max(1, (p.FechaFin.Date - p.FechaInicio.Date).Days + 1),
                    CostoDirecto = 0m,
                    CostoIndirecto = 0m,
                    EgresoTotal = 0m,
                    EstimacionTotal = 0m
                });

            var gruposConcepto = distribuciones
                .Where(d => d.ActividadProgramada?.ConceptoPresupuesto != null
                         && !d.ActividadProgramada.ConceptoPresupuesto.EsAgrupador
                         && d.ActividadProgramada.ConceptoPresupuesto.MatrizId.HasValue)
                .GroupBy(d => d.ActividadProgramada!.ConceptoPresupuestoId!.Value);

            foreach (var grupo in gruposConcepto)
            {
                var concepto = grupo.First().ActividadProgramada!.ConceptoPresupuesto!;
                decimal cantidadConcepto = concepto.Cantidad;
                if (cantidadConcepto <= 0m)
                    continue;

                decimal cdUnit = concepto.CostoDirectoUnitario;
                if (cdUnit <= 0m && concepto.CostoDirectoTotal > 0m)
                    cdUnit = concepto.CostoDirectoTotal / cantidadConcepto;

                decimal totalCdConcepto = engine.Multiply(cantidadConcepto, cdUnit);

                var distribucionesConcepto = grupo
                    .OrderBy(d => periodOrder.TryGetValue(d.PeriodoProgramaId, out var orden) ? orden : int.MaxValue)
                    .ToList();

                AplicarAcumuladoPorDistribucion(proyecto, distribucionesConcepto, cdUnit, totalCdConcepto, acumulados, true, engine);
            }

            foreach (var distribucion in distribuciones.Where(d => d.ActividadProgramada?.ConceptoPresupuesto != null
                                                                && !d.ActividadProgramada.ConceptoPresupuesto.EsAgrupador
                                                                && d.ActividadProgramada.ConceptoPresupuesto.MatrizId.HasValue))
            {
                if (acumulados.TryGetValue(distribucion.PeriodoProgramaId, out var rowEstim))
                    rowEstim.EstimacionTotal = BudgetPricingService.RoundImporte(proyecto, rowEstim.EstimacionTotal + distribucion.ImporteProgramado);
            }

            var rows = periodos
                .Select(p =>
                {
                    var row = acumulados[p.Id];
                    row.CostoDirecto = BudgetPricingService.RoundImporte(proyecto, row.CostoDirecto);
                    row.CostoIndirecto = 0m;
                    row.EgresoTotal = row.CostoDirecto;
                    return row;
                })
                .ToList();

            // Reconciliar indirectos oficiales del proyecto y fijar egreso = CD + CI
            ReconciliarIndirectosYEgresos(proyecto, rows);

            if (rows.Count > 0 && desfaseCobro > 0)
            {
                var ultimo = rows[^1];
                int diasBase = Math.Max(1, ultimo.DiasPeriodo);
                for (int extra = 1; extra <= desfaseCobro; extra++)
                {
                    DateTime inicio = ultimo.FechaFin.Date.AddDays(1 + diasBase * (extra - 1));
                    DateTime fin = inicio.AddDays(diasBase - 1);
                    rows.Add(new PeriodoFinanciamientoCalc
                    {
                        NumeroPeriodo = ultimo.NumeroPeriodo + extra,
                        Etiqueta = $"Período de desfase {extra}",
                        FechaInicio = inicio,
                        FechaFin = fin,
                        DiasPeriodo = diasBase,
                        CostoDirecto = 0m,
                        CostoIndirecto = 0m,
                        EgresoTotal = 0m,
                        EstimacionTotal = 0m
                    });
                }
            }

            return rows;
        }

        private static void ReconciliarIndirectosYEgresos(Proyecto proyecto, List<PeriodoFinanciamientoCalc> rows)
        {
            if (rows.Count == 0)
                return;

            decimal totalCd = rows.Sum(x => x.CostoDirecto);
            var preview = BudgetPreviewCalculationService.BuildPreviewFromReferenceCost(totalCd, new BudgetPercentageInput
            {
                CostoDirectoReferencia = totalCd,
                IndirectosCentral = proyecto.PorcentajeIndirectosCentral,
                IndirectosCampo = proyecto.PorcentajeIndirectosCampo,
                Financiamiento = 0m,
                Utilidad = 0m,
                CargosAdicionales = 0m,
                ModoCalculoPorcentajes = proyecto.ModoCalculoPorcentajes
            });

            decimal totalCiOficial = BudgetPricingService.RoundImporte(proyecto, preview.Subtotal1 - preview.CostoDirecto);
            ReconciliarDistribucion(rows, proyecto, totalCiOficial, esCostoDirecto: false);

            foreach (var row in rows)
            {
                row.CostoDirecto = BudgetPricingService.RoundImporte(proyecto, row.CostoDirecto);
                row.CostoIndirecto = BudgetPricingService.RoundImporte(proyecto, row.CostoIndirecto);
                row.EgresoTotal = BudgetPricingService.RoundImporte(proyecto, row.CostoDirecto + row.CostoIndirecto);
                row.EstimacionTotal = BudgetPricingService.RoundImporte(proyecto, row.EstimacionTotal);
            }
        }

        private static void ReconciliarDistribucion(List<PeriodoFinanciamientoCalc> rows, Proyecto proyecto, decimal totalOficial, bool esCostoDirecto)
        {
            decimal totalActual = rows.Sum(x => esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto);
            if (totalActual == totalOficial)
                return;

            var filasConMonto = rows
                .Where(x => (esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto) != 0m)
                .ToList();

            if (filasConMonto.Count == 0)
            {
                filasConMonto = rows.ToList();
                if (filasConMonto.Count == 0)
                    return;
            }

            if (totalActual == 0m)
            {
                decimal totalBase = rows.Sum(x => x.CostoDirecto);
                if (totalBase > 0m)
                {
                    decimal acumulado = 0m;
                    for (int i = 0; i < rows.Count; i++)
                    {
                        var fila = rows[i];
                        decimal nuevo = i == rows.Count - 1
                            ? totalOficial - acumulado
                            : BudgetPricingService.RoundImporte(proyecto, totalOficial * fila.CostoDirecto / totalBase);
                        if (esCostoDirecto)
                            fila.CostoDirecto = nuevo;
                        else
                            fila.CostoIndirecto = nuevo;
                        acumulado += nuevo;
                    }
                }
                else
                {
                    var ultima = filasConMonto[^1];
                    if (esCostoDirecto)
                        ultima.CostoDirecto = totalOficial;
                    else
                        ultima.CostoIndirecto = totalOficial;
                }
                return;
            }

            decimal acumuladoDistrib = 0m;
            for (int i = 0; i < filasConMonto.Count; i++)
            {
                var fila = filasConMonto[i];
                decimal actual = esCostoDirecto ? fila.CostoDirecto : fila.CostoIndirecto;
                decimal nuevo = i == filasConMonto.Count - 1
                    ? totalOficial - acumuladoDistrib
                    : BudgetPricingService.RoundImporte(proyecto, totalOficial * actual / totalActual);
                if (esCostoDirecto)
                    fila.CostoDirecto = nuevo;
                else
                    fila.CostoIndirecto = nuevo;
                acumuladoDistrib += nuevo;
            }
        }

        private static void AplicarAcumuladoPorDistribucion(
            Proyecto proyecto,
            List<DistribucionPeriodo> distribucionesConcepto,
            decimal precioUnitario,
            decimal totalEsperado,
            Dictionary<int, PeriodoFinanciamientoCalc> acumulados,
            bool esCostoDirecto,
            SoproCalculationEngine engine)
        {
            if (distribucionesConcepto.Count == 0 || totalEsperado == 0m)
                return;

            decimal suma = 0m;
            int ultimoIndiceConMonto = -1;
            var importes = new decimal[distribucionesConcepto.Count];

            for (int i = 0; i < distribucionesConcepto.Count; i++)
            {
                var distribucion = distribucionesConcepto[i];
                decimal importe = engine.Multiply(distribucion.CantidadProgramada, precioUnitario);
                importes[i] = importe;
                suma += importe;
                if (distribucion.CantidadProgramada != 0m || importe != 0m)
                    ultimoIndiceConMonto = i;
            }

            if (ultimoIndiceConMonto < 0)
                ultimoIndiceConMonto = distribucionesConcepto.Count - 1;

            importes[ultimoIndiceConMonto] += totalEsperado - suma;

            for (int i = 0; i < distribucionesConcepto.Count; i++)
            {
                var distribucion = distribucionesConcepto[i];
                if (!acumulados.TryGetValue(distribucion.PeriodoProgramaId, out var row))
                    continue;

                if (esCostoDirecto)
                    row.CostoDirecto += importes[i];
                else
                    row.CostoIndirecto += importes[i];
            }
        }

        private sealed class PeriodoFinanciamientoCalc
        {
            public int NumeroPeriodo { get; set; }
            public string Etiqueta { get; set; } = string.Empty;
            public DateTime FechaInicio { get; set; }
            public DateTime FechaFin { get; set; }
            public int DiasPeriodo { get; set; }
            public decimal CostoDirecto { get; set; }
            public decimal CostoIndirecto { get; set; }
            public decimal EgresoTotal { get; set; }
            public decimal EstimacionTotal { get; set; }
        }
    }
}
