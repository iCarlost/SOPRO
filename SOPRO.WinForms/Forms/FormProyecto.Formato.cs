using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Aplicación de formato desde el ribbon y recálculo global del proyecto.
    /// </summary>
    public partial class FormProyecto
    {

        private void AplicarRibbon()
        {
            if (_cargandoRibbon || _formActivo?.ColumnaSeleccionada == null) return;
            _formActivo.AplicarFormato(BuildFmt());
            AplicarAlineacionVerticalSeleccionActual();
        }

        private ColumnaPersonalizada BuildFmt() => new ColumnaPersonalizada
        {
            NombreFuente = cboFuente.SelectedItem?.ToString() ?? "Segoe UI",
            TamanoFuente = (int)nudTamano.Value,
            Negrita      = EstaBotonActivo(btnNegrita),
            Cursiva      = EstaBotonActivo(btnCursiva),
            Alineacion   = ObtenerAlin(),
            ColorFondo   = HexColor(_colorMuestraFondoRibbon),
            ColorFuente  = HexColor(_colorMuestraTextoRibbon),
            WrapTexto    = EstaBotonActivo(btnWrapRibbon),
            AlineacionVertical = EstaBotonActivo(btnAlinAba) ? 2 : EstaBotonActivo(btnAlinJus) ? 0 : 1,
        };

        // Eventos de controles del ribbon
        private void Ribbon_Changed(object sender, EventArgs e) => AplicarRibbon();

        private void Ribbon_ToggleEstilo(object sender, EventArgs e)
        {
            if (_cargandoRibbon) return;
            var btn = (Button)sender;
            var nuevoEstado = !EstaBotonActivo(btn);
            AplicarTemaBotonRibbon(btn, nuevoEstado);
            AplicarRibbon();
        }

        private void SetAlineacion(AlineacionColumna alin)
        {
            if (_cargandoRibbon) return;
            ActualizarBotonesAlin(alin);
            AplicarRibbon();
        }

        private void SetAlineacionVertical(DataGridViewContentAlignment alineacionBase)
        {
            if (_cargandoRibbon || _formActivo?.GridPrincipal == null) return;

            var grid = _formActivo.GridPrincipal;
            var col = ObtenerColumnaSeleccionadaGrid();
            if (col == null)
            {
                MessageBox.Show("Selecciona primero una columna desde su encabezado.",
                    "Sin columna seleccionada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var actual = col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet
                ? DataGridViewContentAlignment.MiddleLeft
                : col.DefaultCellStyle.Alignment;

            var horizontal = ObtenerHorizontal(actual);
            var nueva = CombinarAlineacion(alineacionBase, horizontal);

            col.DefaultCellStyle.Alignment = nueva;
            if (grid.Columns[col.Index].DefaultCellStyle != null)
                grid.Columns[col.Index].DefaultCellStyle.Alignment = nueva;

            ActualizarBotonesAlinVertical(nueva);
            _formActivo.AplicarFormato(BuildFmt());
            if (col.DefaultCellStyle.WrapMode == DataGridViewTriState.True)
                AjustarAutoAlturaFilas(grid);
            grid.Invalidate();
        }

        private void btnBuscarRibbon_Click(object sender, EventArgs e)
        {
            AbrirBuscadorFlotante();
        }

        private void AplicarIconoBotonRibbon(Button btn)
        {
            var state = GetRibbonButtonState(btn);
            if (state.IconType == null)
            {
                btn.Image = null;
                return;
            }

            if (btn is SoproButton soproButton)
            {
                soproButton.SoproIcon = state.IconType.Value;
                soproButton.SoproIconSize = state.IconSize;
                return;
            }

            var colorIcono = btn.Enabled ? btn.ForeColor : Color.FromArgb(120, 120, 120);
            var options = state.Options ?? SoproIconButtonOptions.ForRibbonText(state.IconType.Value, iconSize: state.IconSize);
            SoproRibbonButtonStyler.Apply(btn, options, colorIcono);
        }

        private void BtnColorFondo_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _colorMuestraFondoRibbon };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _colorMuestraFondoRibbon = dlg.Color;
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
            AplicarRibbon();
        }

        private void BtnColorTexto_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _colorMuestraTextoRibbon };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _colorMuestraTextoRibbon = dlg.Color;
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
            AplicarRibbon();
        }

        private void btnAplicarATodas_Click(object sender, EventArgs e)
        {
            if (_formActivo == null) return;
            _formActivo.AplicarFormatoGlobal(BuildFmt());
            FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);
            MessageBox.Show(
                "Fuente, tamano, estilo, alineacion y color de texto\naplicados a todas las columnas.\n(Color de fondo no modificado)",
                "Aplicado", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private async void btnRecalcularRibbon_Click(object sender, EventArgs e)
        {
            await RecalcularTodoElProyectoAsync();
        }

        private async Task RecalcularTodoElProyectoAsync()
        {
            var svc = new SOPRO.Application.Services.RecalculoGlobalService();
            var proyId = _proyecto.Id;
            var tabActiva = tabControl.SelectedTab;
            var formActivoAntes = tabActiva?.Controls.Count > 0 ? tabActiva.Controls[0] as Form : null;
            var controlConFocoAntes = ObtenerControlConFoco(formActivoAntes) ?? ObtenerControlConFoco(this);

            // Deshabilitar solo el botón para evitar doble ejecución sin perder foco del formulario activo.
            btnRecalcularRibbon.Enabled = false;
            Cursor = Cursors.WaitCursor;

            SOPRO.Application.Services.RecalculoGlobalResultado resultado = null;
            Exception error = null;

            try
            {
                var dbPathRecalculo = _context.DatabasePath;
                resultado = await Task.Run(() =>
                {
                    using var ctx = _factory.Create(dbPathRecalculo);
                    return svc.Ejecutar(ctx, proyId);
                });
            }
            catch (Exception ex)
            {
                error = ex;
            }
            finally
            {
                Cursor = Cursors.Default;
                btnRecalcularRibbon.Enabled = true;
            }

            if (error != null)
            {
                RestaurarFocoTrasRecalculo(tabActiva, formActivoAntes, controlConFocoAntes);
                MessageBox.Show($"Error en el recálculo global:{error.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return;
            }

            // Refrescar todos los tabs abiertos que implementen IRecalculable
            foreach (TabPage tab in tabControl.TabPages)
            {
                if (tab.Controls.Count > 0 && tab.Controls[0] is IRecalculable form)
                {
                    try { form.RecalcularTodo(); }
                    catch { /* no interrumpir por un tab */ }
                }
            }

            // Ajustar altura de filas del form activo
            if (_formActivo?.GridPrincipal != null)
                FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);

            RestaurarFocoTrasRecalculo(tabActiva, formActivoAntes, controlConFocoAntes);

            // Mostrar resultado discretamente como tooltip debajo del botón recalcular
            var tip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 0, ReshowDelay = 0 };
            // Mostrar debajo del botón (y = altura del botón + margen)
            tip.Show(resultado.ResumenTexto, btnRecalcularRibbon,
                0, btnRecalcularRibbon.Height + 4, 5000);
        }
    }
}
