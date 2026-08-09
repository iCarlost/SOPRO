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
    /// Undo/redo de la grilla de actividades y dependencias, y manejo de errores del grid.
    /// </summary>
    public partial class FormProgramaObra
    {

        private bool EsColumnaUndoActividad(string columnName)
        {
            return columnName == "colFechaInicio"
                || columnName == "colFechaFin"
                || columnName == "colDuracionDias"
                || columnName == "colRendimientoDiario"
                || columnName == "colFrentes"
                || columnName == "colCantidad"
                || columnName == "colDescripcion"
                || columnName == "colUnidad"
                || columnName == "colClave";
        }

        private bool EsColumnaUndoDependencia(string columnName)
        {
            return columnName == "colDepClave"
                || columnName == "colDepTipo"
                || columnName == "colDepLag";
        }

        private static bool SonValoresUndoIguales(object? a, object? b)
        {
            if (a is DateTime da && b is DateTime db)
                return da.Date == db.Date;

            if (a is null && b is null)
                return true;

            return Equals(a, b);
        }

        private object? ObtenerValorUndoActividad(ActivityGridRowDto dto, string columnName)
        {
            return columnName switch
            {
                "colClave" => dto.Clave,
                "colDescripcion" => dto.Descripcion,
                "colUnidad" => dto.Unidad,
                "colCantidad" => dto.CantidadTotal,
                "colFechaInicio" => dto.FechaInicioProgramada,
                "colFechaFin" => dto.FechaFinProgramada,
                "colDuracionDias" => dto.DuracionDiasHabiles,
                "colRendimientoDiario" => dto.RendimientoDiario,
                "colFrentes" => dto.FrentesTrabajo,
                _ => null
            };
        }

        private object? ObtenerValorUndoDependencia(DependenciaEditableRow row, string columnName)
        {
            return columnName switch
            {
                "colDepClave" => row.ActividadOrigenId,
                "colDepTipo" => row.TipoDependencia,
                "colDepLag" => row.DesfaseDias,
                _ => null
            };
        }

        private bool TryUndoPrograma()
        {
            if (!_undoManager.CanUndo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Undo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        private bool TryRedoPrograma()
        {
            if (!_undoManager.CanRedo)
                return false;

            try
            {
                _isUndoRedo = true;
                return _undoManager.Redo();
            }
            finally
            {
                _isUndoRedo = false;
            }
        }

        private void AplicarUndoRedoActividadCelda(int rowIndex, string columnName, object? value)
        {
            if (rowIndex < 0 || rowIndex >= dgvActividades.Rows.Count)
                return;

            var row = dgvActividades.Rows[rowIndex];
            if (row.DataBoundItem is not ActivityGridRowDto dto)
                return;

            switch (columnName)
            {
                case "colClave": dto.Clave = value?.ToString() ?? string.Empty; break;
                case "colDescripcion": dto.Descripcion = value?.ToString() ?? string.Empty; break;
                case "colUnidad": dto.Unidad = value?.ToString() ?? string.Empty; break;
                case "colCantidad": dto.CantidadTotal = value is decimal decCant ? decCant : Convert.ToDecimal(value ?? 0m); break;
                case "colFechaInicio": dto.FechaInicioProgramada = value is DateTime fi ? fi : (DateTime?)value; break;
                case "colFechaFin": dto.FechaFinProgramada = value is DateTime ff ? ff : (DateTime?)value; break;
                case "colDuracionDias": dto.DuracionDiasHabiles = value is int dias ? dias : Convert.ToInt32(value ?? 0); break;
                case "colRendimientoDiario": dto.RendimientoDiario = value is decimal decRend ? decRend : Convert.ToDecimal(value ?? 0m); break;
                case "colFrentes": dto.FrentesTrabajo = value is int fr ? fr : Convert.ToInt32(value ?? 0); break;
                default: return;
            }

            var columnIndex = dgvActividades.Columns[columnName].Index;
            dgvActividades.CurrentCell = row.Cells[columnIndex];
            row.Cells[columnIndex].Value = value;
            dgvActividades_CellEndEdit(dgvActividades, new DataGridViewCellEventArgs(columnIndex, rowIndex));
        }

        private void AplicarUndoRedoDependenciaCelda(int rowIndex, string columnName, object? value)
        {
            if (rowIndex < 0 || rowIndex >= dgvDependencias.Rows.Count)
                return;

            var gridRow = dgvDependencias.Rows[rowIndex];
            if (gridRow.DataBoundItem is not DependenciaEditableRow row)
                return;

            switch (columnName)
            {
                case "colDepClave": row.ActividadOrigenId = value is int id ? id : (int?)value; break;
                case "colDepTipo": row.TipoDependencia = value?.ToString() ?? TipoDependenciaActividad.FS.ToString(); break;
                case "colDepLag": row.DesfaseDias = value is int lag ? lag : Convert.ToInt32(value ?? 0); break;
                default: return;
            }

            var columnIndex = dgvDependencias.Columns[columnName].Index;
            dgvDependencias.CurrentCell = gridRow.Cells[columnIndex];
            gridRow.Cells[columnIndex].Value = value;
            dgvDependencias_CellEndEdit(dgvDependencias, new DataGridViewCellEventArgs(columnIndex, rowIndex));
        }

        private void Dgv_DataError(object? sender, DataGridViewDataErrorEventArgs e)
        {
            e.ThrowException = false;
            MessageBox.Show("El valor capturado no tiene un formato válido.", "Programación", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }

        private sealed class DistribucionRow
        {
            public int PeriodoProgramaId { get; set; }
            public int NumeroPeriodo { get; set; }
            public string Periodo { get; set; } = string.Empty;
            public DateTime FechaInicio { get; set; }
            public DateTime FechaFin { get; set; }
            public decimal Cantidad { get; set; }
            public decimal Porcentaje { get; set; }
            public decimal Importe { get; set; }
        }

        private sealed class DependenciaEditableRow
        {
            public int? ActividadOrigenId { get; set; }
            public string Clave { get; set; } = string.Empty;
            public string Descripcion { get; set; } = string.Empty;
            public string TipoDependencia { get; set; } = TipoDependenciaActividad.FS.ToString();
            public int DesfaseDias { get; set; }
        }

        private sealed class ActividadLookupItem
        {
            public int Id { get; set; }
            public string Display { get; set; } = string.Empty;
        }

        private sealed record DependencyTypeOption(string Value, string Text);

        public void RecalcularTodo()
        {
            btnRecalcular_Click(this, EventArgs.Empty);
        }
    }
}
