using System.Reflection;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    internal sealed class DataGridViewStateSnapshot
    {
        public int? RowIndex { get; init; }
        public string? ColumnName { get; init; }
        public int? FirstDisplayedRowIndex { get; init; }
        public object? BoundItemId { get; init; }
    }

    internal static class DataGridViewStateHelper
    {
        public static DataGridViewStateSnapshot Capture(DataGridView dgv)
        {
            return new DataGridViewStateSnapshot
            {
                RowIndex = dgv.CurrentCell?.RowIndex,
                ColumnName = dgv.CurrentCell?.OwningColumn?.Name,
                FirstDisplayedRowIndex = GetFirstDisplayedRowIndexSafe(dgv),
                BoundItemId = GetRowItemId(dgv.CurrentRow)
            };
        }

        public static void Restore(DataGridView dgv, DataGridViewStateSnapshot? state)
        {
            if (state == null || dgv.Rows.Count == 0)
                return;

            try
            {
                int targetRowIndex = FindRowIndexById(dgv, state.BoundItemId);
                if (targetRowIndex < 0 && state.RowIndex.HasValue)
                {
                    targetRowIndex = Math.Max(0, Math.Min(state.RowIndex.Value, dgv.Rows.Count - 1));
                }

                if (targetRowIndex < 0 || targetRowIndex >= dgv.Rows.Count)
                    targetRowIndex = FindFirstVisibleRowIndex(dgv);

                if (targetRowIndex < 0 || targetRowIndex >= dgv.Rows.Count)
                    return;

                var targetColumn = FindColumn(dgv, state.ColumnName) ?? GetFirstVisibleColumn(dgv);

                dgv.ClearSelection();
                if (dgv.Rows[targetRowIndex].Visible)
                    dgv.Rows[targetRowIndex].Selected = true;

                if (!TrySetCurrentCell(dgv, targetRowIndex, targetColumn))
                    TrySetCurrentCell(dgv, targetRowIndex, null);

                if (state.FirstDisplayedRowIndex.HasValue)
                {
                    try
                    {
                        int first = Math.Max(0, Math.Min(state.FirstDisplayedRowIndex.Value, dgv.Rows.Count - 1));
                        while (first < dgv.Rows.Count && !dgv.Rows[first].Visible)
                            first++;

                        if (first >= 0 && first < dgv.Rows.Count)
                            dgv.FirstDisplayedScrollingRowIndex = first;
                    }
                    catch
                    {
                        // ignore scrolling restore issues
                    }
                }
            }
            catch
            {
                EnsureSafeCurrentCell(dgv);
            }
        }

        private static int? GetFirstDisplayedRowIndexSafe(DataGridView dgv)
        {
            try
            {
                return dgv.FirstDisplayedScrollingRowIndex;
            }
            catch
            {
                return null;
            }
        }

        private static DataGridViewColumn? FindColumn(DataGridView dgv, string? columnName)
        {
            if (string.IsNullOrWhiteSpace(columnName))
                return null;

            return dgv.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c => c.Name == columnName && c.Visible);
        }

        public static DataGridViewColumn? GetFirstVisibleColumn(DataGridView dgv)
        {
            return dgv.Columns.Cast<DataGridViewColumn>().FirstOrDefault(c => c.Visible);
        }

        public static bool EnsureSafeCurrentCell(DataGridView dgv, int preferredRowIndex = 0)
        {
            if (dgv.Rows.Count == 0)
                return false;

            var rowIndex = FindNearestVisibleRowIndex(dgv, preferredRowIndex);
            if (rowIndex < 0)
                return false;

            dgv.ClearSelection();
            if (dgv.Rows[rowIndex].Visible)
                dgv.Rows[rowIndex].Selected = true;

            return TrySetCurrentCell(dgv, rowIndex, null);
        }

        public static DataGridViewCell? GetFirstVisibleCell(DataGridView dgv, int preferredRowIndex = 0)
        {
            if (dgv.Rows.Count == 0)
                return null;

            var rowIndex = FindNearestVisibleRowIndex(dgv, preferredRowIndex);
            if (rowIndex < 0)
                return null;

            foreach (DataGridViewColumn column in dgv.Columns)
            {
                if (!column.Visible)
                    continue;

                var cell = dgv.Rows[rowIndex].Cells[column.Index];
                if (cell.Visible)
                    return cell;
            }

            return null;
        }

        private static int FindRowIndexById(DataGridView dgv, object? boundItemId)
        {
            if (boundItemId == null)
                return -1;

            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                if (!dgv.Rows[i].Visible)
                    continue;

                var id = GetRowItemId(dgv.Rows[i]);
                if (id != null && Equals(id, boundItemId))
                    return i;
            }

            return -1;
        }

        private static bool TrySetCurrentCell(DataGridView dgv, int rowIndex, DataGridViewColumn? preferredColumn)
        {
            if (rowIndex < 0 || rowIndex >= dgv.Rows.Count)
                return false;

            if (!dgv.Rows[rowIndex].Visible)
                return false;

            if (preferredColumn != null && preferredColumn.Visible)
            {
                try
                {
                    var preferredCell = dgv.Rows[rowIndex].Cells[preferredColumn.Index];
                    if (preferredCell.Visible)
                    {
                        dgv.CurrentCell = preferredCell;
                        return true;
                    }
                }
                catch
                {
                }
            }

            var fallbackCell = GetFirstVisibleCell(dgv, rowIndex);
            if (fallbackCell == null)
                return false;

            try
            {
                dgv.CurrentCell = fallbackCell;
                return true;
            }
            catch
            {
                return false;
            }
        }

        private static int FindNearestVisibleRowIndex(DataGridView dgv, int preferredRowIndex)
        {
            if (dgv.Rows.Count == 0)
                return -1;

            preferredRowIndex = Math.Max(0, Math.Min(preferredRowIndex, dgv.Rows.Count - 1));
            if (dgv.Rows[preferredRowIndex].Visible)
                return preferredRowIndex;

            for (int i = preferredRowIndex - 1; i >= 0; i--)
            {
                if (dgv.Rows[i].Visible)
                    return i;
            }

            for (int i = preferredRowIndex + 1; i < dgv.Rows.Count; i++)
            {
                if (dgv.Rows[i].Visible)
                    return i;
            }

            return -1;
        }

        private static int FindFirstVisibleRowIndex(DataGridView dgv)
        {
            for (int i = 0; i < dgv.Rows.Count; i++)
            {
                if (dgv.Rows[i].Visible)
                    return i;
            }

            return -1;
        }

        private static object? GetRowItemId(DataGridViewRow? row)
        {
            if (row == null)
                return null;

            return GetBoundItemId(row.DataBoundItem) ?? GetBoundItemId(row.Tag);
        }

        private static object? GetBoundItemId(object? dataBoundItem)
        {
            if (dataBoundItem == null)
                return null;

            PropertyInfo? idProperty = dataBoundItem.GetType().GetProperty("Id", BindingFlags.Public | BindingFlags.Instance);
            return idProperty?.GetValue(dataBoundItem);
        }
    }
}
