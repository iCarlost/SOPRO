using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Depuración, exportación Excel/PDF y ajuste de texto (wrap).
    /// </summary>
    public partial class FormProyecto
    {

        private void btnDepurarRibbon_Click(object sender, EventArgs e)
        {
            try
            {
                var preview = DepuracionService.ObtenerPreview(_context, _proyecto.Id);
                if (preview.TotalCandidatas <= 0)
                {
                    MessageBox.Show(
                        "No se encontraron matrices, auxiliares ni insumos individuales candidatos a depuración.",
                        "Depurar",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                var msg =
                    $"Se eliminarán:\n\n" +
                    $"- {preview.MatricesApuCandidatas} matriz(es) APU no asignadas a conceptos\n" +
                    $"- {preview.MatricesBasicasCandidatas} básico(s) sin uso\n" +
                    $"- {preview.CuadrillasCandidatas} cuadrilla(s) sin uso\n" +
                    $"- {preview.MaterialesCandidatos} material(es) sin uso\n" +
                    $"- {preview.ManoDeObraCandidata} mano(s) de obra sin uso\n" +
                    $"- {preview.HerramientasCandidatas} herramienta(s) sin uso\n" +
                    $"- {preview.MaquinariaCandidata} maquinaria(s) sin uso\n\n" +
                    "Solo se eliminarán si no tienen referencias vigentes.\n" +
                    "Los componentes que sigan usándose en otras matrices permanecerán vivos.\n\n" +
                    "¿Deseas continuar?";

                if (MessageBox.Show(msg, "Depurar", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes)
                    return;

                Cursor = Cursors.WaitCursor;

                var resultado = DepuracionService.Ejecutar(_context, _proyecto.Id);
                RefrescarFormulariosTrasDepuracion();

                MessageBox.Show(
                    $"Depuración completada:\n\n" +
                    $"- APU eliminadas: {resultado.MatricesApuEliminadas}\n" +
                    $"- Básicos eliminados: {resultado.MatricesBasicasEliminadas}\n" +
                    $"- Cuadrillas eliminadas: {resultado.CuadrillasEliminadas}\n" +
                    $"- Materiales eliminados: {resultado.MaterialesEliminados}\n" +
                    $"- Mano de obra eliminada: {resultado.ManoDeObraEliminada}\n" +
                    $"- Herramientas eliminadas: {resultado.HerramientasEliminadas}\n" +
                    $"- Maquinaria eliminada: {resultado.MaquinariaEliminada}",
                    "Depurar",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    $"Error al depurar:\n{ex.Message}",
                    "Error",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void RefrescarFormulariosTrasDepuracion()
        {
            foreach (Form f in System.Windows.Forms.Application.OpenForms)
            {
                if (f is not FormProyecto proyecto) continue;

                foreach (TabPage tab in proyecto.tabControl.TabPages)
                {
                    if (tab.Controls.Count == 0) continue;

                    switch (tab.Controls[0])
                    {
                        case FormMatrices fm:
                            fm.RecargarMatrices();
                            break;
                        case FormPresupuesto fp:
                            fp.RefrescarPreciosDesdeDB();
                            break;
                        case FormCatalogoMateriales fmat:
                            fmat.RecargarCatalogo();
                            break;
                        case FormCatalogoManoObra fmo:
                            fmo.RecargarCatalogo();
                            break;
                        case FormCatalogoHerramientas fh:
                            fh.RecargarCatalogo();
                            break;
                        case FormCatalogoMaquinaria fmq:
                            fmq.RecargarCatalogo();
                            break;
                    }
                }
            }
        }

        private void btnExcelRibbon_Click(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;

                if (_formActivo == null)
                {
                    MessageBox.Show("Abre un modulo primero para generar su reporte.",
                        "Sin modulo activo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                _formActivo.GenerarReporteExcel();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Error al generar el reporte:{ex.Message}",
                    "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }

        private void btnPdfRibbon_Click(object sender, EventArgs e)
        {
            try
            {
                if (tabControl.SelectedTab == null || tabControl.SelectedTab.Controls.Count == 0)
                    return;

                var formActivo = tabControl.SelectedTab.Controls[0];
                if (formActivo is FormPresupuesto frmPresupuesto)
                {
                    using var opciones = new FormExportarReporte(frmPresupuesto.Contexto, frmPresupuesto.ProyectoActual, exportarPdf: true);
                    opciones.ShowDialog(this);
                    return;
                }

                if (formActivo is FormExplosionInsumos frmExplosion)
                {
                    frmExplosion.GenerarPdfExplosion();
                    return;
                }

                if (formActivo is FormIndirectos frmIndirectos)
                {
                    frmIndirectos.GenerarPdfIndirectos();
                    return;
                }

                if (formActivo is FormFSR frmFsr)
                {
                    frmFsr.GenerarPdfFSR();
                    return;
                }

                if (formActivo is FormFinanciamiento frmFinanciamiento)
                {
                    frmFinanciamiento.GenerarPdfFinanciamiento();
                    return;
                }

                if (formActivo is FormUtilidad frmUtilidad)
                {
                    frmUtilidad.GenerarPdfUtilidad();
                    return;
                }

                if (formActivo is FormProgramaObra frmProgramaObra)
                {
                    frmProgramaObra.GenerarPdfProgramaObra();
                    return;
                }

                if (formActivo is FormProgramaInsumos frmProgramaInsumos)
                {
                    frmProgramaInsumos.GenerarPdfProgramaInsumos();
                    return;
                }

                if (formActivo is FormCatalogoMateriales frmCatalogoMateriales)
                {
                    frmCatalogoMateriales.GenerarPdfCatalogoMateriales();
                    return;
                }

                if (formActivo is FormCatalogoManoObra frmCatalogoManoObra)
                {
                    frmCatalogoManoObra.GenerarPdfCatalogoManoObra();
                    return;
                }

                if (formActivo is FormCatalogoHerramientas frmCatalogoHerramientas)
                {
                    frmCatalogoHerramientas.GenerarPdfCatalogoHerramientas();
                    return;
                }

                if (formActivo is FormCatalogoMaquinaria frmCatalogoMaquinaria)
                {
                    frmCatalogoMaquinaria.GenerarPdfCostoHorario();
                    return;
                }

                if (formActivo is FormMatrices frmMatrices)
                {
                    frmMatrices.GenerarPdfCatalogoMatrices();
                    return;
                }

                MessageBox.Show("La exportación PDF aún no está disponible para este módulo.", "PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show($"No fue posible exportar a PDF:\n{ex.Message}", "PDF",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnWrapRibbon_Click(object sender, EventArgs e)
        {
            if (_formActivo?.GridPrincipal == null)
            {
                MessageBox.Show("Abre un modulo primero.",
                    "Sin modulo activo", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var grid = _formActivo.GridPrincipal;
            var nombreColumna = _formActivo.ColumnaSeleccionada?.Nombre;
            if (string.IsNullOrWhiteSpace(nombreColumna))
            {
                MessageBox.Show("Selecciona primero una columna desde su encabezado.",
                    "Sin columna seleccionada", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var col = grid.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.HeaderText, nombreColumna, StringComparison.OrdinalIgnoreCase));

            if (col == null)
            {
                MessageBox.Show("No se encontró la columna seleccionada en el grid activo.",
                    "Columna no encontrada", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            try
            {
                Cursor = Cursors.WaitCursor;

                var activar = col.DefaultCellStyle.WrapMode != DataGridViewTriState.True;
                col.DefaultCellStyle.WrapMode = activar ? DataGridViewTriState.True : DataGridViewTriState.False;

                if (activar && (col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet ||
                                col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.MiddleCenter))
                {
                    col.DefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleLeft;
                }

                AjustarAutoAlturaFilas(grid);
                AplicarTemaBotonRibbon(btnWrapRibbon, activar);
                _formActivo.AplicarFormato(BuildFmt());
                grid.Invalidate();
            }
            finally
            {
                Cursor = Cursors.Default;
            }
        }
    }
}
