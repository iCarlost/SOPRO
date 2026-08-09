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
    public partial class FormExplosionInsumos : Form, IGridFormato, IBusquedaGrid, IRecalculable
    {
        private readonly SOPROContext _context;
        private readonly int _proyectoId;
        private Proyecto _proyecto;

        private Dictionary<int, DatosInsumo> _ultMateriales;
        private Dictionary<int, DatosInsumo> _ultManoObra;
        private Dictionary<int, DatosInsumo> _ultMaquinaria;
        private Dictionary<int, DatosInsumo> _ultHerramientas;
        private decimal _ultCostoDirectoTotal;
        private readonly ExplosionInsumosService _explosionService = new();
        private readonly ExplosionGridRenderService _gridRenderService = new();


        public System.Windows.Forms.DataGridView GridPrincipal => dgvExplosion;
        public System.Windows.Forms.DataGridView GridBusqueda => dgvExplosion;

        
        



        




    }
}
