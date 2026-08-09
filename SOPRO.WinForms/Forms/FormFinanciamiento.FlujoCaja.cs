using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Construcción del flujo de caja, distribución por concepto y reconciliación de totales.
    /// </summary>
    public partial class FormFinanciamiento
    {

        private void CargarTablaFlujo()
        {
            var filas = _context.FilasFlujoCajaFinanciamiento
                .Where(f => f.ConfiguracionFinanciamientoId == _config.Id)
                .OrderBy(f => f.NumeroPeriodo)
                .ToList();

            var baseRows = BuildDisplayRows();

            dgvFlujo.SuspendLayout();
            dgvFlujo.Rows.Clear();

            string fmtImp = $"N{Math.Max(0, _proyecto.DecimalesImporte)}";
            foreach (var fila in filas)
            {
                baseRows.TryGetValue(fila.NumeroPeriodo, out var baseRow);
                decimal cobroNeto = fila.EstimacionCobrada - fila.AmortizacionAnticipo;
                decimal tasaPeriodo = GetTasaPeriodoLabel(fila.DiasPeriodo, fila.SaldoAcumulado);

                var idx = dgvFlujo.Rows.Add(
                    fila.Etiqueta,
                    fila.FechaInicio.ToString("dd/MM/yy"),
                    fila.FechaFin.ToString("dd/MM/yy"),
                    fila.DiasPeriodo,
                    (baseRow?.CostoDirecto ?? 0m).ToString(fmtImp, CultureInfo.CurrentCulture),
                    (baseRow?.CostoIndirecto ?? 0m).ToString(fmtImp, CultureInfo.CurrentCulture),
                    fila.Egresos.ToString(fmtImp, CultureInfo.CurrentCulture),
                    fila.AnticipoRecibido > 0 ? fila.AnticipoRecibido.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    fila.EstimacionCobrada > 0 ? fila.EstimacionCobrada.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    fila.AmortizacionAnticipo > 0 ? fila.AmortizacionAnticipo.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    cobroNeto != 0 ? cobroNeto.ToString(fmtImp, CultureInfo.CurrentCulture) : string.Empty,
                    fila.FlujoNeto.ToString(fmtImp, CultureInfo.CurrentCulture),
                    fila.SaldoAcumulado.ToString(fmtImp, CultureInfo.CurrentCulture),
                    tasaPeriodo.ToString("N4", CultureInfo.CurrentCulture) + "%",
                    fila.InteresPeriodo.ToString("N4", CultureInfo.CurrentCulture));

                var row = dgvFlujo.Rows[idx];
                if (fila.SaldoAcumulado < 0)
                    row.DefaultCellStyle.BackColor = Color.FromArgb(255, 235, 235);
                else if (fila.SaldoAcumulado > 0)
                    row.DefaultCellStyle.BackColor = Color.FromArgb(235, 255, 235);

                if (EsModeloDualSeleccionado())
                {
                    if (fila.InteresPeriodo < 0)
                        row.Cells["colInteres"].Style.ForeColor = Color.DarkRed;
                    else if (fila.InteresPeriodo > 0)
                        row.Cells["colInteres"].Style.ForeColor = Color.DarkGreen;
                }
                else if (fila.InteresPeriodo > 0)
                {
                    row.Cells["colInteres"].Style.ForeColor = fila.SaldoAcumulado < 0 ? Color.DarkRed : Color.DarkGreen;
                }
            }

            dgvFlujo.ResumeLayout();
        }

        private Dictionary<int, DisplayFlowBaseRow> BuildDisplayRows()
        {
            var result = new Dictionary<int, DisplayFlowBaseRow>();

            var programa = _context.ProgramasObra
                .AsNoTracking()
                .FirstOrDefault(p => p.ProyectoId == _proyecto.Id && p.Activo);
            if (programa == null)
                return result;

            var periodos = _context.PeriodosPrograma
                .AsNoTracking()
                .Where(p => p.ProgramaObraId == programa.Id)
                .OrderBy(p => p.NumeroPeriodo)
                .ToList();
            if (periodos.Count == 0)
                return result;

            var distribuciones = _context.DistribucionesPeriodo
                .AsNoTracking()
                .Include(d => d.ActividadProgramada)
                    .ThenInclude(a => a.ConceptoPresupuesto)
                .Where(d => d.PeriodoPrograma.ProgramaObraId == programa.Id)
                .ToList();

            foreach (var periodo in periodos)
            {
                result[periodo.NumeroPeriodo] = new DisplayFlowBaseRow
                {
                    NumeroPeriodo = periodo.NumeroPeriodo,
                    CostoDirecto = 0m,
                    CostoIndirecto = 0m
                };
            }

            var periodOrder = periodos.ToDictionary(p => p.Id, p => p.NumeroPeriodo);
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

                decimal totalCdConcepto = new MotorCalculoSopro(_proyecto).Multiplicar(cantidadConcepto, cdUnit);

                var distribucionesConcepto = grupo
                    .OrderBy(d => periodOrder.TryGetValue(d.PeriodoProgramaId, out var orden) ? orden : int.MaxValue)
                    .ToList();

                DistribuirImportePorConcepto(distribucionesConcepto, cdUnit, totalCdConcepto, result, periodOrder, true);
            }

            foreach (var periodo in periodos)
            {
                if (!result.TryGetValue(periodo.NumeroPeriodo, out var row))
                    continue;

                row.CostoDirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoDirecto);
                row.CostoIndirecto = 0m;
            }

            ReconciliarTotalesConPreview(result);

            if (periodos.Count > 0 && _config.DesfaseCobro > 0)
            {
                var ultimo = periodos[^1];
                for (int extra = 1; extra <= _config.DesfaseCobro; extra++)
                {
                    result[ultimo.NumeroPeriodo + extra] = new DisplayFlowBaseRow
                    {
                        NumeroPeriodo = ultimo.NumeroPeriodo + extra,
                        CostoDirecto = 0m,
                        CostoIndirecto = 0m
                    };
                }
            }

            var filasPersistidas = _context.FilasFlujoCajaFinanciamiento
                .AsNoTracking()
                .Where(f => f.ConfiguracionFinanciamientoId == _config.Id)
                .Select(f => new { f.NumeroPeriodo, f.Egresos })
                .ToList();
            foreach (var filaPersistida in filasPersistidas)
            {
                if (result.TryGetValue(filaPersistida.NumeroPeriodo, out var row))
                {
                    row.CostoDirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoDirecto);
                    row.CostoIndirecto = BudgetPricingService.RoundImporte(_proyecto, filaPersistida.Egresos - row.CostoDirecto);
                }
            }

            return result;
        }


        private void ReconciliarTotalesConPreview(Dictionary<int, DisplayFlowBaseRow> result)
        {
            if (result.Count == 0)
                return;

            var preview = BudgetPreviewCalculationService.BuildPreview(_context, _proyecto, new BudgetPercentageInput
            {
                CostoDirectoReferencia = 0m,
                IndirectosCentral = _proyecto.PorcentajeIndirectosCentral,
                IndirectosCampo = _proyecto.PorcentajeIndirectosCampo,
                Financiamiento = 0m,
                Utilidad = 0m,
                CargosAdicionales = 0m,
                ModoCalculoPorcentajes = _proyecto.ModoCalculoPorcentajes
            });

            var totalCiOficial = BudgetPricingService.RoundImporte(_proyecto, preview.Subtotal1 - preview.CostoDirecto);

            ReconciliarDistribucion(result, totalCiOficial, false);

            foreach (var row in result.Values)
            {
                row.CostoDirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoDirecto);
                row.CostoIndirecto = BudgetPricingService.RoundImporte(_proyecto, row.CostoIndirecto);
            }
        }

        private void ReconciliarDistribucion(Dictionary<int, DisplayFlowBaseRow> result, decimal totalOficial, bool esCostoDirecto)
        {
            var filas = result.Values
                .OrderBy(x => x.NumeroPeriodo)
                .ToList();

            decimal totalActual = filas.Sum(x => esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto);
            if (totalActual == totalOficial)
                return;

            var filasConMonto = filas
                .Where(x => (esCostoDirecto ? x.CostoDirecto : x.CostoIndirecto) != 0m)
                .ToList();

            if (filasConMonto.Count == 0)
            {
                filasConMonto = filas.ToList();
                if (filasConMonto.Count == 0)
                    return;
            }

            if (totalActual == 0m)
            {
                decimal totalBase = filas.Sum(x => x.CostoDirecto);
                if (totalBase > 0m)
                {
                    decimal acumuladoBase = 0m;
                    for (int i = 0; i < filas.Count; i++)
                    {
                        var fila = filas[i];
                        decimal nuevo = i == filas.Count - 1
                            ? totalOficial - acumuladoBase
                            : BudgetPricingService.RoundImporte(_proyecto, totalOficial * fila.CostoDirecto / totalBase);

                        if (esCostoDirecto)
                            fila.CostoDirecto = nuevo;
                        else
                            fila.CostoIndirecto = nuevo;

                        acumuladoBase += nuevo;
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

            decimal acumulado = 0m;
            for (int i = 0; i < filasConMonto.Count; i++)
            {
                var fila = filasConMonto[i];
                decimal actual = esCostoDirecto ? fila.CostoDirecto : fila.CostoIndirecto;

                decimal nuevo = i == filasConMonto.Count - 1
                    ? totalOficial - acumulado
                    : BudgetPricingService.RoundImporte(_proyecto, totalOficial * actual / totalActual);

                if (esCostoDirecto)
                    fila.CostoDirecto = nuevo;
                else
                    fila.CostoIndirecto = nuevo;

                acumulado += nuevo;
            }
        }

        private void DistribuirImportePorConcepto(
            List<DistribucionPeriodo> distribucionesConcepto,
            decimal precioUnitario,
            decimal totalEsperado,
            Dictionary<int, DisplayFlowBaseRow> result,
            Dictionary<int, int> periodOrder,
            bool esCostoDirecto)
        {
            if (distribucionesConcepto.Count == 0 || totalEsperado == 0m)
                return;

            decimal suma = 0m;
            int ultimoIndiceConMonto = -1;
            var importes = new decimal[distribucionesConcepto.Count];

            for (int i = 0; i < distribucionesConcepto.Count; i++)
            {
                var distribucion = distribucionesConcepto[i];
                decimal importe = new MotorCalculoSopro(_proyecto).Multiplicar(distribucion.CantidadProgramada, precioUnitario);
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
                if (!periodOrder.TryGetValue(distribucion.PeriodoProgramaId, out var numeroPeriodo))
                    continue;
                if (!result.TryGetValue(numeroPeriodo, out var row))
                    continue;

                if (esCostoDirecto)
                    row.CostoDirecto += importes[i];
                else
                    row.CostoIndirecto += importes[i];
            }
        }

        private sealed class DisplayFlowBaseRow
        {
            public int NumeroPeriodo { get; set; }
            public decimal CostoDirecto { get; set; }
            public decimal CostoIndirecto { get; set; }
        }
    }
}
