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
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
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

            CatalogColumnLayoutService.GuardarFormatoColumna(_context, _colMatRibbon.Id, fmt);

            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag == _colMatRibbon)
                {
                    AplicarEstiloDesdeColumna((DataGridViewTextBoxColumn)col, fmt);
                    break;
                }
            }
            dgvMateriales.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMateriales.Columns)
            {
                if (col.Tag is not ColumnaMaterial) continue;

                AplicarEstiloDesdeColumna((DataGridViewTextBoxColumn)col, fmt);
            }
            if (_proyectoId.HasValue)
                CatalogColumnLayoutService.GuardarFormatoGlobal(_context, _proyectoId.Value, fmt);
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
            _sessionInfo = LegacySessionBridge.FromLegacy(_context, proyectoId);
            _proyectoId = proyectoId;

            // N4: la sesión es datos puros (no posee contexto): no hay Dispose.
            _listMaterials = new ListMaterials(_factory);
            _deleteMaterial = new DeleteMaterial(_factory);
            _previewMaterialDeletion = new PreviewMaterialDeletion(_factory);

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
