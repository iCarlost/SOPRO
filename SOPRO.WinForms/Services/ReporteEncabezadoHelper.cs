using ClosedXML.Excel;
using SOPRO.Core.Entities;
using System.IO;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Helper compartido para escribir el encabezado estándar SOPRO en cualquier reporte Excel.
    /// Usa la misma lógica que GeneradorExcelExplosion.EscribirEncabezado.
    /// </summary>
    public static class ReporteEncabezadoHelper
    {
        /// <summary>
        /// Escribe el encabezado de 3 zonas (izq/centro/der) en la fila indicada.
        /// Devuelve la siguiente fila disponible (ya dejando la línea separadora).
        /// </summary>
        public static int EscribirEncabezado(
            IXLWorksheet ws,
            PlantillaReporte p,
            Proyecto proyecto,
            int numCols,
            int fila,
            ReporteService svc)
        {
            int c1 = 1;
            int c2 = numCols / 3 + 1;
            int c3 = numCols * 2 / 3 + 1;

            EscribirZona(ws, fila, c1, c2 - 1,
                p.EncabezadoIzqTipo, p.EncabezadoIzqContenido,
                p.EncabezadoIzqFuente, p.EncabezadoIzqTamaño,
                p.EncabezadoIzqNegrita, p.EncabezadoIzqCursiva,
                p.EncabezadoIzqAlineacion, proyecto, p, svc);

            EscribirZona(ws, fila, c2, c3 - 1,
                p.EncabezadoCenTipo, p.EncabezadoCenContenido,
                p.EncabezadoCenFuente, p.EncabezadoCenTamaño,
                p.EncabezadoCenNegrita, p.EncabezadoCenCursiva,
                p.EncabezadoCenAlineacion, proyecto, p, svc);

            EscribirZona(ws, fila, c3, numCols,
                p.EncabezadoDerTipo, p.EncabezadoDerContenido,
                p.EncabezadoDerFuente, p.EncabezadoDerTamaño,
                p.EncabezadoDerNegrita, p.EncabezadoDerCursiva,
                p.EncabezadoDerAlineacion, proyecto, p, svc);

            ws.Row(fila).Height = p.EncabezadoAltura * 0.75;
            fila++;

            // Línea separadora azul igual que en explosión
            ws.Range(fila, 1, fila, numCols).Style.Border.TopBorder      = XLBorderStyleValues.Medium;
            ws.Range(fila, 1, fila, numCols).Style.Border.TopBorderColor = XLColor.FromHtml("#1565C0");

            return fila + 1;
        }



        /// <summary>
        /// Configura las filas superiores que Excel debe repetir al imprimir.
        /// Se usa para repetir el encabezado institucional y, cuando aplique,
        /// también la fila de títulos de columnas.
        /// </summary>
        public static void ConfigurarFilasRepetidas(IXLWorksheet ws, int filaDesde, int filaHasta)
        {
            if (ws == null) return;
            if (filaDesde <= 0 || filaHasta <= 0 || filaHasta < filaDesde) return;

            ws.PageSetup.SetRowsToRepeatAtTop(filaDesde, filaHasta);
        }

        private static void EscribirZona(
            IXLWorksheet ws, int fila, int colIni, int colFin,
            string tipo, string contenido,
            string fuente, float tamaño, bool negrita, bool cursiva,
            string alineacion, Proyecto proyecto, PlantillaReporte plantilla,
            ReporteService svc)
        {
            if (colIni > colFin) return;
            var rango = ws.Range(fila, colIni, fila, colFin);
            rango.Merge();

            if (tipo == "Imagen" && File.Exists(contenido))
            {
                try { ws.AddPicture(contenido).MoveTo(ws.Cell(fila, colIni)).WithSize(120, 50); }
                catch { }
            }
            else
            {
                rango.FirstCell().Value = svc.ResolverCampos(contenido, proyecto, plantilla);
            }

            var est = rango.Style;
            est.Font.FontName        = fuente;
            est.Font.FontSize        = tamaño;
            est.Font.Bold            = negrita;
            est.Font.Italic          = cursiva;
            est.Alignment.WrapText   = true;
            est.Alignment.Vertical   = XLAlignmentVerticalValues.Center;
            est.Alignment.Horizontal = alineacion switch
            {
                "Centro"  => XLAlignmentHorizontalValues.Center,
                "Derecha" => XLAlignmentHorizontalValues.Right,
                _         => XLAlignmentHorizontalValues.Left,
            };
        }
    }
}
