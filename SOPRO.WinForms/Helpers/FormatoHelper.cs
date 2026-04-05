using System;
using System.Globalization;
using System.Windows.Forms;
using SOPRO.Core.Entities;

namespace SOPRO.WinForms.Helpers
{
    /// <summary>
    /// Helper centralizado para formateo de números según configuración del proyecto.
    /// </summary>
    public static class FormatoHelper
    {
        private static Proyecto _proyectoActual;
        
        /// <summary>
        /// Evento que se dispara cuando se actualiza el proyecto y su configuración de decimales
        /// </summary>
        public static event EventHandler ConfiguracionCambiada;
        
        /// <summary>
        /// Establece el proyecto actual para usar su configuración de decimales.
        /// Llamar al abrir cualquier form que muestre datos del proyecto.
        /// </summary>
        public static void EstablecerProyecto(Proyecto proyecto)
        {
            _proyectoActual = proyecto;
            ConfiguracionCambiada?.Invoke(null, EventArgs.Empty);
        }
        
        // ══════════════════════════════════════════════════════════════
        // EXTENSION METHODS PARA DECIMAL
        // ══════════════════════════════════════════════════════════════
        
        /// <summary>
        /// Formatea una cantidad con los decimales configurados en el proyecto.
        /// Ejemplo: 123.456 → "123.46" (si DecimalesCantidad = 2)
        /// </summary>
        public static string ToStringCantidad(this decimal valor)
        {
            int decimales = _proyectoActual?.DecimalesCantidad ?? 2;
            return valor.ToString($"N{decimales}", CultureInfo.CurrentCulture);
        }
        
        /// <summary>
        /// Formatea un importe/precio con los decimales configurados.
        /// Ejemplo: 1234.5678 → "$1,234.57" (si DecimalesImporte = 2)
        /// </summary>
        public static string ToStringImporte(this decimal valor)
        {
            int decimales = _proyectoActual?.DecimalesImporte ?? 2;
            return valor.ToString($"C{decimales}", CultureInfo.CurrentCulture);
        }
        
        /// <summary>
        /// Formatea un porcentaje con los decimales configurados.
        /// Ejemplo: 3.14159 → "3.1416" (si DecimalesPorcentaje = 4)
        /// </summary>
        public static string ToStringPorcentaje(this decimal valor)
        {
            int decimales = _proyectoActual?.DecimalesPorcentaje ?? 4;
            return valor.ToString($"N{decimales}", CultureInfo.CurrentCulture);
        }
        
        // ══════════════════════════════════════════════════════════════
        // MÉTODOS DIRECTOS (sin extension)
        // ══════════════════════════════════════════════════════════════
        
        public static string FormatoCantidad(decimal valor)
        {
            return valor.ToStringCantidad();
        }
        
        public static string FormatoImporte(decimal valor)
        {
            return valor.ToStringImporte();
        }
        
        public static string FormatoPorcentaje(decimal valor)
        {
            return valor.ToStringPorcentaje();
        }
        
        /// <summary>
        /// Obtiene el formato string para NumericUpDown de cantidades.
        /// Ejemplo: si DecimalesCantidad = 3, devuelve "N3"
        /// </summary>
        public static int DecimalesCantidad => _proyectoActual?.DecimalesCantidad ?? 2;
        
        public static int DecimalesImporte => _proyectoActual?.DecimalesImporte ?? 2;
        
        public static int DecimalesPorcentaje => _proyectoActual?.DecimalesPorcentaje ?? 4;
        
        // ══════════════════════════════════════════════════════════════
        // TRUNCAR VALORES SEGÚN CONFIGURACIÓN
        // ══════════════════════════════════════════════════════════════
        // MÉTODOS NUMÉRICOS — ELIMINADOS DEL HELPER DE FORMATO
        // Usar MotorCalculoSopro para toda operación aritmética contable.
        // ══════════════════════════════════════════════════════════════

        /// <summary>
        /// ELIMINADO: dependía de estado global _proyectoActual.
        /// Usar: new MotorCalculoSopro(proyecto).RedondearCantidad(valor)
        /// </summary>
        [Obsolete("Usar new MotorCalculoSopro(proyecto).RedondearCantidad(valor). " +
                  "Este método depende de _proyectoActual (estado global mutable).", error: false)]
        public static decimal RedondearCantidad(this decimal valor)
            => throw new NotSupportedException(
                "FormatoHelper.RedondearCantidad está deshabilitado. " +
                "Usar: new MotorCalculoSopro(proyecto).RedondearCantidad(valor)");

