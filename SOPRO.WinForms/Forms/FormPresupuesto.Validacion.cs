using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Validación de inconsistencias del presupuesto (diálogo, navegación al concepto).
    /// </summary>
    public partial class FormPresupuesto
    {
        /// <summary>
        /// Punto de aislamiento de las pruebas para el diálogo de inconsistencias: si es
        /// null (producción) se construye y muestra el diálogo modal normal; si está
        /// instalado, solo se invoca y no se crea ningún formulario. Permite ejercitar
        /// apertura y operaciones reales sin bloquear el hilo STA del runner.
        /// </summary>
        internal static Action<IReadOnlyList<PresupuestoValidationService.ValidationIssue>, string, bool>?
            InterceptorDialogoInconsistencias { get; set; }

        private List<PresupuestoValidationService.ValidationIssue> ObtenerInconsistenciasPresupuesto()
        {
            return PresupuestoValidationService.Validate(_context, _proyecto.Id);
        }

        private bool ValidarPresupuestoAntesDeContinuar(bool bloquear, string titulo)
        {
            var issues = ObtenerInconsistenciasPresupuesto();
            if (issues.Count == 0)
                return true;

            MostrarDialogoInconsistencias(issues, titulo, bloquear);
            return !bloquear;
        }

        private void MostrarDialogoInconsistencias(List<PresupuestoValidationService.ValidationIssue> issues, string titulo, bool bloquear)
        {
            if (InterceptorDialogoInconsistencias is not null)
            {
                InterceptorDialogoInconsistencias(issues, titulo, bloquear);
                return;
            }

            using var dialog = new Form
            {
                Text = titulo,
                StartPosition = FormStartPosition.CenterParent,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                MinimizeBox = false,
                MaximizeBox = false,
                ShowInTaskbar = false,
                Width = 760,
                Height = 300
            };

            var lblInfo = new Label
            {
                Dock = DockStyle.Top,
                Height = 42,
                Text = bloquear
                    ? "Se detectaron inconsistencias en el presupuesto. Corrija antes de continuar."
                    : "Se detectaron inconsistencias en el presupuesto.",
                Padding = new Padding(12, 12, 12, 0)
            };

            var listBox = new ListBox
            {
                Dock = DockStyle.Fill,
                HorizontalScrollbar = true,
                IntegralHeight = false,
                Font = new Font("Segoe UI", 9F)
            };

            foreach (var issue in issues)
                listBox.Items.Add(issue);

            listBox.Format += (s, e) =>
            {
                if (e.ListItem is PresupuestoValidationService.ValidationIssue issue)
                {
                    var line = issue.ToSingleLine();
                    e.Value = line.Length > 120 ? line.Substring(0, 117) + "..." : line;
                }
            };

            listBox.DoubleClick += (s, e) =>
            {
                if (listBox.SelectedItem is not PresupuestoValidationService.ValidationIssue issue)
                    return;

                IrAConceptoConInconsistencia(issue);
                dialog.DialogResult = DialogResult.OK;
                dialog.Close();
            };

            var lblHint = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 28,
                Padding = new Padding(12, 0, 12, 8),
                Text = "Doble clic en una línea para ir al concepto.",
                ForeColor = SystemColors.GrayText
            };

            var panelBotones = new Panel { Dock = DockStyle.Bottom, Height = 46 };
            var btnCerrar = new Button
            {
                Text = bloquear ? "Corregir" : "Aceptar",
                DialogResult = DialogResult.OK,
                Width = 100,
                Height = 30,
                Left = 640,
                Top = 8,
                Anchor = AnchorStyles.Right | AnchorStyles.Top
            };
            panelBotones.Controls.Add(btnCerrar);

            dialog.Controls.Add(listBox);
            dialog.Controls.Add(lblHint);
            dialog.Controls.Add(panelBotones);
            dialog.Controls.Add(lblInfo);
            dialog.AcceptButton = btnCerrar;

            dialog.ShowDialog(this);
        }

        private void IrAConceptoConInconsistencia(PresupuestoValidationService.ValidationIssue issue)
        {
            if (issue?.ConceptoId == null)
                return;

            for (int i = 0; i < dgvPresupuesto.Rows.Count; i++)
            {
                var row = dgvPresupuesto.Rows[i];
                if (row.Tag is not ConceptoPresupuesto concepto || concepto.Id != issue.ConceptoId.Value)
                    continue;

                dgvPresupuesto.ClearSelection();
                row.Selected = true;
                var targetCell = ObtenerCeldaPorNombreInterno(i, "Cantidad")
                    ?? ObtenerCeldaPorNombreInterno(i, "PrecioUnitario")
                    ?? row.Cells.Cast<DataGridViewCell>().FirstOrDefault();
                if (targetCell != null)
                    dgvPresupuesto.CurrentCell = targetCell;
                dgvPresupuesto.FirstDisplayedScrollingRowIndex = i;
                dgvPresupuesto.Focus();
                break;
            }
        }

        private void MostrarAdvertenciaValidacionEnApertura()
        {
            if (_validacionAperturaMostrada)
                return;

            _validacionAperturaMostrada = true;
            ValidarPresupuestoAntesDeContinuar(bloquear: false, titulo: "Advertencia de presupuesto");
        }
    }
}