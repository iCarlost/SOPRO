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
    /// Reportes y formato del catálogo de matrices.
    /// </summary>
    public partial class FormMatrices
    {

        public bool GenerarReporteExcel()
        {
            ExportarCatalogoMatrices();
            return true;
        }

        public void AplicarFormato(ColumnaPersonalizada fmt)
        {
            if (_colMatRibbon == null) return;

            _colMatRibbon.NombreFuente = fmt.NombreFuente;
            _colMatRibbon.TamanoFuente = fmt.TamanoFuente;
            _colMatRibbon.Negrita = fmt.Negrita;
            _colMatRibbon.Cursiva = fmt.Cursiva;
            _colMatRibbon.Alineacion = fmt.Alineacion;
            _colMatRibbon.ColorFondo = fmt.ColorFondo;
            _colMatRibbon.ColorFuente = fmt.ColorFuente;
            _colMatRibbon.WrapTexto = fmt.WrapTexto;
            _colMatRibbon.AlineacionVertical = fmt.AlineacionVertical;
            _colMatRibbon.FechaModificacion = DateTime.Now;
            _context.SaveChanges();

            foreach (DataGridViewColumn col in dgvMatrices.Columns)
            {
                if (col.Tag == _colMatRibbon && col is DataGridViewTextBoxColumn textCol)
                {
                    AplicarEstiloDesdeColMat(textCol, _colMatRibbon);
                    break;
                }
            }
            dgvMatrices.Invalidate();
        }

        public void AplicarFormatoGlobal(ColumnaPersonalizada fmt)
        {
            foreach (DataGridViewColumn col in dgvMatrices.Columns)
            {
                if (col.Tag is not ColumnaMatriz cm || col is not DataGridViewTextBoxColumn textCol) continue;
                cm.NombreFuente = fmt.NombreFuente;
                cm.TamanoFuente = fmt.TamanoFuente;
                cm.Negrita = fmt.Negrita;
                cm.Cursiva = fmt.Cursiva;
                cm.Alineacion = fmt.Alineacion;
                cm.ColorFuente = fmt.ColorFuente;
                cm.WrapTexto = fmt.WrapTexto;
                cm.AlineacionVertical = fmt.AlineacionVertical;
                cm.FechaModificacion = DateTime.Now;
                AplicarEstiloDesdeColMat(textCol, cm);
            }
            _context.SaveChanges();
            dgvMatrices.Invalidate();
        }

        public void NotificarColumnaSeleccionada(int colIndex)
        {
            _colMatRibbon = null;
            _columnaRibbon = null;

            if (colIndex >= 0 && colIndex < dgvMatrices.Columns.Count)
            {
                var col = dgvMatrices.Columns[colIndex];
                if (col.Tag is ColumnaMatriz cm)
                {
                    _colMatRibbon = cm;
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
