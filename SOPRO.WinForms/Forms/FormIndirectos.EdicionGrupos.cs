using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Alta/baja de grupos y conceptos, auto costo directo y diálogos.
    /// </summary>
    public partial class FormIndirectos
    {

        private void btnAgregarGrupo_Click(object sender, EventArgs e)
        {
            bool esCampo = tabControl.SelectedTab == tabCampo;
            var tipo = esCampo ? TipoIndirecto.Campo : TipoIndirecto.OficinaCentral;

            string nombre = PedirTexto("Nombre del nuevo grupo:");
            if (string.IsNullOrWhiteSpace(nombre)) return;

            int maxOrden = _context.GruposIndirectos
                .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == tipo)
                .Select(g => (int?)g.Orden).Max() ?? 0;

            var grupo = new GrupoIndirecto
            {
                ProyectoId = _proyecto.Id,
                Nombre = nombre.Trim().ToUpper(),
                Tipo = tipo,
                Orden = maxOrden + 1
            };
            _context.GruposIndirectos.Add(grupo);
            _context.SaveChanges();
            CargarGrupos();
        }

        private void btnAgregarConcepto_Click(object sender, EventArgs e)
        {
            bool esCampo = tabControl.SelectedTab == tabCampo;
            var dgv = esCampo ? dgvCampo : dgvOficinaCentral;
            var tipo = esCampo ? TipoIndirecto.Campo : TipoIndirecto.OficinaCentral;

            if (dgv.CurrentRow == null)
            {
                MessageBox.Show("Selecciona primero una fila dentro del grupo.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            // Encontrar el grupo padre de la fila seleccionada
            int grupoId = -1;
            for (int i = dgv.CurrentRow.Index; i >= 0; i--)
            {
                if (dgv.Rows[i].Cells["colTipo"].Value?.ToString() == "GRUPO")
                {
                    grupoId = Convert.ToInt32(dgv.Rows[i].Cells["colId"].Value);
                    break;
                }
            }
            if (grupoId < 0)
            {
                MessageBox.Show("No se encontró un grupo padre. Selecciona una fila dentro de un grupo.", "Info",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            string nombre = PedirTexto("Nombre del concepto:");
            if (string.IsNullOrWhiteSpace(nombre)) return;

            int maxOrden = _context.ConceptosIndirectos
                .Where(c => c.GrupoIndirectoId == grupoId)
                .Select(c => (int?)c.Orden).Max() ?? 0;

            // Calcular duración en meses desde días del proyecto
            int duracionMeses = _proyecto.PlazoEjecucion > 0
                ? (int)Math.Ceiling(_proyecto.PlazoEjecucion / 30.0)
                : 1;

            var concepto = new ConceptoIndirecto
            {
                GrupoIndirectoId = grupoId,
                Concepto = nombre.Trim(),
                Tipo = tipo,
                ImporteMensual = 0,
                DuracionMeses = duracionMeses,
                Orden = maxOrden + 1
            };
            _context.ConceptosIndirectos.Add(concepto);
            _context.SaveChanges();
            CargarGrupos();
        }

        private void btnEliminarFila_Click(object sender, EventArgs e)
        {
            bool esCampo = tabControl.SelectedTab == tabCampo;
            var dgv = esCampo ? dgvCampo : dgvOficinaCentral;

            if (dgv.CurrentRow == null) return;
            var tipoFila = dgv.CurrentRow.Cells["colTipo"].Value?.ToString();
            int id = Convert.ToInt32(dgv.CurrentRow.Cells["colId"].Value);

            string msg = tipoFila == "GRUPO"
                ? "¿Eliminar este grupo y TODOS sus conceptos?"
                : "¿Eliminar este concepto?";

            if (MessageBox.Show(msg, "Confirmar", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;

            try
            {
                if (tipoFila == "GRUPO")
                {
                    var g = _context.GruposIndirectos
                        .Include(x => x.Conceptos)
                        .FirstOrDefault(x => x.Id == id);
                    if (g != null)
                    {
                        _context.ConceptosIndirectos.RemoveRange(g.Conceptos);
                        _context.GruposIndirectos.Remove(g);
                    }
                }
                else
                {
                    var c = _context.ConceptosIndirectos.Find(id);
                    if (c != null) _context.ConceptosIndirectos.Remove(c);
                }
                _context.SaveChanges();
                CargarGrupos();
                ActualizarResumen();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        /// <summary>
        /// Toma el Costo Directo total del presupuesto activo y lo pone en el campo CD.
        /// </summary>
        private void btnAutoCD_Click(object sender, EventArgs e)
        {
            var cd = BudgetPricingService.SumDirectCost(_proyecto,
                _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .AsNoTracking()
                    .AsEnumerable());

            if (cd == 0)
            {
                MessageBox.Show("No hay costo directo en el presupuesto todavía.", "Sin datos",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            txtCostoDirecto.Text = cd.ToStringCantidad();
            MessageBox.Show($"Costo Directo cargado: ${cd:N2}", "OK",
                MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        /// <summary>
        /// Abre FormPorcentajes directamente desde el módulo de Indirectos.
        /// Útil cuando ya terminaste de calcular y quieres asignar Financiamiento/Utilidad.
        /// </summary>
        private void btnPorcentajesDirectos_Click(object sender, EventArgs e)
        {
            using var form = new FormPorcentajes(_context, _proyecto);
            form.ShowDialog(this);
        }

        /// <summary>
        /// Diálogo de entrada de texto simple (reemplaza Microsoft.VisualBasic.InputBox).
        /// </summary>
        private string PedirTexto(string prompt)
        {
            using var dlg = new Form
            {
                Text = "SOPRO",
                Width = 420,
                Height = 130,
                FormBorderStyle = FormBorderStyle.FixedDialog,
                StartPosition = FormStartPosition.CenterParent,
                MaximizeBox = false,
                MinimizeBox = false
            };
            var lbl = new Label { Text = prompt, Location = new Point(12, 12), AutoSize = true };
            var txt = new TextBox { Location = new Point(12, 32), Width = 380 };
            var btnOk = new Button
            {
                Text = "Aceptar",
                DialogResult = DialogResult.OK,
                Location = new Point(220, 60),
                Width = 80
            };
            var btnCancel = new Button
            {
                Text = "Cancelar",
                DialogResult = DialogResult.Cancel,
                Location = new Point(312, 60),
                Width = 80
            };
            dlg.Controls.AddRange(new Control[] { lbl, txt, btnOk, btnCancel });
            dlg.AcceptButton = btnOk; dlg.CancelButton = btnCancel;
            return dlg.ShowDialog() == DialogResult.OK ? txt.Text.Trim() : null;
        }
    }
}
