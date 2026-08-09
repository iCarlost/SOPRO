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
    public partial class FormCatalogoHerramientas : Form, IGridFormato, IBusquedaGrid, IRecalculable, IConsolidacionInsumos
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<Herramienta> _repository;
        private readonly int? _proyectoId;
        private Herramienta _herramientaSeleccionada;
        private List<ColumnaHerramienta> _columnasConfig = new List<ColumnaHerramienta>();
        private bool _cargandoColumnas = false;
        private readonly InsumoConsolidationService _consolidationService = new();

        public event EventHandler EstadoConsolidacionCambiado;

        public DataGridView GridPrincipal => dgvHerramientas;
        public DataGridView GridBusqueda => dgvHerramientas;


        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaHerramienta _colHerRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;


        public bool ConsolidacionDisponible => _proyectoId.HasValue && dgvHerramientas.SelectedRows.Count >= 2;
        public string NombreTipoConsolidacion => "Herramientas";







    }
}
