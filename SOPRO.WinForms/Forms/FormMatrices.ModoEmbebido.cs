using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Configuración del modo embebido y recarga del catálogo.
    /// </summary>
    public partial class FormMatrices
    {

        private void ConfigurarModoEmbebido()
        {
            if (!_modoEmbebido) return;
            if (panelTop != null) { panelTop.Visible = false; panelTop.Height = 0; }
            if (btnCerrar != null) btnCerrar.Visible = false;
            if (dgvMatrices != null) { dgvMatrices.Dock = DockStyle.Fill; dgvMatrices.BringToFront(); }
        }

        public void RecargarMatrices() => CargarMatrices();

        public bool ConsolidacionDisponible
        {
            get
            {
                var seleccion = ObtenerMatricesSeleccionadas();
                if (seleccion.Count < 2) return false;
                var tipos = seleccion.Select(x => x.Tipo).Distinct().ToList();
                return tipos.Count == 1 && tipos[0] != TipoMatriz.APU;
            }
        }

        public string NombreTipoConsolidacion
        {
            get
            {
                var tipo = ObtenerMatricesSeleccionadas().Select(x => x.Tipo).Distinct().SingleOrDefault();
                return tipo switch
                {
                    TipoMatriz.Basico => "Matrices básicas",
                    TipoMatriz.Cuadrilla => "Cuadrillas",
                    _ => "Matrices"
                };
            }
        }
    }
}
