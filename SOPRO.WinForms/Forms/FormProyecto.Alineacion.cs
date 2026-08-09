using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.UI.Controls;
using SOPRO.Application.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Helpers de alineación horizontal y vertical del grid activo.
    /// </summary>
    public partial class FormProyecto
    {

        private DataGridViewColumn ObtenerColumnaSeleccionadaGrid()
        {
            if (_formActivo?.GridPrincipal == null || _formActivo?.ColumnaSeleccionada == null) return null;

            var nombreColumna = _formActivo.ColumnaSeleccionada.Nombre;
            return _formActivo.GridPrincipal.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.HeaderText, nombreColumna, StringComparison.OrdinalIgnoreCase));
        }

        private void AplicarAlineacionVerticalSeleccionActual()
        {
            if (_formActivo?.GridPrincipal == null) return;

            var col = ObtenerColumnaSeleccionadaGrid();
            if (col == null) return;

            var verticalActual = ObtenerAlineacionVerticalBoton();
            var horizontalActual = ObtenerHorizontal(col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet
                ? DataGridViewContentAlignment.MiddleLeft
                : col.DefaultCellStyle.Alignment);

            col.DefaultCellStyle.Alignment = CombinarAlineacion(verticalActual, horizontalActual);

            if (col.DefaultCellStyle.WrapMode == DataGridViewTriState.True)
                AjustarAutoAlturaFilas(_formActivo.GridPrincipal);

            _formActivo.GridPrincipal.Invalidate();
        }

        private DataGridViewContentAlignment ObtenerAlineacionVerticalActual()
        {
            var col = ObtenerColumnaSeleccionadaGrid();
            if (col == null) return DataGridViewContentAlignment.MiddleLeft;

            var actual = col.DefaultCellStyle.Alignment == DataGridViewContentAlignment.NotSet
                ? DataGridViewContentAlignment.MiddleLeft
                : col.DefaultCellStyle.Alignment;

            return actual;
        }

        private DataGridViewContentAlignment ObtenerAlineacionVerticalBoton()
        {
            if (EstaBotonActivo(btnAlinAba)) return DataGridViewContentAlignment.BottomLeft;
            if (EstaBotonActivo(btnAlinJus)) return DataGridViewContentAlignment.TopLeft;
            return DataGridViewContentAlignment.MiddleLeft;
        }

        private void ActualizarBotonesAlinVertical(DataGridViewContentAlignment alin)
        {
            var vertical = ObtenerVertical(alin);

            AplicarTemaBotonRibbon(btnAlinJus, vertical == DataGridViewTriState.True);
            AplicarTemaBotonRibbon(btnAlinMed, vertical == DataGridViewTriState.NotSet);
            AplicarTemaBotonRibbon(btnAlinAba, vertical == DataGridViewTriState.False);
        }

        private DataGridViewTriState ObtenerVertical(DataGridViewContentAlignment alin)
        {
            return alin switch
            {
                DataGridViewContentAlignment.TopLeft or
                DataGridViewContentAlignment.TopCenter or
                DataGridViewContentAlignment.TopRight => DataGridViewTriState.True,
                DataGridViewContentAlignment.BottomLeft or
                DataGridViewContentAlignment.BottomCenter or
                DataGridViewContentAlignment.BottomRight => DataGridViewTriState.False,
                _ => DataGridViewTriState.NotSet
            };
        }

        private HorizontalAlignment ObtenerHorizontal(DataGridViewContentAlignment alin)
        {
            return alin switch
            {
                DataGridViewContentAlignment.TopCenter or
                DataGridViewContentAlignment.MiddleCenter or
                DataGridViewContentAlignment.BottomCenter => HorizontalAlignment.Center,
                DataGridViewContentAlignment.TopRight or
                DataGridViewContentAlignment.MiddleRight or
                DataGridViewContentAlignment.BottomRight => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            };
        }

        private DataGridViewContentAlignment CombinarAlineacion(DataGridViewContentAlignment verticalBase, HorizontalAlignment horizontal)
        {
            var esTop = verticalBase == DataGridViewContentAlignment.TopLeft ||
                        verticalBase == DataGridViewContentAlignment.TopCenter ||
                        verticalBase == DataGridViewContentAlignment.TopRight;
            var esBottom = verticalBase == DataGridViewContentAlignment.BottomLeft ||
                           verticalBase == DataGridViewContentAlignment.BottomCenter ||
                           verticalBase == DataGridViewContentAlignment.BottomRight;

            if (esTop)
            {
                return horizontal switch
                {
                    HorizontalAlignment.Center => DataGridViewContentAlignment.TopCenter,
                    HorizontalAlignment.Right => DataGridViewContentAlignment.TopRight,
                    _ => DataGridViewContentAlignment.TopLeft
                };
            }

            if (esBottom)
            {
                return horizontal switch
                {
                    HorizontalAlignment.Center => DataGridViewContentAlignment.BottomCenter,
                    HorizontalAlignment.Right => DataGridViewContentAlignment.BottomRight,
                    _ => DataGridViewContentAlignment.BottomLeft
                };
            }

            return horizontal switch
            {
                HorizontalAlignment.Center => DataGridViewContentAlignment.MiddleCenter,
                HorizontalAlignment.Right => DataGridViewContentAlignment.MiddleRight,
                _ => DataGridViewContentAlignment.MiddleLeft
            };
        }

        private void AjustarAutoAlturaFilas(DataGridView grid) => FormatoHelper.AjustarAutoAlturaFilas(grid);

        // Helpers ribbon
        private bool ObtenerWrapActual()
        {
            if (_formActivo?.GridPrincipal == null || _formActivo?.ColumnaSeleccionada == null) return false;

            var nombreColumna = _formActivo.ColumnaSeleccionada.Nombre;
            var col = _formActivo.GridPrincipal.Columns
                .Cast<DataGridViewColumn>()
                .FirstOrDefault(c => string.Equals(c.HeaderText, nombreColumna, StringComparison.OrdinalIgnoreCase));

            return col?.DefaultCellStyle.WrapMode == DataGridViewTriState.True;
        }

        private void ActualizarBotonesAlin(AlineacionColumna alin)
        {
            AplicarTemaBotonRibbon(btnAlinIzq, alin == AlineacionColumna.Izquierda);
            AplicarTemaBotonRibbon(btnAlinCen, alin == AlineacionColumna.Centro);
            AplicarTemaBotonRibbon(btnAlinDer, alin == AlineacionColumna.Derecha);
        }

        private AlineacionColumna ObtenerAlin()
        {
            if (EstaBotonActivo(btnAlinCen)) return AlineacionColumna.Centro;
            if (EstaBotonActivo(btnAlinDer)) return AlineacionColumna.Derecha;
            return AlineacionColumna.Izquierda;
        }

        private void ActualizarMuestraTexto()
        {
            AplicarTemaBotonColor(btnColorTexto, _colorMuestraTextoRibbon, "A");
            AplicarTemaBotonColor(btnColorFondo, _colorMuestraFondoRibbon, "■");
        }
    }
}
