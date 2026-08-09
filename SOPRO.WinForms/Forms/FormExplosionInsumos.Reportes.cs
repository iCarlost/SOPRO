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
    /// Reportes y formato de la explosión de insumos.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        public bool GenerarReporteExcel()
        {
            btnExportar_Click(this, EventArgs.Empty);
            return true;
        }
        public event EventHandler ColumnaSeleccionadaCambiada;

        // _columnaExpRibbon es la entidad real de BD; _columnaRibbon es el DTO para la interfaz
        private SOPRO.Core.Entities.ColumnaExplosion _columnaExpRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_columnaExpRibbon == null) return;

            _columnaExpRibbon.NombreFuente      = fmt.NombreFuente;
            _columnaExpRibbon.TamanoFuente      = fmt.TamanoFuente;
            _columnaExpRibbon.Negrita           = fmt.Negrita;
            _columnaExpRibbon.Cursiva           = fmt.Cursiva;
            _columnaExpRibbon.Alineacion        = fmt.Alineacion;
            _columnaExpRibbon.ColorFondo        = fmt.ColorFondo;
            _columnaExpRibbon.ColorFuente       = fmt.ColorFuente;
            _columnaExpRibbon.WrapTexto         = fmt.WrapTexto;
            _columnaExpRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _columnaExpRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvExplosion.Columns)
            {
                if (col.Tag == _columnaExpRibbon)
                {
                    AplicarEstiloDesdeColExp((DataGridViewTextBoxColumn)col, _columnaExpRibbon);
                    break;
                }
            }
            dgvExplosion.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvExplosion.Columns)
            {
                if (col.Tag is not SOPRO.Core.Entities.ColumnaExplosion colExp) continue;
                colExp.NombreFuente      = fmt.NombreFuente;
                colExp.TamanoFuente      = fmt.TamanoFuente;
                colExp.Negrita           = fmt.Negrita;
                colExp.Cursiva           = fmt.Cursiva;
                colExp.Alineacion        = fmt.Alineacion;
                colExp.ColorFuente       = fmt.ColorFuente;
                colExp.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColExp((DataGridViewTextBoxColumn)col, colExp);
            }
            _context.SaveChanges();
            dgvExplosion.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _columnaExpRibbon = null;
            _columnaRibbon    = null;
            if (colIndex >= 0 && colIndex < dgvExplosion.Columns.Count)
            {
                var col = dgvExplosion.Columns[colIndex];
                if (col.Tag is SOPRO.Core.Entities.ColumnaExplosion ce)
                {
                    _columnaExpRibbon = ce;
                    // DTO para el ribbon (solo campos de formato)
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre       = ce.Nombre,
                        NombreFuente = ce.NombreFuente,
                        TamanoFuente = ce.TamanoFuente,
                        Negrita      = ce.Negrita,
                        Cursiva      = ce.Cursiva,
                        Alineacion   = ce.Alineacion,
                        ColorFondo   = ce.ColorFondo,
                        ColorFuente  = ce.ColorFuente,
                        WrapTexto    = ce.WrapTexto,
                        AlineacionVertical = ce.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }

                public FormExplosionInsumos(SOPROContext context, int proyectoId)
        {
            _context = context;
            _proyectoId = proyectoId;
            InitializeComponent();
            
            // Cargar proyecto
            _proyecto = _context.Proyectos.Find(_proyectoId);
            new EditableReportTitleHelper(_context, panelTop, lblTitulo, () => _proyectoId, ReportTitleModuleKeys.ExplosionInsumos).Attach();
            if (_proyecto != null)
            {
                this.Text = $"Explosión de Insumos - {_proyecto.Nombre}";
                FormatoHelper.EstablecerProyecto(_proyecto);
            }
            
            // Suscribirse a cambios de configuración
            FormatoHelper.ConfiguracionCambiada += OnConfiguracionCambiada;
            // Ribbon: detectar click en encabezado de columna
            dgvExplosion.ColumnHeaderMouseClick += (s, e) => NotificarColumnaSeleccionada(e.ColumnIndex);
            dgvExplosion.ColumnWidthChanged += DgvExplosion_ColumnWidthChanged;

            ControlRenderHelper.HabilitarDobleBuffer(this);
            ControlRenderHelper.HabilitarDobleBuffer(dgvExplosion);
        }
    }
}
