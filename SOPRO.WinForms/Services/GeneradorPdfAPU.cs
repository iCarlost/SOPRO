using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.Reporting.Layout;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;
using MigraFont = MigraDoc.DocumentObjectModel.Font;

namespace SOPRO.WinForms.Services
{
    /// <summary>
    /// Genera el reporte PDF del APU (Análisis de Precios Unitarios), un documento
    /// con una sección por concepto. Consume el MISMO
    /// <see cref="ReportColumnSnapshot"/> neutral que la ruta Excel (visibilidad,
    /// orden, encabezado, ancho, alineación, rol numérico y formato). Las columnas
    /// de la tabla de componentes ya NO son un layout fijo: provienen del contrato
    /// compartido grid→PDF/Excel. El reporte conserva su estructura interna
    /// (bloques por tipo de insumo, subtotales e integración del precio unitario).
    /// </summary>
    public class GeneradorPdfAPU
    {
        private readonly ReporteService _svc;
        private readonly SOPROContext _ctx;

        /// <summary>Snapshot vigente de la corrida (aporta decimales y estilos).</summary>
        private ReportColumnSnapshot? _snapshot;

        public GeneradorPdfAPU(ReporteService svc, SOPROContext ctx)
        {
            _svc = svc;
            _ctx = ctx;
        }

        public string Generar(
            Proyecto proyecto,
            List<ConceptoPresupuesto> conceptos,
            PlantillaReporte plantilla,
            ReportColumnSnapshot snapshot,
            string rutaDestino)
        {
            ArgumentNullException.ThrowIfNull(proyecto);
            ArgumentNullException.ThrowIfNull(conceptos);
            ArgumentNullException.ThrowIfNull(plantilla);
            ArgumentNullException.ThrowIfNull(snapshot);
            _snapshot = snapshot;

            var conceptosConAPU = conceptos.Where(c => !c.EsAgrupador && c.MatrizId.HasValue).ToList();
            if (!conceptosConAPU.Any())
                throw new InvalidOperationException("No hay conceptos con APU vinculado en este presupuesto.");

            var cols = snapshot.Columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (cols.Count == 0)
                cols = ApuExportResolver.DefaultColumns().Where(c => c.Visible).OrderBy(c => c.Orden).ToList();

            var matrizIds = conceptosConAPU.Select(c => c.MatrizId!.Value).Distinct().ToList();
            var matrices = _ctx.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Where(m => matrizIds.Contains(m.Id))
                .ToDictionary(m => m.Id);

            var doc = new Document();
            doc.Info.Title = $"APU - {proyecto.Nombre}";
            DefinirEstilos(doc);
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);

