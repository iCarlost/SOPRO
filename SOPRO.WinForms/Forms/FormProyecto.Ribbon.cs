using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Reporting.ReportColumns;

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
            DesuscribirSeleccionGrid();

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
                // P1: además del encabezado, un cambio de SELECCIÓN de celdas del
                // grid debe refrescar el ribbon (compartido para todos los grids).
                SuscribirSeleccionGrid(_formActivo);
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                AplicarTemaCompletoRibbon();

                // Si es el presupuesto, recalcular P.U. e importes con la configuración actual
                if (_formActivo is FormPresupuesto fp)
                    fp.RecargarPreciosDesdeBase();

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

        /// <summary>
        /// Suscribe al <c>SelectionChanged</c> del grid PRINCIPAL del form activo,
        /// de modo que un cambio de selección de CELDAS (1 celda, rango con Shift o
        /// multi-rango con Ctrl) refresque el estado contextual del ribbon. Es
        /// COMPARTIDO para todos los grids: no requiere que cada form notifique.
        /// </summary>
        private void SuscribirSeleccionGrid(IGridFormato form)
        {
            DesuscribirSeleccionGrid();
            if (form?.GridPrincipal is DataGridView grid)
            {
                _gridActivoSeleccion = grid;
                grid.SelectionChanged += FormActivo_SeleccionCeldasCambiada;
            }
        }

        private void DesuscribirSeleccionGrid()
        {
            if (_gridActivoSeleccion is null) return;
            _gridActivoSeleccion.SelectionChanged -= FormActivo_SeleccionCeldasCambiada;
            _gridActivoSeleccion = null;
        }

        /// <summary>
        /// El grid activo cambió su selección de celdas: se refresca TODO el estado
        /// contextual del ribbon de formato (habilitados + estilo efectivo). El
        /// guard <c>_cargandoRibbon</c> evita bucles de reentrancia (cargar el
        /// ribbon no cambia la selección del grid, pero se blinda igualmente).
        /// </summary>
        private void FormActivo_SeleccionCeldasCambiada(object? sender, EventArgs e)
        {
            if (_cargandoRibbon) return;

            if (_formActivo != null)
                ActualizarEstadoFormatoRibbon();
            else
                DesactivarRibbon();
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

        // ════════════════ Overflow del ribbon (grupos semánticos) ════════════════
        //
        // El ribbon se organiza en seis grupos físicos, de IZQUIERDA a DERECHA:
        // Datos → Exportar → Fuente → Alineación → Colores → Celdas. Al reducirse
        // el ancho se colapsan de DERECHA a IZQUIERDA (la parte visible es siempre
        // un prefijo de grupos), por lo que Datos y Exportar son los últimos en
        // ocultarse. El caption del grupo (lblGroup*) y los separadores viajan con
        // su grupo: se ocultan y reaparecen con él.
        //
        // Los selectores de Fuente (cboFuente, nudTamano) y el indicador
        // lblColumna no se pierden: cuando el grupo está oculto se ofrecen en el
        // menú "⋯" operando sobre los MISMOS controles reales (mismos handlers y
        // mismo estado habilitado), sin reparentarlos.

        private const int MargenDerechoOverflowRibbon = 8;
        private const int SeparacionBotonOverflowRibbon = 8;
        private const int MargenIzquierdoOverflowRibbon = 4;

        private readonly HashSet<Control> _controlesOcultosOverflowRibbon = new();
        private bool _clickMenuOverflowEnCurso;
        private bool _menuOverflowReconstruccionPendiente;
        private bool _suscripcionMenuOverflowLista;
        private bool _reconstruccionMenuOverflowDiferidaProgramada;

        private sealed class GrupoRibbonOverflow
        {
            public GrupoRibbonOverflow(string clave, string nombreCaption, params Control[] controles)
            {
                Clave = clave;
                NombreCaption = nombreCaption;
                Controles = controles ?? Array.Empty<Control>();
            }

            public string Clave { get; }

            public string NombreCaption { get; }

            public IReadOnlyList<Control> Controles { get; }
        }

        /// <summary>
        /// Grupos del ribbon en su orden físico de izquierda a derecha. Es la
        /// única fuente de verdad para el overflow y para el orden del menú.
        /// </summary>
        private IReadOnlyList<GrupoRibbonOverflow> ObtenerGruposRibbon()
        {
            return new[]
            {
                new GrupoRibbonOverflow("Datos", "lblGroupDatos",
                    btnRecalcularRibbon, btnDepurarRibbon, btnConsolidarInsumos, btnBuscarRibbon),
                new GrupoRibbonOverflow("Exportar", "lblGroupExportar",
                    btnExcelRibbon, btnPdfRibbon),
                new GrupoRibbonOverflow("Fuente", "lblGroupFuente",
                    cboFuente, nudTamano, btnNegrita, btnCursiva, lblColumna),
                new GrupoRibbonOverflow("Alineacion", "lblGroupAlineacion",
                    btnAlinIzq, btnAlinCen, btnAlinDer, btnAlinJus, btnAlinMed, btnAlinAba),
                new GrupoRibbonOverflow("Colores", "lblGroupColores",
                    btnColorTexto, btnColorFondo),
                new GrupoRibbonOverflow("Celdas", "lblGroupCeldas",
                    btnWrapRibbon, btnAplicarATodas),
            };
        }

        internal IReadOnlyList<string> ObtenerClavesGruposRibbon()
            => ObtenerGruposRibbon().Select(grupo => grupo.Clave).ToArray();

        internal IReadOnlyList<Control> ObtenerControlesGrupoRibbon(string clave)
            => ObtenerGruposRibbon().First(grupo => grupo.Clave == clave).Controles;

        /// <summary>
        /// Caption y separadores que acompañan visualmente a un grupo.
        /// </summary>
        internal IReadOnlyList<Control> ObtenerDecorGrupoRibbon(string clave)
        {
            var grupos = ObtenerGruposRibbon();
            var decor = ObtenerDecorGruposRibbon(grupos);
            var grupo = grupos.First(g => g.Clave == clave);
            return decor[grupo];
        }

        internal bool EstaOcultoPorOverflowRibbon(Control control)
            => control != null && _controlesOcultosOverflowRibbon.Contains(control);

        /// <summary>
        /// Cantidad de grupos visibles (prefijo) que caben en <paramref name="limiteDerecho"/>.
        /// El primer grupo cuyo borde derecho no cabe se oculta junto con todos los
        /// que están a su derecha; así el colapso es siempre de derecha a izquierda
        /// y el orden de prioridad queda fijado por el orden de la lista.
        /// </summary>
        internal static int CalcularGruposVisiblesRibbon(IReadOnlyList<int> bordesDerechos, int limiteDerecho)
        {
            if (bordesDerechos == null)
                return 0;

            for (int i = 0; i < bordesDerechos.Count; i++)
            {
                if (bordesDerechos[i] > limiteDerecho)
                    return i;
            }

            return bordesDerechos.Count;
        }

        internal void ActualizarOverflowRibbon()
        {
            if (panelRibbon == null || btnRibbonMas == null || cmsRibbonOverflow == null || _actualizandoOverflowRibbon)
                return;

            _actualizandoOverflowRibbon = true;
            panelRibbon.SuspendLayout();
            try
            {
                AsegurarSuscripcionMenuOverflowRibbon();

                var grupos = ObtenerGruposRibbon();
                var decor = ObtenerDecorGruposRibbon(grupos);

                // Estado de partida idempotente: cada control recibe su visibilidad
                // definitiva en una sola asignación (sin re-mostrar y volver a
                // ocultar, que podría provocar un parpadeo).
                _controlesOcultosOverflowRibbon.Clear();

                var bordes = grupos.Select(grupo => BordeDerechoGrupoRibbon(grupo, decor[grupo])).ToArray();
                int gruposVisibles = CalcularGruposVisiblesYPosicionarBotonOverflowRibbon(bordes);

                for (int i = 0; i < grupos.Count; i++)
                    AplicarVisibilidadGrupoRibbon(grupos[i], decor[grupos[i]], i < gruposVisibles);

                bool hayOcultos = _controlesOcultosOverflowRibbon.Count > 0;
                btnRibbonMas.Visible = hayOcultos;
                ReconstruirMenuOverflowRibbon();
            }
            finally
            {
                panelRibbon.ResumeLayout();
                _actualizandoOverflowRibbon = false;
            }
        }

        /// <summary>
        /// Calcula cuántos grupos caben y coloca el botón "⋯" inmediatamente a la
        /// derecha del último grupo visible (con un margen seguro), en vez de
        /// dejarlo anclado al borde del panel con un hueco grande en medio. Si no
        /// cabe ningún grupo, el botón queda en el margen izquierdo. Si no hay
        /// overflow, no se mueve del borde (y permanece oculto).
        /// </summary>
        private int CalcularGruposVisiblesYPosicionarBotonOverflowRibbon(IReadOnlyList<int> bordes)
        {
            // Estimación con el botón en el borde derecho: define el overflow real.
            PosicionarBotonOverflowRibbonEn(
                panelRibbon.ClientSize.Width - btnRibbonMas.Width - MargenDerechoOverflowRibbon);
            int margenDerecho = btnRibbonMas.Left - MargenDerechoOverflowRibbon;
            int gruposVisibles = CalcularGruposVisiblesRibbon(bordes, margenDerecho);

            if (gruposVisibles >= bordes.Count)
                return gruposVisibles;

            // Borde real del prefijo visible (tolerante a geometrías no monótonas).
            int bordePrefijoVisible = 0;
            for (int i = 0; i < gruposVisibles; i++)
                bordePrefijoVisible = Math.Max(bordePrefijoVisible, bordes[i]);

            int izquierda = gruposVisibles == 0
                ? MargenIzquierdoOverflowRibbon
                : bordePrefijoVisible + SeparacionBotonOverflowRibbon;

            PosicionarBotonOverflowRibbonEn(izquierda);

            // Recalcular por seguridad: la nueva posición conserva el mismo
            // prefijo (nunca oculta un grupo que ya cabía).
            return CalcularGruposVisiblesRibbon(bordes, btnRibbonMas.Left - MargenDerechoOverflowRibbon);
        }

        private void PosicionarBotonOverflowRibbonEn(int izquierda)
        {
            int x = Math.Max(MargenIzquierdoOverflowRibbon, izquierda);
            int xMaximo = panelRibbon.ClientSize.Width - btnRibbonMas.Width - MargenIzquierdoOverflowRibbon;
            if (xMaximo >= MargenIzquierdoOverflowRibbon)
                x = Math.Min(x, xMaximo);

            btnRibbonMas.Location = new Point(
                x,
                Math.Max(6, (panelRibbon.ClientSize.Height - btnRibbonMas.Height) / 2));
        }

        private void AplicarVisibilidadGrupoRibbon(
            GrupoRibbonOverflow grupo, IEnumerable<Control> decor, bool visible)
        {
            foreach (var control in grupo.Controles.Concat(decor))
            {
                if (control == null)
                    continue;

                control.Visible = visible;
                if (!visible)
                    _controlesOcultosOverflowRibbon.Add(control);
            }
        }

        private Dictionary<GrupoRibbonOverflow, List<Control>> ObtenerDecorGruposRibbon(
            IReadOnlyList<GrupoRibbonOverflow> grupos)
        {
            var decor = grupos.ToDictionary(grupo => grupo, _ => new List<Control>());

            foreach (var grupo in grupos)
            {
                var caption = ObtenerCaptionGrupoRibbon(grupo.NombreCaption);
                if (caption != null)
                    decor[grupo].Add(caption);
            }

            foreach (var separador in ObtenerSeparadoresRibbon())
            {
                var destino = GrupoMasCercanoALaDerechaRibbon(separador, grupos);
                if (destino != null && !decor[destino].Contains(separador))
                    decor[destino].Add(separador);
            }

            return decor;
        }

        private Control? ObtenerCaptionGrupoRibbon(string nombre)
        {
            if (panelRibbon == null || string.IsNullOrEmpty(nombre))
                return null;

            return panelRibbon.Controls.Find(nombre, false).FirstOrDefault();
        }

        /// <summary>
        /// Separadores verticales heredados (1 px). Se resuelven dinámicamente para
        /// que cada uno viaje con el grupo que tiene inmediatamente a su derecha,
        /// sin depender de coordenadas fijas: cuando el lane de diseño reemplace el
        /// layout siguen acompañando al grupo correcto.
        /// </summary>
        private IEnumerable<Control> ObtenerSeparadoresRibbon()
        {
            var candidatos = new Control?[]
            {
                lblSepGlobal, lblSepFuente, lblSepEstilo, lblSepAlin, lblSepReporte, label1, label2
            };

            return candidatos
                .Where(control => control != null && ReferenceEquals(control.Parent, panelRibbon))
                .Select(control => control!);
        }

        private GrupoRibbonOverflow? GrupoMasCercanoALaDerechaRibbon(
            Control separador, IReadOnlyList<GrupoRibbonOverflow> grupos)
        {
            int x = OrigenEnRibbon(separador).X;
            GrupoRibbonOverflow? siguiente = null;
            int siguienteIzquierda = int.MaxValue;
            GrupoRibbonOverflow? anterior = null;
            int anteriorIzquierda = int.MinValue;

            foreach (var grupo in grupos)
            {
                int izquierda = IzquierdaGrupoRibbon(grupo);
                if (izquierda == int.MaxValue)
                    continue;

                if (izquierda >= x && izquierda < siguienteIzquierda)
                {
                    siguiente = grupo;
                    siguienteIzquierda = izquierda;
                }
                else if (izquierda < x && izquierda > anteriorIzquierda)
                {
                    anterior = grupo;
                    anteriorIzquierda = izquierda;
                }
            }

            return siguiente ?? anterior;
        }

        private int IzquierdaGrupoRibbon(GrupoRibbonOverflow grupo)
        {
            int izquierda = int.MaxValue;
            foreach (var control in grupo.Controles)
            {
                if (control == null)
                    continue;

                izquierda = Math.Min(izquierda, OrigenEnRibbon(control).X);
            }

            return izquierda;
        }

        private int BordeDerechoGrupoRibbon(GrupoRibbonOverflow grupo, IEnumerable<Control> decor)
        {
            int borde = int.MinValue;
            foreach (var control in grupo.Controles.Concat(decor))
            {
                if (control == null)
                    continue;

                Point origen = OrigenEnRibbon(control);
                borde = Math.Max(borde, origen.X + control.Width);
            }

            return borde == int.MinValue ? 0 : borde;
        }

        /// <summary>
        /// Origen del control en coordenadas de panelRibbon, aunque el lane de
        /// diseño lo anide en un contenedor intermedio.
        /// </summary>
        private Point OrigenEnRibbon(Control control)
        {
            Point origen = control.Location;
            Control? padre = control.Parent;
            while (padre != null && !ReferenceEquals(padre, panelRibbon))
            {
                origen.Offset(padre.Location);
                padre = padre.Parent;
            }

            return origen;
        }

        private void ReconstruirMenuOverflowRibbon(bool forzarReconstruccion = false)
        {
            AsegurarSuscripcionMenuOverflowRibbon();

            // Nunca se vacía/repuebla el menú desde el Click de uno de sus items
            // (rompería el dispatch en curso) ni con el menú desplegado: la
            // reconstrucción se difiere hasta que el menú se cierre.
            if (!forzarReconstruccion && (_clickMenuOverflowEnCurso || cmsRibbonOverflow.Visible))
            {
                _menuOverflowReconstruccionPendiente = true;
                return;
            }

            _menuOverflowReconstruccionPendiente = false;
            cmsRibbonOverflow.Items.Clear();

            bool primerBloque = true;
            foreach (var grupo in ObtenerGruposRibbon())
            {
                bool grupoTieneOcultos = grupo.Controles.Any(_controlesOcultosOverflowRibbon.Contains);
                if (!grupoTieneOcultos)
                    continue;

                if (!primerBloque)
                    cmsRibbonOverflow.Items.Add(new ToolStripSeparator());
                primerBloque = false;

                // Orden físico dentro del grupo (selectores incluidos).
                foreach (var control in grupo.Controles)
                {
                    if (!_controlesOcultosOverflowRibbon.Contains(control))
                        continue;

                    if (control is Button boton)
                        AgregarItemOverflowRibbon(boton, ObtenerTextoOverflowRibbon(boton));
                    else if (ReferenceEquals(control, cboFuente))
                        AgregarSubmenuFuenteOverflowRibbon();
                    else if (ReferenceEquals(control, nudTamano))
                        AgregarSubmenuTamanoOverflowRibbon();
                    else if (ReferenceEquals(control, lblColumna))
                        AgregarIndicadorColumnaOverflowRibbon();
                }
            }

            btnRibbonMas.Enabled = cmsRibbonOverflow.Items.Count > 0;
        }

        private void AsegurarSuscripcionMenuOverflowRibbon()
        {
            if (_suscripcionMenuOverflowLista || cmsRibbonOverflow == null)
                return;

            cmsRibbonOverflow.Closed += (_, __) => NotificarMenuOverflowCerrado();
            _suscripcionMenuOverflowLista = true;
        }

        /// <summary>
        /// Cierre real del menú (o flush diferido equivalente): recién aquí es
        /// seguro vaciar y repoblar los items que el menú estaba despachando.
        /// </summary>
        internal void NotificarMenuOverflowCerrado()
        {
            if (IsDisposed || Disposing)
                return;

            if (!_menuOverflowReconstruccionPendiente || _clickMenuOverflowEnCurso
                || cmsRibbonOverflow == null || cmsRibbonOverflow.IsDisposed)
                return;

            _menuOverflowReconstruccionPendiente = false;
            ReconstruirMenuOverflowRibbon(forzarReconstruccion: true);
        }

        /// <summary>
        /// Si un comando refrescó el overflow durante su propio Click (sin handle
        /// todavía no hay bucle de mensajes), la reconstrucción se pospone y se
        /// ejecuta fuera del dispatch del item.
        /// </summary>
        private void ProgramarReconstruccionMenuOverflowRibbon()
        {
            if (!_menuOverflowReconstruccionPendiente || _reconstruccionMenuOverflowDiferidaProgramada)
                return;

            if (!IsHandleCreated || IsDisposed || Disposing)
                return; // Pruebas/sin bucle de mensajes: se hará al cerrar o reabrir.

            _reconstruccionMenuOverflowDiferidaProgramada = true;
            BeginInvoke(new Action(() =>
            {
                _reconstruccionMenuOverflowDiferidaProgramada = false;
                if (_menuOverflowReconstruccionPendiente
                    && !_clickMenuOverflowEnCurso
                    && !cmsRibbonOverflow.Visible)
                {
                    NotificarMenuOverflowCerrado();
                }
            }));
        }

        private void AgregarItemOverflowRibbon(Button button, string texto)
        {
            if (button == null)
                return;

            var item = new ToolStripMenuItem(texto)
            {
                Name = $"itemOverflow_{button.Name}",
                Enabled = button.Enabled,
                Tag = button
            };
            item.Click += OverflowRibbonItem_Click;
            cmsRibbonOverflow.Items.Add(item);
        }

        private void AgregarSubmenuFuenteOverflowRibbon()
        {
            var item = new ToolStripMenuItem("Fuente")
            {
                Name = "itemOverflowFuente",
                Enabled = cboFuente.Enabled,
                Tag = cboFuente
            };
            item.DropDownOpening += (_, __) => PoblarSubmenuFuenteRibbon(item);
            cmsRibbonOverflow.Items.Add(item);
        }

        private void AgregarSubmenuTamanoOverflowRibbon()
        {
            var item = new ToolStripMenuItem("Tamaño")
            {
                Name = "itemOverflowTamano",
                Enabled = nudTamano.Enabled,
                Tag = nudTamano
            };
            item.DropDownOpening += (_, __) => PoblarSubmenuTamanoRibbon(item);
            cmsRibbonOverflow.Items.Add(item);
        }

        private void AgregarIndicadorColumnaOverflowRibbon()
        {
            cmsRibbonOverflow.Items.Add(new ToolStripMenuItem($"Columna: {lblColumna.Text}")
            {
                Name = "itemOverflowColumna",
                Enabled = false,
                Tag = lblColumna
            });
        }

        /// <summary>
        /// Replica las fuentes del selector real sobre el mismo control: al elegir
        /// una opción se cambia <c>cboFuente.SelectedIndex</c>, que dispara el
        /// handler de siempre (Ribbon_Changed) y aplica el formato igual que el
        /// ribbon visible.
        /// </summary>
        internal void PoblarSubmenuFuenteRibbon(ToolStripMenuItem item)
        {
            if (item == null)
                return;

            item.DropDownItems.Clear();
            string? seleccionActual = cboFuente.SelectedItem?.ToString();

            foreach (object? opcion in cboFuente.Items)
            {
                string? nombre = opcion?.ToString();
                if (string.IsNullOrEmpty(nombre))
                    continue;

                var opcionItem = new ToolStripMenuItem(nombre)
                {
                    Checked = string.Equals(nombre, seleccionActual, StringComparison.Ordinal),
                    Enabled = cboFuente.Enabled
                };
                string fuente = nombre;
                opcionItem.Click += (_, __) =>
                {
                    int indice = cboFuente.FindStringExact(fuente);
                    if (indice >= 0)
                        cboFuente.SelectedIndex = indice;
                };
                item.DropDownItems.Add(opcionItem);
            }
        }

        /// <summary>
        /// Replica los tamaños disponibles del numeric real sobre el mismo control,
        /// respetando mínimo, máximo e incremento configurados.
        /// </summary>
        internal void PoblarSubmenuTamanoRibbon(ToolStripMenuItem item)
        {
            if (item == null)
                return;

            item.DropDownItems.Clear();
            decimal incremento = nudTamano.Increment > 0 ? nudTamano.Increment : 1m;

            for (decimal valor = nudTamano.Minimum; valor <= nudTamano.Maximum; valor += incremento)
            {
                decimal tamano = valor;
                var opcionItem = new ToolStripMenuItem(tamano.ToString("0.#"))
                {
                    Checked = tamano == nudTamano.Value,
                    Enabled = nudTamano.Enabled
                };
                opcionItem.Click += (_, __) =>
                    nudTamano.Value = Math.Min(Math.Max(tamano, nudTamano.Minimum), nudTamano.Maximum);
                item.DropDownItems.Add(opcionItem);
            }
        }

        private static string ObtenerTextoOverflowRibbon(Button button)
        {
            switch (button.Name)
            {
                case "btnNegrita": return "Negrita";
                case "btnCursiva": return "Cursiva";
                case "btnColorFondo": return "Color de fondo";
                case "btnColorTexto": return "Color de texto";
                case "btnAlinJus": return "Alinear arriba";
                case "btnAlinMed": return "Alinear al medio";
                case "btnAlinAba": return "Alinear abajo";
                case "btnAlinIzq": return "Alinear a la izquierda";
                case "btnAlinCen": return "Centrar";
                case "btnAlinDer": return "Alinear a la derecha";
                case "btnWrapRibbon": return "Ajustar";
                case "btnBuscarRibbon": return "Buscar";
                case "btnRecalcularRibbon": return "Recalcular";
                case "btnDepurarRibbon": return "Depurar";
                case "btnAplicarATodas": return "Aplicar";
                case "btnConsolidarInsumos": return "Consolidar";
                case "btnExcelRibbon": return "Excel";
                case "btnPdfRibbon": return "PDF";
                default: return string.IsNullOrWhiteSpace(button.Text) ? button.Name : button.Text;
            }
        }

        private void OverflowRibbonItem_Click(object? sender, EventArgs e)
        {
            if (sender is not ToolStripMenuItem item || item.Tag is not Button button || !button.Enabled)
                return;

            _clickMenuOverflowEnCurso = true;
            try
            {
                EjecutarClickBotonRibbonOculto(button);
            }
            finally
            {
                try
                {
                    // Refresca estado contextual (habilitados, indicator, etc.),
                    // pero el menú queda intacto mientras el Click está en curso.
                    ActualizarOverflowRibbon();
                }
                finally
                {
                    _clickMenuOverflowEnCurso = false;
                }

                ProgramarReconstruccionMenuOverflowRibbon();
            }
        }

        /// <summary>
        /// Dispara el comportamiento original del comando SIN hacerlo visible: un
        /// comando modal dejaría el botón reapareciendo en el ribbon mientras el
        /// diálogo está abierto. Se despacha el mismo evento Click (OnClick) que
        /// performClick ejecutaría, con el propio botón como sender y sin mover
        /// visibilidad ni geometría.
        /// </summary>
        private void EjecutarClickBotonRibbonOculto(Button button)
        {
            if (button == null || !button.Enabled)
                return;

            try
            {
                MethodInfo? onClick = OnClickMethod;
                if (onClick == null)
                    throw new InvalidOperationException(
                        "No se pudo resolver Control.OnClick para ejecutar el comando del overflow.");

                onClick.Invoke(button, new object[] { EventArgs.Empty });
            }
            catch (TargetInvocationException ex) when (ex.InnerException != null)
            {
                ExceptionDispatchInfo.Capture(ex.InnerException).Throw();
            }
        }

        private void btnRibbonMas_Click(object sender, EventArgs e)
        {
            if (cmsRibbonOverflow == null)
                return;

            AsegurarSuscripcionMenuOverflowRibbon();

            // Un refresco pendiente (de un clic anterior o de un resize con el
            // menú abierto) se aplica antes de mostrar: el menú no está abierto.
            if (_menuOverflowReconstruccionPendiente)
                ReconstruirMenuOverflowRibbon(forzarReconstruccion: true);

            if (cmsRibbonOverflow.Items.Count == 0)
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
        /// OnClick es el punto exacto donde ButtonBase.PerformClick despacha los
        /// handlers de Click. Invocarlo directamente permite ejecutar un comando
        /// del overflow sin hacer visible (ni mover) el botón real.
        /// </summary>
        private static readonly MethodInfo? OnClickMethod = typeof(Control).GetMethod(
            "OnClick", BindingFlags.Instance | BindingFlags.NonPublic);


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

            if (destinoCeldas)
            {
                // P2 (tipo Excel): la SELECCIÓN DE CELDAS manda sobre el encabezado
                // (mismo criterio que el coordinador). Se habilitan los comandos y se
                // VUELCA a los controles el estilo EFECTIVO de la selección; con
                // estado mixto o sin estilo resoluble se muestra un estado neutro.
                _cargandoRibbon = true;
                panelRibbon.Enabled = true;
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                HabilitarControlesFormato(true);
                CargarEstiloEfectivoEnRibbon(
                    _formatoSeleccionCoordinator.EstiloEfectivoSeleccion,
                    _formatoSeleccionCoordinator.EstadoMixto);
                lblColumna.Text = _formatoSeleccionCoordinator.DescripcionDestino;
                lblColumna.ForeColor = Color.White;
                _cargandoRibbon = false;
                ActualizarOverflowRibbon();
            }
            else if (_formActivo?.ColumnaSeleccionada != null)
            {
                // Encabezado activo sin celdas: comportamiento histórico (cargar los
                // valores de la columna en el ribbon).
                HabilitarControlesFormato(true);
                CargarColumnaEnRibbon(_formActivo.ColumnaSeleccionada);
            }
            else
            {
                // Sin encabezado ni selección de celdas formateable: comandos
                // deshabilitados y estado NEUTRO (sin arrastrar el valor anterior).
                _cargandoRibbon = true;
                panelRibbon.Enabled = true;
                btnExcelRibbon.Enabled = true;
                btnPdfRibbon.Enabled = true;
                btnBuscarRibbon.Enabled = _formActivoBuscable?.GridBusqueda != null;
                btnRecalcularRibbon.Enabled = _formActivoRecalculable != null;
                btnDepurarRibbon.Enabled = true;
                btnConsolidarInsumos.Enabled = _formActivoConsolidable?.ConsolidacionDisponible == true;
                HabilitarControlesFormato(false);
                AplicarEstiloNeutroRibbon();
                lblColumna.Text = ObtenerTextoIndicadorSinDestinoRibbon();
                lblColumna.ForeColor = Color.Gray;
                _cargandoRibbon = false;
                ActualizarOverflowRibbon();
            }

            ActualizarTooltipsDestinoRibbon();
        }

        /// <summary>
        /// Vuelca el estilo EFECTIVO de la selección de celdas a los controles del
        /// ribbon (P2, comportamiento tipo Excel): fuente, tamaño, negrita, cursiva,
        /// colores de texto/fondo, alineación H/V y ajuste. Con estado MIXTO o sin
        /// estilo resoluble pinta un estado NEUTRO/indeterminado para NO arrastrar el
        /// valor de la celda anterior. Debe invocarse con <c>_cargandoRibbon</c> en
        /// <c>true</c> para no disparar la aplicación de formato.
        /// </summary>
        private void CargarEstiloEfectivoEnRibbon(ReportStyleOverlay? estilo, bool mixto)
        {
            if (mixto || estilo is null)
            {
                AplicarEstiloNeutroRibbon();
                return;
            }

            int idx = cboFuente.FindStringExact(estilo.Fuente ?? "Segoe UI");
            cboFuente.SelectedIndex = idx >= 0 ? idx : 0;
            nudTamano.Value = estilo.TamanoFuente is >= 6
                ? (decimal)estilo.TamanoFuente.Value
                : nudTamano.Minimum;

            AplicarTemaBotonRibbon(btnNegrita, estilo.Negrita == true);
            AplicarTemaBotonRibbon(btnCursiva, estilo.Cursiva == true);
            AplicarTemaBotonRibbon(btnWrapRibbon, estilo.Wrap == true);

            var horizontal = estilo.AlineacionHorizontal ?? ReportTextAlignment.Izquierda;
            ActualizarBotonesAlin((AlineacionColumna)horizontal);

            var vertical = estilo.AlineacionVertical ?? ReportVerticalAlignment.Medio;
            ActualizarBotonesAlinVertical(
                FormatoHelper.ConvertirAlineacionDgv((AlineacionColumna)horizontal, (int)vertical));

            _colorMuestraFondoRibbon = TryColor(estilo.ColorFondo ?? string.Empty, Color.White);
            _colorMuestraTextoRibbon = TryColor(estilo.ColorFuente ?? string.Empty, Color.Black);
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
        }

        /// <summary>
        /// Estado NEUTRO/indeterminado del bloque de formato: toggles apagados,
        /// colores y alineación por defecto. Se usa sin selección o con estado
        /// mixto, para que el ribbon NO conserve el valor de la celda anterior.
        /// </summary>
        private void AplicarEstiloNeutroRibbon()
        {
            cboFuente.SelectedIndex = cboFuente.Items.Count > 0 ? 0 : -1;
            nudTamano.Value = nudTamano.Minimum;

            AplicarTemaBotonRibbon(btnNegrita, false);
            AplicarTemaBotonRibbon(btnCursiva, false);
            AplicarTemaBotonRibbon(btnWrapRibbon, false);
            AplicarTemaBotonRibbon(btnAlinIzq, false);
            AplicarTemaBotonRibbon(btnAlinCen, false);
            AplicarTemaBotonRibbon(btnAlinDer, false);
            AplicarTemaBotonRibbon(btnAlinJus, false);
            AplicarTemaBotonRibbon(btnAlinMed, false);
            AplicarTemaBotonRibbon(btnAlinAba, false);

            _colorMuestraFondoRibbon = Color.White;
            _colorMuestraTextoRibbon = Color.Black;
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
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
            DesuscribirSeleccionGrid();
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
