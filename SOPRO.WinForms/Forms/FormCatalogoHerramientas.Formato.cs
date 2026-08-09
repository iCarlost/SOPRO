using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.Data.Repositories;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using SOPRO.WinForms.Services;
using ClosedXML.Excel;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de herramientas.
    /// </summary>
    public partial class FormCatalogoHerramientas
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colHerRibbon == null) return;

            _colHerRibbon.NombreFuente = fmt.NombreFuente;
            _colHerRibbon.TamanoFuente = fmt.TamanoFuente;
            _colHerRibbon.Negrita = fmt.Negrita;
            _colHerRibbon.Cursiva = fmt.Cursiva;
            _colHerRibbon.Alineacion = fmt.Alineacion;
            _colHerRibbon.ColorFondo = fmt.ColorFondo;
            _colHerRibbon.ColorFuente = fmt.ColorFuente;
            _colHerRibbon.WrapTexto = fmt.WrapTexto;
            _colHerRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colHerRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvHerramientas.Columns)
            {
                if (col.Tag == _colHerRibbon)
                {
                    AplicarEstiloDesdeColHer((DataGridViewTextBoxColumn)col, _colHerRibbon);
                    break;
                }
            }
            dgvHerramientas.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvHerramientas.Columns)
            {
                if (col.Tag is not ColumnaHerramienta colHer) continue;
                colHer.NombreFuente = fmt.NombreFuente;
                colHer.TamanoFuente = fmt.TamanoFuente;
                colHer.Negrita = fmt.Negrita;
                colHer.Cursiva = fmt.Cursiva;
                colHer.Alineacion = fmt.Alineacion;
                colHer.ColorFuente = fmt.ColorFuente;
                colHer.WrapTexto = fmt.WrapTexto;
                colHer.AlineacionVertical = fmt.AlineacionVertical;
                colHer.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColHer((DataGridViewTextBoxColumn)col, colHer);
            }
            _context.SaveChanges();
            dgvHerramientas.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colHerRibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvHerramientas.Columns.Count)
            {
                var col = dgvHerramientas.Columns[colIndex];
                if (col.Tag is ColumnaHerramienta ch)
                {
                    _colHerRibbon = ch;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = ch.Nombre,
                        NombreFuente = ch.NombreFuente,
                        TamanoFuente = ch.TamanoFuente,
                        Negrita = ch.Negrita,
                        Cursiva = ch.Cursiva,
                        Alineacion = ch.Alineacion,
                        ColorFondo = ch.ColorFondo,
                        ColorFuente = ch.ColorFuente,
                        WrapTexto = ch.WrapTexto,
                        AlineacionVertical = ch.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

        // ── Constructor ───────────────────────────────────────────────────────
        public FormCatalogoHerramientas(SOPROContext context, int? proyectoId = null)
        {
            InitializeComponent();
            dgvHerramientas.AplicarEstiloSOPRO();
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _repository = new Repository<Herramienta>(_context);
            _proyectoId = proyectoId;

            if (_proyectoId.HasValue)
                new EditableReportTitleHelper(_context, panelHeader, lblTitulo, () => _proyectoId ?? 0, ReportTitleModuleKeys.CatalogoHerramientas).Attach();

            if (_proyectoId.HasValue)
                _columnasConfig = ColumnasHerramientaHelper.ObtenerColumnas(_context, _proyectoId.Value);

            ConfigurarGrid();
            this.Load += (s, e) => CargarHerramientas();

            dgvHerramientas.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvHerramientas.ColumnWidthChanged += DgvHerramientas_ColumnWidthChanged;
        }
    }
}
