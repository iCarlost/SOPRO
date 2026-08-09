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
    /// Ribbon: inicialización, cambio de pestañas, temas, iconos y menú de overflow.
    /// </summary>
    public partial class FormProyecto
    {

        private void InicializarRibbon()
        {
            // Cargar fuentes del sistema
            cboFuente.Items.Clear();
            using var familias = new System.Drawing.Text.InstalledFontCollection();
            foreach (var f in familias.Families.OrderBy(x => x.Name))
                cboFuente.Items.Add(f.Name);

            // Eventos de controles del ribbon
            cboFuente.SelectedIndexChanged += Ribbon_Changed;
            nudTamano.ValueChanged          += Ribbon_Changed;
            btnNegrita.Click                += Ribbon_ToggleEstilo;
            btnCursiva.Click                += Ribbon_ToggleEstilo;
            btnColorFondo.Click             += BtnColorFondo_Click;
            btnColorTexto.Click             += BtnColorTexto_Click;

            ConfigurarIconosRibbon();
            AplicarTemaCompletoRibbon();
            panelRibbon.Resize += (_, __) =>
            {
                AjustarLayoutAccionesRibbon();
                ActualizarOverflowRibbon();
            };
            btnAlinIzq.Click += (s, ev) => SetAlineacion(AlineacionColumna.Izquierda);
            btnAlinCen.Click += (s, ev) => SetAlineacion(AlineacionColumna.Centro);
            btnAlinDer.Click += (s, ev) => SetAlineacion(AlineacionColumna.Derecha);
            btnAlinJus.Click += (s, ev) => SetAlineacionVertical(DataGridViewContentAlignment.TopLeft);
            btnAlinMed.Click += (s, ev) => SetAlineacionVertical(DataGridViewContentAlignment.MiddleLeft);
            btnAlinAba.Click += (s, ev) => SetAlineacionVertical(DataGridViewContentAlignment.BottomLeft);

            // Detectar cambio de tab
            tabControl.SelectedIndexChanged += TabControl_SelectedIndexChanged;
        }

        private void TabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            // Desuscribir del form anterior
            if (_formActivo != null)
            {
                _formActivo.ColumnaSeleccionadaCambiada -= FormActivo_ColumnaSeleccionadaCambiada;
                _formActivo = null;
                _formActivoRecalculable = null;
                _formActivoBuscable = null;
                if (_formActivoConsolidable != null) _formActivoConsolidable.EstadoConsolidacionCambiado -= FormActivoConsolidacionCambiada;
                _formActivoConsolidable = null;
            }

            // Buscar si el form activo implementa IGridFormato
            if (tabControl.SelectedTab?.Controls.Count > 0
                && tabControl.SelectedTab.Controls[0] is IGridFormato gf)
            {
                _formActivo = gf;
                _formActivoRecalculable = tabControl.SelectedTab.Controls[0] as IRecalculable;
                _formActivoBuscable = tabControl.SelectedTab.Controls[0] as IBusquedaGrid;
                _formActivoConsolidable = tabControl.SelectedTab.Controls[0] as IConsolidacionInsumos;
                if (_formActivoConsolidable != null) _formActivoConsolidable.EstadoConsolidacionCambiado += FormActivoConsolidacionCambiada;
                _formActivo.ColumnaSeleccionadaCambiada += FormActivo_ColumnaSeleccionadaCambiada;
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnWrapRibbon.Enabled = _formActivo.GridPrincipal != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                AplicarTemaCompletoRibbon();

                // Si es el presupuesto, recalcular P.U. e importes con la configuración actual
                if (_formActivo is FormPresupuesto fp)
                    fp.RefrescarPreciosDesdeDB();

                if (_formActivo.GridPrincipal != null)
                    FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);

                if (_formActivo.ColumnaSeleccionada != null)
                {
                    HabilitarControlesFormato(true);
                    CargarColumnaEnRibbon(_formActivo.ColumnaSeleccionada);
                }
                else
                {
                    _cargandoRibbon = true;
                    panelRibbon.Enabled = true;
                    HabilitarControlesFormato(false);
                    lblColumna.Text = tabControl.SelectedTab.Controls[0] is FormFSR
                        ? "Factor de Salario Real"
                        : "Clic en encabezado de columna para formatear";
                    lblColumna.ForeColor = System.Drawing.Color.Gray;
                    _cargandoRibbon = false;
                }

                ActualizarOverflowRibbon();
            }
            else
            {
                DesactivarRibbon();
            }
        }

        private void FormActivo_ColumnaSeleccionadaCambiada(object sender, EventArgs e)
        {
            if (_formActivo?.ColumnaSeleccionada != null)
            {
                HabilitarControlesFormato(true);
                CargarColumnaEnRibbon(_formActivo.ColumnaSeleccionada);
            }
            else if (_formActivo != null)
            {
                _cargandoRibbon = true;
                panelRibbon.Enabled = true;
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnWrapRibbon.Enabled = _formActivo.GridPrincipal != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                HabilitarControlesFormato(false);
                lblColumna.Text = tabControl.SelectedTab?.Controls.Count > 0 && tabControl.SelectedTab.Controls[0] is FormFSR
                    ? "Factor de Salario Real"
                    : "Clic en encabezado de columna para formatear";
                lblColumna.ForeColor = Color.Gray;
                _cargandoRibbon = false;
                ActualizarOverflowRibbon();
            }
            else
            {
                DesactivarRibbon();
            }
        }


        private void FormActivoConsolidacionCambiada(object sender, EventArgs e)
        {
            btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
            ActualizarOverflowRibbon();
        }

        private void btnConsolidarInsumos_Click(object sender, EventArgs e)
        {
            if (_formActivoConsolidable == null)
                return;

            _formActivoConsolidable.EjecutarConsolidacion();
            btnConsolidarInsumos.Enabled = _formActivoConsolidable.ConsolidacionDisponible;
            ActualizarOverflowRibbon();
        }

        private void ConfigurarIconosRibbon()
        {
            ConfigurarBotonIcono(btnBuscarRibbon, "Buscar", SoproIconType.Buscar, 16, showText: true);
            ConfigurarBotonIcono(btnExcelRibbon, string.Empty, SoproIconType.Excel, 16, showText: false);
            ConfigurarBotonIcono(btnPdfRibbon, string.Empty, SoproIconType.Pdf, 16, showText: false);
            ConfigurarBotonIcono(btnWrapRibbon, "Ajustar", SoproIconType.AjustarTexto, 16, showText: true);
            ConfigurarBotonIcono(btnRecalcularRibbon, "Recalcular", SoproIconType.Recalcular, 16, showText: true);
            ConfigurarBotonIcono(btnDepurarRibbon, "Depurar", SoproIconType.Depurar, 16, showText: true);
            ConfigurarBotonIcono(btnAplicarATodas, "Aplicar", SoproIconType.AplicarATodas, 16, showText: true);
            ConfigurarBotonIcono(btnConsolidarInsumos, "Consolidar", SoproIconType.Consolidar, 16, showText: true);

            ConfigurarBotonIcono(btnAlinIzq, string.Empty, SoproIconType.AlinearIzquierda, 16, showText: false);
            ConfigurarBotonIcono(btnAlinCen, string.Empty, SoproIconType.AlinearCentro, 16, showText: false);
            ConfigurarBotonIcono(btnAlinDer, string.Empty, SoproIconType.AlinearDerecha, 16, showText: false);
            ConfigurarBotonIcono(btnAlinJus, string.Empty, SoproIconType.AlinearArriba, 16, showText: false);
            ConfigurarBotonIcono(btnAlinMed, string.Empty, SoproIconType.AlinearMedio, 16, showText: false);
            ConfigurarBotonIcono(btnAlinAba, string.Empty, SoproIconType.AlinearAbajo, 16, showText: false);

        }

        private void ConfigurarBotonIcono(Button btn, string text, SoproIconType iconType, int iconSize, bool showText)
        {
            if (btn == null) return;

            if (btn is SoproButton soproButton)
            {
                if (string.IsNullOrWhiteSpace(soproButton.Text))
                    soproButton.Text = text;

                if (soproButton.SoproIcon == null)
                    soproButton.SoproIcon = iconType;

                if (soproButton.SoproIconSize <= 0)
                    soproButton.SoproIconSize = iconSize;

                if (!showText && soproButton.SoproShowText)
                    soproButton.SoproShowText = false;

                if (!showText && soproButton.SoproAutoSizeToContent)
                    soproButton.SoproAutoSizeToContent = false;
            }
            else
            {
                btn.Text = text;
                var options = showText
                    ? SoproIconButtonOptions.ForRibbonText(iconType, iconSize: iconSize)
                    : SoproIconButtonOptions.ForRibbonGlyph(iconType, iconSize);
                SoproRibbonButtonStyler.Apply(btn, options, btn.Enabled ? btn.ForeColor : Color.FromArgb(120, 120, 120));
            }

            var stateIconType = btn is SoproButton sb && sb.SoproIcon.HasValue ? sb.SoproIcon.Value : iconType;
            var stateIconSize = btn is SoproButton sb2 && sb2.SoproIconSize > 0 ? sb2.SoproIconSize : iconSize;
            btn.Tag = new RibbonButtonState(stateIconType, false, stateIconSize, null);
        }

        private bool DebeAplicarLayoutAutomaticoRibbonAcciones() => false;

        private void AjustarLayoutAccionesRibbon()
        {
            if (panelRibbon == null || !DebeAplicarLayoutAutomaticoRibbonAcciones())
                return;

            int topRowY = btnBuscarRibbon.Top;
            int bottomRowY = btnDepurarRibbon.Top;
            int x = lblSepGlobal.Right + SoproUiMetrics.RibbonSeparatorGap;

            ReubicarBotonRibbon(btnBuscarRibbon, x, topRowY);
            x = btnBuscarRibbon.Right + SoproUiMetrics.RibbonButtonHorizontalGap;

            ReubicarBotonRibbon(btnWrapRibbon, x, topRowY);
            x = btnWrapRibbon.Right + SoproUiMetrics.RibbonSeparatorGap;

            label1.Location = new Point(x, label1.Top);
            x = label1.Right + SoproUiMetrics.RibbonSeparatorGap;

            int stackWidth = Math.Max(btnRecalcularRibbon.Width, btnDepurarRibbon.Width);
            ReubicarBotonRibbon(btnRecalcularRibbon, x, topRowY, stackWidth);
            ReubicarBotonRibbon(btnDepurarRibbon, x, bottomRowY, stackWidth);
            x += stackWidth + SoproUiMetrics.RibbonButtonHorizontalGap;

            ReubicarBotonRibbon(btnAplicarATodas, x, topRowY);
            x = btnAplicarATodas.Right + SoproUiMetrics.RibbonSeparatorGap;

            lblSepReporte.Location = new Point(x, lblSepReporte.Top);
            x = lblSepReporte.Right + 20;

            label2.Location = new Point(x, label2.Top);
            x = label2.Right + 7;

            btnExcelRibbon.Location = new Point(x, btnExcelRibbon.Top);
        }

        private void ReubicarBotonRibbon(Button button, int x, int y, int? forcedWidth = null)
        {
            if (button == null)
                return;

            if (forcedWidth.HasValue)
                button.Width = forcedWidth.Value;

            button.Location = new Point(x, y);
        }

        private void AplicarTemaBotonOverflowRibbon()
        {
            if (btnRibbonMas == null)
                return;

            btnRibbonMas.UseVisualStyleBackColor = false;
            btnRibbonMas.FlatStyle = FlatStyle.Flat;
            btnRibbonMas.FlatAppearance.BorderSize = 0;
            btnRibbonMas.FlatAppearance.BorderColor = RibbonBaseColor;
            btnRibbonMas.BackColor = RibbonBaseColor;
            btnRibbonMas.ForeColor = Color.Silver;
            btnRibbonMas.FlatAppearance.MouseOverBackColor = ControlPaint.Light(RibbonBaseColor);
            btnRibbonMas.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(RibbonBaseColor);
        }

        private void ActualizarOverflowRibbon()
        {
            if (panelRibbon == null || btnRibbonMas == null || cmsRibbonOverflow == null || _actualizandoOverflowRibbon)
                return;

            _actualizandoOverflowRibbon = true;
            panelRibbon.SuspendLayout();
            try
            {
                btnRibbonMas.Location = new Point(Math.Max(4, panelRibbon.ClientSize.Width - btnRibbonMas.Width - 6), Math.Max(6, (panelRibbon.ClientSize.Height - btnRibbonMas.Height) / 2));

                foreach (var grupo in ObtenerGruposOverflowRibbon())
                {
                    foreach (var control in grupo)
                        control.Visible = true;
                }

                int rightLimit = btnRibbonMas.Left - 8;
                bool hayOcultos = false;

                foreach (var grupo in ObtenerGruposOverflowRibbon())
                {
                    int groupRight = grupo.Max(c => c.Right);
                    if (groupRight > rightLimit)
                    {
                        foreach (var control in grupo)
                            control.Visible = false;

                        hayOcultos = true;
                    }
                }

                btnRibbonMas.Visible = hayOcultos;
                ReconstruirMenuOverflowRibbon();
            }
            finally
            {
                panelRibbon.ResumeLayout();
                _actualizandoOverflowRibbon = false;
            }
        }

        private IEnumerable<Control[]> ObtenerGruposOverflowRibbon()
        {
            yield return new Control[] { lblSepReporte, label2, btnExcelRibbon, btnPdfRibbon };
            yield return new Control[] { btnAplicarATodas, btnConsolidarInsumos };
            yield return new Control[] { label1, btnRecalcularRibbon, btnDepurarRibbon };
            yield return new Control[] { lblSepGlobal, btnWrapRibbon, btnBuscarRibbon };
            yield return new Control[] { lblSepAlin, btnAlinJus, btnAlinMed, btnAlinAba, btnAlinIzq, btnAlinCen, btnAlinDer };
            yield return new Control[] { lblSepEstilo, btnColorFondo, btnColorTexto };
            yield return new Control[] { btnNegrita, btnCursiva };
        }

        private void ReconstruirMenuOverflowRibbon()
        {
            cmsRibbonOverflow.Items.Clear();

            AgregarItemOverflowRibbon(btnNegrita, "Negrita");
            AgregarItemOverflowRibbon(btnCursiva, "Cursiva");
            AgregarItemOverflowRibbon(btnColorFondo, "Color de fondo");
            AgregarItemOverflowRibbon(btnColorTexto, "Color de texto");
            AgregarItemOverflowRibbon(btnAlinJus, "Alinear arriba");
            AgregarItemOverflowRibbon(btnAlinMed, "Alinear medio");
            AgregarItemOverflowRibbon(btnAlinAba, "Alinear abajo");
            AgregarItemOverflowRibbon(btnAlinIzq, "Alinear izquierda");
            AgregarItemOverflowRibbon(btnAlinCen, "Alinear centro");
            AgregarItemOverflowRibbon(btnAlinDer, "Alinear derecha");
            AgregarItemOverflowRibbon(btnWrapRibbon, "Ajustar");
            AgregarItemOverflowRibbon(btnBuscarRibbon, "Buscar");
            AgregarItemOverflowRibbon(btnRecalcularRibbon, "Recalcular");
            AgregarItemOverflowRibbon(btnDepurarRibbon, "Depurar");
            AgregarItemOverflowRibbon(btnAplicarATodas, "Aplicar");
            AgregarItemOverflowRibbon(btnConsolidarInsumos, "Consolidar");
            AgregarItemOverflowRibbon(btnExcelRibbon, "Excel");
            AgregarItemOverflowRibbon(btnPdfRibbon, "PDF");

            btnRibbonMas.Enabled = cmsRibbonOverflow.Items.Count > 0;
        }

        private void AgregarItemOverflowRibbon(Button button, string texto)
        {
            if (button == null || button.Visible)
                return;

            var item = new ToolStripMenuItem(texto)
            {
                Enabled = button.Enabled,
                Tag = button
            };
            item.Click += OverflowRibbonItem_Click;
            cmsRibbonOverflow.Items.Add(item);
        }

        private void OverflowRibbonItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem item || item.Tag is not Button button || !button.Enabled)
                return;

            EjecutarClickBotonRibbonOculto(button);
        }

        private void EjecutarClickBotonRibbonOculto(Button button)
        {
            if (button == null)
                return;

            bool visibleOriginal = button.Visible;
            try
            {
                if (!visibleOriginal)
                    button.Visible = true;

                button.PerformClick();
            }
            finally
            {
                if (!visibleOriginal)
                    button.Visible = false;
            }
        }

        private void btnRibbonMas_Click(object sender, EventArgs e)
        {
            if (cmsRibbonOverflow == null || cmsRibbonOverflow.Items.Count == 0)
                return;

            cmsRibbonOverflow.Show(btnRibbonMas, new Point(0, btnRibbonMas.Height));
        }

        private RibbonButtonState GetRibbonButtonState(Button btn)
        {
            if (btn?.Tag is RibbonButtonState state)
                return state;

            if (btn?.Tag is bool active)
                return new RibbonButtonState(null, active, 16, null);

            return new RibbonButtonState(null, false, 16, null);
        }

        private Color RibbonBaseColor => panelTop?.BackColor ?? Color.FromArgb(51, 51, 76);
        private Color RibbonActiveColor => ControlPaint.Light(RibbonBaseColor);

        private bool EstaBotonActivo(Button btn)
        {
            return GetRibbonButtonState(btn).Active;
        }

        private void AplicarTemaBotonRibbon(Button btn, bool activo)
        {
            if (btn == null) return;

            var state = GetRibbonButtonState(btn) with { Active = activo };
            btn.Tag = state;
            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.BorderColor = RibbonBaseColor;
            btn.ForeColor = activo ? Color.White : Color.Silver;
            btn.BackColor = activo ? RibbonActiveColor : RibbonBaseColor;
            btn.FlatAppearance.MouseOverBackColor = activo
                ? ControlPaint.LightLight(RibbonActiveColor)
                : ControlPaint.Light(RibbonBaseColor);
            btn.FlatAppearance.MouseDownBackColor = activo
                ? ControlPaint.Dark(RibbonActiveColor)
                : ControlPaint.Dark(RibbonBaseColor);

            AplicarIconoBotonRibbon(btn);
        }

        private void AplicarTemaBotonColor(Button btn, Color sampleColor, string texto)
        {
            if (btn == null) return;

            btn.UseVisualStyleBackColor = false;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.FlatAppearance.BorderColor = RibbonBaseColor;
            btn.Text = texto;
            btn.BackColor = RibbonBaseColor;
            btn.ForeColor = sampleColor;
            btn.FlatAppearance.MouseOverBackColor = ControlPaint.Light(RibbonBaseColor);
            btn.FlatAppearance.MouseDownBackColor = ControlPaint.Dark(RibbonBaseColor);
            btn.Image = null;
        }

        private void AplicarTemaCompletoRibbon()
        {
            AplicarTemaBotonRibbon(btnNegrita, EstaBotonActivo(btnNegrita));
            AplicarTemaBotonRibbon(btnCursiva, EstaBotonActivo(btnCursiva));
            AplicarTemaBotonRibbon(btnWrapRibbon, EstaBotonActivo(btnWrapRibbon));

            AplicarTemaBotonRibbon(btnAlinIzq, EstaBotonActivo(btnAlinIzq));
            AplicarTemaBotonRibbon(btnAlinCen, EstaBotonActivo(btnAlinCen));
            AplicarTemaBotonRibbon(btnAlinDer, EstaBotonActivo(btnAlinDer));
            AplicarTemaBotonRibbon(btnAlinJus, EstaBotonActivo(btnAlinJus));
            AplicarTemaBotonRibbon(btnAlinMed, EstaBotonActivo(btnAlinMed));
            AplicarTemaBotonRibbon(btnAlinAba, EstaBotonActivo(btnAlinAba));

            AplicarTemaBotonRibbon(btnBuscarRibbon, false);
            AplicarTemaBotonRibbon(btnAplicarATodas, false);
            AplicarTemaBotonRibbon(btnConsolidarInsumos, false);
            AplicarTemaBotonRibbon(btnExcelRibbon, false);
            AplicarTemaBotonRibbon(btnPdfRibbon, false);
            AplicarTemaBotonRibbon(btnRecalcularRibbon, false);
            AplicarTemaBotonRibbon(btnDepurarRibbon, false);
            AplicarTemaBotonOverflowRibbon();

            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, '■'.ToString());
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, 'A'.ToString());
        }

        private void CargarColumnaEnRibbon(ColumnaPersonalizada col)
        {
            _cargandoRibbon = true;

            int idx = cboFuente.FindStringExact(col.NombreFuente ?? "Segoe UI");
            cboFuente.SelectedIndex = idx >= 0 ? idx : 0;
            nudTamano.Value = col.TamanoFuente >= 6 ? col.TamanoFuente : 9;

            AplicarTemaBotonRibbon(btnNegrita, col.Negrita);
            AplicarTemaBotonRibbon(btnCursiva, col.Cursiva);

            ActualizarBotonesAlin(col.Alineacion);

            _colorMuestraFondoRibbon = TryColor(col.ColorFondo, Color.White);
            _colorMuestraTextoRibbon = TryColor(col.ColorFuente, Color.Black);
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
            AplicarTemaBotonRibbon(btnWrapRibbon, col.WrapTexto);
            ActualizarBotonesAlinVertical(FormatoHelper.ConvertirAlineacionDgv(col.Alineacion, col.AlineacionVertical));

            lblColumna.Text      = col.Nombre;
            lblColumna.ForeColor = Color.White;
            panelRibbon.Enabled  = true;

            _cargandoRibbon = false;
            ActualizarOverflowRibbon();
        }

        private void DesactivarRibbon()
        {
            _cargandoRibbon = true;
            panelRibbon.Enabled = true;
            btnExcelRibbon.Enabled = false;
            btnPdfRibbon.Enabled = false;
            btnBuscarRibbon.Enabled = false;
            btnWrapRibbon.Enabled = false;
            btnRecalcularRibbon.Enabled = false;
            btnDepurarRibbon.Enabled = true;
            btnConsolidarInsumos.Enabled = false;
            HabilitarControlesFormato(false);
            lblColumna.Text      = "-- sin seleccion --";
            lblColumna.ForeColor = Color.Gray;
            _cargandoRibbon = false;
            ActualizarOverflowRibbon();
        }

        private void HabilitarControlesFormato(bool enabled)
        {
            cboFuente.Enabled = enabled;
            nudTamano.Enabled = enabled;
            btnNegrita.Enabled = enabled;
            btnCursiva.Enabled = enabled;
            btnAlinIzq.Enabled = enabled;
            btnAlinCen.Enabled = enabled;
            btnAlinDer.Enabled = enabled;
            btnAlinJus.Enabled = enabled;
            btnAlinMed.Enabled = enabled;
            btnAlinAba.Enabled = enabled;
            btnColorFondo.Enabled = enabled;
            btnColorTexto.Enabled = enabled;
            btnAplicarATodas.Enabled = enabled;
            AplicarTemaCompletoRibbon();
            ActualizarOverflowRibbon();
        }
    }
}
