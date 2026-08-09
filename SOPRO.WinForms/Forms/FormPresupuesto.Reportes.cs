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
    /// Generación de reportes (PDF/Excel) del presupuesto y columnas de reporte.
    /// </summary>
    public partial class FormPresupuesto
    {

        public void GenerarPdfPresupuesto()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnasBD = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");

                foreach (var cfg in columnasBD)
                    cfg.Visible = false;

                var colsEnReporte = dgvPresupuesto.Columns
                    .Cast<System.Windows.Forms.DataGridViewColumn>()
                    .Where(col => col.Visible)
                    .Where(col => col.Name != "colRelleno")
                    .Where(col =>
                    {
                        if (col.Name == "colNumero") return true;
                        if (col.Tag is not Core.Entities.ColumnaPersonalizada cd2) return false;
                        return !string.Equals(cd2.NombreInterno, "Tipo", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(col => col.DisplayIndex)
                    .ToList();

                int ordenReporte = 0;
                foreach (var col in colsEnReporte)
                {
                    string nombreInternoGrid = col.Name == "colNumero"
                        ? "Numero"
                        : ((Core.Entities.ColumnaPersonalizada)col.Tag).NombreInterno;

                    string nombreInternoReporte = ObtenerNombreInternoReporteDesdeGrid(nombreInternoGrid);
                    var cfg = columnasBD.FirstOrDefault(r => r.NombreInterno == nombreInternoReporte);
                    if (cfg == null)
                    {
                        cfg = CrearConfigColumnaReporteDesdeGrid(col, nombreInternoReporte);
                        cfg.ProyectoId = _proyecto.Id;
                        cfg.TipoReporte = "Presupuesto";
                        columnasBD.Add(cfg);
                    }

                    cfg.Visible = true;
                    cfg.Orden = ordenReporte++;
                    cfg.Ancho = col.Width;
                    cfg.Encabezado = col.HeaderText;
                    cfg.WrapTexto = (col.Tag as Core.Entities.ColumnaPersonalizada)?.WrapTexto == true
                        || col.DefaultCellStyle.WrapMode == DataGridViewTriState.True;
                    ActualizarConfigColumnaReporteDesdeGrid(cfg, col);
                }

                svc.GuardarColumnas(columnasBD);

                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show("El presupuesto no tiene conceptos.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = $"Presupuesto_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                decimal factorPU = CalcularFactorPU();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Presupuesto, lblTitulo.Text);
                var gen = new Services.GeneradorPdfPresupuesto(svc);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, columnasBD, dlg.FileName, factorPU, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte PDF generado:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte PDF:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GenerarPdfAPU()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                var conceptosConAPU = conceptos
                    .Where(c => !c.EsAgrupador && c.MatrizId.HasValue)
                    .ToList();

                if (!conceptosConAPU.Any())
                {
                    MessageBox.Show(
                        "No hay conceptos con APU vinculado en este presupuesto." +
                        "Vincula una Matriz APU a cada concepto desde la columna correspondiente.",
                        "Sin APU", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte APU PDF",
                    Filter = "PDF (*.pdf)|*.pdf",
                    FileName = "APU_" + _proyecto.Nombre + "_" + DateTime.Now.ToString("yyyyMMdd") + ".pdf",
                    DefaultExt = "pdf"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var estiloDescripcionApu = ObtenerEstiloDescripcionPresupuestoParaApu(svc);
                var gen = new Services.GeneradorPdfAPU(svc, _context);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, dlg.FileName, estiloDescripcionApu);
                Cursor = Cursors.Default;

                if (MessageBox.Show("APUs PDF generados:" + ruta + "¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar APUs PDF:" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        public void GenerarExcelPresupuesto()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);

                // Tomar las columnas visibles en el orden actual del grid
                // en lugar de las de la BD, para respetar lo que el usuario ve.
                // Se excluye intencionalmente la columna Tipo.
                var columnasBD = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");

                foreach (var cfg in columnasBD)
                    cfg.Visible = false;

                var colsEnReporte = dgvPresupuesto.Columns
                    .Cast<System.Windows.Forms.DataGridViewColumn>()
                    .Where(col => col.Visible)
                    .Where(col => col.Name != "colRelleno")
                    .Where(col =>
                    {
                        if (col.Name == "colNumero") return true;
                        if (col.Tag is not Core.Entities.ColumnaPersonalizada cd2) return false;
                        return !string.Equals(cd2.NombreInterno, "Tipo", StringComparison.OrdinalIgnoreCase);
                    })
                    .OrderBy(col => col.DisplayIndex)
                    .ToList();

                int ordenReporte = 0;
                foreach (var col in colsEnReporte)
                {
                    string nombreInternoGrid = col.Name == "colNumero"
                        ? "Numero"
                        : ((Core.Entities.ColumnaPersonalizada)col.Tag).NombreInterno;

                    string nombreInternoReporte = ObtenerNombreInternoReporteDesdeGrid(nombreInternoGrid);
                    var cfg = columnasBD.FirstOrDefault(r => r.NombreInterno == nombreInternoReporte);
                    if (cfg == null)
                    {
                        cfg = CrearConfigColumnaReporteDesdeGrid(col, nombreInternoReporte);
                        cfg.ProyectoId = _proyecto.Id;
                        cfg.TipoReporte = "Presupuesto";
                        columnasBD.Add(cfg);
                    }

                    cfg.Visible = true;
                    cfg.Orden = ordenReporte++;
                    cfg.Ancho = col.Width;
                    cfg.Encabezado = col.HeaderText;
                    cfg.WrapTexto = (col.Tag as Core.Entities.ColumnaPersonalizada)?.WrapTexto == true
                        || col.DefaultCellStyle.WrapMode == DataGridViewTriState.True;
                    ActualizarConfigColumnaReporteDesdeGrid(cfg, col);
                }

                svc.GuardarColumnas(columnasBD);

                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show("El presupuesto no tiene conceptos.", "Sin datos",
                        MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte Excel",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = $"Presupuesto_{_proyecto.Nombre}_{DateTime.Now:yyyyMMdd}.xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                decimal factorPU = CalcularFactorPU();
                var tituloCfg = new ConfiguracionTituloReporteService(_context).ObtenerOCrear(_proyecto.Id, ReportTitleModuleKeys.Presupuesto, lblTitulo.Text);
                var gen = new Services.GeneradorExcelPresupuesto(svc);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, columnasBD, dlg.FileName, factorPU, tituloCfg);
                Cursor = Cursors.Default;

                if (MessageBox.Show("Reporte generado:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar el reporte:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }


        private static string ObtenerNombreInternoReporteDesdeGrid(string nombreInternoGrid)
        {
            return nombreInternoGrid switch
            {
                "Importe" => "ImporteTotal",
                _ => nombreInternoGrid
            };
        }

        private static Core.Entities.ConfigColumnaReporte CrearConfigColumnaReporteDesdeGrid(DataGridViewColumn col, string nombreInternoReporte)
        {
            var sourceColumn = col.Tag as Core.Entities.ColumnaPersonalizada;
            var tipoDato = sourceColumn?.TipoDato ?? Core.Entities.TipoDatoColumna.Texto;
            var alineacion = sourceColumn?.Alineacion ?? Core.Entities.AlineacionColumna.Izquierda;

            var cfg = new Core.Entities.ConfigColumnaReporte
            {
                NombreInterno = nombreInternoReporte,
                Encabezado = col.HeaderText,
                Visible = col.Visible,
                Orden = col.DisplayIndex,
                Ancho = col.Width,
                EncFuente = "Segoe UI",
                EncTamaño = 9f,
                EncNegrita = true,
                EncAlineacion = "Centro",
                EncColorFondo = "#1565C0",
                EncColorTexto = "#FFFFFF",
                ConFuente = "Segoe UI",
                ConTamaño = 9f,
                ConNegrita = false,
                ConAlineacion = alineacion switch
                {
                    Core.Entities.AlineacionColumna.Centro => "Centro",
                    Core.Entities.AlineacionColumna.Derecha => "Derecha",
                    _ => tipoDato == Core.Entities.TipoDatoColumna.Moneda || tipoDato == Core.Entities.TipoDatoColumna.Numerico || tipoDato == Core.Entities.TipoDatoColumna.Porcentaje
                        ? "Derecha"
                        : "Izquierda"
                },
                ConColorFondo = "#FFFFFF",
                ConColorTexto = "#000000",
                WrapTexto = (sourceColumn?.WrapTexto ?? false) || col.DefaultCellStyle.WrapMode == DataGridViewTriState.True,
                FormatoNumero = ObtenerFormatoNumeroReporte(nombreInternoReporte, tipoDato)
            };

            ActualizarConfigColumnaReporteDesdeGrid(cfg, col);
            return cfg;
        }

        private static void ActualizarConfigColumnaReporteDesdeGrid(Core.Entities.ConfigColumnaReporte cfg, DataGridViewColumn col)
        {
            var sourceColumn = col.Tag as Core.Entities.ColumnaPersonalizada;
            var estiloColumna = col.DefaultCellStyle;
            var fuente = estiloColumna.Font;

            cfg.ConFuente = !string.IsNullOrWhiteSpace(sourceColumn?.NombreFuente)
                ? sourceColumn.NombreFuente
                : (!string.IsNullOrWhiteSpace(fuente?.Name) ? fuente.Name : "Segoe UI");
            cfg.ConTamaño = sourceColumn?.TamanoFuente > 0
                ? sourceColumn.TamanoFuente
                : (fuente?.Size ?? 9f);
            cfg.ConNegrita = sourceColumn?.Negrita ?? (fuente?.Bold ?? false);
            cfg.ConCursiva = sourceColumn?.Cursiva ?? (fuente?.Italic ?? false);
            cfg.ConColorFondo = !string.IsNullOrWhiteSpace(sourceColumn?.ColorFondo)
                ? sourceColumn.ColorFondo
                : ColorAHex(estiloColumna.BackColor.IsEmpty ? Color.White : estiloColumna.BackColor);
            cfg.ConColorTexto = !string.IsNullOrWhiteSpace(sourceColumn?.ColorFuente)
                ? sourceColumn.ColorFuente
                : ColorAHex(estiloColumna.ForeColor.IsEmpty ? Color.Black : estiloColumna.ForeColor);
        }


        private static string ColorAHex(Color color)
        {
            if (color.IsEmpty) color = Color.Black;
            return $"#{color.R:X2}{color.G:X2}{color.B:X2}";
        }

        private Core.Entities.ConfigColumnaReporte ObtenerEstiloDescripcionPresupuestoParaApu(Services.ReporteService svc)
        {
            var columnasReporte = svc.ObtenerOCrearColumnas(_proyecto.Id, "Presupuesto");
            var descripcionReporte = columnasReporte.FirstOrDefault(c => c.NombreInterno == "Descripcion");

            var columnaGridDescripcion = dgvPresupuesto.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(col => col.Tag is Core.Entities.ColumnaPersonalizada cp &&
                    string.Equals(cp.NombreInterno, "Descripcion", StringComparison.OrdinalIgnoreCase));

            if (descripcionReporte == null)
            {
                descripcionReporte = new Core.Entities.ConfigColumnaReporte
                {
                    NombreInterno = "Descripcion",
                    Encabezado = "Descripción",
                    ConFuente = "Segoe UI",
                    ConTamaño = 9f,
                    ConColorFondo = "#FFFFFF",
                    ConColorTexto = "#000000",
                    ConAlineacion = "Izquierda"
                };
            }

            if (columnaGridDescripcion != null)
                ActualizarConfigColumnaReporteDesdeGrid(descripcionReporte, columnaGridDescripcion);

            return descripcionReporte;
        }

        private static string ObtenerFormatoNumeroReporte(string nombreInternoReporte, Core.Entities.TipoDatoColumna tipoDato)
        {
            return nombreInternoReporte switch
            {
                "Cantidad" => "N3",
                "PrecioUnitario" or "ImporteTotal" or "Subtotal" or "IVA" or "Total" or "Indirectos" or "Financiamiento" or "Utilidad" => "N2",
                _ => tipoDato switch
                {
                    Core.Entities.TipoDatoColumna.Moneda => "N2",
                    Core.Entities.TipoDatoColumna.Numerico => "N3",
                    _ => string.Empty
                }
            };
        }

        public void GenerarExcelAPU()
        {
            if (!ValidarPresupuestoAntesDeContinuar(bloquear: true, titulo: "Validación de presupuesto"))
                return;

            try
            {
                var conceptos = _context.ConceptosPresupuesto
                    .Where(c => c.ProyectoId == _proyecto.Id && !c.EsAgrupador && c.MatrizId.HasValue)
                    .OrderBy(c => c.Orden)
                    .ToList();

                if (!conceptos.Any())
                {
                    MessageBox.Show(
                        "No hay conceptos con APU vinculado en este presupuesto.\n\n" +
                        "Vincula una Matriz APU a cada concepto desde la columna correspondiente.",
                        "Sin datos", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    return;
                }

                var svc = new Services.ReporteService(_context);
                var plantilla = svc.ObtenerOCrearPlantilla(_proyecto.Id);
                var columnas = svc.ObtenerOCrearColumnas(_proyecto.Id, "APU");

                using var dlg = new SaveFileDialog
                {
                    Title = "Guardar reporte APU",
                    Filter = "Excel (*.xlsx)|*.xlsx",
                    FileName = "APU_" + _proyecto.Nombre + "_" + DateTime.Now.ToString("yyyyMMdd") + ".xlsx",
                    DefaultExt = "xlsx"
                };
                if (dlg.ShowDialog() != DialogResult.OK) return;

                Cursor = Cursors.WaitCursor;
                var estiloDescripcionApu = ObtenerEstiloDescripcionPresupuestoParaApu(svc);
                var gen = new Services.GeneradorExcelAPU(svc, _context);
                var ruta = gen.Generar(_proyecto, conceptos, plantilla, columnas, dlg.FileName, estiloDescripcionApu);
                Cursor = Cursors.Default;

                if (MessageBox.Show("APUs generados:\n" + ruta + "\n\n¿Abrir ahora?",
                    "Listo", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    { FileName = ruta, UseShellExecute = true });
                }
            }
            catch (Exception ex)
            {
                Cursor = Cursors.Default;
                MessageBox.Show("Error al generar APUs:\n" + ex.Message, "Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        }

        private void btnColumnas_Click(object sender, EventArgs e)
        {
            // Guardar la columna actual para insertar nuevas columnas a su derecha
            int columnaActualIndex = dgvPresupuesto.CurrentCell?.ColumnIndex ?? -1;

            using var form = new FormColumnasPersonalizadas(_context, _proyecto.Id, columnaActualIndex);
            if (form.ShowDialog() == DialogResult.OK || form.CambiosRealizados)
            {
                try
                {
                    // Solo guardar el orden visual actual de columnas
                    foreach (DataGridViewColumn col in dgvPresupuesto.Columns)
                    {
                        if (col.Tag is ColumnaPersonalizada colDef)
                        {
                            var columnaDB = _context.ColumnasPersonalizadas.Find(colDef.Id);
                            if (columnaDB != null)
                                columnaDB.Orden = col.DisplayIndex;
                        }
                    }
                    _context.SaveChanges();
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Error al guardar orden de columnas:\n{ex.Message}",
                        "Error", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    return;
                }

                // Recargar presupuesto con nuevas columnas, preservando fila/scroll/celda cuando sea posible
                RecargarPresupuestoPreservandoEstado();
            }
        }
    }
}
