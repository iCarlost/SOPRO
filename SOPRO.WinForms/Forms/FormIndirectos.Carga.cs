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
    /// Carga de configuración, grupos y llenado de los grids.
    /// </summary>
    public partial class FormIndirectos
    {

        private void CargarDatos()
        {
            // Cargar configuración
            _configuracion = _context.ConfiguracionesIndirectos
                .FirstOrDefault(c => c.ProyectoId == _proyecto.Id);

            if (_configuracion == null)
            {
                _configuracion = new ConfiguracionIndirectos
                {
                    ProyectoId = _proyecto.Id
                };
                _context.ConfiguracionesIndirectos.Add(_configuracion);
                _context.SaveChanges();
            }

            // Calcular duración en meses desde días del proyecto
            int duracionMeses = _proyecto.PlazoEjecucion > 0
                ? (int)Math.Ceiling(_proyecto.PlazoEjecucion / 30.0)
                : 1;

            // Actualizar duración de TODOS los conceptos si es diferente
            var conceptosActualizar = _context.ConceptosIndirectos
                .Include(c => c.GrupoIndirecto)
                .Where(c => c.GrupoIndirecto.ProyectoId == _proyecto.Id && c.DuracionMeses != duracionMeses)
                .ToList();

            if (conceptosActualizar.Any())
            {
                foreach (var concepto in conceptosActualizar)
                {
                    concepto.DuracionMeses = duracionMeses;
                }
                _context.SaveChanges();
            }

            // Recalcular costo directo siempre (aplica precisión de pantalla actual)
            {
                var cd = new SOPRO.Application.Services.MotorCalculoSopro(_proyecto).SumarCostoDirecto(
                    _context.ConceptosPresupuesto
                        .Where(c => c.ProyectoId == _proyecto.Id)
                        .AsNoTracking()
                        .AsEnumerable());

                if (cd > 0)
                {
                    _configuracion.CostoDirectoObra = cd;
                    _context.SaveChanges();
                }
            }

            // Mostrar configuración
            txtVolumenAnual.Text = _configuracion.VolumenAnualObra.ToStringCantidad();
            txtCostoDirecto.Text = _configuracion.CostoDirectoObra.ToStringCantidad();

            // Cargar grupos
            CargarGrupos();
            ActualizarResumen();
        }

        private void CargarGrupos()
        {
            var gruposOC = _context.GruposIndirectos
                .Include(g => g.Conceptos)
                .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.OficinaCentral)
                .OrderBy(g => g.Orden)
                .ToList();

            var gruposCampo = _context.GruposIndirectos
                .Include(g => g.Conceptos)
                .Where(g => g.ProyectoId == _proyecto.Id && g.Tipo == TipoIndirecto.Campo)
                .OrderBy(g => g.Orden)
                .ToList();

            LlenarGrid(dgvOficinaCentral, gruposOC, showDuracion: false);
            LlenarGrid(dgvCampo, gruposCampo, showDuracion: false);
        }

        private void LlenarGrid(DataGridView dgv, System.Collections.Generic.List<GrupoIndirecto> grupos, bool showDuracion)
        {
            dgv.Rows.Clear();

            foreach (var grupo in grupos)
            {
                // Fila del grupo (header)
                var rowGrupo = dgv.Rows.Add();
                dgv.Rows[rowGrupo].Cells["colGrupo"].Value = grupo.Nombre;
                dgv.Rows[rowGrupo].Cells["colImporteMensual"].Value = DBNull.Value;
                dgv.Rows[rowGrupo].Cells["colDuracion"].Value = DBNull.Value;
                dgv.Rows[rowGrupo].Cells["colImporteTotal"].Value = grupo.Total;
                dgv.Rows[rowGrupo].Cells["colId"].Value = grupo.Id;
                dgv.Rows[rowGrupo].Cells["colTipo"].Value = "GRUPO";
                dgv.Rows[rowGrupo].DefaultCellStyle.Font = new Font(dgv.Font, FontStyle.Bold);
                dgv.Rows[rowGrupo].DefaultCellStyle.BackColor = Color.FromArgb(230, 230, 230);
                dgv.Rows[rowGrupo].ReadOnly = true;

                // Filas de conceptos
                foreach (var concepto in grupo.Conceptos.OrderBy(c => c.Orden))
                {
                    var rowConcepto = dgv.Rows.Add();
                    dgv.Rows[rowConcepto].Cells["colGrupo"].Value = "    " + concepto.Concepto;
                    dgv.Rows[rowConcepto].Cells["colImporteMensual"].Value = concepto.ImporteMensual;
                    dgv.Rows[rowConcepto].Cells["colDuracion"].Value = showDuracion ? concepto.DuracionMeses : (object)DBNull.Value;
                    dgv.Rows[rowConcepto].Cells["colImporteTotal"].Value = concepto.ImporteTotal;
                    dgv.Rows[rowConcepto].Cells["colId"].Value = concepto.Id;
                    dgv.Rows[rowConcepto].Cells["colTipo"].Value = "CONCEPTO";
                }
            }

            // Ocultar columna duración si no aplica
            dgv.Columns["colDuracion"].Visible = showDuracion;
        }
    }
}
