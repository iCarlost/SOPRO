using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el "Catálogo de Auxiliares" (Básicos y/o Cuadrillas) en formato OPUS.
    /// Una sola hoja con todas las matrices en secuencia vertical.
    /// El título cambia según el filtro activo (Todos / APU / Básico / Cuadrilla).
    /// </summary>
    public class GeneradorExcelCatalogoMatrices
    {
        private readonly ReporteService _svc;
        private readonly SOPROContext   _ctx;

        // ── Colores ───────────────────────────────────────────────────────────
        private const string ColorEncabezadoMatriz = "#33334C"; // azul oscuro SOPRO
        private const string ColorFilaMatriz        = "#E8EAF6"; // lavanda claro (fila de la matriz compuesta)
        private const string ColorEncabezadoCols    = "#4A4A6A"; // encabezado de columnas
        private const string ColorSuma              = "#E3F2FD"; // azul muy claro para fila Suma
        private const string ColorAlt               = "#F5F5F5"; // alternado

        public GeneradorExcelCatalogoMatrices(ReporteService svc, SOPROContext ctx)
        {
            _svc = svc;
            _ctx = ctx;
        }

        public string Generar(
            Proyecto proyecto,
            List<Matriz> matrices,
            PlantillaReporte plantilla,
            string filtroTitulo,   // "Todos" | "APU" | "Básicos" | "Cuadrillas"
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (rutaDestino == null)
            {
                var carpeta = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                    "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"CatalogoMatrices_{Sanitizar(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.xlsx");
            }

            // Cargar componentes con todas las navegaciones
            var ids = matrices.Select(m => m.Id).ToList();
            var matricesCargadas = _ctx.Matrices
                .Include(m => m.Componentes.OrderBy(c => c.Orden))
                    .ThenInclude(c => c.Material)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes)
                    .ThenInclude(c => c.Auxiliar)
                .Where(m => ids.Contains(m.Id))
                .OrderBy(m => m.Clave)
                .ToList();

            using var wb = new XLWorkbook();
            var ws = wb.Worksheets.Add("Catálogo");

            // Orientación carta horizontal, igual que OPUS
            ws.PageSetup.PageOrientation = XLPageOrientation.Landscape;
            ws.PageSetup.PaperSize       = XLPaperSize.LetterPaper;
            ws.PageSetup.FitToPages(1, 0);
            ws.PageSetup.Margins.Left    = 0.5;
            ws.PageSetup.Margins.Right   = 0.5;
            ws.PageSetup.Margins.Top     = 0.75;
            ws.PageSetup.Margins.Bottom  = 0.75;

            // Anchos de columna: A=Prefijo, B=Clave, C=Descripción(fill), D=Unidad, E=Cantidad, F=C.U., G=Total
            ws.Column(1).Width =  4;   // Prefijo (+, M, H, etc.)
            ws.Column(2).Width = 16;   // Clave
            ws.Column(3).Width = 46;   // Descripción
            ws.Column(4).Width =  8;   // Unidad
            ws.Column(5).Width = 12;   // Cantidad
            ws.Column(6).Width = 14;   // Costo Unitario
            ws.Column(7).Width = 14;   // Total
            int numCols = 7;

            int fila = 1;

            // ── Encabezado estándar SOPRO ─────────────────────────────────────
            fila = ReporteEncabezadoHelper.EscribirEncabezado(ws, plantilla, proyecto, numCols, fila, _svc);

            // ── Título del catálogo ───────────────────────────────────────────
            string titulo = filtroTitulo switch
            {
                "APU"        => ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "CATÁLOGO DE MATRICES") + " (APU)",
                "Básicos"    => ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "CATÁLOGO DE MATRICES") + " (BÁSICOS)",
                "Cuadrillas" => ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "CATÁLOGO DE MATRICES") + " (CUADRILLAS)",
                _            => ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "CATÁLOGO DE MATRICES")
            };

            var rngTitulo = ws.Range(fila, 1, fila, numCols);
            ReportTitleStyleHelper.ApplyToClosedXmlTitle(rngTitulo, tituloCfg, titulo, ColorEncabezadoMatriz);
            ws.Row(fila).Height = 22;
            fila++;

            // ── Encabezado de columnas ────────────────────────────────────────
            EscribirEncabezadoColumnas(ws, fila, numCols);
            fila++;

            // ── Matrices ──────────────────────────────────────────────────────
            foreach (var m in matricesCargadas)
            {
                fila = EscribirMatriz(ws, m, fila, numCols);
            }

            // Borde exterior de toda la tabla
            if (fila > 4)
                ws.Range(3, 1, fila - 1, numCols).Style.Border.OutsideBorder = XLBorderStyleValues.Medium;

            wb.SaveAs(rutaDestino);
            return rutaDestino;
        }

        // ── Encabezado de columnas ────────────────────────────────────────────
        private void EscribirEncabezadoColumnas(IXLWorksheet ws, int fila, int numCols)
        {
            string[] headers = { "", "Clave", "Descripción", "Unidad", "Cantidad", "Costo Unitario", "Total" };
            for (int c = 1; c <= numCols; c++)
            {
                var cell = ws.Cell(fila, c);
                cell.Value = headers[c - 1];
                cell.Style.Font.Bold            = true;
                cell.Style.Font.FontSize        = 9;
                cell.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorEncabezadoCols);
                cell.Style.Font.FontColor       = XLColor.White;
                cell.Style.Alignment.Horizontal = c >= 5
                    ? XLAlignmentHorizontalValues.Right
                    : c == 4 ? XLAlignmentHorizontalValues.Center
                    : XLAlignmentHorizontalValues.Left;
                cell.Style.Border.BottomBorder      = XLBorderStyleValues.Medium;
                cell.Style.Border.BottomBorderColor = XLColor.FromHtml("#1565C0");
            }
            ws.Row(fila).Height = 18;
        }

        // ── Una matriz completa ───────────────────────────────────────────────
        private int EscribirMatriz(IXLWorksheet ws, Matriz m, int fila, int numCols)
        {
            // ── Fila encabezado de la matriz (el "compuesto") ─────────────────
            // Col A: "+"
            // Col B: Clave
            // Col C-D merged: Descripción   Unidad al final (col D sola)
            ws.Cell(fila, 1).Value = "+";
            ws.Cell(fila, 1).Style.Font.Bold = true;
            ws.Cell(fila, 1).Style.Font.FontSize = 9;

            ws.Cell(fila, 2).Value = m.Clave ?? "";
            ws.Cell(fila, 2).Style.Font.Bold    = true;
            ws.Cell(fila, 2).Style.Font.FontSize = 9;

            // Descripción ocupa col C (puede ser larga)
            ws.Cell(fila, 3).Value = m.Descripcion ?? "";
            ws.Cell(fila, 3).Style.Font.Bold      = true;
            ws.Cell(fila, 3).Style.Font.FontSize  = 9;
            ws.Cell(fila, 3).Style.Alignment.WrapText = true;

            ws.Cell(fila, 4).Value = m.Unidad ?? "";
            ws.Cell(fila, 4).Style.Font.Bold    = true;
            ws.Cell(fila, 4).Style.Font.FontSize = 9;
            ws.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // Cols E-G vacías en la fila de la matriz
            var rngHeader = ws.Range(fila, 1, fila, numCols);
            rngHeader.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorFilaMatriz);
            rngHeader.Style.Border.BottomBorder  = XLBorderStyleValues.Thin;

            ws.Row(fila).Height = 18;
            fila++;

            // ── Componentes ───────────────────────────────────────────────────
            // Calcular totalMO para los %MO
            decimal totalMO = 0;
            foreach (var comp in m.Componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null
                    && !comp.ManoDeObra.EsPorcentajeMO)
                    totalMO += comp.Cantidad * comp.ManoDeObra.SalarioReal;
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar
                    && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                    totalMO += comp.Cantidad * comp.Auxiliar.CostoDirecto;
            }

            bool alt = false;
            foreach (var comp in m.Componentes.OrderBy(c => c.Orden))
            {
                fila = EscribirComponente(ws, comp, fila, totalMO, alt);
                alt = !alt;
            }

            // ── Fila Suma ─────────────────────────────────────────────────────
            var rngSuma = ws.Range(fila, 1, fila, numCols - 1);
            rngSuma.Merge();
            rngSuma.Value = "Suma";
            rngSuma.Style.Font.Bold      = true;
            rngSuma.Style.Font.FontSize  = 9;
            rngSuma.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            rngSuma.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSuma);

            ws.Cell(fila, numCols).Value = m.CostoDirecto;
            ws.Cell(fila, numCols).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(fila, numCols).Style.Font.Bold    = true;
            ws.Cell(fila, numCols).Style.Font.FontSize = 9;
            ws.Cell(fila, numCols).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;
            ws.Cell(fila, numCols).Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(ColorSuma);

            ws.Range(fila, 1, fila, numCols).Style.Border.TopBorder    = XLBorderStyleValues.Thin;
            ws.Range(fila, 1, fila, numCols).Style.Border.BottomBorder = XLBorderStyleValues.Medium;
            ws.Row(fila).Height = 16;
            fila++;

            // Espacio entre matrices
            ws.Row(fila).Height = 6;
            fila++;

            return fila;
        }

        // ── Un componente ─────────────────────────────────────────────────────
        private int EscribirComponente(IXLWorksheet ws, ComponenteMatriz comp, int fila,
            decimal totalMO, bool alt)
        {
            string bgColor = alt ? ColorAlt : "#FFFFFF";

            // Prefijo de tipo (columna A)
            string prefijo = comp.TipoComponente switch
            {
                TipoComponenteMatriz.Material    => "M",
                TipoComponenteMatriz.Maquinaria  => "H",
                TipoComponenteMatriz.Herramienta => "H",
                TipoComponenteMatriz.Auxiliar    => "+",
                TipoComponenteMatriz.ManoDeObra  => "",
                _ => ""
            };

            // Datos del insumo
            var (clave, descripcion, unidad, costoUnitario) = ObtenerDatosInsumo(comp);

            // Calcular importe real (incluyendo %MO)
            decimal importe;
            if ((comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra?.EsPorcentajeMO == true) ||
                (comp.TipoComponente == TipoComponenteMatriz.Herramienta && comp.Herramienta?.EsPorcentajeMO == true))
            {
                importe      = totalMO * comp.Cantidad;
                costoUnitario = totalMO; // mostrar el total MO como base
            }
            else
            {
                importe = comp.Cantidad * costoUnitario;
            }

            // Escribir celdas
            ws.Cell(fila, 1).Value = prefijo;
            ws.Cell(fila, 1).Style.Font.Bold     = prefijo == "+";
            ws.Cell(fila, 1).Style.Font.FontSize = 9;
            ws.Cell(fila, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(fila, 2).Value = clave;
            ws.Cell(fila, 2).Style.Font.FontSize = 9;

            ws.Cell(fila, 3).Value = descripcion;
            ws.Cell(fila, 3).Style.Font.FontSize  = 9;
            ws.Cell(fila, 3).Style.Alignment.WrapText = true;

            ws.Cell(fila, 4).Value = unidad;
            ws.Cell(fila, 4).Style.Font.FontSize  = 9;
            ws.Cell(fila, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(fila, 5).Value = comp.Cantidad;
            ws.Cell(fila, 5).Style.NumberFormat.Format = "0.00000";
            ws.Cell(fila, 5).Style.Font.FontSize  = 9;
            ws.Cell(fila, 5).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(fila, 6).Value = costoUnitario;
            ws.Cell(fila, 6).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(fila, 6).Style.Font.FontSize  = 9;
            ws.Cell(fila, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            ws.Cell(fila, 7).Value = importe;
            ws.Cell(fila, 7).Style.NumberFormat.Format = "#,##0.00";
            ws.Cell(fila, 7).Style.Font.FontSize  = 9;
            ws.Cell(fila, 7).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Right;

            // Estilo de fila
            var rng = ws.Range(fila, 1, fila, 7);
            rng.Style.Fill.BackgroundColor = ExcelColorHelper.SafeFromHtml(bgColor);
            rng.Style.Border.BottomBorder  = XLBorderStyleValues.Hair;
            rng.Style.Border.BottomBorderColor = XLColor.LightGray;

            ws.Row(fila).Height = 15;
            return fila + 1;
        }

        // ── Datos del insumo según tipo ───────────────────────────────────────
        private static (string clave, string desc, string unidad, decimal cu) ObtenerDatosInsumo(
            ComponenteMatriz comp) => comp.TipoComponente switch
        {
            TipoComponenteMatriz.Material   => (
                comp.Material?.Clave ?? "",
                comp.Material?.Descripcion ?? "",
                comp.Material?.Unidad ?? "",
                comp.Material?.PrecioUnitario ?? 0),

            TipoComponenteMatriz.ManoDeObra => (
                comp.ManoDeObra?.Clave ?? "",
                comp.ManoDeObra?.Descripcion ?? "",
                "jor",
                comp.ManoDeObra?.SalarioReal ?? 0),

            TipoComponenteMatriz.Maquinaria => (
                comp.Maquinaria?.Clave ?? "",
                comp.Maquinaria?.Descripcion ?? "",
                "hora",
                comp.Maquinaria?.CostoHorario ?? 0),

            TipoComponenteMatriz.Herramienta => (
                comp.Herramienta?.Clave ?? "",
                comp.Herramienta?.Descripcion ?? "",
                comp.Herramienta?.Unidad ?? "%",
                comp.Herramienta?.PrecioUnitario ?? 0),

            TipoComponenteMatriz.Auxiliar => (
                comp.Auxiliar?.Clave ?? "",
                comp.Auxiliar?.Descripcion ?? "",
                comp.Auxiliar?.Unidad ?? "",
                comp.Auxiliar?.CostoDirecto ?? 0),

            _ => ("", "", "", 0)
        };

        private static string Sanitizar(string nombre)
        {
            if (string.IsNullOrEmpty(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars())
                nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre[..40] : nombre;
        }
    }
}
