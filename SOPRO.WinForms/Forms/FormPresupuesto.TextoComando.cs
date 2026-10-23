using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using System;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Edición de Descripción o Unidad de un concepto YA persistido (ADR-0001). La
    /// intención viaja como comando inmutable a
    /// <see cref="BudgetPersistenceService.ExecuteTextEditCommand"/>, que la aplica
    /// sobre un contexto NUEVO. La entidad compartida por el formulario no se toca
    /// antes de conocer el resultado; al confirmarse se sincroniza solo el campo
    /// editado y queda <c>Unchanged</c> para que el autoguardado heredado no lo
    /// reescriba.
    /// <para>
    /// Límite conocido de esta migración parcial: si el comando falla o entra en
    /// conflicto, este evento no ejecuta <see cref="GuardarCambios"/>, pero otras
    /// operaciones todavía pueden invocar la ruta heredada (p. ej. el pegado) y
    /// reescribir el borrador desde el grid. No se declara integridad completa de
    /// esas rutas hasta migrarlas.
    /// </para>
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Clasificación de la última edición de texto ejecutada como comando, o
        /// <c>null</c> si no llegó a enviarse (fila sin persistir, valor sin cambios
        /// o trabajo pendiente en el contexto compartido). Observabilidad interna
        /// para las pruebas del ensamblado; no es API pública ni se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoEdicionTexto { get; private set; }

        /// <summary>
        /// Consume la edición de Descripción/Unidad de una fila persistida. Devuelve
        /// <c>false</c> solo cuando la fila aún no existe en la base y debe seguir por
        /// la ruta heredada de alta; en cualquier otro caso el evento queda atendido y
        /// el llamador NO debe invocar <see cref="GuardarCambios"/>.
        /// Se expone a las pruebas del ensamblado porque asignar a la celda el mismo
        /// borrador no vuelve a disparar <c>CellValueChanged</c>.
        /// </summary>
        internal bool ProcesarEdicionTextoConComando(int rowIndex, string nombreInterno)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return false;

            if (!TryResolverCampoTexto(nombreInterno, out var campo))
                return false;

            var row = dgvPresupuesto.Rows[rowIndex];
            if (row.Tag is not ConceptoPresupuesto concepto || concepto.Id <= 0)
                return false;

            UltimoEstadoEdicionTexto = null;

            var celda = ObtenerCeldaPorNombreInterno(rowIndex, nombreInterno);
            if (celda == null)
                return true;

            string propuesta = celda.Value?.ToString() ?? string.Empty;
            string actual = campo == BudgetTextEditField.Descripcion
                ? concepto.Descripcion
                : concepto.Unidad;

            if (string.Equals(actual, propuesta, StringComparison.Ordinal))
                return true;

            // Conservador: si el contexto compartido arrastra trabajo pendiente de
            // otras rutas, no se envía el comando. El borrador permanece visible y
            // no se ejecuta el autoguardado heredado para este evento.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return true;

            BudgetTextEditResult resultado;
            try
            {
                resultado = BudgetPersistenceService.ExecuteTextEditCommand(
                    _factory,
                    _context.DatabasePath,
                    new BudgetTextEditCommand(
                        _proyecto.Id,
                        concepto.Id,
                        campo,
                        actual,
                        propuesta));
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Edición de texto: {ex.Message}");
                return true;
            }

            UltimoEstadoEdicionTexto = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                // Conflicto, fallo o resultado incierto: la celda conserva el borrador
                // del usuario y el tracker compartido queda intacto. No se afirma guardado.
                System.Diagnostics.Debug.WriteLine(
                    $"Edición de texto no confirmada ({resultado.Status}): {resultado.Diagnostic}");
                return true;
            }

            SincronizarEntidadTextoCompartida(concepto, resultado.Confirmed);
            return true;
        }

        private static bool TryResolverCampoTexto(string nombreInterno, out BudgetTextEditField campo)
        {
            if (string.Equals(nombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase))
            {
                campo = BudgetTextEditField.Descripcion;
                return true;
            }

            if (string.Equals(nombreInterno, "Unidad", StringComparison.OrdinalIgnoreCase))
            {
                campo = BudgetTextEditField.Unidad;
                return true;
            }

            campo = default;
            return false;
        }

        /// <summary>
        /// Copia sobre la entidad COMPARTIDA solo el campo confirmado y la marca de
        /// modificación, y deja su entrada <c>Unchanged</c> con OriginalValues iguales
        /// a los actuales: un <c>SaveChanges</c> posterior de otra operación no vuelve
        /// a escribir esta fila.
        /// </summary>
        private void SincronizarEntidadTextoCompartida(
            ConceptoPresupuesto entidad,
            BudgetTextEditSnapshot confirmado)
        {
            switch (confirmado.Field)
            {
                case BudgetTextEditField.Descripcion:
                    entidad.Descripcion = confirmado.Value;
                    break;
                case BudgetTextEditField.Unidad:
                    entidad.Unidad = confirmado.Value;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(confirmado), confirmado.Field, null);
            }

            entidad.FechaModificacion = confirmado.FechaModificacion;

            var entry = _context.Entry(entidad);
            if (entry.State == EntityState.Detached) return;

            entry.OriginalValues.SetValues(entry.CurrentValues);
            entry.State = EntityState.Unchanged;
        }
    }
}
