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
    public partial class FormCatalogoMateriales : Form, IGridFormato, IBusquedaGrid, IRecalculable, IConsolidacionInsumos
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<Material> _repository;
        private readonly CatalogLoadService _catalogLoadService = new();
        private readonly int? _proyectoId;
        private Material _materialSeleccionado;
        private List<ColumnaMaterial> _columnasConfig = new List<ColumnaMaterial>();
        private bool _cargandoColumnas = false;
        private readonly InsumoConsolidationService _consolidationService = new();

        public event EventHandler EstadoConsolidacionCambiado;

        public DataGridView GridPrincipal => dgvMateriales;
        public DataGridView GridBusqueda => dgvMateriales;


        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaMaterial _colMatRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;


        public bool ConsolidacionDisponible => _proyectoId.HasValue && dgvMateriales.SelectedRows.Count >= 2;
        public string NombreTipoConsolidacion => "Materiales";







    }
}
