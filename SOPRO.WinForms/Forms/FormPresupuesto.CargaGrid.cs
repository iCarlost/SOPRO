using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Undo;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Carga del grid del presupuesto: filas, columnas personalizadas y jerarquía.
    /// </summary>
    public partial class FormPresupuesto
    {

        private int ObtenerUltimaFilaConDatos()
        {
            for (int i = dgvPresupuesto.Rows.Count - 1; i >= 0; i--)
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(i, "Tipo");
                var descCell = ObtenerCeldaPorNombreInterno(i, "Descripcion");
                var claveCell = ObtenerCeldaPorNombreInterno(i, "Clave");

                if ((tipoCell != null && !string.IsNullOrWhiteSpace(tipoCell.Value?.ToString()))
                    || (descCell != null && !string.IsNullOrWhiteSpace(descCell.Value?.ToString()))
                    || (claveCell != null && !string.IsNullOrWhiteSpace(claveCell.Value?.ToString())))
                {
                    return i;
                }
            }

            return -1;
        }

        private int ObtenerPrimeraFilaVacia()
        {
            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var tipoCell = ObtenerCeldaPorNombreInterno(i, "Tipo");
                var descCell = ObtenerCeldaPorNombreInterno(i, "Descripcion");

                // Fila vacía = no tiene tipo ni descripción
                if ((tipoCell == null || tipoCell.Value == null || string.IsNullOrWhiteSpace(tipoCell.Value.ToString())) &&
                    (descCell == null || descCell.Value == null || string.IsNullOrWhiteSpace(descCell.Value.ToString())))
                {
                    return i;
                }
            }
            return dgvPresupuesto.Rows.Count; // Todas están llenas
        }

        private List<SOPRO.Application.Models.Presupuesto.BudgetHierarchyRow> BuildHierarchyRowsSnapshot()
        {
            var rows = new List<SOPRO.Application.Models.Presupuesto.BudgetHierarchyRow>();

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                string tipo = ObtenerCeldaTexto(i, "Tipo");
                string clave = ObtenerCeldaTexto(i, "Clave");
                string descripcion = ObtenerCeldaTexto(i, "Descripcion");
                decimal importe = ObtenerCeldaDecimal(i, "Importe");
                var concepto = dgvPresupuesto.Rows[i].Tag as ConceptoPresupuesto;

                bool hasContent = !string.IsNullOrWhiteSpace(tipo)
                    || !string.IsNullOrWhiteSpace(clave)
                    || !string.IsNullOrWhiteSpace(descripcion)
                    || concepto != null;

                rows.Add(new SOPRO.Application.Models.Presupuesto.BudgetHierarchyRow
                {
                    RowIndex = i,
                    Tipo = string.IsNullOrWhiteSpace(tipo) ? "Concepto" : tipo,
                    Importe = importe,
                    HasContent = hasContent,
                    IsConcept = string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase)
                        || (string.IsNullOrWhiteSpace(tipo) && concepto is { EsAgrupador: false })
                });
            }

            return rows;
        }

        private void RemoveIntermediateEmptyRows()
        {
            var rows = BuildHierarchyRowsSnapshot();
            var indexes = BudgetHierarchyService.GetIntermediateEmptyRowIndexes(rows);
            for (int i = indexes.Count - 1; i >= 0; i--)
            {
                dgvPresupuesto.Rows.RemoveAt(indexes[i]);
            }
        }

        /// <summary>
        /// Obtiene una celda por el nombre interno de su columna
        /// </summary>
        private DataGridViewCell ObtenerCeldaPorNombreInterno(int rowIndex, string nombreInterno)
        {
            if (rowIndex < 0 || rowIndex >= dgvPresupuesto.Rows.Count) return null;

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef && colDef.NombreInterno == nombreInterno)
                {
                    return dgvPresupuesto.Rows[rowIndex].Cells[col.Index];
                }
            }
            return null;
        }

        /// <summary>
        /// Obtiene el índice de una columna por su nombre interno
        /// </summary>
        private int ObtenerIndiceColumnaPorNombreInterno(string nombreInterno)
        {
            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef && colDef.NombreInterno == nombreInterno)
                {
                    return col.Index;
                }
            }
            return -1;
        }

        private void RecargarPresupuestoPreservandoEstado()
        {
            var state = DataGridViewStateHelper.Capture(dgvPresupuesto);
            CargarPresupuesto(state);
        }

        private void CargarPresupuesto(DataGridViewStateSnapshot? stateToRestore = null)
        {
            try
            {
                _cargando = true;

                var loadState = BudgetLoadService.BuildLoadState(_context, _proyecto);
                CargarColumnasPersonalizadas(loadState);

                dgvPresupuesto.Rows.Clear();

                foreach (var rowDisplay in loadState.RowDisplays)
                {
                    AgregarFilaConcepto(rowDisplay);
                }

                for (int i = 0; i < loadState.EmptyRowsToAppend; i++)
                {
                    dgvPresupuesto.Rows.Add();
                }

                RecalcularTodosLosTotales();
                GuardarCambios();       // Persistir totales de agrupadores recalculados en BD
                ReasignarNumerosConceptos();
                ActualizarEstadisticas();

                if (stateToRestore != null && dgvPresupuesto.Rows.Count > 0)
                {
                    DataGridViewStateHelper.Restore(dgvPresupuesto, stateToRestore);
                }

                _cargando = false;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al cargar presupuesto:\n{ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                _cargando = false;
            }
        }

        private void CargarColumnasPersonalizadas(SOPRO.Application.Models.Presupuesto.BudgetGridLoadState? loadState = null)
        {
            var columnasAEliminar = dgvPresupuesto.Columns.Cast<DataGridViewColumn>()
                .Where(c => c.Name != "colId" && c.Name != "colNumero")
                .ToList();

            foreach (var col in columnasAEliminar)
            {
                dgvPresupuesto.Columns.Remove(col);
            }

            loadState ??= BudgetLoadService.BuildLoadState(_context, _proyecto);

            foreach (var definition in loadState.ColumnDefinitions)
            {
                DataGridViewColumn nuevaColumna;

                if (definition.IsFillColumn)
                {
                    nuevaColumna = new DataGridViewTextBoxColumn
                    {
                        Name = definition.Name,
                        HeaderText = definition.HeaderText,
                        ReadOnly = true,
                        AutoSizeMode = DataGridViewAutoSizeColumnMode.Fill,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        DefaultCellStyle = new DataGridViewCellStyle { BackColor = Color.FromArgb(250, 250, 250) }
                    };
                    dgvPresupuesto.Columns.Add(nuevaColumna);
                    continue;
                }

                if (definition.IsTypeSelector)
                {
                    var comboCol = new DataGridViewComboBoxColumn
                    {
                        Name = definition.Name,
                        HeaderText = definition.HeaderText,
                        Width = definition.Width,
                        DisplayStyle = DataGridViewComboBoxDisplayStyle.Nothing,
                        Tag = definition.SourceColumn,
                        Visible = definition.IsVisible,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        ReadOnly = definition.IsReadOnly
                    };
                    comboCol.Items.AddRange(new object[] { "Capitulo", "Subcapitulo", "Nivel 1", "Nivel 2", "Nivel 3", "Concepto" });
                    nuevaColumna = comboCol;
                }
                else
                {
                    nuevaColumna = definition.TipoDato == TipoDatoColumna.Booleano
                        ? new DataGridViewCheckBoxColumn()
                        : new DataGridViewTextBoxColumn();

                    nuevaColumna.Name = definition.Name;
                    nuevaColumna.HeaderText = definition.HeaderText;
                    nuevaColumna.Width = definition.Width;
                    nuevaColumna.Tag = definition.SourceColumn;
                    nuevaColumna.Visible = definition.IsVisible;
                    nuevaColumna.SortMode = DataGridViewColumnSortMode.NotSortable;
                    nuevaColumna.ReadOnly = definition.IsReadOnly;

                    if (definition.AlignRight)
                    {
                        nuevaColumna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleRight;
                    }
                    else if (definition.AlignCenter)
                    {
                        nuevaColumna.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
                    }

                    if (definition.UseCalculatedBackColor)
                    {
                        nuevaColumna.DefaultCellStyle.BackColor = Color.FromArgb(250, 250, 250);
                    }

                    if (definition.SourceColumn != null && string.Equals(definition.SourceColumn.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
                    {
                        nuevaColumna.ValueType = typeof(decimal);
                        nuevaColumna.DefaultCellStyle.Format = $"N{_proyecto.DecimalesCantidad}";
                    }
                }

                dgvPresupuesto.Columns.Add(nuevaColumna);
            }

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada colDef)
                {
                    int displayIndex = colDef.Orden;
                    if (displayIndex >= 0 && displayIndex < dgvPresupuesto.Columns.Count)
                    {
                        col.DisplayIndex = displayIndex;
                    }
                }
            }

            dgvPresupuesto.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvPresupuesto.ScrollBars = ScrollBars.Both;

            if (dgvPresupuesto.Columns["colNumero"] != null)
                dgvPresupuesto.Columns["colNumero"].Width = 40;

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is ColumnaPersonalizada)
                {
                    col.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                }
            }

            FormatoHelper.AplicarWrapYAlineacionPersistidos(dgvPresupuesto);
            FormatoHelper.AjustarAutoAlturaFilas(dgvPresupuesto);
        }

        private void AgregarFilaConcepto(SOPRO.Application.Models.Presupuesto.BudgetGridRowDisplay rowDisplay)
        {
            var row = dgvPresupuesto.Rows[dgvPresupuesto.Rows.Add()];
            row.Tag = rowDisplay.Concepto;

            foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
            {
                if (col.Tag is not ColumnaPersonalizada colDef) continue;

                if (rowDisplay.ValuesByInternalName.TryGetValue(colDef.NombreInterno, out var valor) && valor != null)
                {
                    row.Cells[col.Index].Value = valor;
                }
            }
        }
    }
}
