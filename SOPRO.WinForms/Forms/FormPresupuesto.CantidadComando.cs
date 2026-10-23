using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.WinForms.Helpers;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Edición de cantidad de un concepto YA persistido (ADR-0001): la intención
    /// viaja como comando inmutable a <see cref="BudgetPersistenceService.ExecuteQuantityEditCommand"/>,
    /// que la aplica sobre un contexto NUEVO. La entidad y el Tag compartidos por el
    /// formulario no se tocan antes de conocer el resultado y, si se confirma, se
    /// sincronizan con la instantánea confirmada dejándolos <c>Unchanged</c> para que
    /// ningún guardado heredado posterior los vuelva a escribir.
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Suprime <see cref="DgvPresupuesto_CellValueChanged"/> mientras el propio
        /// comando actualiza las celdas calculadas: evita reentrar y disparar otro comando.
        /// </summary>
        private bool _actualizandoDesdeComando;

        /// <summary>
        /// Clasificación de la última edición de cantidad ejecutada como comando, o
        /// <c>null</c> si la edición no llegó a enviarse (fila aún no persistida o
        /// trabajo pendiente en el contexto compartido). Observabilidad interna para
        /// las pruebas del ensamblado; no es API pública ni se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoEdicionCantidad { get; private set; }

        /// <summary>
        /// Consume la edición de la celda Cantidad con la ruta de comando. Devuelve
        /// <c>false</c> sólo cuando la fila no corresponde a un concepto persistido y
        /// debe seguir por la ruta heredada; en cualquier otro caso la edición queda
        /// atendida aquí y el llamador NO debe invocar <see cref="GuardarCambios"/>.
        /// Es el mismo manejador que invoca <see cref="DgvPresupuesto_CellValueChanged"/>;
        /// se expone a las pruebas del ensamblado para reintentar una intención cuya
        /// celda ya contiene el borrador (asignarle el mismo valor no vuelve a disparar
        /// el evento del grid).
        /// </summary>
        internal bool ProcesarEdicionCantidadConComando(int rowIndex)
        {
            var row = dgvPresupuesto.Rows[rowIndex];
            if (row.Tag is not ConceptoPresupuesto concepto || concepto.Id <= 0)
                return false;

            UltimoEstadoEdicionCantidad = null;

            decimal propuesta;
            if (!TryParseCantidadPropuesta(rowIndex, out propuesta))
                return true;

            // Conservador: si el contexto compartido arrastra trabajo pendiente de
            // otras rutas, no se envía el comando ni se guarda nada (un guardado
            // ajeno podría confirmar ese trabajo involuntariamente).
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return true;

            BudgetQuantityEditResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecuteQuantityEditCommand(
                    _factory,
                    _context.DatabasePath,
                    new BudgetQuantityEditCommand(
                        _proyecto.Id,
                        concepto.Id,
                        BudgetProjectCalculationBase.FromProyecto(_proyecto),
                        BudgetConceptQuantityBase.FromConcepto(concepto),
                        propuesta));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Edición de cantidad: {ex.Message}");
                return true;
            }

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                UltimoEstadoEdicionCantidad = resultado.Status;
                // Conflicto, fallo o resultado incierto: la celda conserva el borrador
                // del usuario y el tracker compartido queda intacto. No se afirma guardado.
                System.Diagnostics.Debug.WriteLine(
                    $"Edición de cantidad no confirmada ({resultado.Status}): {resultado.Diagnostic}");
                return true;
            }

            UltimoEstadoEdicionCantidad = resultado.Status;

            AplicarCantidadConfirmada(rowIndex, concepto, resultado.Confirmed, propuesta);
            return true;
        }

        private bool TryParseCantidadPropuesta(int rowIndex, out decimal cantidad)
        {
            cantidad = 0m;
            var cell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
            if (cell?.Value is null) return false;

            if (cell.Value is decimal dec)
            {
                cantidad = dec;
                return true;
            }

            var texto = cell.Value.ToString();
            if (string.IsNullOrWhiteSpace(texto)) return false;

            return decimal.TryParse(texto, NumberStyles.Any, CultureInfo.CurrentCulture, out cantidad)
                || decimal.TryParse(texto, NumberStyles.Any, CultureInfo.InvariantCulture, out cantidad);
        }

        /// <summary>
        /// Vuelca lo CONFIRMADO en el grid: primero las entidades compartidas (concepto
        /// editado y agrupadores confirmados) y luego las celdas visibles, todo bajo el
        /// guard de reentrada para no disparar otro comando.
        /// </summary>
        private void AplicarCantidadConfirmada(
            int rowIndex,
            ConceptoPresupuesto concepto,
            BudgetQuantityEditSnapshot confirmado,
            decimal cantidadPropuesta)
        {
            _actualizandoDesdeComando = true;
            try
            {
                var filasPorConcepto = ConstruirIndiceFilasPorConcepto();

                SincronizarEntidadCompartida(concepto, confirmado.Concept);

                var celdas = BudgetRowEditFlowService.HandleQuantityCellChange(
                    _proyecto,
                    concepto,
                    cantidadPropuesta.ToString(CultureInfo.CurrentCulture));

                var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
                if (cantidadCell != null && celdas.HasChanges)
                    cantidadCell.Value = celdas.Cantidad;

                AplicarCeldasCalculadasConcepto(rowIndex, celdas);

                foreach (var agrupador in confirmado.Aggregators)
                {
                    var entidad = ResolverEntidadCompartida(filasPorConcepto, agrupador.ConceptId);
                    if (entidad is not null)
                        SincronizarEntidadCompartida(entidad, agrupador);

                    if (!filasPorConcepto.TryGetValue(agrupador.ConceptId, out int filaAgrupador))
                        continue;

                    var importeCell = ObtenerCeldaPorNombreInterno(filaAgrupador, "Importe");
                    if (importeCell != null)
                        importeCell.Value = agrupador.ImporteTotal.ToStringImporte();
                }
            }
            finally
            {
                _actualizandoDesdeComando = false;
            }
        }

        private void AplicarCeldasCalculadasConcepto(int rowIndex, BudgetQuantityChangeResult resultado)
        {
            if (!resultado.HasChanges) return;

            var puCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
            if (puCell != null) puCell.Value = resultado.PrecioUnitario.ToStringImporte();

            var importeCell = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");
            if (importeCell != null) importeCell.Value = resultado.Importe.ToStringImporte();

            var subtotalCell = ObtenerCeldaPorNombreInterno(rowIndex, "Subtotal");
            if (subtotalCell != null) subtotalCell.Value = resultado.Subtotal.ToStringImporte();

            var ivaCell = ObtenerCeldaPorNombreInterno(rowIndex, "IVA");
            if (ivaCell != null) ivaCell.Value = resultado.Iva.ToStringImporte();

            var totalCell = ObtenerCeldaPorNombreInterno(rowIndex, "Total");
            if (totalCell != null) totalCell.Value = resultado.Total.ToStringImporte();

            var puLetraCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitarioLetra");
            if (puLetraCell != null) puLetraCell.Value = resultado.PrecioUnitarioLetra;

            var totalLetraCell = ObtenerCeldaPorNombreInterno(rowIndex, "TotalLetra");
            if (totalLetraCell != null) totalLetraCell.Value = resultado.TotalLetra;
        }

        /// <summary>
        /// Índice Id de concepto → fila del grid, resuelto por el Tag (identidad estable
        /// de la fila). Es lo que permite localizar los agrupadores confirmados aunque
        /// su orden visual no coincida con el de la instantánea.
        /// </summary>
        private Dictionary<int, int> ConstruirIndiceFilasPorConcepto()
        {
            var indice = new Dictionary<int, int>();
            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                if (dgvPresupuesto.Rows[i].Tag is ConceptoPresupuesto concepto && concepto.Id > 0)
                    indice[concepto.Id] = i;
            }

            return indice;
        }

        private ConceptoPresupuesto? ResolverEntidadCompartida(
            IReadOnlyDictionary<int, int> filasPorConcepto,
            int conceptId)
        {
            if (filasPorConcepto.TryGetValue(conceptId, out int fila)
                && dgvPresupuesto.Rows[fila].Tag is ConceptoPresupuesto porFila)
            {
                return porFila;
            }

            return _context.ChangeTracker.Entries<ConceptoPresupuesto>()
                .Select(e => e.Entity)
                .FirstOrDefault(c => c.Id == conceptId);
        }

        /// <summary>
        /// Copia los valores confirmados sobre la entidad COMPARTIDA y deja su entrada
        /// como <c>Unchanged</c> con OriginalValues iguales a los actuales: así ningún
        /// <c>SaveChanges</c> posterior (ruta heredada de otras operaciones) vuelve a
        /// escribir esta fila.
        /// </summary>
        private void SincronizarEntidadCompartida(ConceptoPresupuesto entidad, BudgetConfirmedConceptRow confirmado)
        {
            entidad.Cantidad = confirmado.Cantidad;
            entidad.CostoDirectoUnitario = confirmado.CostoDirectoUnitario;
            entidad.CostoDirectoTotal = confirmado.CostoDirectoTotal;
            entidad.PrecioUnitario = confirmado.PrecioUnitario;
            entidad.ImporteTotal = confirmado.ImporteTotal;
            entidad.FechaModificacion = confirmado.FechaModificacion;

            var entry = _context.Entry(entidad);
            if (entry.State == EntityState.Detached) return;

            entry.OriginalValues.SetValues(entry.CurrentValues);
            entry.State = EntityState.Unchanged;
        }
    }
}
