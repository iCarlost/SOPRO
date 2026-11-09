using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Recalculo y manejo de teclado del catálogo.
    /// </summary>
    public partial class FormCatalogoMaquinaria
    {

        public void RecalcularTodo() => CargarMaquinaria();

        private void OnDecimalesActualizados_Cat(object sender, EventArgs e)
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) dgvMaquinaria.Refresh(); }));
        }

        // ───────────────────────── Suscripción al bus propio ──────────────────

        private void SuscribirBusInsumos()
        {
            _onInsumos = OnInsumosModificados;
            InsumosModificados += _onInsumos;
            this.Disposed += (s, e) => LiberarSuscripcionPropia();
        }

        private void OnInsumosModificados(object? sender, EventArgs e) => RecargarTrasCambioDeInsumos();

        /// <summary>
        /// Desuscribe el evento ESTÁTICO propio. Idempotente y seguro de invocar
        /// tanto desde <see cref="OnFormClosed"/> como desde <c>Disposed</c>: el
        /// catálogo puede embeberse en una pestaña y descartarse con el proyecto
        /// sin que se dispare <c>FormClosed</c>, y el evento estático retendría
        /// esta instancia con su contexto ya liberado.
        /// </summary>
        private void LiberarSuscripcionPropia()
        {
            if (_suscripcionLiberada)
                return;

            _suscripcionLiberada = true;
            InsumosModificados -= _onInsumos;
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            LiberarSuscripcionPropia();
            base.OnFormClosed(e);
        }

        private void DgvMaquinaria_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Insert && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnNuevo_Click(sender, EventArgs.Empty);
                return;
            }

            if (CatalogoBatchDeletionHelper.DebeDespacharSupr(
                    e.KeyCode, e.Shift, e.Alt,
                    dgvMaquinaria.IsCurrentCellInEditMode,
                    ObtenerIdsSeleccionados().Count > 0))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                btnEliminar_Click(sender, EventArgs.Empty);
                return;
            }

            if (e.KeyCode == Keys.Enter && !e.Control && !e.Shift && !e.Alt)
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
                if (btnEditar.Enabled)
                    btnEditar_Click(sender, EventArgs.Empty);
            }
        }
    }
}
