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
    /// Recalculo y configuración de la explosión de insumos.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        public void RecalcularTodo() => GenerarExplosion();
        
        /// <summary>
        /// Maneja el evento de cambio de configuración de decimales
        /// </summary>
        private void OnConfiguracionCambiada(object sender, EventArgs e)
        {
            if (IsDisposed) return;
            // Recargar proyecto con nuevos decimales y regenerar aunque el tab no esté activo
            _proyecto = _context.Proyectos.Find(_proyectoId);
            if (IsHandleCreated)
                BeginInvoke(new Action(() => { if (!IsDisposed) GenerarExplosion(); }));
            else
                GenerarExplosion();
        }
    }
}
