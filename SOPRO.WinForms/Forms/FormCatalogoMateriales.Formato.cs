using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de materiales.
    /// </summary>
    public partial class FormCatalogoMateriales
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMatRibbon == null) return;

            _colMatRibbon.NombreFuente = fmt.NombreFuente;
            _colMatRibbon.TamanoFuente = fmt.TamanoFuente;
            _colMatRibbon.Negrita = fmt.Negrita;
            _colMatRibbon.Cursiva = fmt.Cursiva;
            _colMatRibbon.Alineacion = fmt.Alineacion;
            _colMatRibbon.ColorFondo = fmt.ColorFondo;
            _colMatRibbon.ColorFuente = fmt.ColorFuente;
            _colMatRibbon.WrapTexto = fmt.WrapTexto;
            _colMatRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMatRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag == _colMatRibbon)
                {
                    AplicarEstiloDesdeColMat((DataGridViewTextBoxColumn)col, _colMatRibbon);
                    break;
                }
            }
            dgvMateriales.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag is not ColumnaMaterial colMat) continue;
                colMat.NombreFuente = fmt.NombreFuente;
                colMat.TamanoFuente = fmt.TamanoFuente;
                colMat.Negrita = fmt.Negrita;
                colMat.Cursiva = fmt.Cursiva;
                colMat.Alineacion = fmt.Alineacion;
                colMat.ColorFuente = fmt.ColorFuente;
                colMat.WrapTexto = fmt.WrapTexto;
                colMat.AlineacionVertical = fmt.AlineacionVertical;
                colMat.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMat((DataGridViewTextBoxColumn)col, colMat);
            }
            _context.SaveChanges();
            dgvMateriales.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMatRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvMateriales.Columns.Count)
            {
                var col = dgvMateriales.Columns[colIndex];
                if (col.Tag is ColumnaMaterial cm)
                {
                    _colMatRibbon = cm;
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
        public FormCatalogoMateriales(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            FormRenderHelper.OptimizeForGridRendering(this);
            btnExportarExcel.Visible = false;
            btnImportarExcel.Visible = false;
            dgvMateriales.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Material>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoMateriales).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasMaterialHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            this.Load += (s, e) => CargarMateriales();

            dgvMateriales.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvMateriales.ColumnWidthChanged += DgvMateriales_ColumnWidthChanged;
        }
    }
}
