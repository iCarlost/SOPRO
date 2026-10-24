using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Refresco de precios del presupuesto (ADR-0001). La intención viaja como
    /// comando inmutable a <see cref="SOPRO.Application.Services.BudgetPersistenceService.RefreshBudgetPrices"/>
    /// sobre un contexto NUEVO; el formulario no escribe en su contexto compartido.
    /// La recarga posterior a cambios ya propagados por otros módulos usa
    /// <see cref="RecargarPreciosDesdeBase"/>, que solo reconcilia la vista y nunca
    /// vuelve a emitir el comando.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Clasificación del último refresco de precios ejecutado como comando, o
        /// <c>null</c> si no llegó a enviarse (trabajo pendiente o edición activa).
        /// Observabilidad interna para las pruebas del ensamblado.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoRefrescoPrecios { get; private set; }

        /// <summary>Detalle técnico del último refresco no confirmado; nunca se muestra.</summary>
        internal string? UltimoDiagnosticoRefrescoPrecios { get; private set; }

        /// <summary>
        /// Refresca los precios de todo el presupuesto como comando. Se usa cuando
        /// cambió un precio de matriz que NO fue propagado por otra ruta o cuando
        /// cambiaron los porcentajes del proyecto. Si el contexto compartido tiene
        /// trabajo pendiente o hay una edición activa, no se envía nada y la vista
        /// conserva lo que muestra sin afirmar que se guardó.
        /// </summary>
        public void RefreshBudgetPrices()
        {
            if (_cargando || IsDisposed)
                return;

            UltimoEstadoRefrescoPrecios = null;
            UltimoDiagnosticoRefrescoPrecios = null;

            try
            {
                if (dgvPresupuesto.IsCurrentCellInEditMode)
                    return;

                _context.ChangeTracker.DetectChanges();
                if (_context.ChangeTracker.HasChanges())
                    return;

                var filas = dgvPresupuesto.Rows
                    .Cast<DataGridViewRow>()
                    .Select(r => r.Tag as ConceptoPresupuesto)
                    .Where(c => c is not null && c.Id > 0)
                    .Select(c => c!)
                    .ToList();

                if (filas.Count == 0)
                    return;

                var comando = new BudgetPriceRefreshCommand(
                    _proyecto.Id,
                    BudgetProjectCalculationBase.FromProyecto(_proyecto),
                    BudgetPriceRefreshRowset.FromConceptos(filas));

                BudgetPriceRefreshResult resultado = SOPRO.Application.Services.BudgetPersistenceService
                    .RefreshBudgetPrices(_factory, _context.DatabasePath, comando);

                UltimoEstadoRefrescoPrecios = resultado.Status;

                if (!resultado.IsCommitted || resultado.Confirmed is null)
                {
                    UltimoDiagnosticoRefrescoPrecios = resultado.Diagnostic;
                    return;
                }

                SincronizarProyectoCompartido(resultado.Confirmed.Project);
                AplicarFilasConfirmadas(resultado.Confirmed.Rows);
                ActualizarTitulo();
                _panelMatricesEmbebido?.RecargarMatrizVisibleDesdeBd();
                dgvPresupuesto.Refresh();
            }
            catch (Exception ex)
            {
                // Un fallo al preparar la intención no confirma ningún guardado.
                UltimoDiagnosticoRefrescoPrecios = ex.ToString();
            }
        }

        /// <summary>
        /// Recarga la vista desde la base SIN escribir: la usan los módulos que ya
        /// propagaron y guardaron sus cambios (catálogos de insumos, depuración,
        /// recálculo global, navegación). Si el contexto compartido tiene trabajo
        /// pendiente no se toca nada, para no pisar borradores ajenos.
        /// </summary>
        public void RecargarPreciosDesdeBase()
            => IntentarRecargarPreciosDesdeBase();

        /// <summary>
        /// Núcleo verificable de <see cref="RecargarPreciosDesdeBase"/>: devuelve
        /// <c>true</c> SÓLO cuando la lectura terminó y su instantánea quedó
        /// aplicada sobre proyecto, filas, panel y grid. Errores de lectura, trabajo
        /// pendiente, edición activa o proyecto ausente devuelven <c>false</c> sin
        /// tocar nada; si las filas se aplicaron pero el panel no pudo reconciliar su
        /// matriz visible, también devuelve <c>false</c> para que el llamador no
        /// libere bloqueos sobre una vista parcial. El contrato público no cambia:
        /// los llamadores heredados ignoran el resultado.
        /// </summary>
        internal bool IntentarRecargarPreciosDesdeBase()
        {
            if (_cargando || IsDisposed)
                return false;

            try
            {
                _context.ChangeTracker.DetectChanges();
                if (_context.ChangeTracker.HasChanges())
                    return false;

                if (dgvPresupuesto.IsCurrentCellInEditMode)
                    return false;

                var proyectoFresco = _context.Proyectos
                    .AsNoTracking()
                    .FirstOrDefault(p => p.Id == _proyecto.Id);

                if (proyectoFresco == null)
                    return false;

                var filas = _context.ConceptosPresupuesto
                    .AsNoTracking()
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ThenBy(c => c.Id)
                    .ToList();

                SincronizarProyectoCompartido(BudgetProjectCalculationBase.FromProyecto(proyectoFresco));
                AplicarFilasConfirmadas(filas.Select(BudgetConfirmedConceptRow.FromConcepto).ToArray());
                ActualizarTitulo();

                // La recarga sólo es segura para reconciliar si el panel existe y
                // confirma que aplicó una matriz persistida. Sin selección/matriz
                // visible, en modo creación o con borradores, NO hubo reconciliación
                // visual y el llamador no puede liberar bloqueos.
                if (_panelMatricesEmbebido == null
                    || !_panelMatricesEmbebido.IntentarRecargarMatrizVisibleDesdeBd())
                {
                    return false;
                }

                dgvPresupuesto.Refresh();

                // La instantánea sólo cuenta como aplicada si no dejó trabajo
                // pendiente ni una edición activa a medio reconciliar.
                _context.ChangeTracker.DetectChanges();
                if (_context.ChangeTracker.HasChanges() || dgvPresupuesto.IsCurrentCellInEditMode)
                    return false;

                return true;
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"RecargarPrecios: {ex.Message}");
                return false;
            }
        }

        private void SincronizarProyectoCompartido(BudgetProjectCalculationBase confirmado)
        {
            _proyecto.PorcentajeIndirectosCentral = confirmado.PorcentajeIndirectosCentral;
            _proyecto.PorcentajeIndirectosCampo = confirmado.PorcentajeIndirectosCampo;
            _proyecto.PorcentajeFinanciamiento = confirmado.PorcentajeFinanciamiento;
            _proyecto.PorcentajeUtilidad = confirmado.PorcentajeUtilidad;
            _proyecto.PorcentajeCargosAdicionales = confirmado.PorcentajeCargosAdicionales;
            _proyecto.ModoCalculoPorcentajes = confirmado.ModoCalculoPorcentajes ?? _proyecto.ModoCalculoPorcentajes;
            _proyecto.DecimalesCantidad = confirmado.DecimalesCantidad;
            _proyecto.DecimalesImporte = confirmado.DecimalesImporte;
            _proyecto.DecimalesPorcentaje = confirmado.DecimalesPorcentaje;

            var entry = _context.Entry(_proyecto);
            if (entry.State != EntityState.Detached)
            {
                entry.OriginalValues.SetValues(entry.CurrentValues);
                entry.State = EntityState.Unchanged;
            }

            FormatoHelper.EstablecerProyecto(_proyecto);
        }

        /// <summary>
        /// Vuelca las filas confirmadas en las entidades compartidas (por Id) y en
        /// las celdas económicas del grid, bajo el guard de reentrada para no
        /// disparar otro comando. La selección actual se conserva.
        /// </summary>
        private void AplicarFilasConfirmadas(IReadOnlyList<BudgetConfirmedConceptRow> filas)
        {
            int filaActual = dgvPresupuesto.CurrentCell?.RowIndex ?? -1;
            int colActual = dgvPresupuesto.CurrentCell?.ColumnIndex ?? -1;

            _actualizandoDesdeComando = true;
            try
            {
                var indice = ConstruirIndiceFilasPorConcepto();

                foreach (var fila in filas)
                {
                    var entidad = ResolverEntidadCompartida(indice, fila.ConceptId);
                    if (entidad is not null)
                        SincronizarEntidadCompartida(entidad, fila);

                    if (!indice.TryGetValue(fila.ConceptId, out int rowIndex))
                        continue;

                    AplicarCeldasEconomicas(rowIndex, fila);
                }
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }

            if (filaActual >= 0 && filaActual < dgvPresupuesto.Rows.Count
                && dgvPresupuesto.CurrentCell is null)
            {
                try
                {
                    int colRestaurar = colActual >= 0 && colActual < dgvPresupuesto.Columns.Count
                        ? colActual : 0;
                    dgvPresupuesto.CurrentCell = dgvPresupuesto.Rows[filaActual].Cells[colRestaurar];
                }
                catch
                {
                    // El grid puede haberse reconstruido; conservar la selección es best-effort.
                }
            }

            ActualizarEstadisticas();
        }

        private void AplicarCeldasEconomicas(int rowIndex, BudgetConfirmedConceptRow fila)
        {
            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is not ColumnaPersonalizada colDef)
                    continue;

                switch (colDef.NombreInterno)
                {
                    case "CostoDirectoUnitario":
                        dgvPresupuesto.Rows[rowIndex].Cells[col.Index].Value = fila.CostoDirectoUnitario.ToStringImporte();
                        break;
                    case "CostoDirectoTotal":
                        dgvPresupuesto.Rows[rowIndex].Cells[col.Index].Value = fila.CostoDirectoTotal.ToStringImporte();
                        break;
                    case "PrecioUnitario":
                        dgvPresupuesto.Rows[rowIndex].Cells[col.Index].Value = fila.PrecioUnitario.ToStringImporte();
                        break;
                    case "Importe":
                    case "ImporteTotal":
                        dgvPresupuesto.Rows[rowIndex].Cells[col.Index].Value = fila.ImporteTotal.ToStringImporte();
                        break;
                }
            }
        }
    }
}
