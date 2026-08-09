using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Insumos;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Restauración y acumulación de selección en el grid.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void RestoreSelections()
        {
            _suppressPersistentSelectionSync = true;
            try
            {
                foreach (DataGridViewRow row in dgvInsumos.Rows)
                    ApplyAccumulatedState(row, reselectVisibleRow: true);
            }
            finally
            {
                _suppressPersistentSelectionSync = false;
            }
        }

        private void ApplyAccumulatedState(DataGridViewRow row, bool reselectVisibleRow)
        {
            var key = BuildSelectionKey(row.Tag);
            var isAccumulated = key != null && _selectedItems.ContainsKey(key);

            if (isAccumulated && key != null)
                _selectedItems[key] = row.Tag!;

            row.DefaultCellStyle.BackColor = isAccumulated ? AccumulatedRowBackColor : Color.White;
            row.DefaultCellStyle.SelectionBackColor = isAccumulated ? AccumulatedRowSelectionBackColor : SystemColors.Highlight;
            row.DefaultCellStyle.SelectionForeColor = Color.White;

            // La selección persistente ya se comunica con color de fondo suave.
            // No debemos forzar row.Selected porque eso estorba la selección múltiple
            // nativa con Ctrl/Shift y hace parecer que todo ya quedó "activo".
            if (reselectVisibleRow)
                row.Selected = false;
        }

        private void RefreshAccumulatedSelectionFromGrid()
        {
            if (_suppressPersistentSelectionSync) return;

            var accumulateGesture = dgvInsumos.SelectedRows.Count > 1 ||
                                    (Control.ModifierKeys & (Keys.Control | Keys.Shift)) != Keys.None;

            if (accumulateGesture)
            {
                foreach (DataGridViewRow row in dgvInsumos.SelectedRows)
                {
                    var key = BuildSelectionKey(row.Tag);
                    if (key == null || row.Tag == null) continue;
                    _selectedItems[key] = row.Tag;
                }
            }

            _suppressPersistentSelectionSync = true;
            try
            {
                foreach (DataGridViewRow row in dgvInsumos.Rows)
                    ApplyAccumulatedState(row, reselectVisibleRow: false);
            }
            finally
            {
                _suppressPersistentSelectionSync = false;
            }
        }

        private string? Filtro()
        {
            var t = txtBuscar?.Text?.Trim();
            return string.IsNullOrWhiteSpace(t) ? null : t;
        }
    }
}
