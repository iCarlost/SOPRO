using System.ComponentModel;
using System.Threading.Tasks;
using System.Drawing;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Models;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Layout persistido, estado del panel de detalle y carga inicial.
    /// </summary>
    public partial class FormProgramaObra
    {

        private void FormProgramaObra_Load(object sender, EventArgs e)
        {
            _layoutPersistenceReady = false;
            var previousOpacity = Opacity;
            var hideWithOpacity = previousOpacity > 0d;

            if (hideWithOpacity)
                Opacity = 0d;

            SuspendLayout();
            splitPrincipal.SuspendLayout();
            splitActividadesGantt.SuspendLayout();
            try
            {
                AplicarLayoutPersistido();
                CargarPrograma();
            }
            finally
            {
                splitActividadesGantt.ResumeLayout(true);
                splitPrincipal.ResumeLayout(true);
                ResumeLayout(true);
            }

            BeginInvoke(new Action(() =>
            {
                try
                {
                    _aplicandoLayoutPersistido = true;
                    AplicarLayoutPersistido();
                }
                finally
                {
                    _aplicandoLayoutPersistido = false;
                    _layoutPersistenceReady = true;
                    if (hideWithOpacity && !IsDisposed)
                        Opacity = previousOpacity;
                }
            }));
        }



        private void AplicarLayoutPersistido()
        {
            if (splitActividadesGantt.Width <= 0)
                return;

            try
            {
                _aplicandoLayoutPersistido = true;
                var state = FormProgramaObraLayoutStateStore.Load(_proyecto.Id);
                if (state != null)
                {
                    var oldCargando = _cargando;
                    _cargando = true;
                    try
                    {
                        if (state.VistaCurvaMode.HasValue && Enum.IsDefined(typeof(CurvaSViewMode), state.VistaCurvaMode.Value))
                            cmbVistaCurva.ComboBox.SelectedValue = (CurvaSViewMode)state.VistaCurvaMode.Value;

                        if (state.PieGanttMode.HasValue && Enum.IsDefined(typeof(GanttFooterDisplayMode), state.PieGanttMode.Value))
                            cmbPieGantt.ComboBox.SelectedValue = (GanttFooterDisplayMode)state.PieGanttMode.Value;

                        if (state.SegmentLabelPosition.HasValue && Enum.IsDefined(typeof(GanttSegmentLabelPosition), state.SegmentLabelPosition.Value))
                            cmbEtiquetaSegmentoGantt.ComboBox.SelectedValue = (GanttSegmentLabelPosition)state.SegmentLabelPosition.Value;

                        if (state.GanttCellWidth.HasValue && _ganttControl != null)
                            _ganttControl.TimelineCellWidth = state.GanttCellWidth.Value;

                        AplicarConfiguracionVisualGanttDesdeEstado(state);
                    }
                    catch
                    {
                    }
                    finally
                    {
                        _cargando = oldCargando;
                    }

                    ActualizarDisponibilidadPieGantt();
                    _ultimaAlturaPanelInferior = state.BottomPanelExpandedHeight > 0 ? state.BottomPanelExpandedHeight : Math.Max(_ultimaAlturaPanelInferior, state.BottomPanelHeight);

                    if (state.GanttPanelWidth > 0)
                    {
                        var splitterWidth = Math.Max(splitActividadesGantt.SplitterWidth, 6);
                        var minLeft = Math.Max(splitActividadesGantt.Panel1MinSize, 320);
                        var minRight = Math.Max(splitActividadesGantt.Panel2MinSize, 260);
                        var maxRight = Math.Max(minRight, splitActividadesGantt.Width - splitterWidth - minLeft);
                        var clampedRight = Math.Max(minRight, Math.Min(state.GanttPanelWidth, maxRight));
                        var desiredDistance = splitActividadesGantt.Width - splitterWidth - clampedRight;
                        desiredDistance = Math.Max(minLeft, Math.Min(desiredDistance, splitActividadesGantt.Width - splitterWidth - minRight));

                        if (desiredDistance > 0 && desiredDistance < splitActividadesGantt.Width)
                            splitActividadesGantt.SplitterDistance = desiredDistance;
                    }

                    if (state.BottomPanelCollapsed)
                    {
                        splitPrincipal.Panel2Collapsed = true;
                    }
                    else if (state.BottomPanelHeight > 0 && splitPrincipal.Height > 0)
                    {
                        splitPrincipal.SuspendLayout();
                        try
                        {
                            RestaurarAlturaPanelInferior(state.BottomPanelHeight);
                            if (splitPrincipal.Panel2Collapsed)
                                splitPrincipal.Panel2Collapsed = false;
                        }
                        finally
                        {
                            splitPrincipal.ResumeLayout(true);
                        }
                    }

                    ActualizarTextoBotonDetalle();
                }
            }
            catch
            {
            }
            finally
            {
                _aplicandoLayoutPersistido = false;
            }
        }

        private void GuardarLayoutPersistido()
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady || splitActividadesGantt.Width <= 0)
                return;

            try
            {
                var ganttWidth = splitActividadesGantt.Panel2.Width;
                if (ganttWidth <= 0)
                    return;

                if (!splitPrincipal.Panel2Collapsed && splitPrincipal.Panel2.Height > 0)
                    _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

                var bottomHeight = splitPrincipal.Panel2Collapsed ? 0 : splitPrincipal.Panel2.Height;

                var visualSettings = ObtenerConfiguracionVisualGanttActual();

                FormProgramaObraLayoutStateStore.Save(_proyecto.Id, new FormProgramaObraLayoutState
                {
                    GanttPanelWidth = ganttWidth,
                    BottomPanelHeight = bottomHeight,
                    BottomPanelExpandedHeight = _ultimaAlturaPanelInferior,
                    BottomPanelCollapsed = splitPrincipal.Panel2Collapsed,
                    VistaCurvaMode = (int)ObtenerVistaCurvaSeleccionada(),
                    PieGanttMode = (int)ObtenerPieGanttSeleccionado(),
                    SegmentLabelPosition = (int)ObtenerPosicionEtiquetaSegmentoSeleccionada(),
                    GanttCellWidth = _ganttControl?.TimelineCellWidth,
                    GanttFontFamily = visualSettings.FontFamilyName,
                    GanttFontStyle = (int)visualSettings.FontStyle,
                    GanttTextColorArgb = visualSettings.TextColor.ToArgb(),
                    GanttOutlineColorArgb = visualSettings.OutlineColor.ToArgb(),
                    GanttNormalBarColorArgb = visualSettings.NormalBarColor.ToArgb(),
                    GanttCriticalBarColorArgb = visualSettings.CriticalBarColor.ToArgb(),
                    GanttSummaryBarColorArgb = visualSettings.SummaryBarColor.ToArgb()
                });
            }
            catch
            {
            }
        }

        private void ganttTimeline_TimelineCellWidthChanged(object? sender, EventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady)
                return;

            GuardarLayoutPersistido();
        }

        private void splitActividadesGantt_SplitterMoved(object? sender, SplitterEventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady)
                return;

            GuardarLayoutPersistido();
        }

        private void splitPrincipal_SplitterMoved(object? sender, SplitterEventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady || _suspendBottomPanelTracking)
                return;

            if (!splitPrincipal.Panel2Collapsed && splitPrincipal.Panel2.Height > 0)
                _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

            GuardarLayoutPersistido();
        }

        private void FormProgramaObra_FormClosing(object? sender, FormClosingEventArgs e)
        {
            GuardarLayoutPersistido();
        }

        private void RestaurarAlturaPanelInferior(int desiredBottomHeight)
        {
            if (splitPrincipal.Height <= 0)
                return;

            var splitterWidthBottom = Math.Max(splitPrincipal.SplitterWidth, 6);
            var minTop = 180;
            var minBottom = 150;
            var maxBottom = Math.Max(minBottom, splitPrincipal.Height - splitterWidthBottom - minTop);
            var clampedBottom = Math.Max(minBottom, Math.Min(desiredBottomHeight, maxBottom));
            var desiredTop = splitPrincipal.Height - splitterWidthBottom - clampedBottom;
            desiredTop = Math.Max(minTop, Math.Min(desiredTop, splitPrincipal.Height - splitterWidthBottom - minBottom));

            if (desiredTop > 0 && desiredTop < splitPrincipal.Height)
                splitPrincipal.SplitterDistance = desiredTop;
        }

        private void ActualizarTextoBotonDetalle()
        {
            btnToggleDetalle.Text = splitPrincipal.Panel2Collapsed ? "▸ Detalle" : "▾ Detalle";
            btnToggleDetalle.ToolTipText = splitPrincipal.Panel2Collapsed
                ? "Mostrar panel inferior con periodos, distribución, dependencias y Curva S."
                : "Ocultar panel inferior para dar más espacio al programa.";
        }

        private void AsegurarPanelDetalleVisible(bool enfocarTabs = false)
        {
            var estabaColapsado = splitPrincipal.Panel2Collapsed;
            if (estabaColapsado)
            {
                var desiredExpandedHeight = _ultimaAlturaPanelInferior > 0 ? _ultimaAlturaPanelInferior : 243;
                _suspendBottomPanelTracking = true;
                splitPrincipal.SuspendLayout();
                try
                {
                    RestaurarAlturaPanelInferior(desiredExpandedHeight);
                    splitPrincipal.Panel2Collapsed = false;
                }
                finally
                {
                    splitPrincipal.ResumeLayout(true);
                    _suspendBottomPanelTracking = false;
                }

                if (!splitPrincipal.Panel2Collapsed && splitPrincipal.Panel2.Height > 0)
                    _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

                ActualizarTextoBotonDetalle();
                GuardarLayoutPersistido();
            }

            if (enfocarTabs)
                tabPrograma.Focus();

            if (estabaColapsado)
                ProcesarDetallePendienteSiAplica(true);
        }

        private void AlternarPanelDetalle()
        {
            if (splitPrincipal.Panel2Collapsed)
            {
                AsegurarPanelDetalleVisible(true);
                return;
            }

            if (splitPrincipal.Panel2.Height > 0)
                _ultimaAlturaPanelInferior = splitPrincipal.Panel2.Height;

            _suspendBottomPanelTracking = true;
            try
            {
                splitPrincipal.Panel2Collapsed = true;
            }
            finally
            {
                _suspendBottomPanelTracking = false;
            }

            ActualizarTextoBotonDetalle();
            GuardarLayoutPersistido();
        }

        private bool EsAtajoDependenciasValido(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0)
                return false;

            var columnName = dgvActividades.Columns[columnIndex].Name;
            if (!string.Equals(columnName, "colPredecesora", StringComparison.Ordinal))
                return false;

            return dgvActividades.Rows[rowIndex].DataBoundItem is ActivityGridRowDto dto && !dto.EsResumen;
        }

        private void AbrirDependenciasDesdePrograma()
        {
            AsegurarPanelDetalleVisible();
            tabPrograma.SelectedTab = tabDependencias;
            CargarDetalleActividadSeleccionada();
            dgvDependencias.Focus();
        }
    }
}
