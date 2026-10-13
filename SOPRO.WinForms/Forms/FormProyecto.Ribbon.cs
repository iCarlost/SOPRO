using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
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
            nudTamano.ValueChanged += Ribbon_Changed;
            btnNegrita.Click += Ribbon_ToggleEstilo;
            btnCursiva.Click += Ribbon_ToggleEstilo;
            btnColorFondo.Click += BtnColorFondo_Click;
            btnColorTexto.Click += BtnColorTexto_Click;

            ConfigurarIconosRibbon();
            AplicarTemaCompletoRibbon();
            // ShowAlways permite que los comandos DESHABILITADOS también muestren
            // su tooltip: así el tooltip explica por qué el comando está inactivo.
            toolTipRibbon.ShowAlways = true;
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
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                AplicarTemaCompletoRibbon();

                // Si es el presupuesto, recalcular P.U. e importes con la configuración actual
                if (_formActivo is FormPresupuesto fp)
                    fp.RefrescarPreciosDesdeDB();

                if (_formActivo.GridPrincipal != null)
                    FormatoHelper.AjustarAutoAlturaFilas(_formActivo.GridPrincipal);

                // Habilitados + indicador + tooltips según el DESTINO real
                // (columna / celdas / ninguno) decidido por el coordinador (3.1).
                ActualizarEstadoFormatoRibbon();

                ActualizarOverflowRibbon();
            }
            else
            {
                DesactivarRibbon();
            }
        }

        /// <summary>
        /// El grid activo notificó un cambio de selección (encabezado de columna
        /// hoy; también celdas cuando el grid implemente IGridSeleccionFormato).
        /// Se refresca TODO el estado contextual del ribbon de formato.
        /// </summary>
        private void FormActivo_ColumnaSeleccionadaCambiada(object sender, EventArgs e)
        {
            if (_formActivo != null)
            {
                ActualizarEstadoFormatoRibbon();
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
                SoproRibbonButtonStyler.Apply(btn, options, btn.Enabled ? btn.ForeColor : SoproTheme.Colors.RibbonDisabledIcon);
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
            btnRibbonMas.ForeColor = SoproTheme.Colors.RibbonText;
            btnRibbonMas.FlatAppearance.MouseOverBackColor = SoproTheme.Colors.RibbonHover;
            btnRibbonMas.FlatAppearance.MouseDownBackColor = SoproTheme.Colors.RibbonDown;
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

        private Color RibbonBaseColor => panelTop?.BackColor ?? SoproTheme.Colors.BrandMd;
        private Color RibbonActiveColor => SoproTheme.Colors.RibbonActive;

        private static readonly MethodInfo? SetStyleMethod = typeof(Control).GetMethod(
            "SetStyle", BindingFlags.Instance | BindingFlags.NonPublic);

        /// <summary>
        /// Habilita SupportsTransparentBackColor en un control cualquiera vía
        /// reflexión (mismo patrón que SoproTheme.EnableDoubleBuffering). Es
        /// necesario para los Button planos del ribbon (Negrita/Cursiva), que no
        /// son SoproButton y por tanto no heredan ese SetStyle del constructor.
        /// </summary>
        private static void HabilitarFondoTransparente(Control control)
        {
            if (control == null) return;
            SetStyleMethod?.Invoke(control, new object[] { ControlStyles.SupportsTransparentBackColor, true });
        }

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
            btn.ForeColor = activo ? Color.White : SoproTheme.Colors.RibbonText;
            // En reposo el botón es transparente para fundirse con el header
            // solido; activo usa el azul de marca con texto blanco. Los Button
            // planos (p. ej. Negrita/Cursiva) necesitan SupportsTransparentBackColor
            // vía reflexión; SoproButton ya lo habilita en su constructor.
            HabilitarFondoTransparente(btn);
            btn.BackColor = activo ? RibbonActiveColor : Color.Transparent;
            btn.FlatAppearance.MouseOverBackColor = SoproTheme.Colors.RibbonHover;
            btn.FlatAppearance.MouseDownBackColor = SoproTheme.Colors.RibbonDown;

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
            btn.FlatAppearance.MouseOverBackColor = SoproTheme.Colors.RibbonHover;
            btn.FlatAppearance.MouseDownBackColor = SoproTheme.Colors.RibbonDown;
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

            lblColumna.Text = col.Nombre;
            lblColumna.ForeColor = Color.White;
            panelRibbon.Enabled = true;

            _cargandoRibbon = false;
            ActualizarOverflowRibbon();
        }

        // ════════════════ Estado contextual del ribbon de formato (Paso 3.2) ════════════════
        //
        // El DESTINO real (columna vs celdas vs ninguno) lo decide exclusivamente
        // FormatoSeleccionCoordinator (Paso 3.1); aquí sólo se REFLEJA en la UI:
        // habilitados, indicador lblColumna y tooltips contextuales por comando.
        //
        // Fase 4.3 (formato por TIPO DE FILA): cuando el contrato exponga ese
        // destino, añadir su caso al switch de ActualizarTooltipsDestinoRibbon y
        // su texto al indicador. Mientras, no se inventa UI para él.

        /// <summary>
        /// Comandos de formato del ribbon con su texto base de tooltip (idéntico
        /// al estático del Designer, para que al sobreescribirlos con el destino
        /// la UI no cambie de vocabulario).
        /// </summary>
        private IEnumerable<(Control Control, string TextoBase)> ObtenerComandosFormatoRibbon()
        {
            yield return (cboFuente, "Tipo de fuente");
            yield return (nudTamano, "Tamano de fuente");
            yield return (btnNegrita, "Negrita");
            yield return (btnCursiva, "Cursiva");
            yield return (btnColorTexto, "Color de texto");
            yield return (btnColorFondo, "Color de fondo de celda");
            yield return (btnAlinIzq, "Alinear a la izquierda");
            yield return (btnAlinCen, "Centrar");
            yield return (btnAlinDer, "Alinear a la derecha");
            yield return (btnAlinJus, "Alinear arriba");
            yield return (btnAlinMed, "Alinear al medio");
            yield return (btnAlinAba, "Alinear abajo");
            yield return (btnWrapRibbon, "Ajustar texto");
        }

        /// <summary>
        /// Sincroniza al coordinador con el grid activo y refresca habilitados,
        /// indicador de destino y tooltips contextuales del bloque de formato.
        /// </summary>
        private void ActualizarEstadoFormatoRibbon()
        {
            _formatoSeleccionCoordinator.EstablecerGrid(_formActivo);
            bool destinoCeldas = _formatoSeleccionCoordinator.Destino == DestinoFormatoGrid.Celdas;

            if (_formActivo?.ColumnaSeleccionada != null)
            {
                // Encabezado activo: comportamiento histórico (cargar valores de la
                // columna en el ribbon). Si además hay celdas seleccionadas, el
                // coordinador prioriza CELDAS como destino: los valores cargados
                // sirven de referencia y el tooltip/indicador así lo aclaran.
                HabilitarControlesFormato(true);
                CargarColumnaEnRibbon(_formActivo.ColumnaSeleccionada);
            }
            else
            {
                // Sin encabezado: los comandos de formato se habilitan SOLO si el
                // grid expone una selección de celdas formateable (IGridSeleccionFormato).
                _cargandoRibbon = true;
                panelRibbon.Enabled = true;
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                HabilitarControlesFormato(destinoCeldas);

                lblColumna.Text = destinoCeldas
                    ? _formatoSeleccionCoordinator.DescripcionDestino
                    : ObtenerTextoIndicadorSinDestinoRibbon();
                lblColumna.ForeColor = destinoCeldas ? Color.White : Color.Gray;
                _cargandoRibbon = false;
                ActualizarOverflowRibbon();
            }

            ActualizarTooltipsDestinoRibbon();
        }

        /// <summary>
        /// Texto del indicador cuando no hay ningún destino de formato activo.
        /// Mantiene longitud similar al actual (lblColumna tiene ancho fijo).
        /// </summary>
        private string ObtenerTextoIndicadorSinDestinoRibbon()
            => _formActivo is FormFSR
                ? "Factor de Salario Real"
                : "Clic en encabezado o selecciona celdas";

        /// <summary>
        /// Reescribe los tooltips de los comandos de formato para que cada uno
        /// declare SOBRE QUÉ se aplicará: la columna X, la selección de N
        /// celda(s), o la guía de qué hay que seleccionar. Coherentes con
        /// <see cref="FormatoSeleccionCoordinator.DescripcionDestino"/>.
        /// </summary>
        private void ActualizarTooltipsDestinoRibbon()
        {
            if (toolTipRibbon == null) return;

            string sufijo;
            switch (_formatoSeleccionCoordinator.Destino)
            {
                case DestinoFormatoGrid.Columna:
                    sufijo = $"Aplicar a la {_formatoSeleccionCoordinator.DescripcionDestino}";
                    break;
                case DestinoFormatoGrid.Celdas:
                    sufijo = $"Aplicar a la {_formatoSeleccionCoordinator.DescripcionDestino}";
                    if (_formatoSeleccionCoordinator.EstadoMixto)
                        sufijo += " · las celdas tienen formatos distintos";
                    break;
                default:
                    // Ninguno (y futuros destinos sin UI aún, p. ej. tipo de fila en 4.3):
                    // tooltip guía, con ShowAlways también visible con el comando deshabilitado.
                    sufijo = _formActivo is FormFSR
                        ? "Este módulo no admite formato"
                        : "Selecciona una columna o un rango de celdas";
                    break;
            }

            foreach (var (control, textoBase) in ObtenerComandosFormatoRibbon())
            {
                if (control == null) continue;
                toolTipRibbon.SetToolTip(control, $"{textoBase} — {sufijo}");
            }
        }

        /// <summary>
        /// Restaura los tooltips estáticos (sin destino) de los comandos de formato.
        /// </summary>
        private void RestaurarTooltipsBaseRibbon()
        {
            if (toolTipRibbon == null) return;

            foreach (var (control, textoBase) in ObtenerComandosFormatoRibbon())
            {
                if (control == null) continue;
                toolTipRibbon.SetToolTip(control, textoBase);
            }
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
            lblColumna.Text = "-- sin seleccion --";
            lblColumna.ForeColor = Color.Gray;
            // Sin form activo no hay destino posible: tooltips al texto base.
            _formatoSeleccionCoordinator.EstablecerGrid(null);
            RestaurarTooltipsBaseRibbon();
            _cargandoRibbon = false;
            ActualizarOverflowRibbon();
        }

        /// <summary>
        /// Encendido/apagado del bloque de formato. Se llama SIEMPRE junto con
        /// <see cref="ActualizarTooltipsDestinoRibbon"/> (vía
        /// <see cref="ActualizarEstadoFormatoRibbon"/>) para que un comando
        /// inactivo explique su motivo en el tooltip.
        /// </summary>
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
            // Ajustar texto TAMBIÉN depende del destino: sin columna ni celdas su
            // aplicación era un no-op silencioso (AplicarRibbon salía sin hacer nada).
            btnWrapRibbon.Enabled = enabled;
            AplicarTemaCompletoRibbon();
            ActualizarOverflowRibbon();
        }
    }
}
