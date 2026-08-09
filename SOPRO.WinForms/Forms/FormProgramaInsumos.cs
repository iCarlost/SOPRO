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
    public partial class FormProgramaInsumos : Form, IGridFormato, IRecalculable, IBusquedaGrid
    {
        private readonly SOPROContext _context;
        private readonly Proyecto _proyecto;
        private readonly ProgramacionInsumosService _service = new();
        private readonly ProgramacionInsumosGanttService _ganttService = new();
        private bool _cargando;
        private bool _layoutPersistenceReady;
        private bool _programaReloadPending;
        private bool _visualRefreshPending;
        private bool _splitterTrackingSuspended;
        private ProgramaInsumosResultDto? _actual;
        private GanttTimelineControl? _ganttControl;

        private List<ColumnaProgramaInsumos> _columnasConfig = new();
        private bool _cargandoColumnas = false;
        private ColumnaProgramaInsumos? _colRibbon;
        private ColumnaPersonalizada? _columnaRibbon;

        public DataGridView GridPrincipal => dgvProgramaInsumos;
        public DataGridView GridBusqueda => dgvProgramaInsumos;
        public event EventHandler? ColumnaSeleccionadaCambiada;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon!;

        private enum VistaProgramaInsumos
        {
            Cantidades,
            Importes,
            Mixto
        }

        public FormProgramaInsumos(SOPROContext context, Proyecto proyecto)
        {
            _context = context;
            _proyecto = proyecto;
            InitializeComponent();
            ConfigurarFormulario();
            ConfigurarGrid();
            ConfigurarGantt();

            FormDatosProyecto.DecimalesActualizados += OnDecimalesActualizados;
            this.FormClosed += (s, e) => FormDatosProyecto.DecimalesActualizados -= OnDecimalesActualizados;
        }







        private DataGridViewCellStyle DecimalStyle(FontStyle style = FontStyle.Regular) => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F, style) };
        private DataGridViewCellStyle MoneyStyle(FontStyle style = FontStyle.Regular) => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleRight, Font = new Font("Segoe UI", 9F, style) };
        private DataGridViewCellStyle CenterStyle() => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter };
        private DataGridViewCellStyle DateStyle() => new DataGridViewCellStyle { Alignment = DataGridViewContentAlignment.MiddleCenter, Format = "dd/MM/yyyy" };
        private string FormatDecimal(decimal value) => value.ToString($"N{_proyecto.DecimalesCantidad}", CultureInfo.CurrentCulture);
        private string FormatMoney(decimal value) => value.ToString($"N{_proyecto.DecimalesImporte}", CultureInfo.CurrentCulture);



    }
}
