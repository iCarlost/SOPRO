using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Models;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Reportes y formato del programa de insumos.
    /// </summary>
    public partial class FormProgramaInsumos
    {

        public bool GenerarReporteExcel()
        {
            try
            {
                if (_actual == null || _ganttControl == null || _ganttControl.RenderModel == null)
                {
                    MessageBox.Show("No hay un programa de insumos cargado para exportar.", "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                var ganttModel = _ganttControl.RenderModel;
                if (ganttModel.Filas.Count == 0 || ganttModel.Escala.Count == 0)
                {
                    MessageBox.Show("El programa de insumos no tiene datos suficientes para generar el reporte Excel.", "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return false;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                FormatoHelper.EstablecerProyecto(_proyecto);

                var tituloReporte = ObtenerTituloReporteProgramaInsumos();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.ProgramaInsumos, lblTitulo.Text);
                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Excel del programa de insumos",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"{tituloReporte.Replace(' ', '_')}_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK)
                    return false;

                Cursor = Cursors.WaitCursor;
                var generador = new Services.GeneradorExcelProgramaInsumos(svc);
                var ruta = generador.Generar(
                    _proyecto,
                    plantilla,
                    dgvProgramaInsumos,
                    ganttModel,
                    _ganttControl.VisualSettings,
                    _ganttControl.FooterDisplayMode,
                    _ganttControl.TimelineCellWidth,
                    tituloReporte,
                    dlg.FileName,
                    tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte generado:\n" + ruta + "\n\n¿Abrir ahora?", "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = ruta,
                        UseShellExecute = true
                    });
                }

                return true;
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte de insumos:\n" + ex.Message, "Programa de insumos", MessageBoxButtons.OK, MessageBoxIcon.Error);
                return false;
            }
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colRibbon == null) return;
            _colRibbon.NombreFuente = fmt.NombreFuente;
            _colRibbon.TamanoFuente = fmt.TamanoFuente;
            _colRibbon.Negrita = fmt.Negrita;
            _colRibbon.Cursiva = fmt.Cursiva;
            _colRibbon.Alineacion = fmt.Alineacion;
            _colRibbon.ColorFondo = fmt.ColorFondo;
            _colRibbon.ColorFuente = fmt.ColorFuente;
            _colRibbon.WrapTexto = fmt.WrapTexto;
            _colRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();
            CargarProgramaInsumos();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (var cfg in _columnasConfig)
            {
                cfg.NombreFuente = fmt.NombreFuente;
                cfg.TamanoFuente = fmt.TamanoFuente;
                cfg.Negrita = fmt.Negrita;
                cfg.Cursiva = fmt.Cursiva;
                cfg.Alineacion = fmt.Alineacion;
                cfg.ColorFuente = fmt.ColorFuente;
                cfg.WrapTexto = fmt.WrapTexto;
                cfg.AlineacionVertical = fmt.AlineacionVertical;
                cfg.FechaModificacion = DateTime.Now;
            }
            _context.SaveChanges();
            CargarProgramaInsumos();
        }

        private void NotificarColumnaSeleccionada(int colIndex)
        {
            _colRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvProgramaInsumos.Columns.Count)
            {
                var col = dgvProgramaInsumos.Columns[colIndex];
                if (col.Tag is ColumnaProgramaInsumos cfg)
                {
                    _colRibbon = cfg;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = cfg.Nombre,
                        NombreFuente = cfg.NombreFuente,
                        TamanoFuente = cfg.TamanoFuente,
                        Negrita = cfg.Negrita,
                        Cursiva = cfg.Cursiva,
                        Alineacion = cfg.Alineacion,
                        ColorFondo = cfg.ColorFondo,
                        ColorFuente = cfg.ColorFuente,
                        WrapTexto = cfg.WrapTexto,
                        AlineacionVertical = cfg.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }
    }
}
