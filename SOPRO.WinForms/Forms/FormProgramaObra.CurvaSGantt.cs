using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Models;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Curva S y Gantt: inicialización, vista, pintado y actualización diferida.
    /// </summary>
    public partial class FormProgramaObra
    {

        private void InicializarTabCurvaS()
        {
            _tabCurvaS = new TabPage("Curva S");
            _splitCurvaS = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Horizontal,
                SplitterDistance = 110
            };

            _lblCurvaResumen = new Label
            {
                Dock = DockStyle.Top,
                Height = 28,
                Padding = new Padding(10, 0, 0, 0),
                TextAlign = ContentAlignment.MiddleLeft,
                Text = "Curva S física y financiera programada"
            };
            _picCurvaS = new BufferedChartPanel
            {
                Dock = DockStyle.Fill
            };
            _picCurvaS.Paint += picCurvaS_Paint;
            _dgvCurvaS = new DataGridView
            {
                Dock = DockStyle.Fill,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                ReadOnly = true,
                AutoGenerateColumns = false,
                BackgroundColor = Color.White,
                RowHeadersVisible = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect
            };
            _splitCurvaS.Panel1.Controls.Add(_picCurvaS);
            _splitCurvaS.Panel1.Controls.Add(_lblCurvaResumen);
            _splitCurvaS.Panel2.Controls.Add(_dgvCurvaS);
            _tabCurvaS.Controls.Add(_splitCurvaS);
            tabPrograma.Controls.Add(_tabCurvaS);
            ControlRenderHelper.HabilitarDobleBuffer(_tabCurvaS);
            ControlRenderHelper.HabilitarDobleBuffer(_splitCurvaS);
            ControlRenderHelper.HabilitarDobleBuffer(_splitCurvaS.Panel1);
            ControlRenderHelper.HabilitarDobleBuffer(_splitCurvaS.Panel2);
        }

        private void InicializarPanelGantt()
        {
            _ganttControl = ganttTimeline;
            _ganttControl.BindGrid(dgvActividades);
            _ganttControl.TimelineCellWidthChanged += ganttTimeline_TimelineCellWidthChanged;
            _ganttControl.VisualSettingsChanged += ganttTimeline_VisualSettingsChanged;
            _ganttControl.RenderModel = new GanttRenderModel
            {
                TipoPeriodo = ObtenerTipoPeriodoSeleccionado(),
                ViewMode = GanttViewMode.ProgramaObra
            };
        }

        private void ganttTimeline_VisualSettingsChanged(object? sender, EventArgs e)
        {
            if (_aplicandoLayoutPersistido || !_layoutPersistenceReady)
                return;

            GuardarLayoutPersistido();
        }

        private GanttVisualSettings ObtenerConfiguracionVisualGanttActual()
        {
            return _ganttControl?.VisualSettings ?? GanttVisualSettings.CreateDefault();
        }

        private void AplicarConfiguracionVisualGanttDesdeEstado(FormProgramaObraLayoutState state)
        {
            if (_ganttControl == null)
                return;

            var settings = GanttVisualSettings.CreateDefault();

            if (!string.IsNullOrWhiteSpace(state.GanttFontFamily))
                settings.FontFamilyName = state.GanttFontFamily;
            if (state.GanttFontStyle.HasValue && Enum.IsDefined(typeof(FontStyle), state.GanttFontStyle.Value))
                settings.FontStyle = (FontStyle)state.GanttFontStyle.Value;
            if (state.GanttTextColorArgb.HasValue)
                settings.TextColor = Color.FromArgb(state.GanttTextColorArgb.Value);
            if (state.GanttOutlineColorArgb.HasValue)
                settings.OutlineColor = Color.FromArgb(state.GanttOutlineColorArgb.Value);
            if (state.GanttNormalBarColorArgb.HasValue)
                settings.NormalBarColor = Color.FromArgb(state.GanttNormalBarColorArgb.Value);
            if (state.GanttCriticalBarColorArgb.HasValue)
                settings.CriticalBarColor = Color.FromArgb(state.GanttCriticalBarColorArgb.Value);
            if (state.GanttSummaryBarColorArgb.HasValue)
                settings.SummaryBarColor = Color.FromArgb(state.GanttSummaryBarColorArgb.Value);

            _ganttControl.VisualSettings = settings;
        }

        private bool IntentarBeginInvokeSeguro(Action action)
        {
            if (action == null || IsDisposed)
                return false;

            Control? dispatcher = null;
            if (IsHandleCreated)
                dispatcher = this;
            else if (dgvActividades != null && dgvActividades.IsHandleCreated)
                dispatcher = dgvActividades;
            else if (_ganttControl != null && _ganttControl.IsHandleCreated)
                dispatcher = _ganttControl;
            else if (_picCurvaS != null && _picCurvaS.IsHandleCreated)
                dispatcher = _picCurvaS;

            if (dispatcher != null)
            {
                dispatcher.BeginInvoke(action);
                return true;
            }

            EventHandler? onHandleCreated = null;
            onHandleCreated = (_, __) =>
            {
                HandleCreated -= onHandleCreated;
                if (IsDisposed)
                    return;

                BeginInvoke(action);
            };

            HandleCreated += onHandleCreated;
            return true;
        }

        private void SolicitarRedibujoCurvaS()
        {
            if (_picCurvaS == null)
                return;

            if (!IsHandleCreated || IsDisposed)
            {
                _picCurvaS.Invalidate();
                return;
            }

            if (_curvaSRedrawPending)
                return;

            _curvaSRedrawPending = true;
            if (!IntentarBeginInvokeSeguro(() =>
            {
                _curvaSRedrawPending = false;
                if (IsDisposed || _picCurvaS == null)
                    return;

                _picCurvaS.Invalidate();
            }))
            {
                _curvaSRedrawPending = false;
            }
        }

        private void ActualizarGantt()
        {
            if (_ganttControl == null)
                return;

            var vista = ObtenerVistaCurvaSeleccionada();
            var tipoPeriodo = ObtenerTipoPeriodoSeleccionado();
            var model = vista switch
            {
                CurvaSViewMode.Financiera => _ganttService.BuildFinancialGantt(_context, _programaActual, tipoPeriodo),
                CurvaSViewMode.Ambas => _ganttService.BuildFinancialGantt(_context, _programaActual, tipoPeriodo, mixedMode: true),
                _ => _ganttService.BuildProgramGantt(_programaActual, tipoPeriodo, GanttViewMode.ProgramaObra)
            };

            _ganttControl.FooterDisplayMode = vista == CurvaSViewMode.Financiera ? ObtenerPieGanttSeleccionado() : GanttFooterDisplayMode.Ninguno;
            _ganttControl.RenderModel = model;
            _ganttControl.SelectedActivityId = GetActividadSeleccionada()?.Id;
        }

        private bool DebeDiferirCargaDetalle()
        {
            return splitPrincipal.Panel2Collapsed;
        }

        private bool DebeRecargarDetalle(int? actividadId, bool forzar)
        {
            if (forzar)
                return true;

            if (_detalleSeleccionDeferred)
                return true;

            return _ultimaActividadDetalleId != actividadId;
        }

        private void ProcesarDetallePendienteSiAplica(bool forzar = false)
        {
            if (!forzar && !_detalleSeleccionDeferred)
                return;

            if (DebeDiferirCargaDetalle())
                return;

            SolicitarActualizacionDetalleSeleccion(true);
        }

        private void SolicitarActualizacionDetalleSeleccion(bool forzar = false)
        {
            if (!forzar && (_cargando || !IsHandleCreated || IsDisposed))
                return;

            var actividadId = GetActividadSeleccionada()?.Id;
            if (!DebeRecargarDetalle(actividadId, forzar))
                return;

            if (DebeDiferirCargaDetalle())
            {
                _detalleSeleccionDeferred = true;
                return;
            }

            if (_detalleSeleccionRefreshPending)
                return;

            _detalleSeleccionRefreshPending = true;
            if (!IntentarBeginInvokeSeguro(() =>
            {
                _detalleSeleccionRefreshPending = false;
                if (IsDisposed)
                    return;

                if (_cargando && !forzar)
                    return;

                if (DebeDiferirCargaDetalle())
                {
                    _detalleSeleccionDeferred = true;
                    return;
                }

                CargarDetalleActividadSeleccionada();
            }))
            {
                _detalleSeleccionRefreshPending = false;
            }
        }

        private void SolicitarActualizacionVisualPrograma(bool forzar = false)
        {
            if (!forzar && (_cargando || !IsHandleCreated || IsDisposed))
                return;

            if (_visualProgramRefreshPending)
                return;

            _visualProgramRefreshPending = true;
            if (!IntentarBeginInvokeSeguro(() =>
            {
                _visualProgramRefreshPending = false;
                if (IsDisposed)
                    return;

                if (_cargando && !forzar)
                    return;

                if (_ganttControl != null)
                {
                    var vistaActual = ObtenerVistaCurvaSeleccionada();
                    var expectedViewMode = vistaActual switch
                    {
                        CurvaSViewMode.Financiera => GanttViewMode.Erogaciones,
                        CurvaSViewMode.Ambas => GanttViewMode.Mixto,
                        _ => GanttViewMode.ProgramaObra
                    };
                    var expectedFooterMode = vistaActual == CurvaSViewMode.Financiera ? ObtenerPieGanttSeleccionado() : GanttFooterDisplayMode.Ninguno;
                    var requiereReconstruirGantt = _ganttControl.RenderModel == null
                        || _ganttControl.RenderModel.ViewMode != expectedViewMode
                        || _ganttControl.FooterDisplayMode != expectedFooterMode;

                    if (requiereReconstruirGantt)
                    {
                        ActualizarGantt();
                    }
                    else
                    {
                        var actividad = GetActividadSeleccionada();
                        _ganttControl.SelectedActivityId = actividad?.Id;
                        _ganttControl.RequestRefresh();
                    }
                }

                SolicitarRedibujoCurvaS();
            }))
            {
                _visualProgramRefreshPending = false;
            }
        }

        private void ConfigurarGridCurvaS()
        {
            if (_dgvCurvaS == null)
                return;

            _dgvCurvaS.AplicarEstiloSOPRO();
            DgvCeldaHelper.Aplicar(_dgvCurvaS);
            _dgvCurvaS.Columns.Clear();
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaNumero", DataPropertyName = nameof(CurvaSRowDto.NumeroPeriodo), HeaderText = "#", Width = 45 });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaEtiqueta", DataPropertyName = nameof(CurvaSRowDto.Etiqueta), HeaderText = "Periodo", AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaInicio", DataPropertyName = nameof(CurvaSRowDto.FechaInicio), HeaderText = "Inicio", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaFin", DataPropertyName = nameof(CurvaSRowDto.FechaFin), HeaderText = "Fin", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "dd/MM/yyyy" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaCantPeriodo", DataPropertyName = nameof(CurvaSRowDto.CantidadPeriodo), HeaderText = "Cant. período", Width = 95, DefaultCellStyle = new DataGridViewCellStyle { Format = "N4" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaCantAcum", DataPropertyName = nameof(CurvaSRowDto.CantidadAcumulada), HeaderText = "Cant. acumulada", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "N4" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFisPeriodo", DataPropertyName = nameof(CurvaSRowDto.PorcentajeFisicoPeriodo), HeaderText = "% físico período", Width = 105, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFisAcum", DataPropertyName = nameof(CurvaSRowDto.PorcentajeFisicoAcumulado), HeaderText = "% físico acumulado", Width = 115, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaImpPeriodo", DataPropertyName = nameof(CurvaSRowDto.ImportePeriodo), HeaderText = "Importe período", Width = 110, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaImpAcum", DataPropertyName = nameof(CurvaSRowDto.ImporteAcumulado), HeaderText = "Importe acumulado", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFinPeriodo", DataPropertyName = nameof(CurvaSRowDto.PorcentajePeriodo), HeaderText = "% financiero período", Width = 120, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.Columns.Add(new DataGridViewTextBoxColumn { Name = "colCurvaPctFinAcum", DataPropertyName = nameof(CurvaSRowDto.PorcentajeAcumulado), HeaderText = "% financiero acumulado", Width = 130, DefaultCellStyle = new DataGridViewCellStyle { Format = "N2" } });
            _dgvCurvaS.DataSource = _curvaS;
            AplicarVistaCurvaS();
        }

        private void AplicarVistaCurvaS()
        {
            if (_dgvCurvaS == null)
                return;
            var vista = ObtenerVistaCurvaSeleccionada();
            bool fis = vista != CurvaSViewMode.Financiera;
            bool fin = vista != CurvaSViewMode.Fisica;
            void SetVisible(string name, bool value)
            {
                if (_dgvCurvaS.Columns.Contains(name))
                    _dgvCurvaS.Columns[name].Visible = value;
            }
            SetVisible("colCurvaCantPeriodo", fis);
            SetVisible("colCurvaCantAcum", fis);
            SetVisible("colCurvaPctFisPeriodo", fis);
            SetVisible("colCurvaPctFisAcum", fis);
            SetVisible("colCurvaImpPeriodo", fin);
            SetVisible("colCurvaImpAcum", fin);
            SetVisible("colCurvaPctFinPeriodo", fin);
            SetVisible("colCurvaPctFinAcum", fin);
            SolicitarRedibujoCurvaS();
        }

        private void LimpiarCurvaS()
        {
            _curvaS = new BindingList<CurvaSRowDto>();
            if (_dgvCurvaS != null)
                _dgvCurvaS.DataSource = _curvaS;
            if (_lblCurvaResumen != null)
                _lblCurvaResumen.Text = "Curva S física y financiera programada";
            SolicitarRedibujoCurvaS();
        }

        private void CargarCurvaS()
        {
            if (_programaActual == null)
            {
                LimpiarCurvaS();
                return;
            }

            var rows = _curvaSService.BuildFinancialCurve(_context, _programaActual.ProgramaObraId);
            _curvaS = new BindingList<CurvaSRowDto>(rows);
            if (_dgvCurvaS != null)
                _dgvCurvaS.DataSource = _curvaS;

            if (_lblCurvaResumen != null)
            {
                var total = rows.LastOrDefault()?.ImporteAcumulado ?? 0m;
                var periodos = rows.Count;
                var totalFisico = rows.LastOrDefault()?.CantidadAcumulada ?? 0m;
                _lblCurvaResumen.Text = $"Curva S física y financiera programada. Periodos: {periodos}. Total físico: {totalFisico:N4}. Total financiero: {total:N2}";
            }

            AplicarVistaCurvaS();
            SolicitarRedibujoCurvaS();
        }

        private void picCurvaS_Paint(object? sender, PaintEventArgs e)
        {
            if (_picCurvaS == null)
                return;

            var g = e.Graphics;
            g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
            g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            g.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
            g.Clear(Color.White);
            var rows = _curvaS?.ToList() ?? new List<CurvaSRowDto>();
            var rect = new Rectangle(50, 15, Math.Max(100, _picCurvaS.Width - 75), Math.Max(80, _picCurvaS.Height - 45));
            using var axisPen = new Pen(Color.Silver, 1);
            using var linePenFin = new Pen(Color.FromArgb(41, 128, 185), 2);
            using var fillBrushFin = new SolidBrush(Color.FromArgb(41, 128, 185));
            using var linePenFis = new Pen(Color.FromArgb(39, 174, 96), 2);
            using var fillBrushFis = new SolidBrush(Color.FromArgb(39, 174, 96));
            using var textBrush = new SolidBrush(Color.DimGray);
            using var font = new Font("Segoe UI", 8f);

            g.DrawRectangle(axisPen, rect);
            for (int i = 0; i <= 4; i++)
            {
                var y = rect.Bottom - (rect.Height * i / 4f);
                g.DrawLine(axisPen, rect.Left, y, rect.Right, y);
                var pct = i * 25;
                g.DrawString($"{pct}%", font, textBrush, 8, y - 8);
            }

            if (rows.Count == 0)
            {
                g.DrawString("Sin datos para Curva S.", font, textBrush, rect.Left + 10, rect.Top + 10);
                return;
            }

            var pointsFin = new List<PointF>();
            var pointsFis = new List<PointF>();
            for (int i = 0; i < rows.Count; i++)
            {
                var x = rows.Count == 1 ? rect.Left : rect.Left + (rect.Width * i / (float)(rows.Count - 1));
                var yFin = rect.Bottom - (rect.Height * ((float)rows[i].PorcentajeAcumulado / 100f));
                var yFis = rect.Bottom - (rect.Height * ((float)rows[i].PorcentajeFisicoAcumulado / 100f));
                pointsFin.Add(new PointF(x, yFin));
                pointsFis.Add(new PointF(x, yFis));
                g.DrawString(rows[i].NumeroPeriodo.ToString(), font, textBrush, x - 6, rect.Bottom + 4);
            }

            var vista = ObtenerVistaCurvaSeleccionada();
            bool showFis = vista != CurvaSViewMode.Financiera;
            bool showFin = vista != CurvaSViewMode.Fisica;

            if (showFin && pointsFin.Count > 1)
                g.DrawLines(linePenFin, pointsFin.ToArray());
            if (showFis && pointsFis.Count > 1)
                g.DrawLines(linePenFis, pointsFis.ToArray());

            if (showFin)
                foreach (var p in pointsFin)
                    g.FillEllipse(fillBrushFin, p.X - 3, p.Y - 3, 6, 6);
            if (showFis)
                foreach (var p in pointsFis)
                    g.FillEllipse(fillBrushFis, p.X - 3, p.Y - 3, 6, 6);

            // Leyenda
            var legendY = rect.Top + 8;
            if (showFis)
            {
                g.DrawLine(linePenFis, rect.Right - 190, legendY + 7, rect.Right - 165, legendY + 7);
                g.FillEllipse(fillBrushFis, rect.Right - 180, legendY + 4, 6, 6);
                g.DrawString("Física acumulada", font, textBrush, rect.Right - 160, legendY);
            }
            if (showFin)
            {
                var off = showFis ? 17 : 0;
                g.DrawLine(linePenFin, rect.Right - 190, legendY + 7 + off, rect.Right - 165, legendY + 7 + off);
                g.FillEllipse(fillBrushFin, rect.Right - 180, legendY + 4 + off, 6, 6);
                g.DrawString("Financiera acumulada", font, textBrush, rect.Right - 160, legendY + off);
            }
        }

        private CurvaSViewMode ObtenerVistaCurvaSeleccionada()
        {
            if (cmbVistaCurva.ComboBox.SelectedValue is CurvaSViewMode vistaPorValor)
                return vistaPorValor;

            if (cmbVistaCurva.ComboBox.SelectedItem is VistaCurvaOption opcion)
                return opcion.Value;

            return CurvaSViewMode.Ambas;
        }

        private GanttFooterDisplayMode ObtenerPieGanttSeleccionado()
        {
            if (cmbPieGantt.ComboBox.SelectedValue is GanttFooterDisplayMode modoPorValor)
                return modoPorValor;

            if (cmbPieGantt.ComboBox.SelectedItem is PieGanttOption opcion)
                return opcion.Value;

            return GanttFooterDisplayMode.Ninguno;
        }

        private GanttSegmentLabelPosition ObtenerPosicionEtiquetaSegmentoSeleccionada()
        {
            if (cmbEtiquetaSegmentoGantt.ComboBox.SelectedValue is GanttSegmentLabelPosition modoPorValor)
                return modoPorValor;

            if (cmbEtiquetaSegmentoGantt.ComboBox.SelectedItem is SegmentLabelGanttOption opcion)
                return opcion.Value;

            return GanttSegmentLabelPosition.Arriba;
        }

        private void ActualizarDisponibilidadPieGantt()
        {
            var vista = ObtenerVistaCurvaSeleccionada();
            var financiera = vista == CurvaSViewMode.Financiera;
            lblPieGantt.Visible = financiera;
            cmbPieGantt.Visible = financiera;
            cmbPieGantt.Enabled = financiera;
            lblEtiquetaSegmentoGantt.Visible = false;
            cmbEtiquetaSegmentoGantt.Visible = false;
            cmbEtiquetaSegmentoGantt.Enabled = false;
            if (_ganttControl != null)
            {
                _ganttControl.FooterDisplayMode = financiera ? ObtenerPieGanttSeleccionado() : GanttFooterDisplayMode.Ninguno;
                _ganttControl.SegmentLabelPosition = ObtenerPosicionEtiquetaSegmentoSeleccionada();
            }
        }
    }
}