        /// <summary>
        /// ELIMINADO: dependía de estado global _proyectoActual.
        /// Usar: new MotorCalculoSopro(proyecto).RedondearImporte(valor)
        /// </summary>
        [Obsolete("Usar new MotorCalculoSopro(proyecto).RedondearImporte(valor). " +
                  "Este método depende de _proyectoActual (estado global mutable).", error: false)]
        public static decimal RedondearImporte(this decimal valor)
            => throw new NotSupportedException(
                "FormatoHelper.RedondearImporte está deshabilitado. " +
                "Usar: new MotorCalculoSopro(proyecto).RedondearImporte(valor)");

        /// <summary>
        /// ELIMINADO: dependía de estado global.
        /// Usar: new MotorCalculoSopro(proyecto).Multiplicar(cantidad, precioUnitario)
        /// </summary>
        [Obsolete("Usar new MotorCalculoSopro(proyecto).Multiplicar(cantidad, precioUnitario). " +
                  "Este método depende del estado global _proyectoActual.", error: false)]
        public static decimal MultiplicarCantidadPorPU(decimal cantidad, decimal precioUnitario)
            => throw new NotSupportedException(
                "FormatoHelper.MultiplicarCantidadPorPU está deshabilitado. " +
                "Usar: new MotorCalculoSopro(proyecto).Multiplicar(cantidad, precioUnitario)");


        public static DataGridViewContentAlignment ConvertirAlineacionDgv(AlineacionColumna alineacion, int alineacionVertical = 1)
        {
            var horizontal = alineacion switch
            {
                AlineacionColumna.Centro => HorizontalAlignment.Center,
                AlineacionColumna.Derecha => HorizontalAlignment.Right,
                _ => HorizontalAlignment.Left
            };

            return alineacionVertical switch
            {
                0 => horizontal switch
                {
                    HorizontalAlignment.Center => DataGridViewContentAlignment.TopCenter,
                    HorizontalAlignment.Right => DataGridViewContentAlignment.TopRight,
                    _ => DataGridViewContentAlignment.TopLeft
                },
                2 => horizontal switch
                {
                    HorizontalAlignment.Center => DataGridViewContentAlignment.BottomCenter,
                    HorizontalAlignment.Right => DataGridViewContentAlignment.BottomRight,
                    _ => DataGridViewContentAlignment.BottomLeft
                },
                _ => horizontal switch
                {
                    HorizontalAlignment.Center => DataGridViewContentAlignment.MiddleCenter,
                    HorizontalAlignment.Right => DataGridViewContentAlignment.MiddleRight,
                    _ => DataGridViewContentAlignment.MiddleLeft
                }
            };
        }

        public static void AplicarWrapYAlineacionPersistidos(DataGridView grid)
        {
            if (grid == null) return;

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.Tag is not ColumnaPersonalizada cfg) continue;
                col.DefaultCellStyle.WrapMode = cfg.WrapTexto ? DataGridViewTriState.True : DataGridViewTriState.False;
                col.DefaultCellStyle.Alignment = ConvertirAlineacionDgv(cfg.Alineacion, cfg.AlineacionVertical);
            }
        }

        public static void AjustarAutoAlturaFilas(DataGridView grid)
        {
            if (grid == null) return;

            AplicarWrapYAlineacionPersistidos(grid);
            grid.SuspendLayout();
            try
            {
                bool hayWrap = false;
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (col.Visible && col.DefaultCellStyle.WrapMode == DataGridViewTriState.True)
                    {
                        hayWrap = true;
                        break;
                    }
                }

                grid.AutoSizeRowsMode = hayWrap
                    ? DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders
                    : DataGridViewAutoSizeRowsMode.None;

                if (hayWrap)
                    grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.AllCellsExceptHeaders);
                else
                {
                    foreach (DataGridViewRow row in grid.Rows)
                        row.Height = grid.RowTemplate.Height > 0 ? grid.RowTemplate.Height : 35;
                }
            }
            finally
            {
                grid.ResumeLayout();
            }
        }

        // CalcularCostoDirectoConPrecision() fue eliminado de FormatoHelper.
        // Usar: new MotorCalculoSopro(proyecto).SumarCostoDirecto(conceptos)
    }
}
