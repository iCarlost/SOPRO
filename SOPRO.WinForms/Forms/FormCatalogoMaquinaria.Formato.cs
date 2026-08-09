using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de maquinaria.
    /// </summary>
    public partial class FormCatalogoMaquinaria
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMaqRibbon == null) return;

            _colMaqRibbon.NombreFuente = fmt.NombreFuente;
            _colMaqRibbon.TamanoFuente = fmt.TamanoFuente;
            _colMaqRibbon.Negrita = fmt.Negrita;
            _colMaqRibbon.Cursiva = fmt.Cursiva;
            _colMaqRibbon.Alineacion = fmt.Alineacion;
            _colMaqRibbon.ColorFondo = fmt.ColorFondo;
            _colMaqRibbon.ColorFuente = fmt.ColorFuente;
            _colMaqRibbon.WrapTexto = fmt.WrapTexto;
            _colMaqRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMaqRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvMaquinaria.Columns)
            {
                if (col.Tag == _colMaqRibbon)
                {
                    AplicarEstiloDesdeColMaq((DataGridViewTextBoxColumn)col, _colMaqRibbon);
                    break;
                }
            }
            dgvMaquinaria.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMaquinaria.Columns)
            {
                if (col.Tag is not ColumnaMaquinaria colMaq) continue;
                colMaq.NombreFuente = fmt.NombreFuente;
                colMaq.TamanoFuente = fmt.TamanoFuente;
                colMaq.Negrita = fmt.Negrita;
                colMaq.Cursiva = fmt.Cursiva;
                colMaq.Alineacion = fmt.Alineacion;
                colMaq.ColorFuente = fmt.ColorFuente;
                colMaq.WrapTexto = fmt.WrapTexto;
                colMaq.AlineacionVertical = fmt.AlineacionVertical;
                colMaq.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMaq((DataGridViewTextBoxColumn)col, colMaq);
            }
            _context.SaveChanges();
            dgvMaquinaria.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMaqRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvMaquinaria.Columns.Count)
            {
                var col = dgvMaquinaria.Columns[colIndex];
                if (col.Tag is ColumnaMaquinaria cm)
                {
                    _colMaqRibbon = cm;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = cm.Nombre,
                        NombreFuente = cm.NombreFuente,
                        TamanoFuente = cm.TamanoFuente,
                        Negrita = cm.Negrita,
                        Cursiva = cm.Cursiva,
                        Alineacion = cm.Alineacion,
                        ColorFondo = cm.ColorFondo,
                        ColorFuente = cm.ColorFuente,
                        WrapTexto = cm.WrapTexto,
                        AlineacionVertical = cm.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public FormCatalogoMaquinaria(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMaquinaria.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Maquinaria>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoMaquinaria).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasMaquinariaHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            this.Load += (s, e) => CargarMaquinaria();

            dgvMaquinaria.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvMaquinaria.ColumnWidthChanged += DgvMaquinaria_ColumnWidthChanged;
        }
    }
}
