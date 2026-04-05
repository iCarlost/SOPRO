using System.Drawing;
using System.Reflection;
using System.Windows.Forms;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Helper para aplicar estilo consistente a todos los DataGridView del sistema.
    /// </summary>
    public static class DataGridViewStyleHelper
    {
        // Color de líneas estilo Excel (gris muy claro)
        private static readonly Color GridColor = Color.FromArgb(230, 230, 230);
        
        /// <summary>
        /// Aplica estilo estándar SOPRO a un DataGridView.
        /// </summary>
        public static void AplicarEstiloSOPRO(this DataGridView dgv)
        {
            HabilitarDobleBuffer(dgv);
            dgv.GridColor = GridColor;
            dgv.AutoSizeRowsMode = DataGridViewAutoSizeRowsMode.None;
            dgv.RowHeadersWidthSizeMode = DataGridViewRowHeadersWidthSizeMode.DisableResizing;
            dgv.BackgroundColor = Color.White;
            dgv.BorderStyle = BorderStyle.None;
            dgv.CellBorderStyle = DataGridViewCellBorderStyle.Single;
            dgv.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.RowHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;
            dgv.EnableHeadersVisualStyles = false;
            
            // Header style
            dgv.ColumnHeadersDefaultCellStyle.BackColor = Color.FromArgb(245, 245, 248);
            dgv.ColumnHeadersDefaultCellStyle.ForeColor = Color.FromArgb(60, 60, 60);
            dgv.ColumnHeadersDefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            dgv.ColumnHeadersDefaultCellStyle.SelectionBackColor = dgv.ColumnHeadersDefaultCellStyle.BackColor;
            
            // Row header style
            dgv.RowHeadersDefaultCellStyle.BackColor = Color.FromArgb(248, 248, 250);
            dgv.RowHeadersDefaultCellStyle.SelectionBackColor = dgv.RowHeadersDefaultCellStyle.BackColor;
            
            // Alternating row colors (muy sutil)
            dgv.AlternatingRowsDefaultCellStyle.BackColor = Color.FromArgb(252, 252, 254);
        }

        private static void HabilitarDobleBuffer(DataGridView dgv)
        {
            if (dgv == null) return;

            var prop = typeof(DataGridView).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(dgv, true, null);
        }

    }
}