            int numero = 1;
            foreach (var concepto in conceptosConAPU)
            {
                if (!matrices.TryGetValue(concepto.MatrizId!.Value, out var matriz))
                    continue;

                var section = doc.AddSection();
                section.PageSetup.PageFormat = PageFormat.Letter;
                section.PageSetup.Orientation = MOrientation.Landscape;
                section.PageSetup.DifferentFirstPageHeaderFooter = true;

                var headerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaEncabezadoCm(plantilla, elementosPdf);
                var footerHeightCm = PlantillaLibrePdfRenderer.ObtenerAlturaPieCm(plantilla, elementosPdf);
                section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
                section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
                section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
                section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
                section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + 0.65);
                section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + 0.8);

                ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
                ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
                ConstruirApu(section, proyecto, concepto, matriz, numero, cols, snapshot);
                numero++;
            }

            var renderer = new PdfDocumentRenderer() { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            normal.Font.Size = 8.5;

            var title = doc.Styles.AddStyle("ApuTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 12;

            var section = doc.Styles.AddStyle("ApuSection", "Normal");
            section.Font.Bold = true;
        }

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            ConstruirEncabezadoInstitucional(section.Headers.FirstPage, proyecto, plantilla, headerHeightCm);
            ConstruirEncabezadoInstitucional(section.Headers.Primary, proyecto, plantilla, headerHeightCm);
        }

        private void ConstruirEncabezadoInstitucional(HeaderFooter container, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var section = container.Section;
            if (section != null && PlantillaLibrePdfRenderer.TryRenderHeader(container, section, proyecto, plantilla, elementosPdf, _svc))
                return;
            var table = container.AddTable();
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(headerHeightCm);

            EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.EncabezadoIzqTipo, plantilla.EncabezadoIzqContenido,
                plantilla.EncabezadoIzqFuente, plantilla.EncabezadoIzqTamaño, plantilla.EncabezadoIzqNegrita, plantilla.EncabezadoIzqCursiva,
                plantilla.EncabezadoIzqAlineacion, false);
            EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.EncabezadoCenTipo, plantilla.EncabezadoCenContenido,
                plantilla.EncabezadoCenFuente, plantilla.EncabezadoCenTamaño, plantilla.EncabezadoCenNegrita, plantilla.EncabezadoCenCursiva,
                plantilla.EncabezadoCenAlineacion, false);
            EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.EncabezadoDerTipo, plantilla.EncabezadoDerContenido,
                plantilla.EncabezadoDerFuente, plantilla.EncabezadoDerTamaño, plantilla.EncabezadoDerNegrita, plantilla.EncabezadoDerCursiva,
                plantilla.EncabezadoDerAlineacion, false);
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            ConstruirPie(section.Footers.FirstPage, proyecto, plantilla, footerHeightCm);
            ConstruirPie(section.Footers.Primary, proyecto, plantilla, footerHeightCm);
        }

        private void ConstruirPie(HeaderFooter footer, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            var elementosPdf = _svc.ObtenerElementosPlantillaPdf(plantilla.Id);
            var section = footer.Section;
            if (section != null && PlantillaLibrePdfRenderer.TryRenderFooter(footer, section, proyecto, plantilla, elementosPdf, _svc))
                return;
            var table = footer.AddTable();
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(footerHeightCm);

            EscribirCeldaPlantilla(row.Cells[0], proyecto, plantilla, plantilla.PiePaginaIzqTipo, plantilla.PiePaginaIzqContenido,
                plantilla.PiePaginaIzqFuente, plantilla.PiePaginaIzqTamaño, plantilla.PiePaginaIzqNegrita, plantilla.PiePaginaIzqCursiva,
                plantilla.PiePaginaIzqAlineacion, true);
            EscribirCeldaPlantilla(row.Cells[1], proyecto, plantilla, plantilla.PiePaginaCenTipo, plantilla.PiePaginaCenContenido,
                plantilla.PiePaginaCenFuente, plantilla.PiePaginaCenTamaño, plantilla.PiePaginaCenNegrita, plantilla.PiePaginaCenCursiva,
                plantilla.PiePaginaCenAlineacion, true);
            EscribirCeldaPlantilla(row.Cells[2], proyecto, plantilla, plantilla.PiePaginaDerTipo, plantilla.PiePaginaDerContenido,
                plantilla.PiePaginaDerFuente, plantilla.PiePaginaDerTamaño, plantilla.PiePaginaDerNegrita, plantilla.PiePaginaDerCursiva,
                plantilla.PiePaginaDerAlineacion, true);
        }

        private void EscribirCeldaPlantilla(Cell cell, Proyecto proyecto, PlantillaReporte plantilla,
            string tipo, string contenido, string fuente, float tamano, bool negrita, bool cursiva,
            string alineacion, bool soportaCamposPagina)
        {
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            cell.Borders.Visible = false;

            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase) && File.Exists(contenido))
            {
                var img = cell.AddImage(contenido);
                img.LockAspectRatio = true;
                img.Height = Unit.FromCentimeter(1.4);
                return;
            }

            var p = cell.AddParagraph();
            p.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
            p.Format.Font.Name = PdfFontHelper.NormalizeFontName(string.IsNullOrWhiteSpace(fuente) ? "Segoe UI" : fuente);
            p.Format.Font.Size = tamano <= 0 ? 9 : tamano;
            p.Format.Font.Bold = negrita;
            p.Format.Font.Italic = cursiva;
            var texto = _svc.ResolverCampos(contenido ?? string.Empty, proyecto, plantilla);
            if (!soportaCamposPagina)
            {
                p.AddText(texto);
                return;
            }
            AgregarTextoConCamposPagina(p, texto);
        }

        private static void AgregarTextoConCamposPagina(Paragraph p, string texto)
        {
            texto ??= string.Empty;
            int index = 0;
            while (index < texto.Length)
            {
                int posPagina = texto.IndexOf("{pagina}", index, StringComparison.OrdinalIgnoreCase);
                int posTotal = texto.IndexOf("{total_paginas}", index, StringComparison.OrdinalIgnoreCase);
                int next = new[] { posPagina, posTotal }.Where(x => x >= 0).DefaultIfEmpty(-1).Min();
                if (next < 0)
                {
                    p.AddText(texto[index..]);
                    break;
                }
                if (next > index)
                    p.AddText(texto[index..next]);
                if (next == posPagina)
                {
                    p.AddPageField();
                    index = posPagina + "{pagina}".Length;
                }
                else
                {
                    p.AddNumPagesField();
                    index = posTotal + "{total_paginas}".Length;
                }
            }
        }

        /// <summary>Estilo de contenido neutral vigente (snapshot o default de catálogo).</summary>
        private ReportTextStyle Contenido
            => _snapshot?.EstiloTabla.EstiloContenido ?? ReportTableStyle.LegacyCatalogo().EstiloContenido;

        private void AplicarFuenteContenido(MigraFont font, bool boldOverride = false, string? colorOverride = null)
        {
            var estilo = Contenido;
            PdfFontHelper.ApplyFont(font, estilo.Fuente, estilo.Tamano > 0 ? estilo.Tamano : 9, boldOverride || estilo.Negrita, estilo.Cursiva);
            font.Color = ParseColorSafe(string.IsNullOrWhiteSpace(colorOverride) ? estilo.ColorFuente : colorOverride, "#000000");
        }

        private void ConstruirApu(
            Section section,
            Proyecto proyecto,
            ConceptoPresupuesto concepto,
            Matriz matriz,
            int numero,
            IReadOnlyList<ReportColumnDefinition> cols,
            ReportColumnSnapshot snapshot)
        {
            const double altoUtilPaginaCm = 16.20;
            int nCols = cols.Count;
            int idxDesc = IndiceDe(cols, ApuExportResolver.IdentificadorDescripcion, 2, nCols);
            int idxPct = IndiceDe(cols, ApuExportResolver.IdentificadorCantidad, 4, nCols);
            int idxImporte = IndiceDe(cols, ApuExportResolver.IdentificadorImporte, nCols - 1, nCols);

            var grupos = new[]
            {
                (Tipo: TipoComponenteMatriz.Material,    Titulo: "MATERIALES",           Color: "#E3F2FD"),
                (Tipo: TipoComponenteMatriz.ManoDeObra,  Titulo: "MANO DE OBRA",         Color: "#F3E5F5"),
                (Tipo: TipoComponenteMatriz.Maquinaria,  Titulo: "MAQUINARIA Y EQUIPO",  Color: "#FFF3E0"),
                (Tipo: TipoComponenteMatriz.Herramienta, Titulo: "HERRAMIENTA MENOR",    Color: "#E8F5E9"),
                (Tipo: TipoComponenteMatriz.Auxiliar,    Titulo: "BÁSICOS / AUXILIARES", Color: "#FFF9C4"),
            };

            decimal costoDirecto = 0;
            decimal totalMO = CalcularTotalMO(matriz);
            Table? tabla = null;
            double espacioRestanteCm = 0;

            void IniciarNuevaPagina()
            {
                if (tabla != null)
                    section.AddPageBreak();

                tabla = CrearTablaBaseApu(section, cols);
                AgregarBloqueIdentificadorApu(tabla, concepto, numero, nCols);
                espacioRestanteCm = altoUtilPaginaCm - EstimarAlturaBloqueIdentificadorCm(concepto);
            }

            void AsegurarEspacio(double requeridoCm)
            {
                if (tabla == null)
                {
                    IniciarNuevaPagina();
                    return;
                }

                if (espacioRestanteCm < requeridoCm)
                    IniciarNuevaPagina();
            }

            IniciarNuevaPagina();

            foreach (var grupo in grupos)
            {
                var componentes = matriz.Componentes
                    .Where(c => ApuComponenteClasificacionHelper.ObtenerTipoSeccion(c) == grupo.Tipo)
                    .OrderBy(c => c.Orden)
                    .ToList();
                if (!componentes.Any())
                    continue;

                double altoEncabezadoSeccionCm = 0.95;
                double altoSubtotalCm = 0.50;
                double altoMinimoAperturaCm = altoEncabezadoSeccionCm + EstimarAlturaFilaComponenteCm(componentes[0], grupo.Titulo) + altoSubtotalCm;
                AsegurarEspacio(altoMinimoAperturaCm);
                AgregarEncabezadoSeccionComponentes(tabla!, grupo.Titulo, grupo.Color, cols, nCols);
                espacioRestanteCm -= altoEncabezadoSeccionCm;

                decimal subtotal = 0;
                for (int idx = 0; idx < componentes.Count; idx++)
                {
                    var comp = componentes[idx];
                    double altoFilaCm = EstimarAlturaFilaComponenteCm(comp, grupo.Titulo);
                    double reservaSubtotalCm = altoSubtotalCm;

                    if (espacioRestanteCm < altoFilaCm + reservaSubtotalCm)
                    {
                        IniciarNuevaPagina();
                        AgregarEncabezadoSeccionComponentes(tabla!, grupo.Titulo, grupo.Color, cols, nCols);
                        espacioRestanteCm -= altoEncabezadoSeccionCm;
                    }

                    var (clave, desc, unidad, pu) = ApuComponenteClasificacionHelper.ObtenerDatosInsumo(comp);
                    decimal cantidad = comp.Cantidad;
                    decimal importe;
                    if ((grupo.Tipo == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra?.EsPorcentajeMO == true) ||
                        (grupo.Tipo == TipoComponenteMatriz.Herramienta && comp.Herramienta?.EsPorcentajeMO == true))
                    {
                        importe = totalMO * cantidad;
                        pu = totalMO;
                    }
                    else
                    {
                        importe = cantidad * pu;
                    }
                    subtotal += importe;

                    string fondoFila = (idx % 2 == 0) ? grupo.Color : "#FFFFFF";
                    var row = tabla!.AddRow();
                    for (int i = 0; i < nCols; i++)
                    {
                        var col = cols[i];
                        var cell = row.Cells[i];
                        cell.Shading.Color = MColor.Parse(fondoFila);
                        var p = cell.AddParagraph(ResolverTextoComponente(col, grupo.Titulo, clave, desc, unidad, cantidad, pu, importe, snapshot));
                        AplicarFuenteContenido(p.Format.Font);
                        p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                        p.Format.SpaceBefore = 0;
                        p.Format.SpaceAfter = 0;
                        AplicarBordeInferior(cell);
                    }

                    espacioRestanteCm -= altoFilaCm;
                }

                if (espacioRestanteCm < altoSubtotalCm)
                {
                    IniciarNuevaPagina();
                    AgregarEncabezadoSeccionComponentes(tabla!, grupo.Titulo, grupo.Color, cols, nCols);
                    espacioRestanteCm -= altoEncabezadoSeccionCm;
                }

                AgregarSubtotal(tabla!, grupo.Titulo, subtotal, nCols, snapshot);
                costoDirecto += subtotal;
                espacioRestanteCm -= altoSubtotalCm;
            }

            if (espacioRestanteCm < 0.35)
                IniciarNuevaPagina();

            var espacio = tabla!.AddRow();
            espacio.Height = Unit.FromCentimeter(0.10);
            espacio.Cells[0].MergeRight = nCols - 1;
            espacio.Borders.Visible = false;
            espacioRestanteCm -= 0.10;

            var integracion = ApuPrecioUnitarioIntegracionHelper.Calcular(proyecto, concepto, costoDirecto);
            double altoHeaderIntegracionCm = 0.95;
            double altoPrecioUnitarioCm = 0.55;
            double altoMinimoIntegracionCm = altoHeaderIntegracionCm + EstimarAlturaFilaIntegracionCm(integracion.Lineas.First()) + altoPrecioUnitarioCm;
            AsegurarEspacio(altoMinimoIntegracionCm);
            AgregarEncabezadoIntegracion(tabla!, nCols, idxDesc, idxPct, idxImporte);
            espacioRestanteCm -= altoHeaderIntegracionCm;

            for (int idx = 0; idx < integracion.Lineas.Count; idx++)
            {
                var linea = integracion.Lineas[idx];
                double altoFilaCm = EstimarAlturaFilaIntegracionCm(linea);
                double reservaCm = (idx == integracion.Lineas.Count - 1 ? altoPrecioUnitarioCm : 0.0) + 0.05;

                if (espacioRestanteCm < altoFilaCm + reservaCm)
                {
                    IniciarNuevaPagina();
                    AgregarEncabezadoIntegracion(tabla!, nCols, idxDesc, idxPct, idxImporte);
                    espacioRestanteCm -= altoHeaderIntegracionCm;
                }

                var r = tabla!.AddRow();
                var pDesc = r.Cells[idxDesc].AddParagraph(linea.Etiqueta);
                AplicarFuenteContenido(pDesc.Format.Font);
                if (!string.IsNullOrWhiteSpace(linea.PorcentajeTexto))
                {
                    var pPct = r.Cells[idxPct].AddParagraph(linea.PorcentajeTexto);
                    AplicarFuenteContenido(pPct.Format.Font);
                    pPct.Format.Alignment = MParagraphAlignment.Right;
                }
                var pMonto = r.Cells[idxImporte].AddParagraph(FormatearMoneda(linea.Monto, snapshot));
                AplicarFuenteContenido(pMonto.Format.Font);
                pMonto.Format.Alignment = MParagraphAlignment.Right;

                for (int i = 0; i < nCols; i++)
                    AplicarBordeInferior(r.Cells[i]);

                espacioRestanteCm -= altoFilaCm;
            }

            if (espacioRestanteCm < altoPrecioUnitarioCm)
            {
                IniciarNuevaPagina();
                AgregarEncabezadoIntegracion(tabla!, nCols, idxDesc, idxPct, idxImporte);
                espacioRestanteCm -= altoHeaderIntegracionCm;
            }

            AgregarPrecioUnitario(tabla!, concepto, integracion.PrecioUnitario, nCols, idxDesc, idxImporte, snapshot);
        }

        private Table CrearTablaBaseApu(Section section, IReadOnlyList<ReportColumnDefinition> cols)
        {
            var tabla = section.AddTable();
            tabla.Borders.Visible = false;
            tabla.TopPadding = 0;
            tabla.BottomPadding = 0;

            double availableCm = ReportColumnWidthConverter.GetLetterUsableWidthCm(landscape: true);
            int[] anchosPx = cols.Select(c => c.Ancho).ToArray();
            double[] anchosCm = ReportColumnWidthConverter.PxToCm(anchosPx, availableCm);
            for (int i = 0; i < cols.Count; i++)
                tabla.AddColumn(Unit.FromCentimeter(anchosCm[i]));
            return tabla;
        }

        private void AgregarSubtotal(Table tabla, string tituloGrupo, decimal subtotal, int nCols, ReportColumnSnapshot snapshot)
        {
            var sub = tabla.AddRow();
            sub.Shading.Color = MColor.Parse("#ECEFF1");
            sub.Cells[0].MergeRight = Math.Max(0, nCols - 2);
            var pSubLbl = sub.Cells[0].AddParagraph($"Subtotal {tituloGrupo}");
            AplicarFuenteContenido(pSubLbl.Format.Font, boldOverride: true);
            var pSubVal = sub.Cells[nCols - 1].AddParagraph(FormatearMoneda(subtotal, snapshot));
            AplicarFuenteContenido(pSubVal.Format.Font, boldOverride: true);
            pSubVal.Format.Alignment = MParagraphAlignment.Right;
            AplicarBordeInferior(sub.Cells[0]);
            AplicarBordeInferior(sub.Cells[nCols - 1]);
        }

        private void AgregarPrecioUnitario(Table tabla, ConceptoPresupuesto concepto, decimal precioUnitario,
            int nCols, int idxDesc, int idxImporte, ReportColumnSnapshot snapshot)
        {
            var precioU = tabla.AddRow();
            precioU.Shading.Color = MColor.Parse("#FFF8E1");
            precioU.Cells[idxDesc].MergeRight = Math.Max(0, idxImporte - idxDesc - 1);
            var pPrecioLbl = precioU.Cells[idxDesc].AddParagraph($"PRECIO UNITARIO (Unidad: {concepto.Unidad})");
            AplicarFuenteContenido(pPrecioLbl.Format.Font, boldOverride: true);
            precioU.Cells[idxDesc].Format.Alignment = MParagraphAlignment.Left;
            var pPrecioVal = precioU.Cells[idxImporte].AddParagraph(FormatearMoneda(precioUnitario, snapshot));
            AplicarFuenteContenido(pPrecioVal.Format.Font, boldOverride: true);
            pPrecioVal.Format.Alignment = MParagraphAlignment.Right;
            for (int i = 0; i < nCols; i++)
                AplicarBordeInferior(precioU.Cells[i]);
        }

        private void AgregarEncabezadoSeccionComponentes(Table tabla, string titulo, string color, IReadOnlyList<ReportColumnDefinition> cols, int nCols)
        {
            var secRow = tabla.AddRow();
            secRow.KeepWith = 1;
            secRow.Shading.Color = MColor.Parse(color);
            secRow.Cells[0].MergeRight = nCols - 1;
            var pTituloSeccion = secRow.Cells[0].AddParagraph(titulo);
            AplicarFuenteContenido(pTituloSeccion.Format.Font, boldOverride: true);
            secRow.Cells[0].Format.Alignment = MParagraphAlignment.Left;
            secRow.Cells[0].Format.LeftIndent = 2;
            secRow.Cells[0].VerticalAlignment = VerticalAlignment.Center;
            secRow.Cells[0].Borders.Visible = false;

            var header = tabla.AddRow();
            header.KeepWith = 1;
            header.Shading.Color = MColor.Parse(_snapshot?.EstiloTabla.EstiloEncabezado.ColorFondo ?? "#4A4A6A");
            for (int i = 0; i < nCols; i++)
            {
                var col = cols[i];
                var enc = col.EstiloEncabezado;
                var c = header.Cells[i];
                var p = c.AddParagraph(col.Encabezado ?? string.Empty);
                PdfFontHelper.ApplyFont(p.Format.Font, enc.Fuente, Math.Max(8f, enc.Tamano), enc.Negrita, enc.Cursiva);
                p.Format.Font.Color = ParseColorSafe(enc.ColorFuente, "#FFFFFF");
                p.Format.Alignment = ConvertirAlineacion(col.Alineacion);
                p.Format.SpaceBefore = 0;
                p.Format.SpaceAfter = 0;
                c.VerticalAlignment = VerticalAlignment.Center;
                AplicarBordeInferior(c);
            }
        }

        private void AgregarEncabezadoIntegracion(Table tabla, int nCols, int idxDesc, int idxPct, int idxImporte)
        {
            var resHeader = tabla.AddRow();
            resHeader.KeepWith = 1;
            resHeader.Shading.Color = MColor.Parse(_snapshot?.EstiloTabla.EstiloEncabezado.ColorFondo ?? "#37474F");
            resHeader.Cells[0].MergeRight = nCols - 1;
            var pResHeader = resHeader.Cells[0].AddParagraph("INTEGRACIÓN DEL PRECIO UNITARIO");
            AplicarFuenteContenido(pResHeader.Format.Font, boldOverride: true, colorOverride: "#FFFFFF");
            resHeader.Cells[0].Format.Alignment = MParagraphAlignment.Left;
            AplicarBordeInferior(resHeader.Cells[0]);

            var resCols = tabla.AddRow();
            resCols.KeepWith = 1;
            resCols.Shading.Color = MColor.Parse("#ECEFF1");
            for (int i = 0; i < nCols; i++)
            {
                string cap = i == idxDesc ? "Descripción" : i == idxPct ? "%" : i == idxImporte ? "Importe" : string.Empty;
                var c = resCols.Cells[i];
                var p = c.AddParagraph(cap);
                AplicarFuenteContenido(p.Format.Font, boldOverride: true);
                p.Format.Alignment = (i == idxPct || i == idxImporte) ? MParagraphAlignment.Right : MParagraphAlignment.Left;
                p.Format.SpaceBefore = 0;
                p.Format.SpaceAfter = 0;
                AplicarBordeInferior(c);
            }
        }

        private static string ResolverTextoComponente(
            ReportColumnDefinition col, string tituloGrupo,
            string clave, string desc, string unidad,
            decimal cantidad, decimal pu, decimal importe,
            ReportColumnSnapshot snapshot)
        {
            if (ApuExportResolver.EsTipo(col)) return ApuExportResolver.AbreviarTipo(tituloGrupo);
            if (ApuExportResolver.EsCantidad(col)) return ReportColumnGridFormat.FormatearPdf(cantidad, col, snapshot);
            if (ApuExportResolver.EsPrecioUnitario(col)) return ReportColumnGridFormat.FormatearPdf(pu, col, snapshot);
            if (ApuExportResolver.EsImporte(col)) return ReportColumnGridFormat.FormatearPdf(importe, col, snapshot);
            return ApuExportResolver.ResolveTexto(col, clave, desc, unidad);
        }

        private static string FormatearMoneda(decimal valor, ReportColumnSnapshot snapshot)
            => valor.ToString(ReportColumnGridFormat.FormatoMonedaPdf(snapshot.DecimalesImporte), CultureInfo.CurrentCulture);

        private static int IndiceDe(IReadOnlyList<ReportColumnDefinition> cols, string identificador, int fallback, int nCols)
        {
            for (int i = 0; i < cols.Count; i++)
                if (string.Equals(cols[i].Identificador, identificador, StringComparison.OrdinalIgnoreCase))
                    return i;
            return Math.Clamp(fallback, 0, Math.Max(0, nCols - 1));
        }

        private static double EstimarAlturaBloqueIdentificadorCm(ConceptoPresupuesto concepto)
        {
            var descripcion = (concepto.Descripcion ?? string.Empty).Replace("\r", " ").Replace("\n", " ");
            int lineasDescripcion = Math.Max(1, (int)Math.Ceiling(descripcion.Length / 58.0));
            return 0.95 + (lineasDescripcion * 0.34) + 0.35;
        }

        private static double EstimarAlturaFilaComponenteCm(ComponenteMatriz comp, string tituloGrupo)
        {
            var (_, desc, _, _) = ApuComponenteClasificacionHelper.ObtenerDatosInsumo(comp);
            int lineas = Math.Max(1, (int)Math.Ceiling((desc ?? string.Empty).Length / 48.0));
            return Math.Max(0.42, 0.18 + (lineas * 0.24));
        }

        private static double EstimarAlturaFilaIntegracionCm(ApuPrecioUnitarioIntegracionHelper.LineaIntegracion linea)
        {
            int lineas = Math.Max(1, (int)Math.Ceiling((linea.Etiqueta ?? string.Empty).Length / 42.0));
            return Math.Max(0.42, 0.18 + (lineas * 0.24));
        }

        private void AgregarBloqueIdentificadorApu(Table tabla, ConceptoPresupuesto concepto, int numero, int nCols)
        {
            var titulo = tabla.AddRow();
            titulo.HeadingFormat = true;
            titulo.Cells[0].MergeRight = nCols - 1;
            var pTitulo = titulo.Cells[0].AddParagraph($"ANÁLISIS DE PRECIOS UNITARIOS #{numero}");
            pTitulo.Format.Alignment = MParagraphAlignment.Center;
            pTitulo.Format.Font.Name = PdfFontHelper.NormalizeFontName(Contenido.Fuente);
            pTitulo.Format.Font.Size = 14;
            pTitulo.Format.Font.Bold = true;
            pTitulo.Format.Font.Color = ParseColorSafe(Contenido.ColorFuente, ReportTitleStyleHelper.StandardTextHex);
            titulo.Cells[0].Shading.Color = MColor.Parse(ReportTitleStyleHelper.StandardBackgroundHex);
            titulo.Cells[0].VerticalAlignment = VerticalAlignment.Center;
            titulo.Cells[0].Borders.Visible = false;
            titulo.Cells[0].Format.SpaceAfter = Unit.FromCentimeter(0.08);

            var info1 = tabla.AddRow();
            info1.HeadingFormat = true;
            var pClaveLbl = info1.Cells[0].AddParagraph("Clave:");
            AplicarFuenteContenido(pClaveLbl.Format.Font, boldOverride: true);
            var pClaveVal = info1.Cells[1].AddParagraph(concepto.Clave ?? string.Empty);
            AplicarFuenteContenido(pClaveVal.Format.Font);
            info1.Cells[2].MergeRight = Math.Max(0, nCols - 5);
            var idxEtq = Math.Max(0, nCols - 2);
            var pUnidadLbl = info1.Cells[idxEtq].AddParagraph("Unidad:");
            AplicarFuenteContenido(pUnidadLbl.Format.Font, boldOverride: true);
            var pUnidadVal = info1.Cells[nCols - 1].AddParagraph(concepto.Unidad ?? string.Empty);
            AplicarFuenteContenido(pUnidadVal.Format.Font);
            LimpiarBordesFila(info1);

            var info2 = tabla.AddRow();
            info2.HeadingFormat = true;
            var pDescLbl = info2.Cells[0].AddParagraph("Descripción:");
            AplicarFuenteContenido(pDescLbl.Format.Font, boldOverride: true);
            info2.Cells[1].MergeRight = Math.Max(0, nCols - 4);
            var pDescVal = info2.Cells[1].AddParagraph(concepto.Descripcion ?? string.Empty);
            AplicarFuenteContenido(pDescVal.Format.Font);
            var pPuLbl = info2.Cells[idxEtq].AddParagraph("P.U.:");
            AplicarFuenteContenido(pPuLbl.Format.Font, boldOverride: true);
            var pPuVal = info2.Cells[nCols - 1].AddParagraph(FormatearMoneda(concepto.PrecioUnitario, _snapshot!));
            AplicarFuenteContenido(pPuVal.Format.Font);
            info2.Cells[nCols - 1].Format.Alignment = MParagraphAlignment.Left;
            LimpiarBordesFila(info2);

            var espacio = tabla.AddRow();
            espacio.HeadingFormat = true;
            espacio.Height = Unit.FromCentimeter(0.08);
            espacio.Cells[0].MergeRight = nCols - 1;
            espacio.Borders.Visible = false;
        }

        private static void LimpiarBordesFila(Row row)
        {
            foreach (Cell cell in row.Cells)
            {
                cell.Borders.Visible = false;
                cell.VerticalAlignment = VerticalAlignment.Top;
            }
        }

        private static MColor ParseColorSafe(string? html, string fallback)
        {
            try { return MColor.Parse(string.IsNullOrWhiteSpace(html) ? fallback : html); }
            catch { return MColor.Parse(fallback); }
        }

        private static void AplicarBordeInferior(Cell cell)
        {
            cell.Borders.Bottom.Visible = true;
            cell.Borders.Bottom.Width = 0.3;
            cell.Borders.Bottom.Color = MColor.Parse("#D7DDE3");
            cell.Borders.Left.Visible = false;
            cell.Borders.Right.Visible = false;
            cell.Borders.Top.Visible = false;
        }

        private static MParagraphAlignment ConvertirAlineacion(ReportTextAlignment a) => a switch
        {
            ReportTextAlignment.Centro => MParagraphAlignment.Center,
            ReportTextAlignment.Derecha => MParagraphAlignment.Right,
            ReportTextAlignment.Justificado => MParagraphAlignment.Justify,
            _ => MParagraphAlignment.Left,
        };

        private static MParagraphAlignment ConvertirAlineacionTexto(string? alineacion)
            => (alineacion ?? "Izquierda").Trim().ToLowerInvariant() switch
            {
                "centro" or "centrado" => MParagraphAlignment.Center,
                "derecha" => MParagraphAlignment.Right,
                "justificado" => MParagraphAlignment.Justify,
                _ => MParagraphAlignment.Left
            };

        private decimal CalcularTotalMO(Matriz matriz)
        {
            decimal total = 0;
            foreach (var comp in matriz.Componentes)
            {
                if (comp.TipoComponente == TipoComponenteMatriz.ManoDeObra && comp.ManoDeObra != null && !comp.ManoDeObra.EsPorcentajeMO)
                    total += comp.Cantidad * comp.ManoDeObra.SalarioReal;
                else if (comp.TipoComponente == TipoComponenteMatriz.Auxiliar && comp.Auxiliar?.Tipo == TipoMatriz.Cuadrilla)
                    total += comp.Cantidad * comp.Auxiliar.CostoDirecto;
            }
            return total;
        }
    }
}
