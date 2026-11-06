using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Workspace de matrices: selector embebido, panel de estado e importación desde Excel.
    /// </summary>
    public partial class FormPresupuesto
    {
        // Política de tamaño del workspace APU (Panel2 del split). Única fuente de verdad.
        // Valores en píxeles de diseño a 96 DPI; se escalan con EscalarPixelesDiseno.
        // Modo Abajo se rige por la dimensión de ALTO y el modo Derecha por la de ANCHO.
        private const int AltoMinimoFuncionalApu = 250;    // toolbar (31) + host operativo fuera del modo compacto
        private const int AltoMinimoDegradadoApu = 150;    // piso duro en ventanas pequeñas (modo compacto)
        private const double FactorAlturaWorkspace = 0.42; // crecimiento proporcional sobre el alto del split
        private const int AnchoMinimoFuncionalApu = 560;   // objetivo por defecto del modo Derecha
        private const int AnchoMinimoDegradadoApu = 320;   // piso duro del modo Derecha (scroll horizontal aceptado)
        private const double FactorAnchoWorkspace = 0.35;  // crecimiento proporcional sobre el ancho del split
        private const decimal CantidadMinimaSelector = 0.01m;
        private const decimal CantidadPorDefectoPanel = 1m;
        private bool _politicaWorkspaceInicializada;
        private bool _ajustandoDimensionWorkspace;
        private bool _splitterMovimientoUsuario;

        internal PosicionWorkspace PosicionWorkspaceActual => _posicionWorkspace;

        internal SplitContainer SplitWorkspace => splitContainer!;

        internal string TextoBotonToggleMatrices => btnToggleMatrices.Text!;

        internal string TextoBotonPosicion => btnWorkspacePosicion.Text!;

        private bool EsModoDerecha => _posicionWorkspace == PosicionWorkspace.Derecha;

        private void LoadWorkspacePanelState()
        {
            try
            {
                string? dir = Path.GetDirectoryName(WorkspacePanelStatePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                PresupuestoWorkspaceEstado estado;
                if (File.Exists(WorkspacePanelStatePath))
                {
                    estado = PresupuestoWorkspaceEstado.Leer(WorkspacePanelStatePath);
                }
                else if (File.Exists(LegacyWorkspacePanelStatePath))
                {
                    // La ruta heredada (solo Id) se importa a la nueva (base + Id) una
                    // sola vez; el archivo heredado NO se borra: otra app o proyecto
                    // puede seguir usándolo. Desde aquí solo escribe la nueva ruta.
                    estado = PresupuestoWorkspaceEstado.Leer(LegacyWorkspacePanelStatePath);
                    estado.Guardar(WorkspacePanelStatePath);
                }
                else
                {
                    estado = PresupuestoWorkspaceEstado.PorDefecto;
                }

                _posicionWorkspace = estado.Posicion;
                _workspacePanelHeight = estado.Alto;
                _workspacePanelWidth = estado.Ancho;
            }
            catch { }

            InicializarPoliticaWorkspace();
        }

        private void SaveWorkspacePanelState()
        {
            // No persistir tamaños fijados por la política (evita congelar la dimensión y realimentar Resize).
            // Solo escribe las preferencias actuales en memoria: nada recalcula
            // geometría aquí, así que ningún movimiento interno del split (que
            // WinForms emite también por DPI y relayout) puede escribir basura.
            if (_ajustandoDimensionWorkspace)
                return;

            try
            {
                string? dir = Path.GetDirectoryName(WorkspacePanelStatePath);
                if (!string.IsNullOrWhiteSpace(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

                new PresupuestoWorkspaceEstado(_posicionWorkspace, _workspacePanelHeight, _workspacePanelWidth)
                    .Guardar(WorkspacePanelStatePath);
            }
            catch { }
        }

        /// <summary>
        /// Movimiento de separador INICIADO por el usuario (ratón o teclado): WinForms
        /// solo dispara <see cref="SplitContainer.SplitterMoving"/> en esos recorridos.
        /// Cualquier otro <see cref="SplitContainer.SplitterMoved"/> proviene del propio
        /// control — mantenimiento de layout (FixedPanel.None) y escalamiento DPI en
        /// ScaleControl — y resulta en una geometría que NO representa la elección del
        /// usuario; persistir ahí es lo que hacía crecer el ancho/alto guardado hacia
        /// el tope del form (reporte del issue #92: al reabrir el módulo el panel
        /// aparecía al ancho máximo sin respeto al valor persistido).
        /// </summary>
        private void SplitContainer_WorkspaceSplitterMoving(object? sender, SplitterCancelEventArgs e)
            => _splitterMovimientoUsuario = true;

        /// <summary>
        /// Consumo del movimiento del separador. Solo un movimiento iniciado por el
        /// usuario actualiza la preferencia con la dimensión REAL de Panel2 en la
        /// orientación vigente (sin el ancho del separador); los SplitterMoved internos
        /// del control no tocan las preferencias ni el archivo. La reafirmación de la
        /// fila visible del presupuesto corre en todos los casos porque es de vista,
        /// no de preferencia.
        /// </summary>
        private void SplitContainer_WorkspaceSplitterMoved(object? sender, SplitterEventArgs e)
        {
            bool movimientoUsuario = _splitterMovimientoUsuario && !_ajustandoDimensionWorkspace;
            _splitterMovimientoUsuario = false;

            if (movimientoUsuario)
            {
                RegistrarDimensionWorkspaceDesdeSplit();
                SaveWorkspacePanelState();
            }

            if (!splitContainer.Panel2Collapsed)
                ProgramarAsegurarFilaActualVisibleEnPresupuesto();
        }

        /// <summary>
        /// Transfiere a la preferencia la dimensión de trabajo visible de Panel2
        /// (ancho en Derecha, alto en Abajo) leída de los BOUNDS reales del panel, sin
        /// derivar del SplitterDistance interno (que WinForms ajusta por su cuenta y
        /// que además incluía el ancho del separador en el recuento). Una dimensión
        /// igual al piso Panel2MinSize es válida: solo se descartan valores menores.
        /// </summary>
        private void RegistrarDimensionWorkspaceDesdeSplit()
        {
            if (splitContainer.Panel2Collapsed)
                return;

            int dimension = EsModoDerecha ? splitContainer.Panel2.Width : splitContainer.Panel2.Height;
            if (dimension < splitContainer.Panel2MinSize)
                return;

            if (EsModoDerecha)
                _workspacePanelWidth = dimension;
            else
                _workspacePanelHeight = dimension;
        }

        /// <summary>
        /// Escala una medida de diseño (96 DPI) al DPI real del formulario.
        /// </summary>
        private int EscalarPixelesDiseno(int pixelesBase)
        {
            int dpi = DeviceDpi > 0 ? DeviceDpi : 96;
            return (int)Math.Ceiling(pixelesBase * dpi / 96d);
        }

        /// <summary>
        /// Configura Panel2MinSize escalado por DPI como piso de degradación compacta
        /// de la orientación vigente (alto en Abajo, ancho en Derecha), nunca como
        /// mínimo funcional (eso lo resuelve el tope, no el mínimo duro).
        /// </summary>
        private void AplicarPanel2MinSizeEscaladoDpi()
        {
            int minimoDegradado = EscalarPixelesDiseno(
                EsModoDerecha ? AnchoMinimoDegradadoApu : AltoMinimoDegradadoApu);
            if (splitContainer.Panel2MinSize != minimoDegradado)
                splitContainer.Panel2MinSize = minimoDegradado;
        }

        /// <summary>
        /// Aplica la posición guardada al split ANTES de suscribir eventos: cambia la
        /// orientación sin mover controles (el presupuesto sigue en Panel1 y el
        /// workspace en Panel2 en ambas orientaciones) conservando el estado colapsado.
        /// </summary>
        private void AplicarPosicionGuardadaAlSplit()
        {
            bool guardPrevio = _ajustandoDimensionWorkspace;
            _ajustandoDimensionWorkspace = true;
            try
            {
                bool colapsado = splitContainer.Panel2Collapsed;
                splitContainer.Panel2Collapsed = true;
                splitContainer.Orientation = EsModoDerecha ? Orientation.Vertical : Orientation.Horizontal;
                AplicarPanel2MinSizeEscaladoDpi();
                splitContainer.Panel2Collapsed = colapsado;
            }
            catch
            {
                // Una geometría imposible al abrir degrada al default: Abajo.
                _posicionWorkspace = PosicionWorkspace.Abajo;
                try
                {
                    splitContainer.Orientation = Orientation.Horizontal;
                    AplicarPanel2MinSizeEscaladoDpi();
                }
                catch { }
                splitContainer.Panel2Collapsed = true;
            }
            finally
            {
                _ajustandoDimensionWorkspace = guardPrevio;
            }
        }

        /// <summary>
        /// Única fuente de la política de tamaño del workspace APU. Devuelve el tamaño
        /// objetivo (px) de Panel2 en la dimensión de trabajo de la orientación vigente
        /// (alto del split en Abajo, ancho en Derecha): mínimo funcional escalado por
        /// DPI cuando hay espacio, crecimiento proporcional con la medida disponible y
        /// tope que reserva Panel1MinSize al presupuesto. En ventanas demasiado
        /// pequeñas degrada (nunca impone un mínimo imposible).
        /// </summary>
        private int CalcularDimensionObjetivoWorkspace()
        {
            bool derecha = EsModoDerecha;
            int medidaSplit = derecha ? splitContainer.Width : splitContainer.Height;
            if (medidaSplit <= 0)
                return EscalarPixelesDiseno(derecha ? AnchoMinimoDegradadoApu : AltoMinimoDegradadoApu);

            int separador = Math.Max(0, splitContainer.SplitterWidth);
            int minimoFuncional = EscalarPixelesDiseno(derecha ? AnchoMinimoFuncionalApu : AltoMinimoFuncionalApu);
            int pisoDegradado = splitContainer.Panel2MinSize > 0
                ? splitContainer.Panel2MinSize
                : EscalarPixelesDiseno(derecha ? AnchoMinimoDegradadoApu : AltoMinimoDegradadoApu);

            // Tope: deja al presupuesto (Panel1) su área mínima.
            int tope = medidaSplit - splitContainer.Panel1MinSize - separador;

            // Preferencia del usuario si existe; si no, mínimo funcional con
            // crecimiento proporcional según la medida disponible.
            int guardada = derecha ? _workspacePanelWidth : _workspacePanelHeight;
            double factor = derecha ? FactorAnchoWorkspace : FactorAlturaWorkspace;
            int objetivo = guardada > 0
                ? guardada
                : Math.Max(minimoFuncional, (int)(medidaSplit * factor));

            // No invadir el área mínima del presupuesto.
            if (objetivo > tope)
                objetivo = tope;

            // Ventana pequeña: no forzar el mínimo funcional; bajar hasta el piso
            // degradado (el modo compacto del panel se encarga del resto).
            if (objetivo < pisoDegradado)
                objetivo = Math.Min(pisoDegradado, Math.Max(1, tope));

            return Math.Max(1, objetivo);
        }

        /// <summary>
        /// Recalcula y fija el tamaño del workspace (alto en Abajo, ancho en Derecha)
        /// si el panel está expandido, sin persistir el valor ni realimentar Resize.
        /// </summary>
        private void AplicarDimensionObjetivoWorkspace()
        {
            if (splitContainer.Panel2Collapsed)
                return;

            bool derecha = EsModoDerecha;
            int medidaSplit = derecha ? splitContainer.Width : splitContainer.Height;
            if (medidaSplit <= 0)
                return;

            int minimoCombinado = splitContainer.Panel1MinSize + splitContainer.Panel2MinSize + splitContainer.SplitterWidth;
            if (medidaSplit < minimoCombinado)
                return; // ventana demasiado pequeña: no forzar una geometría imposible

            int objetivo = CalcularDimensionObjetivoWorkspace();
            int distancia = medidaSplit - objetivo - splitContainer.SplitterWidth;
            distancia = Math.Max(splitContainer.Panel1MinSize, distancia);

            if (distancia == splitContainer.SplitterDistance)
                return;

            bool guardPrevio = _ajustandoDimensionWorkspace;
            _ajustandoDimensionWorkspace = true;
            try
            {
                splitContainer.SplitterDistance = distancia;
            }
            finally
            {
                _ajustandoDimensionWorkspace = guardPrevio;
            }
        }

        private void InicializarPoliticaWorkspace()
        {
            if (_politicaWorkspaceInicializada)
                return;

            _politicaWorkspaceInicializada = true;
            AplicarPosicionGuardadaAlSplit();
            splitContainer.Resize += SplitContainer_WorkspaceResize;
            this.Load += (_, __) =>
            {
                AplicarPanel2MinSizeEscaladoDpi();
                if (!splitContainer.Panel2Collapsed)
                    AplicarDimensionObjetivoWorkspace();
            };
            this.DpiChanged += (_, __) =>
            {
                AplicarPanel2MinSizeEscaladoDpi();
                if (!splitContainer.Panel2Collapsed)
                    AplicarDimensionObjetivoWorkspace();
            };

            ActualizarTextoBotonToggleMatrices();
            ActualizarTextoBotonPosicion();
        }

        private void SplitContainer_WorkspaceResize(object? sender, EventArgs e)
        {
            // Limpieza del marcador de movimiento de usuario: el split solo se
            // redimensiona con la ventana (nunca durante un arrastre del separador),
            // así que sanitizar aquí no invalida un arrastre en curso y sí mata un
            // marcador huérfano de un movimiento cancelado sin SplitterMoved.
            _splitterMovimientoUsuario = false;

            if (_ajustandoDimensionWorkspace)
                return;

            AplicarDimensionObjetivoWorkspace();
        }

        /// <summary>
        /// Texto único del botón de toggling de matrices: la flecha anuncia dónde
        /// vive el oculto/visible workspace en la orientación vigente.
        /// </summary>
        private void ActualizarTextoBotonToggleMatrices()
        {
            btnToggleMatrices.Text = EsModoDerecha
                ? (splitContainer.Panel2Collapsed ? "📐 Matrices ◀" : "📐 Matrices ▶")
                : (splitContainer.Panel2Collapsed ? "📐 Matrices ▼" : "📐 Matrices ▲");
        }

        /// <summary>
        /// Texto y marca del selector compacto de posición en la toolbar del workspace.
        /// </summary>
        private void ActualizarTextoBotonPosicion()
        {
            bool derecha = EsModoDerecha;
            btnWorkspacePosicion.Text = derecha ? "Posición: Derecha" : "Posición: Abajo";
            menuPosicionAbajo.Checked = !derecha;
            menuPosicionDerecha.Checked = derecha;
        }

        /// <summary>
        /// Cambia la posición del workspace entre Abajo y Derecha reutilizando el
        /// MISMO split, host y panel embebido: colapsa, cambia la orientación, aplica
        /// mínimos escalados por DPI, restaura el estado colapsado previo y recoloca
        /// el SplitterDistance con la política vigente. La orientación NO toca el
        /// panel embebido (mismo contenedor), así que ninguna edición de cabecera se
        /// descarta ni se resuelve aquí: los guard de cabecera quedan en las rutas de
        /// cierre y navegación del panel. En ventanas demasiado pequeñas la
        /// orientación no cambia (sin excepción) y el form queda consistente.
        /// </summary>
        private void CambiarPosicionWorkspace(PosicionWorkspace nueva)
        {
            if (nueva == _posicionWorkspace)
                return;

            bool estabaColapsado = splitContainer.Panel2Collapsed;
            PosicionWorkspace previa = _posicionWorkspace;

            // Aborto determinístico: si la posición pedida no cabe en su mínimo
            // degradado, no se cambia nada (el setter de SplitContainer degrada
            // geométricamente sin lanzar; la política evita el estado a medias).
            if (!CabePosicionEnSplit(nueva))
            {
                ActualizarTextoBotonPosicion();
                return;
            }

            bool aplicada = false;
            bool guardPrevio = _ajustandoDimensionWorkspace;
            _ajustandoDimensionWorkspace = true;
            try
            {
                try
                {
                    splitContainer.Panel2Collapsed = true;
                    splitContainer.Orientation = OrientacionDe(nueva);
                    _posicionWorkspace = nueva;
                    AplicarPanel2MinSizeEscaladoDpi();
                    if (!estabaColapsado)
                    {
                        splitContainer.Panel2Collapsed = false;
                        AplicarDimensionObjetivoWorkspace();
                    }
                    aplicada = true;
                }
                catch
                {
                    // Ventana demasiado pequeña para la nueva orientación: restaurar
                    // la geometría vigente en lugar de partir el form a medias.
                    aplicada = false;
                    _posicionWorkspace = previa;
                    try
                    {
                        splitContainer.Panel2Collapsed = true;
                        if (splitContainer.Orientation != OrientacionDe(previa))
                        {
                            splitContainer.Orientation = OrientacionDe(previa);
                            AplicarPanel2MinSizeEscaladoDpi();
                        }
                    }
                    catch { }
                }
            }
            finally
            {
                // El estado colapsado previo SIEMPRE se restaura: ni un aborto por
                // ventana pequeña pierde el workspace abierto (o cerrado) al iniciar.
                bool destinoColapsado = estabaColapsado;
                if (splitContainer.Panel2Collapsed != destinoColapsado)
                {
                    try
                    {
                        splitContainer.Panel2Collapsed = destinoColapsado;
                    }
                    catch
                    {
                        try { splitContainer.Panel2Collapsed = true; } catch { }
                    }
                }
                _ajustandoDimensionWorkspace = guardPrevio;
            }

            if (aplicada)
            {
                SaveWorkspacePanelState();
                ActualizarTextoBotonToggleMatrices();
            }

            ActualizarTextoBotonPosicion();
            ActualizarEstadoWorkspace(_selectorApuEmbebido != null);

            if (!estabaColapsado)
            {
                Control? superficie = (Control?)_selectorApuEmbebido ?? _panelMatricesEmbebido;
                superficie?.Focus();
                ProgramarAsegurarFilaActualVisibleEnPresupuesto();
            }
        }

        private static Orientation OrientacionDe(PosicionWorkspace posicion)
            => posicion == PosicionWorkspace.Derecha ? Orientation.Vertical : Orientation.Horizontal;

        private void menuPosicionAbajo_Click(object? sender, EventArgs e)
            => CambiarPosicionWorkspace(PosicionWorkspace.Abajo);

        private void menuPosicionDerecha_Click(object? sender, EventArgs e)
            => CambiarPosicionWorkspace(PosicionWorkspace.Derecha);

        private void EnsureWorkspaceExpanded()
        {
            if (splitContainer.Panel2Collapsed)
                AbrirWorkspaceSeguro();

            ActualizarTextoBotonToggleMatrices();
            ProgramarAsegurarFilaActualVisibleEnPresupuesto();
        }

        /// <summary>
        /// Apertura determinística sin excepción (contrato del issue #92): si Derecha
        /// no cabe en su mínimo combinado pero Abajo SÍ, cae a Abajo con un flip de
        /// orientación en colapso (mínimos relajados solo dentro del flip y
        /// restaurados garantizados en finally) y persiste la caída solo después de
        /// que la expansión tuvo éxito. Si ni Derecha ni Abajo cabe, el workspace
        /// permanece colapsado y se conserva por completo la preferencia y el estado
        /// previos. Nunca deja mínimos de 1px en estado visible y nunca lanza.
        /// </summary>
        private void AbrirWorkspaceSeguro()
        {
            // 1) Caída a Abajo solo si Derecha no cabe y Abajo cabe (flip en colapso).
            bool cayo = false;
            bool guardPrevio = _ajustandoDimensionWorkspace;
            _ajustandoDimensionWorkspace = true;
            try
            {
                if (EsModoDerecha && !CabePosicionEnSplit(_posicionWorkspace)
                    && CabePosicionEnSplit(PosicionWorkspace.Abajo))
                {
                    cayo = DegradarAPosicionAbajo();
                }
            }
            finally
            {
                _ajustandoDimensionWorkspace = guardPrevio;
            }
            ActualizarTextoBotonPosicion();

            // 2) Sin superficie viable en ninguna orientación: queda colapsado y
            //    conserva la preferencia y el estado previos (nada se persiste).
            if (!CabePosicionEnSplit(_posicionWorkspace))
            {
                ActualizarTextoBotonToggleMatrices();
                return;
            }

            // 3) Expansión con política, en UNA zona real de guardado bloqueado.
            guardPrevio = _ajustandoDimensionWorkspace;
            _ajustandoDimensionWorkspace = true;
            try
            {
                try
                {
                    splitContainer.Panel2Collapsed = false;
                    AplicarDimensionObjetivoWorkspace();
                }
                catch
                {
                    try { splitContainer.Panel2Collapsed = true; } catch { }
                }
            }
            finally
            {
                _ajustandoDimensionWorkspace = guardPrevio;
            }

            // 4) La caída se persiste solo si la expansión tuvo éxito.
            if (cayo && !splitContainer.Panel2Collapsed)
                SaveWorkspacePanelState();

            ActualizarTextoBotonToggleMatrices();
        }

        /// <summary>
        /// Ahorro mínimo determinística: cabe (mínimo combinado escalado del PISO de la
        /// orientación pedida) o no cabe; el setter de SplitContainer degrada por su
        /// cuenta sin excepción; esta pregunta decide la política (caída/aborto).
        /// </summary>
        private bool CabePosicionEnSplit(PosicionWorkspace posicion)
        {
            int pisoObjetivo = EscalarPixelesDiseno(posicion == PosicionWorkspace.Derecha
                ? AnchoMinimoDegradadoApu
                : AltoMinimoDegradadoApu);
            int minimoCombinado = splitContainer.Panel1MinSize + pisoObjetivo + splitContainer.SplitterWidth;
            return posicion == PosicionWorkspace.Derecha
                ? splitContainer.Width >= minimoCombinado
                : splitContainer.Height >= minimoCombinado;
        }

        /// <summary>
        /// Degradación determinística a Abajo SOLO en colapso: relaja los mínimos a
        /// 1px únicamente para ejecutar el flip de orientación y los restaura
        /// GARANTIZADO en finally (nunca quedan 1px en estado visible). Si el flip no
        /// pudo aplicarse, revierte la posición vigente sin cambios de estado.
        /// </summary>
        private bool DegradarAPosicionAbajo()
        {
            PosicionWorkspace previa = _posicionWorkspace;
            int p1MinOriginal = splitContainer.Panel1MinSize;
            bool aplicada = false;
            try
            {
                splitContainer.Panel2Collapsed = true;
                // El setter de Orientation valida el SplitterDistance frente a los
                // mínimos; una split metricamente imposible exige abrir los mínimos
                // SOLO durante el flip (la misma propiedad ya demuestra aceptar un
                // mínimo imposible; el estado completo queda colapsado).
                splitContainer.Panel1MinSize = 1;
                splitContainer.Panel2MinSize = 1;
                try
                {
                    splitContainer.Orientation = Orientation.Horizontal;
                    _posicionWorkspace = PosicionWorkspace.Abajo;
                    aplicada = true;
                }
                catch
                {
                    _posicionWorkspace = previa;
                    try { splitContainer.Orientation = Orientation.Vertical; } catch { }
                }
                return aplicada;
            }
            finally
            {
                try { splitContainer.Panel1MinSize = p1MinOriginal; } catch { }
                try { AplicarPanel2MinSizeEscaladoDpi(); } catch { }
            }
        }

        private void ActualizarEstadoWorkspace(bool mostrandoSelector)
        {
            lblWorkspaceTitulo.Text = mostrandoSelector ? "Área de trabajo: Selector APU" : "Área de trabajo: Matriz";
            btnWorkspaceMatriz.BackColor = mostrandoSelector ? Color.Transparent : Color.FromArgb(221, 235, 247);
            btnWorkspaceSelector.BackColor = mostrandoSelector ? Color.FromArgb(221, 235, 247) : Color.Transparent;
            btnWorkspaceMatriz.Font = new Font(btnWorkspaceMatriz.Font, mostrandoSelector ? FontStyle.Regular : FontStyle.Bold);
            btnWorkspaceSelector.Font = new Font(btnWorkspaceSelector.Font, mostrandoSelector ? FontStyle.Bold : FontStyle.Regular);
            btnWorkspaceSelector.Enabled = dgvPresupuesto.CurrentRow != null && ((ObtenerCeldaPorNombreInterno(dgvPresupuesto.CurrentRow.Index, "Tipo")?.Value?.ToString()) == "Concepto");
            btnWorkspaceCerrar.Enabled = !splitContainer.Panel2Collapsed;
        }

        private void MostrarPanelMatrizEmbebido(bool recargarFilaActual)
        {
            EnsureWorkspaceExpanded();

            if (_selectorApuEmbebido != null)
            {
                try
                {
                    panelMatricesHost.Controls.Remove(_selectorApuEmbebido);
                    _selectorApuEmbebido.Dispose();
                }
                catch { }
                _selectorApuEmbebido = null;
            }

            _selectorApuEmbebidoRowIndex = -1;
            if (_panelMatricesEmbebido != null)
            {
                if (!panelMatricesHost.Controls.Contains(_panelMatricesEmbebido))
                    panelMatricesHost.Controls.Add(_panelMatricesEmbebido);

                _panelMatricesEmbebido.Dock = DockStyle.Fill;
                _panelMatricesEmbebido.Visible = true;
                _panelMatricesEmbebido.BringToFront();

                if (recargarFilaActual && dgvPresupuesto.CurrentRow != null)
                    _panelMatricesEmbebido.NotificarFilaCambiada(dgvPresupuesto.CurrentRow.Index);
            }

            ActualizarEstadoWorkspace(false);
        }

        private static bool DebeUsarDescripcionDeMatriz(string? descripcionActual, string? descripcionMatriz)
        {
            string texto = (descripcionActual ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(texto))
                return true;

            if (string.IsNullOrWhiteSpace(descripcionMatriz))
                return false;

            if (texto.Length <= 12)
                return true;

            if (!texto.Contains(' ') && texto.Length <= 25)
                return true;

            string normalActual = NormalizarTextoBusqueda(texto);
            string normalMatriz = NormalizarTextoBusqueda(descripcionMatriz);

            if (normalActual.Length <= 30 && normalMatriz.StartsWith(normalActual, StringComparison.OrdinalIgnoreCase))
                return true;

            return false;
        }

        private static string NormalizarTextoBusqueda(string? texto)
        {
            if (string.IsNullOrWhiteSpace(texto))
                return string.Empty;

            return texto.Trim().ToUpperInvariant();
        }

        /// <summary>
        /// Lee la cantidad visible de la fila conservando un fallback positivo: un valor
        /// vacío, no parseable o &lt;= 0 nunca debe sobrescribir el fallback con cero.
        /// </summary>
        private decimal LeerCantidadPositivaDeFila(int rowIndex, decimal fallback)
        {
            var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
            if (cantidadCell?.Value is null)
                return fallback;

            return decimal.TryParse(cantidadCell.Value.ToString(), out decimal cantidad) && cantidad > 0m
                ? cantidad
                : fallback;
        }

        private void MostrarSelectorApuEmbebido(int rowIndex, decimal cantidadActual, int? matrizIdActual, string? filtroInicial)
        {
            EnsureWorkspaceExpanded();

            if (_panelMatricesEmbebido != null
                && !_panelMatricesEmbebido.IntentarSalirDeCabeceraActual())
            {
                return;
            }

            if (cantidadActual <= 0m)
                cantidadActual = CantidadMinimaSelector;

            if (_panelMatricesEmbebido != null)
                _panelMatricesEmbebido.Visible = false;

            if (_selectorApuEmbebido != null)
            {
                try
                {
                    panelMatricesHost.Controls.Remove(_selectorApuEmbebido);
                    _selectorApuEmbebido.Dispose();
                }
                catch { }
                _selectorApuEmbebido = null;
            }

            _selectorApuEmbebidoRowIndex = rowIndex;
            var targetRowIndex = rowIndex;
            var selector = new SelectorApuEmbebidoControl();
            selector.InitializeSelector(_context, _proyecto.Id, cantidadActual, matrizIdActual, filtroInicial, _workspaceService);
            selector.Accepted += (_, __) =>
            {
                try
                {
                    if (targetRowIndex >= 0 && selector.MatrizSeleccionada != null)
                        AsignarMatrizAPresupuesto(targetRowIndex, selector.MatrizSeleccionada, selector.Cantidad, preserveCurrentTexts: true);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al asignar APU: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
                finally
                {
                    MostrarPanelMatrizEmbebido(true);
                    if (targetRowIndex >= 0 && targetRowIndex < dgvPresupuesto.Rows.Count)
                    {
                        var cell = ObtenerCeldaPorNombreInterno(targetRowIndex, "Descripcion")
                                   ?? ObtenerCeldaPorNombreInterno(targetRowIndex, "PrecioUnitario");
                        if (cell != null)
                            dgvPresupuesto.CurrentCell = cell;
                        dgvPresupuesto.Focus();
                    }
                }
            };
            selector.Cancelled += (_, __) =>
            {
                MostrarPanelMatrizEmbebido(true);
                if (rowIndex >= 0 && rowIndex < dgvPresupuesto.Rows.Count)
                {
                    var cell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")
                               ?? ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
                    if (cell != null)
                        dgvPresupuesto.CurrentCell = cell;
                    dgvPresupuesto.Focus();
                }
            };
            selector.RequestNewMatrix += (_, __) =>
            {
                // El selector se dispone al mostrar el panel: la cantidad y el tipo
                // elegidos por el usuario deben capturarse AHORA (la cantidad leída al
                // abrir la fila puede ser cero/obsoleta). El flujo nace para asignar
                // una matriz al concepto, así que solo APU es válido.
                decimal cantidadNuevaMatriz = selector.CantidadParaNuevaMatriz;
                TipoMatriz tipoNuevaMatriz = selector.SelectedTipo;
                int? conceptoOrigenId = (dgvPresupuesto.Rows[targetRowIndex].Tag as ConceptoPresupuesto)?.Id;
                IniciarCreacionMatrizDesdeSelector(
                    targetRowIndex,
                    tipoNuevaMatriz,
                    cantidadNuevaMatriz,
                    onCancelled: () => MostrarSelectorApuEmbebido(targetRowIndex, cantidadNuevaMatriz, matrizIdActual, filtroInicial),
                    conceptoOrigenId);
            };
            selector.RequestEditMatrix += (_, __) =>
            {
                decimal cantidadVisible = selector.CantidadParaNuevaMatriz;
                var selectedMatrixId = selector.SelectedMatrixId;
                if (!selectedMatrixId.HasValue)
                {
                    MessageBox.Show("Selecciona una matriz para editar.", "Selector APU", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (_panelMatricesEmbebido != null
                    && !_panelMatricesEmbebido.IntentarSalirDeCabeceraActual())
                {
                    return;
                }

                MostrarPanelMatrizEmbebido(false);
                _panelMatricesEmbebido?.BeginEditMatrix(selectedMatrixId.Value,
                    onSaved: null,
                    onCancelled: () => MostrarSelectorApuEmbebido(targetRowIndex, cantidadVisible, selectedMatrixId, filtroInicial));
            };

            _selectorApuEmbebido = selector;
            selector.Dock = DockStyle.Fill;
            panelMatricesHost.Controls.Add(selector);
            selector.BringToFront();
            selector.Focus();

            ActualizarEstadoWorkspace(true);
        }

        private void btnWorkspaceMatriz_Click(object sender, EventArgs e)
        {
            MostrarPanelMatrizEmbebido(true);
        }

        /// <summary>
        /// Entrada de la creación desde el selector APU. Solo APU puede vincularse al
        /// concepto; un tipo distinto se informa y no inicia borrador. La asignación
        /// posterior ocurre por el evento NuevaMatrizConComponentesConfirmada, nunca
        /// por un callback heredado.
        /// </summary>
        internal void IniciarCreacionMatrizDesdeSelectorParaPrueba(
            int rowIndex, TipoMatriz tipo, decimal cantidad, int? conceptoOrigenId = null)
            => IniciarCreacionMatrizDesdeSelector(
                rowIndex, tipo, cantidad, onCancelled: null, conceptoOrigenId);

        private void IniciarCreacionMatrizDesdeSelector(
            int rowIndex,
            TipoMatriz tipo,
            decimal cantidad,
            Action? onCancelled,
            int? conceptoOrigenId = null)
        {
            if (tipo != TipoMatriz.APU)
            {
                MostrarAvisoAsignacionMatriz(
                    "Nueva matriz",
                    "Solo se puede crear y asignar una matriz de tipo APU a un concepto del presupuesto. " +
                    "Seleccione APU para continuar.",
                    MessageBoxIcon.Warning);
                return;
            }

            MostrarPanelMatrizEmbebido(false);
            _panelMatricesEmbebido?.BeginCreateMatrix(
                TipoMatriz.APU,
                onSaved: null,
                onCancelled: onCancelled,
                soloApu: true,
                cantidad: cantidad,
                conceptoOrigenId: conceptoOrigenId ?? (dgvPresupuesto.Rows[rowIndex].Tag as ConceptoPresupuesto)?.Id);
        }

        private void IniciarCreacionMatrizDesdePanel()
        {
            if (_panelMatricesEmbebido == null)
                return;

            int rowIndex = _panelMatricesEmbebido.FilaConceptoActual;
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                rowIndex = dgvPresupuesto.CurrentRow?.Index ?? -1;
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count)
                return;

            var targetRowIndex = rowIndex;
            var concepto = dgvPresupuesto.Rows[targetRowIndex].Tag as ConceptoPresupuesto;
            if (concepto is { Id: > 0 } && concepto.MatrizId.HasValue)
                return; // el concepto ya tiene matriz: nada que crear aquí

            decimal cantidadActual = LeerCantidadPositivaDeFila(targetRowIndex, CantidadPorDefectoPanel);

            _panelMatricesEmbebido.BeginCreateMatrix(TipoMatriz.APU, soloApu: true,
                cantidad: cantidadActual,
                conceptoOrigenId: concepto?.Id,
                onCancelled: () =>
                {
                    // El panel ya restauró la superficie; la fila del concepto sigue
                    // activa, así que esto repone el mensaje "Concepto sin APU asignada"
                    // con el acceso de creación visible.
                    if (targetRowIndex >= 0 && targetRowIndex < dgvPresupuesto.Rows.Count)
                        _panelMatricesEmbebido.NotificarFilaCambiada(targetRowIndex);
                });
        }

        private void btnWorkspaceCerrar_Click(object sender, EventArgs e)
        {
            if (_panelMatricesEmbebido != null
                && !_panelMatricesEmbebido.IntentarSalirDeCabeceraActual())
            {
                return;
            }

            SaveWorkspacePanelState();
            splitContainer.Panel2Collapsed = true;
            ActualizarTextoBotonToggleMatrices();
            btnWorkspaceCerrar.Enabled = false;
            dgvPresupuesto.Focus();
        }

        private void btnImportarExcel_Click(object sender, EventArgs e)
        {
            using var frm = new FormImportarPresupuestoExcel();
            if (frm.ShowDialog(this) != DialogResult.OK || frm.FilasImportadas == null || frm.FilasImportadas.Count == 0)
                return;

            ImportarFilasPresupuestoDesdeExcel(frm.FilasImportadas);
        }

        /// <summary>
        /// Clasificación de la última importación ejecutada como comando, o <c>null</c>
        /// si no llegó a enviarse (lote vacío o trabajo pendiente en el contexto
        /// compartido). Observabilidad interna para las pruebas del ensamblado; no es
        /// API pública ni se muestra en la UI.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoImportacion { get; private set; }

        private void ImportarFilasPresupuestoDesdeExcel(List<BudgetExcelImportRowDto> filas)
        {
            try
            {
                var resultado = EjecutarImportacionPresupuesto(filas);
                if (resultado is null)
                {
                    MessageBox.Show("No se generaron conceptos válidos para importar.", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                if (!resultado.IsCommitted || resultado.Confirmed is null)
                {
                    MessageBox.Show(
                        resultado.Message ?? "No se pudo importar el presupuesto; no se guardó ningún renglón.",
                        "Importar presupuesto",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                    return;
                }

                MessageBox.Show(
                    $"Se importaron {resultado.Confirmed.Rows.Count} renglones desde Excel\n\nNo se importaron P.U. ni Importe.",
                    "Importar presupuesto",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al importar el presupuesto desde Excel:\n{ex.Message}", "Importar presupuesto", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Construye el comando inmutable con las filas YA normalizadas por la vista
        /// (clasificación de agrupadores incluida), lo ejecuta sobre un contexto nuevo
        /// y solo recarga el grid si la operación se confirmó. No escribe nada en el
        /// contexto compartido ni ejecuta el guardado heredado.
        /// </summary>
        internal BudgetImportResult? EjecutarImportacionPresupuesto(IReadOnlyList<BudgetExcelImportRowDto>? filas)
        {
            UltimoEstadoImportacion = null;

            if (filas == null || filas.Count == 0)
                return null;

            var renglones = ConstruirRenglonesImportacion(filas);
            if (renglones.Count == 0)
                return null;

            // Conservador: si el contexto compartido arrastra trabajo pendiente de otras
            // rutas, no se envía el comando ni se guarda nada aquí.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                throw new InvalidOperationException(
                    "Hay cambios pendientes en el presupuesto; guarde o descarte antes de importar.");

            var command = new BudgetImportCommand(
                _proyecto.Id,
                BudgetProjectCalculationBase.FromProyecto(_proyecto),
                renglones);

            var resultado = BudgetPersistenceService.ExecuteImportCommand(
                _factory,
                _context.DatabasePath,
                command);

            UltimoEstadoImportacion = resultado.Status;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                System.Diagnostics.Debug.WriteLine(
                    $"Importación no confirmada ({resultado.Status}): {resultado.Diagnostic}");
                return resultado;
            }

            // La vista se actualiza SOLO después del commit. La recarga usa la ruta de
            // carga que ya no persiste totales de agrupadores.
            RecargarPresupuestoPreservandoEstado();
            return resultado;
        }

        private List<BudgetImportRow> ConstruirRenglonesImportacion(IReadOnlyList<BudgetExcelImportRowDto> filas)
        {
            var renglones = new List<BudgetImportRow>(filas.Count);
            bool yaSeAsignoCapituloInferido = false;

            foreach (var fila in filas)
            {
                if (fila is null)
                    continue;

                string tipo = ResolverTipoImportado(fila, ref yaSeAsignoCapituloInferido);
                if (string.Equals(tipo, "No importar", StringComparison.OrdinalIgnoreCase))
                    continue;

                bool esAgrupador = !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase);

                renglones.Add(new BudgetImportRow(
                    fila.SourceRowNumber > 0 ? fila.SourceRowNumber : null,
                    fila.Clave,
                    fila.Descripcion,
                    esAgrupador ? string.Empty : fila.Unidad,
                    esAgrupador ? 0m : (fila.Cantidad ?? 0m),
                    tipo));
            }

            return renglones;
        }

        private string ResolverTipoImportado(BudgetExcelImportRowDto fila, ref bool yaSeAsignoCapituloInferido)
        {
            string tipoTexto = (fila.TipoTexto ?? string.Empty).Trim();
            if (!string.IsNullOrWhiteSpace(tipoTexto))
            {
                string normalized = tipoTexto.ToLowerInvariant();
                if (normalized.Contains("no import") || normalized == "omit" || normalized == "omitir") return "No importar";
                if (normalized.Contains("subcap")) return "Subcapitulo";
                if (normalized.Contains("nivel 3") || normalized == "n3") return "Nivel 3";
                if (normalized.Contains("nivel 2") || normalized == "n2") return "Nivel 2";
                if (normalized.Contains("nivel 1") || normalized == "n1") return "Nivel 1";
                if (normalized.Contains("cap")) return "Capitulo";
                if (normalized.Contains("titulo") || normalized.Contains("título") || normalized.Contains("encabezado")) return "Capitulo";
                if (normalized.Contains("concept")) return "Concepto";
            }

            bool hasUnidad = !string.IsNullOrWhiteSpace(fila.Unidad);
            bool hasCantidad = fila.Cantidad.HasValue && fila.Cantidad.Value != 0m;
            if (hasUnidad || hasCantidad)
                return "Concepto";

            if (!yaSeAsignoCapituloInferido)
            {
                yaSeAsignoCapituloInferido = true;
                return "Capitulo";
            }

            return "Subcapitulo";
        }

        private void btnWorkspaceSelector_Click(object sender, EventArgs e)
        {
            if (dgvPresupuesto.CurrentRow == null)
                return;

            var rowIndex = dgvPresupuesto.CurrentRow.Index;
            var tipo = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo")?.Value?.ToString();
            if (!string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase))
            {
                MessageBox.Show("Seleccione una fila tipo Concepto para cambiar su APU.", "Selector APU", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            decimal cantidadActual = LeerCantidadPositivaDeFila(rowIndex, CantidadMinimaSelector);

            int? matrizIdActual = (dgvPresupuesto.Rows[rowIndex].Tag as ConceptoPresupuesto)?.MatrizId;
            var descripcionFiltro = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")?.Value?.ToString()?.Trim();
            var filtroInicial = matrizIdActual.HasValue ? null : (!string.IsNullOrWhiteSpace(descripcionFiltro) ? descripcionFiltro : null);

            MostrarSelectorApuEmbebido(rowIndex, cantidadActual, matrizIdActual, filtroInicial);
        }

        private void OpenApuSelectorForRow(int rowIndex)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count) return;

            if (_panelMatricesEmbebido != null
                && !_panelMatricesEmbebido.IntentarSalirDeCabeceraActual())
            {
                return;
            }

            var row = dgvPresupuesto.Rows[rowIndex];
            var tipoCell = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo");
            if (tipoCell?.Value?.ToString() != "Concepto") return;

            decimal cantidadActual = LeerCantidadPositivaDeFila(rowIndex, CantidadMinimaSelector);

            int? matrizIdActual = (row.Tag as ConceptoPresupuesto)?.MatrizId;
            var descripcionFiltro = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion")?.Value?.ToString()?.Trim();

            // Si el concepto ya tiene matriz asignada, F2 / doble click en P.U. deben abrir
            // directamente el panel embebido sobre esa matriz actual. El cambio de matriz se
            // hace manualmente desde el workspace usando el selector APU.
            if (matrizIdActual.HasValue)
            {
                MostrarPanelMatrizEmbebido(false);
                _panelMatricesEmbebido?.BeginEditMatrix(matrizIdActual.Value,
                    onSaved: null,
                    onCancelled: () => MostrarPanelMatrizEmbebido(true));

                if (rowIndex >= 0 && rowIndex < dgvPresupuesto.Rows.Count)
                {
                    var cell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario")
                               ?? ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
                    if (cell != null)
                        dgvPresupuesto.CurrentCell = cell;
                }
                return;
            }

            var filtroInicial = !string.IsNullOrWhiteSpace(descripcionFiltro)
                ? descripcionFiltro
                : null;

            MostrarSelectorApuEmbebido(rowIndex, cantidadActual, null, filtroInicial);
        }

        internal bool TryApplyBudgetConceptAssignmentByKey(int rowIndex, bool showErrors = true)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count) return false;

            var tipoCell = ObtenerCeldaPorNombreInterno(rowIndex, "Tipo");
            if (tipoCell?.Value?.ToString() != "Concepto") return false;

            string claveNueva = ObtenerCeldaPorNombreInterno(rowIndex, "Clave")?.Value?.ToString()?.Trim() ?? string.Empty;
            if (string.IsNullOrWhiteSpace(claveNueva)) return false;

            var row = dgvPresupuesto.Rows[rowIndex];
            var conceptoActual = row.Tag as ConceptoPresupuesto;

            var assignment = BudgetConceptAssignmentService.ResolveByKey(
                _context,
                _proyecto,
                rowIndex,
                claveNueva,
                conceptoActual,
                ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad")?.Value?.ToString(),
                BuildKeyRowSnapshots());

            if (assignment.IsInvalidMatrixTypeSelection)
            {
                if (showErrors)
                {
                    MessageBox.Show(
                        string.IsNullOrWhiteSpace(assignment.InvalidSelectionMessage)
                            ? "Este tipo de matriz no se puede asignar directamente a un concepto del presupuesto. Seleccione una matriz de tipo APU."
                            : assignment.InvalidSelectionMessage,
                        "Selección no válida",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                }

                var claveCellInvalida = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
                if (claveCellInvalida != null)
                    claveCellInvalida.Value = _valorAnteriorClaveEnEdicion ?? conceptoActual?.Clave ?? string.Empty;

                return false;
            }

            if (!assignment.HasAssignment || assignment.Draft == null)
                return false;

            var draft = assignment.Draft;

            if (assignment.RequiresConfirmation)
            {
                var result = MessageBox.Show(
                    assignment.ConfirmationMessage,
                    "Clave Duplicada - Confirmar Reemplazo",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (result == DialogResult.No)
                {
                    var claveCell = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
                    if (claveCell != null)
                        claveCell.Value = assignment.RestoreKeyValue;
                    return false;
                }
            }

            // La asignación ya no usa ApplyDraft/guardado heredado: tanto la
            // actualización de una fila persistida como el alta de una fila nueva
            // (Id 0/Tag null) viajan como comando inmutable a un contexto nuevo.
            // ResolveByKey solo resuelve la fuente y el borrador visible; el comando
            // transporta identidad/bases inmutable y recalcula en Application.
            var target = ConstruirTargetAsignacion(assignment, draft);
            if (target is null)
                return false;

            if (conceptoActual is { Id: > 0 })
            {
                return TryApplyMatrixAssignmentWithCommand(
                    rowIndex,
                    claveNueva,
                    conceptoActual,
                    target,
                    draft) == MatrixAssignmentDispatch.Sent;
            }

            var despachoNuevaFila = TryApplyMatrixAssignmentWithCommand(
                rowIndex,
                claveNueva,
                conceptoActual: null,
                target,
                draft);

            if (despachoNuevaFila == MatrixAssignmentDispatch.NotSentPendingChanges
                || (despachoNuevaFila != MatrixAssignmentDispatch.NotSentCommandFailure
                    && UltimoEstadoAsignacionMatriz != BudgetOperationStatus.Committed))
            {
                // El alta no se confirmó (conflicto, fallo o trabajo ajeno pendiente):
                // el borrador queda visible y la fila conserva su Id 0/Tag null. Un fallo
                // local al construir/enviar el comando no debe volcar un borrador ficticio.
                ApplyAssignmentDraftToGrid(rowIndex, draft);
            }

            return true;
        }

        private BudgetAssignmentTarget? ConstruirTargetAsignacion(
            BudgetConceptAssignmentResult assignment,
            BudgetConceptAssignmentDraft draft)
        {
            if (assignment.SourceRowIndex >= 0 && assignment.SourceConcept is { Id: > 0 } origen)
                return BudgetAssignmentTarget.ForSourceConcept(
                    BudgetPasteSourceConceptSnapshot.FromConcepto(origen));

            if (draft.Matriz is { Id: > 0 } matriz)
                return BudgetAssignmentTarget.ForMatrix(
                    matriz.Id, BudgetMatrixAssignmentBase.FromMatriz(matriz));

            if (draft.MatrizId is int matrizId && matrizId > 0)
            {
                var matrizActual = _context.Matrices
                    .AsNoTracking()
                    .FirstOrDefault(m => m.Id == matrizId);

                if (matrizActual != null)
                    return BudgetAssignmentTarget.ForMatrix(
                        matrizActual.Id, BudgetMatrixAssignmentBase.FromMatriz(matrizActual));
            }

            return null;
        }

        /// <summary>
        /// Construye el rowset COMPLETO de filas persistidas que la vista ve antes de
        /// crear una fila, y la posición de inserción según la fila actual del grid.
        /// </summary>
        private BudgetAssignmentRowset ConstruirRowsetAsignacion(int rowIndex, out int insertionIndex)
        {
            var filas = new List<BudgetAssignmentRow>(dgvPresupuesto.Rows.Count);
            insertionIndex = 0;

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                if (dgvPresupuesto.Rows[i].Tag is not ConceptoPresupuesto concepto || concepto.Id <= 0)
                    continue;

                filas.Add(BudgetAssignmentRow.FromConcepto(concepto));
                if (i < rowIndex)
                    insertionIndex++;
            }

            return new BudgetAssignmentRowset(filas);
        }

        /// <summary>
        /// Clasificación de la última asignación de matriz ejecutada como comando, o
        /// <c>null</c> si la asignación no llegó a enviarse (trabajo pendiente en el
        /// contexto compartido o reintento bloqueado por un commit incierto).
        /// Observabilidad interna para las pruebas del ensamblado; no es API pública.
        /// </summary>
        internal BudgetOperationStatus? UltimoEstadoAsignacionMatriz { get; private set; }

        /// <summary>Diagnóstico técnico del último intento no confirmado; nunca se muestra tal cual.</summary>
        internal string? UltimoDiagnosticoAsignacionMatriz { get; private set; }

        /// <summary>
        /// Punto de aislamiento de avisos para las pruebas: la ruta de comando no debe
        /// fallar en silencio ni dejar un borrador ficticio cuando la excepción impide
        /// siquiera construir/enviar el comando.
        /// </summary>
        internal static Action<string, string, MessageBoxIcon> MostrarAvisoAsignacionMatriz { get; set; }
            = (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);

        private enum MatrixAssignmentDispatch
        {
            NotSentPendingChanges,
            NotSentCommandFailure,
            NotSentReconciliationPending,
            Sent
        }

        /// <summary>
        /// Asigna una matriz/copia a un concepto persistido o crea una fila nueva con la
        /// ruta de comando: no muta el Tag compartido antes de conocer el resultado y no
        /// ejecuta el guardado heredado para este evento. Si falla o entra en conflicto,
        /// la clave editada permanece visible como borrador.
        /// </summary>
        private MatrixAssignmentDispatch TryApplyMatrixAssignmentWithCommand(
            int rowIndex,
            string claveNueva,
            ConceptoPresupuesto? conceptoActual,
            BudgetAssignmentTarget target,
            BudgetConceptAssignmentDraft draft,
            string? descriptionOverride = null,
            string? unitOverride = null)
        {
            UltimoEstadoAsignacionMatriz = null;
            UltimoDiagnosticoAsignacionMatriz = null;

            // Conservador: si el contexto compartido arrastra trabajo pendiente de
            // otras rutas, no se envía el comando ni se guarda nada aquí. El llamador
            // debe conservar el borrador y NO caer a la ruta heredada de guardado.
            _context.ChangeTracker.DetectChanges();
            if (_context.ChangeTracker.HasChanges())
                return MatrixAssignmentDispatch.NotSentPendingChanges;

            bool esNuevaFila = conceptoActual is null || conceptoActual.Id <= 0;

            // Un commit incierto no autoriza a reintentar el alta a ciegas: pudo haber
            // insertado la fila y solo se sabe reconciliando con la base.
            if (esNuevaFila && _asignacionNuevaFilaPendienteReconciliacion)
            {
                // La causa es la INCERTIDUMBRE de la alta anterior, no trabajo pendiente
                // en el contexto compartido. Se fija el estado y el diagnóstico reales
                // (no se borran) para que el llamador no anuncie "cambios pendientes" ni
                // reintente a ciegas.
                UltimoEstadoAsignacionMatriz = BudgetOperationStatus.OutcomeUnknown;
                UltimoDiagnosticoAsignacionMatriz =
                    "Alta de fila bloqueada: la asignación anterior quedó con resultado incierto " +
                    "(no se sabe si la fila se insertó). Reconcilie con la base —recargue el " +
                    "presupuesto— antes de reintentar.";
                System.Diagnostics.Debug.WriteLine(UltimoDiagnosticoAsignacionMatriz);
                return MatrixAssignmentDispatch.NotSentReconciliationPending;
            }

            BudgetAssignmentRowset? rowset = null;
            int? insertionIndex = null;
            if (esNuevaFila)
            {
                rowset = ConstruirRowsetAsignacion(rowIndex, out int indice);
                insertionIndex = indice;
            }

            BudgetMatrixAssignmentResult resultado;
            try
            {
                var command = esNuevaFila
                    ? BudgetMatrixAssignmentCommand.ForNewRow(
                        _proyecto.Id,
                        BudgetProjectCalculationBase.FromProyecto(_proyecto),
                        target,
                        claveNueva,
                        descriptionOverride,
                        unitOverride,
                        draft.Cantidad,
                        rowset!,
                        insertionIndex!.Value)
                    : BudgetMatrixAssignmentCommand.ForExistingRow(
                        _proyecto.Id,
                        conceptoActual!.Id,
                        BudgetProjectCalculationBase.FromProyecto(_proyecto),
                        BudgetConceptAssignmentBase.FromConcepto(conceptoActual),
                        target,
                        claveNueva,
                        descriptionOverride,
                        unitOverride,
                        draft.Cantidad);

                resultado = BudgetPersistenceService.ExecuteMatrixAssignmentCommand(
                    _factory,
                    _context.DatabasePath,
                    command);
            }
            catch (Exception ex)
            {
                // Falló la construcción/envío del comando: no hubo confirmación y no
                // puede presentarse un borrador como si la asignación se hubiera
                // guardado. Se clasifica el intento, se registra el diagnóstico y se
                // avisa de forma inequívoca; la fila/Tag/celdas quedan intactos.
                UltimoEstadoAsignacionMatriz = BudgetOperationStatus.Failed;
                UltimoDiagnosticoAsignacionMatriz = ex.ToString();
                System.Diagnostics.Debug.WriteLine($"Asignación de matriz no enviada: {ex}");
                MostrarAvisoAsignacionMatriz(
                    "Asignación de matriz",
                    "No se pudo asignar la matriz: no se guardó ningún cambio en la fila. " +
                    "Revise la cantidad y los datos de la matriz e intente de nuevo." +
                    $"\n\nDetalle: {ex.Message}",
                    MessageBoxIcon.Warning);
                return MatrixAssignmentDispatch.NotSentCommandFailure;
            }

            UltimoEstadoAsignacionMatriz = resultado.Status;

            if (resultado.Status == BudgetOperationStatus.OutcomeUnknown && esNuevaFila)
                _asignacionNuevaFilaPendienteReconciliacion = true;

            if (!resultado.IsCommitted || resultado.Confirmed is null)
            {
                // Conflicto, fallo o resultado incierto: el borrador de la clave queda
                // visible y el tracker compartido intacto. No se invoca el guardado
                // heredado para este evento.
                UltimoDiagnosticoAsignacionMatriz = resultado.Diagnostic;
                System.Diagnostics.Debug.WriteLine(
                    $"Asignación de matriz no confirmada ({resultado.Status}): {resultado.Diagnostic}");
                return MatrixAssignmentDispatch.Sent;
            }

            AplicarAsignacionMatrizConfirmada(rowIndex, conceptoActual, resultado.Confirmed);
            return MatrixAssignmentDispatch.Sent;
        }

        /// <summary>
        /// Vuelca lo CONFIRMADO por la asignación: adjunta el concepto creado cuando la
        /// fila era nueva, sincroniza Tag y celdas visibles (incluidos los Orden
        /// desplazados y los agrupadores recalculados) bajo el guard de reentrada y deja
        /// todo <c>Unchanged</c> para que el autoguardado heredado no lo reescriba.
        /// </summary>
        private void AplicarAsignacionMatrizConfirmada(
            int rowIndex,
            ConceptoPresupuesto? concepto,
            BudgetMatrixAssignmentSnapshot confirmado)
        {
            var filasPorConcepto = ConstruirIndiceFilasPorConcepto();
            bool asignandoPrevio = _asignandoMatriz;
            _asignandoMatriz = true;
            try
            {
                if (concepto is { Id: > 0 })
                {
                    SincronizarEntidadAsignacion(concepto, confirmado);
                }
                else
                {
                    var entidadCreada = AdjuntarConceptoConfirmado(confirmado.Concept);
                    dgvPresupuesto.Rows[rowIndex].Tag = entidadCreada;
                }

                var claveCell = ObtenerCeldaPorNombreInterno(rowIndex, "Clave");
                var descCell = ObtenerCeldaPorNombreInterno(rowIndex, "Descripcion");
                var unidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Unidad");
                var cantidadCell = ObtenerCeldaPorNombreInterno(rowIndex, "Cantidad");
                var puCell = ObtenerCeldaPorNombreInterno(rowIndex, "PrecioUnitario");
                var importeCell = ObtenerCeldaPorNombreInterno(rowIndex, "Importe");

                if (claveCell != null) claveCell.Value = confirmado.Clave;
                if (descCell != null) descCell.Value = confirmado.Descripcion;
                if (unidadCell != null) unidadCell.Value = confirmado.Unidad;
                if (cantidadCell != null) cantidadCell.Value = confirmado.Cantidad;
                if (puCell != null) puCell.Value = confirmado.PrecioUnitario.ToStringImporte();
                if (importeCell != null) importeCell.Value = confirmado.ImporteTotal.ToStringImporte();

                // Las filas posteriores pudieron desplazar su Orden dentro del mismo
                // comando: la instantánea trae el rowset completo confirmado.
                foreach (var fila in confirmado.Rows)
                {
                    if (fila.ConceptId == confirmado.ConceptId)
                        continue;

                    var entidadFila = ResolverEntidadCompartida(filasPorConcepto, fila.ConceptId);
                    if (entidadFila is not null)
                        SincronizarConceptoCompartido(entidadFila, fila);
                }

                FinalizeBudgetConceptAssignment(rowIndex);

                // La instantánea confirmada manda sobre el total que haya recalculado
                // el grid: se aplica al final para que Tag y celdas coincidan con la base.
                foreach (var agrupador in confirmado.Aggregators)
                {
                    var entidad = ResolverEntidadCompartida(filasPorConcepto, agrupador.ConceptId);
                    if (entidad is not null)
                        SincronizarEntidadCompartida(entidad, agrupador);

                    if (!filasPorConcepto.TryGetValue(agrupador.ConceptId, out int filaAgrupador))
                        continue;

                    var importeAgrupadorCell = ObtenerCeldaPorNombreInterno(filaAgrupador, "Importe");
                    if (importeAgrupadorCell != null)
                        importeAgrupadorCell.Value = agrupador.ImporteTotal.ToStringImporte();
                }
            }
            finally
            {
                _asignandoMatriz = asignandoPrevio;
            }

            _asignacionNuevaFilaPendienteReconciliacion = false;
        }

        private void SincronizarEntidadAsignacion(
            ConceptoPresupuesto entidad,
            BudgetMatrixAssignmentSnapshot confirmado)
        {
            entidad.Clave = confirmado.Clave;
            entidad.Descripcion = confirmado.Descripcion;
            entidad.Unidad = confirmado.Unidad;
            entidad.Cantidad = confirmado.Cantidad;
            entidad.CostoDirectoUnitario = confirmado.CostoDirectoUnitario;
            entidad.CostoDirectoTotal = confirmado.CostoDirectoTotal;
            entidad.PrecioUnitario = confirmado.PrecioUnitario;
            entidad.ImporteTotal = confirmado.ImporteTotal;
            entidad.MatrizId = confirmado.MatrizId;
            entidad.FechaModificacion = confirmado.FechaModificacion;

            var entry = _context.Entry(entidad);
            if (entry.State == EntityState.Detached) return;

            entry.OriginalValues.SetValues(entry.CurrentValues);
            entry.State = EntityState.Unchanged;
        }
    }
}
