using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using SOPRO.Application.DTOs.Insumos;
using SOPRO.Application.Models;
using SOPRO.Application.Models.ExternalProjects;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Optimizaciones de renderizado y configuración inicial del formulario.
    /// </summary>
    public partial class FormSeleccionarInsumo
    {

        private void ApplyRenderOptimizations()
        {
            FormRenderHelper.OptimizeForGridRendering(this);
            EnableDoubleBuffer(dgvInsumos);
            EnableDoubleBuffer(panelTop);
            EnableDoubleBuffer(panelBottom);
            EnableDoubleBuffer(panelFiltroMO);
        }

        private static void EnableDoubleBuffer(Control control)
        {
            if (control == null) return;
            var prop = typeof(Control).GetProperty("DoubleBuffered", BindingFlags.Instance | BindingFlags.NonPublic);
            prop?.SetValue(control, true, null);
        }

        private void RunGridUpdate(Action action)
        {
            SuspendLayout();
            dgvInsumos.SuspendLayout();
            panelBottom.SuspendLayout();
            using (GridRedrawHelper.Suspend(dgvInsumos))
            {
                try { action(); }
                finally
                {
                    panelBottom.ResumeLayout();
                    dgvInsumos.ResumeLayout();
                    ResumeLayout(true);
                }
            }
        }

        private void ConfigurarFormulario()
        {
            Text = $"Seleccionar {ObtenerNombreTipo()}";
            lblTitulo.Text = $"SELECCIONAR {ObtenerNombreTipo().ToUpper()}";
            panelFiltroMO.Visible = (_tipoComponente == TipoComponenteMatriz.ManoDeObra);
        }

        private string ObtenerNombreTipo()
        {
            return _tipoComponente switch
            {
                TipoComponenteMatriz.Material => "Material",
                TipoComponenteMatriz.ManoDeObra => "Mano de Obra",
                TipoComponenteMatriz.Maquinaria => "Maquinaria",
                TipoComponenteMatriz.Auxiliar => "Básico",
                TipoComponenteMatriz.Herramienta => "Herramienta",
                _ => "Insumo"
            };
        }
    }
}
