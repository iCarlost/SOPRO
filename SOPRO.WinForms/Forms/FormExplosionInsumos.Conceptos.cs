using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Generación de secciones de conceptos y filas de totales.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        private void ExplotarConceptos(
            List<ConceptoPresupuesto> conceptos,
            out Dictionary<int, DatosInsumo> materiales,
            out Dictionary<int, DatosInsumo> manoObra,
            out Dictionary<int, DatosInsumo> maquinaria,
            out Dictionary<int, DatosInsumo> herramientas)
        {
            materiales   = new Dictionary<int, DatosInsumo>();
            manoObra     = new Dictionary<int, DatosInsumo>();
            maquinaria   = new Dictionary<int, DatosInsumo>();
            herramientas = new Dictionary<int, DatosInsumo>();

            foreach (var concepto in conceptos)
            {
                if (concepto.Matriz == null) continue;
                // Calcular con el mismo redondeo que usa el presupuesto en pantalla
                decimal importeConcepto = new MotorCalculoSopro(_proyecto).Multiplicar(
                    concepto.Cantidad, concepto.CostoDirectoUnitario);
                ExplotarMatriz(concepto.Matriz, importeConcepto, concepto.Cantidad,
                    materiales, manoObra, maquinaria, herramientas);
            }
        }

        private void GenerarSeccionDesdeDict(
            string titulo,
            Dictionary<int, DatosInsumo> dic,
            decimal costoDirectoTotal)
        {
            if (dic.Count == 0) return;

            AgregarFilaEncabezado(titulo);

            decimal total = 0;
            foreach (var kvp in dic.OrderBy(x => x.Value.Clave))
            {
                var ins = kvp.Value;
                // ins.Cantidad = importe acumulado (método OPUS PLANET)
                decimal importe = ins.Cantidad;
                total += importe;
                decimal pct = costoDirectoTotal > 0 ? importe / costoDirectoTotal : 0;

                // Inferir cantidad: importe / PU  (como hace OPUS PLANET)
                // PU=0 indica %MO — cantidad no aplica
                string cantStr, puStr;
                if (ins.PrecioUnitario == 0m || ins.Unidad?.Trim().ToUpper() == "%MO")
                {
                    cantStr = "—";
                    puStr   = "—";
                }
                else
                {
                    cantStr = ins.CantidadFisica.ToStringCantidad();
                    puStr   = ins.PrecioUnitario.ToStringImporte();
                }

                dgvExplosion.Rows.Add(
                    ins.Clave, ins.Descripcion, ins.Unidad,
                    cantStr, puStr,
                    importe.ToStringImporte(),
                    pct);
            }

            decimal pctTotal = costoDirectoTotal > 0 ? total / costoDirectoTotal : 0;
            AgregarFilaTotal($"TOTAL {titulo}", total, pctTotal);
            AgregarFilaVacia();
        }

        // Mantener métodos originales de sección como wrappers del nuevo sistema
        private void GenerarSeccionMateriales(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("MATERIALES", dic, costoDirectoTotal);

        private void GenerarSeccionManoDeObra(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("MANO DE OBRA", dic, costoDirectoTotal);

        private void GenerarSeccionMaquinaria(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("MAQUINARIA", dic, costoDirectoTotal);

        private void GenerarSeccionHerramientas(List<ConceptoPresupuesto> conceptos, decimal costoDirectoTotal,
            Dictionary<int, DatosInsumo> dic) => GenerarSeccionDesdeDict("HERRAMIENTAS", dic, costoDirectoTotal);

                private void AgregarFilaEncabezado(string texto)
        {
            int rowIndex = dgvExplosion.Rows.Add("", texto, "", "", "", "", "");
            var row = dgvExplosion.Rows[rowIndex];
            
            // Estilo de encabezado
            row.DefaultCellStyle.BackColor = Color.FromArgb(70, 130, 180);
            row.DefaultCellStyle.ForeColor = Color.White;
            row.DefaultCellStyle.Font = new Font("Segoe UI", 9.5F, FontStyle.Bold);
            row.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
        }
        
        private void AgregarFilaTotal(string texto, decimal importe, decimal porcentaje)
        {
            int rowIndex = dgvExplosion.Rows.Add("", texto, "", "", "", importe.ToStringImporte(), porcentaje);
            var row = dgvExplosion.Rows[rowIndex];
            
            // Estilo de total
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            row.DefaultCellStyle.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
            row.Cells["colImporte"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            row.Cells["colPorcentaje"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        
        private void AgregarFilaTotalGeneral(decimal costoDirectoTotal)
        {
            int rowIndex = dgvExplosion.Rows.Add("", "TOTAL DEL REPORTE", "", "", "", costoDirectoTotal.ToStringImporte(), 1.0m);
            var row = dgvExplosion.Rows[rowIndex];
            
            // Estilo especial para total general
            row.DefaultCellStyle.BackColor = Color.FromArgb(45, 45, 48);
            row.DefaultCellStyle.ForeColor = Color.White;
            row.DefaultCellStyle.Font = new Font("Segoe UI", 10F, FontStyle.Bold);
            row.Cells["colImporte"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
            row.Cells["colPorcentaje"].Style.Alignment = DataGridViewContentAlignment.MiddleRight;
        }
        
        private void AgregarFilaVacia()
        {
            int rowIndex = dgvExplosion.Rows.Add("", "", "", "", "", "", "");
            var row = dgvExplosion.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = Color.White;
            row.Height = 10;
        }

        private void AgregarFilaReferencia(string etiqueta, decimal valor, decimal porcentaje)
        {
            int rowIndex = dgvExplosion.Rows.Add("", etiqueta, "", "", "",
                valor.ToStringImporte(), porcentaje);
            var row = dgvExplosion.Rows[rowIndex];
            row.DefaultCellStyle.BackColor = Color.FromArgb(240, 240, 240);
            row.DefaultCellStyle.ForeColor = Color.FromArgb(100, 100, 100);
            row.DefaultCellStyle.Font = new Font("Segoe UI", 8.5F, FontStyle.Italic);
        }
        
        private void cmbFiltro_SelectedIndexChanged(object sender, EventArgs e)
        {
            GenerarExplosion();
        }
    }
}
