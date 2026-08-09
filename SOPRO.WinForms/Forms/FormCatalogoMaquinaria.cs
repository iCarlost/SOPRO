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
    public partial class FormCatalogoMaquinaria : Form, IGridFormato, IBusquedaGrid, IRecalculable, IConsolidacionInsumos
    {
        public static event EventHandler InsumosModificados;

        private readonly SOPROContext _context;
        private readonly Repository<Maquinaria> _repository;
        private readonly CatalogLoadService _catalogLoadService = new();
        private readonly int? _proyectoId;
        private Maquinaria _maquinariaSeleccionada;
        private List<ColumnaMaquinaria> _columnasConfig = new List<ColumnaMaquinaria>();
        private bool _cargandoColumnas = false;
        private readonly InsumoConsolidationService _consolidationService = new();

        public event EventHandler EstadoConsolidacionCambiado;
        private List<Maquinaria> _listaActual = new List<Maquinaria>();

        public DataGridView GridPrincipal => dgvMaquinaria;
        public DataGridView GridBusqueda => dgvMaquinaria;


        public event EventHandler ColumnaSeleccionadaCambiada;

        private ColumnaMaquinaria _colMaqRibbon;
        private ColumnaPersonalizada _columnaRibbon;
        public ColumnaPersonalizada ColumnaSeleccionada => _columnaRibbon;


        public bool ConsolidacionDisponible => _proyectoId.HasValue && dgvMaquinaria.SelectedRows.Count >= 2;
        public string NombreTipoConsolidacion => "Maquinaria / Equipo";







    }
}
