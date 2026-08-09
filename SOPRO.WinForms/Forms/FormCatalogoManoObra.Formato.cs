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
using SOPRO.Application.Models.Catalogs;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Formato y estilo del grid del catálogo de mano de obra.
    /// </summary>
    public partial class FormCatalogoManoObra
    {

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMORibbon == null) return;

            _colMORibbon.NombreFuente = fmt.NombreFuente;
            _colMORibbon.TamanoFuente = fmt.TamanoFuente;
            _colMORibbon.Negrita = fmt.Negrita;
            _colMORibbon.Cursiva = fmt.Cursiva;
            _colMORibbon.Alineacion = fmt.Alineacion;
            _colMORibbon.ColorFondo = fmt.ColorFondo;
            _colMORibbon.ColorFuente = fmt.ColorFuente;
            _colMORibbon.WrapTexto = fmt.WrapTexto;
            _colMORibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMORibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvManoObra.Columns)
            {
                if (col.Tag == _colMORibbon)
                {
                    AplicarEstiloDesdeColMO((DataGridViewTextBoxColumn)col, _colMORibbon);
                    break;
                }
            }
            dgvManoObra.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvManoObra.Columns)
            {
                if (col.Tag is not ColumnaManoObra colMO) continue;
                colMO.NombreFuente = fmt.NombreFuente;
                colMO.TamanoFuente = fmt.TamanoFuente;
                colMO.Negrita = fmt.Negrita;
                colMO.Cursiva = fmt.Cursiva;
                colMO.Alineacion = fmt.Alineacion;
                colMO.ColorFuente = fmt.ColorFuente;
                colMO.WrapTexto = fmt.WrapTexto;
                colMO.AlineacionVertical = fmt.AlineacionVertical;
                colMO.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMO((DataGridViewTextBoxColumn)col, colMO);
            }
            _context.SaveChanges();
            dgvManoObra.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMORibbon = null;
            _columnaRibbon = null;
            if (colIndex >= 0 && colIndex < dgvManoObra.Columns.Count)
            {
                var col = dgvManoObra.Columns[colIndex];
                if (col.Tag is ColumnaManoObra cm)
                {
                    _colMORibbon = cm;
                    _columnaRibbon = new ColumnaPersonalizada
                    {
                        Nombre = cm.Nombre,
                        NombreFuente = cm.NombreFuente,
                        TamanoFuente = cm.TamanoFuente,
                        Negrita = cm.Negrita,
                        Cursiva = cm.Cursiva,
                        Alineacion = cm.Alineacion,
                        ColorFondo = cm.ColorFondo,
                        ColorFuente = cm.ColorFuente,
                        WrapTexto = cm.WrapTexto,
                        AlineacionVertical = cm.AlineacionVertical,
                    };
                }
            }
            ColumnaSeleccionadaCambiada?.Invoke(this, EventArgs.Empty);
        }
    }
}
