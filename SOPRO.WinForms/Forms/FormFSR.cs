using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Repositories;
using SOPRO.WinForms.Services;
using SOPRO.WinForms.Helpers;
using System.Drawing;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Windows.Forms;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    public partial class FormFSR : Form, IGridFormato
    {

        public DataGridView GridPrincipal => null;
        public ColumnaPersonalizada ColumnaSeleccionada => null;
#pragma warning disable CS0067 // Requerido por IGridFormato; FSR no expone grid formateable.
        public event EventHandler ColumnaSeleccionadaCambiada;
#pragma warning restore CS0067











    }
}
