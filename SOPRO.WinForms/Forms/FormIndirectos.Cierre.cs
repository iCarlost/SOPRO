using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using SOPRO.Application.Services;
using SOPRO.WinForms.Undo;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Cierre del módulo y recálculo externo.
    /// </summary>
    public partial class FormIndirectos
    {

        private void btnCerrar_Click(object sender, EventArgs e)
        {
            Close();
        }

        public void RecalcularTodo()
        {
            CargarDatos();
        }
    }
}
