using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Actualización del gantt y sincronización con la selección del grid.
    /// </summary>
    public partial class FormProgramaInsumos
    {

        private void ActualizarGanttInsumos(ProgramaInsumoTipo tipo, VistaProgramaInsumos vista)
        {
            if (_ganttControl == null)
                return;

            var mode = vista switch
            {
                VistaProgramaInsumos.Cantidades => GanttViewMode.ProgramaObra,
                VistaProgramaInsumos.Importes => GanttViewMode.Erogaciones,
                VistaProgramaInsumos.Mixto => GanttViewMode.Mixto,
                _ => GanttViewMode.ProgramaObra
            };

            var model = _ganttService.Build(_context, _proyecto, tipo, mode, out var programa);
            _actual = programa;
            _ganttControl.FooterDisplayMode = mode == GanttViewMode.Erogaciones ? GanttFooterDisplayMode.ImportePeriodo : GanttFooterDisplayMode.Ninguno;
            _ganttControl.RenderModel = model;
            _ganttControl.SelectedActivityId = ObtenerInsumoSeleccionadoId();
            _ganttControl.RequestRefresh();
        }

        private int? ObtenerInsumoSeleccionadoId()
        {
            if (dgvProgramaInsumos.CurrentRow?.Tag is int id)
                return id;
            if (dgvProgramaInsumos.SelectedRows.Count > 0 && dgvProgramaInsumos.SelectedRows[0].Tag is int selectedId)
                return selectedId;
            return null;
        }
    }
}
