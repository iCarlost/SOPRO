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
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Aplicación de formato desde el ribbon y recálculo global del proyecto.
    /// </summary>
    public partial class FormProyecto
    {
        /// <summary>
        /// Decide el DESTINO del formato del ribbon: selección de celdas
        /// (si el grid activo implementa <see cref="IGridSeleccionFormato"/>) o
        /// columna (comportamiento previo intacto).
        /// </summary>
        private readonly FormatoSeleccionCoordinator _formatoSeleccionCoordinator = new FormatoSeleccionCoordinator();

        /// <summary>
        /// Propiedad del ribbon que el usuario acaba de modificar. Permite aplicar a
        /// la selección de CELDAS SOLO esa propiedad (delta parcial) y preservar el
        /// resto del formato ya aplicado a cada celda.
        /// </summary>
        internal enum PropiedadFormatoRibbon
        {
            Fuente,
            TamanoFuente,
            Negrita,
            Cursiva,
            ColorFuente,
            ColorFondo,
            AlineacionHorizontal,
            AlineacionVertical,
        }

        /// <summary>
        /// Aplica al destino actual la PROPIEDAD del ribbon que cambió. Para la
        /// selección de CELDAS envía SÓLO esa propiedad (delta parcial) y el
        /// coordinador la FUSIONA sobre el overlay previo de cada celda: cada celda
        /// conserva exactamente lo que se le aplicó y una edición (p. ej. la
        /// alineación) NO arrastra ni pisa el estado de otra celda (p. ej. su fondo).
        /// Para la COLUMNA se conserva el comportamiento histórico (formato completo).
        /// </summary>
        private void AplicarRibbonPropiedad(PropiedadFormatoRibbon propiedad)
        {
            if (_cargandoRibbon) return;

            _formatoSeleccionCoordinator.EstablecerGrid(_formActivo);

            // Destino CELDAS: grid que implementa IGridSeleccionFormato con celdas
            // seleccionadas → delta por celda (pintado inmediato) y pares
            // antes/después listos para el undo/persistencia (Fase 4.1).
            if (_formatoSeleccionCoordinator.PuedeAplicarASeleccion)
            {
                _formatoSeleccionCoordinator.AplicarASeleccion(
                    RestringirAPropiedad(FormatoSeleccionCoordinator.ConstruirOverlay(BuildFmt()), propiedad));
                return;
            }

            // Destino COLUMNA: comportamiento existente (encabezado seleccionado).
            if (_formActivo?.ColumnaSeleccionada == null) return;
            _formActivo.AplicarFormato(BuildFmt());
            AplicarAlineacionVerticalSeleccionActual();
        }

        /// <summary>
        /// Devuelve un overlay con SOLO la propiedad indicada del formato completo del
        /// ribbon (el resto queda <c>null</c> = heredar / sin cambios). Así una edición
        /// de una sola propiedad (p. ej. alineación) NO viaja con los valores
        /// retenidos de otra (p. ej. el color de fondo elegido antes), que es lo que
        /// provocaba la "fuga" de formato entre celdas.
        /// </summary>
        internal static ReportStyleOverlay RestringirAPropiedad(
            ReportStyleOverlay completo,
            PropiedadFormatoRibbon propiedad)
        {
            ArgumentNullException.ThrowIfNull(completo);

            return propiedad switch
            {
                PropiedadFormatoRibbon.Fuente => new ReportStyleOverlay(Fuente: completo.Fuente),
                PropiedadFormatoRibbon.TamanoFuente => new ReportStyleOverlay(TamanoFuente: completo.TamanoFuente),
                PropiedadFormatoRibbon.Negrita => new ReportStyleOverlay(Negrita: completo.Negrita),
                PropiedadFormatoRibbon.Cursiva => new ReportStyleOverlay(Cursiva: completo.Cursiva),
                PropiedadFormatoRibbon.ColorFuente => new ReportStyleOverlay(ColorFuente: completo.ColorFuente),
                PropiedadFormatoRibbon.ColorFondo => new ReportStyleOverlay(ColorFondo: completo.ColorFondo),
                PropiedadFormatoRibbon.AlineacionHorizontal => new ReportStyleOverlay(AlineacionHorizontal: completo.AlineacionHorizontal),
                PropiedadFormatoRibbon.AlineacionVertical => new ReportStyleOverlay(AlineacionVertical: completo.AlineacionVertical),
                _ => completo,
            };
        }

        private ColumnaPersonalizada BuildFmt() => new ColumnaPersonalizada
        {
            NombreFuente = cboFuente.SelectedItem?.ToString() ?? "Segoe UI",
            TamanoFuente = (int)nudTamano.Value,
            Negrita = EstaBotonActivo(btnNegrita),
            Cursiva = EstaBotonActivo(btnCursiva),
            Alineacion = ObtenerAlin(),
            ColorFondo = HexColor(_colorMuestraFondoRibbon),
            ColorFuente = HexColor(_colorMuestraTextoRibbon),
            WrapTexto = EstaBotonActivo(btnWrapRibbon),
            AlineacionVertical = EstaBotonActivo(btnAlinAba) ? 2 : EstaBotonActivo(btnAlinJus) ? 0 : 1,
        };

        // Eventos de controles del ribbon
        private void Ribbon_Changed(object sender, EventArgs e)
            => AplicarRibbonPropiedad(ReferenceEquals(sender, nudTamano)
                ? PropiedadFormatoRibbon.TamanoFuente
                : PropiedadFormatoRibbon.Fuente);

        private void Ribbon_ToggleEstilo(object sender, EventArgs e)
        {
            if (_cargandoRibbon) return;
            var btn = (Button)sender;
            var nuevoEstado = !EstaBotonActivo(btn);
            AplicarTemaBotonRibbon(btn, nuevoEstado);
            AplicarRibbonPropiedad(ReferenceEquals(btn, btnCursiva)
                ? PropiedadFormatoRibbon.Cursiva
                : PropiedadFormatoRibbon.Negrita);
        }

        private void SetAlineacion(AlineacionColumna alin)
        {
            if (_cargandoRibbon) return;
            ActualizarBotonesAlin(alin);
            AplicarRibbonPropiedad(PropiedadFormatoRibbon.AlineacionHorizontal);
        }

        private void SetAlineacionVertical(DataGridViewContentAlignment alineacionBase)
        {
            if (_cargandoRibbon || _formActivo?.GridPrincipal == null) return;

            _formatoSeleccionCoordinator.EstablecerGrid(_formActivo);

            // Destino CELDAS: aplicar la alineación vertical a la selección
            // (pintado inmediato) sin tocar el encabezado/columna.
            if (_formatoSeleccionCoordinator.PuedeAplicarASeleccion)
            {
                ActualizarBotonesAlinVertical(alineacionBase);
                _formatoSeleccionCoordinator.AplicarASeleccion(
                    RestringirAPropiedad(
                        FormatoSeleccionCoordinator.ConstruirOverlay(BuildFmt()),
                        PropiedadFormatoRibbon.AlineacionVertical));
                return;
            }

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

            var colorIcono = btn.Enabled ? btn.ForeColor : SoproTheme.Colors.RibbonDisabledIcon;
            var options = state.Options ?? SoproIconButtonOptions.ForRibbonText(state.IconType.Value, iconSize: state.IconSize);
            SoproRibbonButtonStyler.Apply(btn, options, colorIcono);
        }

        private void BtnColorFondo_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _colorMuestraFondoRibbon };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _colorMuestraFondoRibbon = dlg.Color;
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
            AplicarRibbonPropiedad(PropiedadFormatoRibbon.ColorFondo);
        }

        private void BtnColorTexto_Click(object sender, EventArgs e)
        {
            using var dlg = new ColorDialog { Color = _colorMuestraTextoRibbon };
            if (dlg.ShowDialog() != DialogResult.OK) return;
            _colorMuestraTextoRibbon = dlg.Color;
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
            AplicarRibbonPropiedad(PropiedadFormatoRibbon.ColorFuente);
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
            try
            {
                await RecalcularTodoElProyectoAsync();
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                btnRecalcularRibbon.Enabled = true;
                MessageBox.Show(
                    RecalculoGlobalUiFeedback.MensajeFalloInesperado(ex.Message),
                    RecalculoGlobalUiFeedback.TituloFalloInesperado,
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
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

            SOPRO.Application.Contracts.Result<SOPRO.Application.Services.RecalculoGlobalResultado>? resultado = null;
            Exception? errorCalculo = null;

            try
            {
                var dbPathRecalculo = _context.DatabasePath;
                resultado = await Task.Run(() =>
                {
                    using var ctx = _factory.Create(dbPathRecalculo);
                    return svc.TryEjecutar(ctx, proyId);
                });
            }
            catch (Exception ex)
            {
                errorCalculo = ex;
            }
            finally
            {
                Cursor = Cursors.Default;
                btnRecalcularRibbon.Enabled = true;
            }

            if (errorCalculo != null)
            {
                RestaurarFocoSeguro(tabActiva, formActivoAntes, controlConFocoAntes);
                MostrarErrorCalculo(errorCalculo.Message);
                return;
            }

            if (resultado == null || !resultado.IsSuccess)
            {
                RestaurarFocoSeguro(tabActiva, formActivoAntes, controlConFocoAntes);
                MostrarErrorCalculo(resultado?.Error?.Message);
                return;
            }

            var recalculado = resultado.Value!;

            RefrescoPostRecalculoResultado refresco;
            try
            {
                refresco = EjecutarRefrescoPostRecalculo();
            }
            catch (Exception ex)
            {
                // El refresco ya no puede hacer fallar el flujo: el cálculo está guardado.
                refresco = RefrescoPostRecalculoResultado.ConError(ex);
            }

            RestaurarFocoSeguro(tabActiva, formActivoAntes, controlConFocoAntes);

            if (refresco.TieneFallos)
            {
                MessageBox.Show(
                    RecalculoGlobalUiFeedback.MensajeFalloRefresco(refresco.PestanasFallidas, refresco.Error),
                    RecalculoGlobalUiFeedback.TituloFalloRefresco,
                    MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            MostrarResumenRecalculo(recalculado);
        }

        /// <summary>
        /// Refresca las pestañas abiertas después de un recálculo exitoso. Un fallo
        /// por pestaña se cuenta y no interrumpe las restantes; cualquier otro fallo
        /// del refresco se devuelve para informarlo sin sugerir una reversión.
        /// </summary>
        internal RefrescoPostRecalculoResultado EjecutarRefrescoPostRecalculo()
        {
            int pestanasFallidas = 0;
            Exception? error = null;

            try
            {
                foreach (TabPage tab in tabControl.TabPages)
                {
                    if (tab.Controls.Count > 0 && tab.Controls[0] is IRecalculable form)
                    {
                        try { form.RecalcularTodo(); }
                        catch { pestanasFallidas++; }
                    }
                }

                // Ajustar altura de filas del form activo
                if (_formActivo?.GridPrincipal != null)
                    FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);
            }
            catch (Exception ex)
            {
                error = ex;
            }

            return new RefrescoPostRecalculoResultado(pestanasFallidas, error);
        }

        private static void MostrarErrorCalculo(string? detalle)
        {
            MessageBox.Show(
                RecalculoGlobalUiFeedback.MensajeFalloCalculo(detalle),
                RecalculoGlobalUiFeedback.TituloFalloCalculo,
                MessageBoxButtons.OK, MessageBoxIcon.Error);
        }

        private void MostrarResumenRecalculo(RecalculoGlobalResultado recalculado)
        {
            try
            {
                // Mostrar resultado discretamente como tooltip debajo del botón recalcular
                var tip = new ToolTip { AutoPopDelay = 5000, InitialDelay = 0, ReshowDelay = 0 };
                tip.Show(recalculado.ResumenTexto, btnRecalcularRibbon,
                    0, btnRecalcularRibbon.Height + 4, 5000);
            }
            catch
            {
                // El resumen es informativo; un fallo al mostrarlo no debe propagarse.
            }
        }

        private void RestaurarFocoSeguro(TabPage tabActiva, Form formActivoAntes, Control controlConFocoAntes)
        {
            try
            {
                RestaurarFocoTrasRecalculo(tabActiva, formActivoAntes, controlConFocoAntes);
            }
            catch
            {
                // La restauración de foco es best-effort y no debe ocultar el resultado.
            }
        }
    }
}
