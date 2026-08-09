using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Cierre del módulo y recálculo externo.
    /// </summary>
    public partial class FormFinanciamiento
    {

        private void btnCerrar_Click(object sender, EventArgs e) => Close();

        public void RecalcularTodo()
        {
            btnCalcular_Click(this, EventArgs.Empty);
        }
    }
}
