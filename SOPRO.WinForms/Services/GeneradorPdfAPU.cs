using Microsoft.EntityFrameworkCore;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;
using MigraFont = MigraDoc.DocumentObjectModel.Font;

namespace SOPRO.WinForms.Services
{
    public class GeneradorPdfAPU
    {
        private readonly ReporteService _svc;
        private readonly SOPROContext _ctx;
        private ConfigColumnaReporte? _estiloDescripcionPresupuesto;

        public GeneradorPdfAPU(ReporteService svc, SOPROContext ctx)
        {
            _svc = svc;
            _ctx = ctx;
        }

        public string Generar(Proyecto proyecto, List<ConceptoPresupuesto> conceptos, PlantillaReporte plantilla, string rutaDestino, ConfigColumnaReporte? estiloDescripcionPresupuesto = null)
        {
            _estiloDescripcionPresupuesto = estiloDescripcionPresupuesto;

            var conceptosConAPU = conceptos.Where(c => !c.EsAgrupador && c.MatrizId.HasValue).ToList();
            if (!conceptosConAPU.Any())
                throw new InvalidOperationException("No hay conceptos con APU vinculado en este presupuesto.");

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

            bool primero = true;
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
                primero = false;

                ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
                ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
                ConstruirApu(section, proyecto, concepto, matriz, numero);
                numero++;
            }

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
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
            cell.Format.Alignment = ConvertirAlineacion(alineacion);
            cell.Borders.Visible = false;

            if (string.Equals(tipo, "Imagen", StringComparison.OrdinalIgnoreCase) && File.Exists(contenido))
            {
                var img = cell.AddImage(contenido);
                img.LockAspectRatio = true;
                img.Height = Unit.FromCentimeter(1.4);
                return;
            }

            var p = cell.AddParagraph();
            p.Format.Alignment = ConvertirAlineacion(alineacion);
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

        private ConfigColumnaReporte ObtenerEstiloBaseApu()
        {
            return _estiloDescripcionPresupuesto ?? new ConfigColumnaReporte
            {
                ConFuente = "Segoe UI",
                ConTamaño = 8.5f,
                ConNegrita = false,
                ConCursiva = false,
                ConColorTexto = "#000000",
                ConAlineacion = "Izquierda"
            };
        }

        private void AplicarFuenteBaseApu(MigraFont font, bool boldOverride = false, string? colorOverride = null)
        {
            var estilo = ObtenerEstiloBaseApu();
            PdfFontHelper.ApplyFont(font, estilo.ConFuente, estilo.ConTamaño > 0 ? estilo.ConTamaño : 8.5, boldOverride || estilo.ConNegrita, estilo.ConCursiva);
            font.Color = ParseColorSafe(string.IsNullOrWhiteSpace(colorOverride) ? estilo.ConColorTexto : colorOverride, "#000000");
        }

        private void ConstruirApu(Section section, Proyecto proyecto, ConceptoPresupuesto concepto, Matriz matriz, int numero)
        {
            const double altoUtilPaginaCm = 16.20;

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

                tabla = CrearTablaBaseApu(section);
                AgregarBloqueIdentificadorApu(tabla, concepto, numero);
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
                AgregarEncabezadoSeccionComponentes(tabla!, grupo.Titulo, grupo.Color);
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
                        AgregarEncabezadoSeccionComponentes(tabla!, grupo.Titulo, grupo.Color);
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

                    string[] vals =
                    {
                        AbreviarTipo(grupo.Titulo), clave, desc, unidad,
                        cantidad.ToString("N4"), pu.ToString("N2"), importe.ToString("N2")
                    };

                    var row = tabla!.AddRow();
                    for (int i = 0; i < vals.Length; i++)
                    {
                        var c = row.Cells[i];
                        var p = c.AddParagraph(vals[i]);
                        AplicarFuenteBaseApu(p.Format.Font);
                        p.Format.Alignment = i >= 4 ? MParagraphAlignment.Right : MParagraphAlignment.Left;
                        if (i == 2)
                            p.Format.Font.Size = Math.Min(Math.Max(8.0, ObtenerEstiloBaseApu().ConTamaño), 9.0);
                        AplicarBordeInferior(c);
                    }

                    espacioRestanteCm -= altoFilaCm;
                }

                if (espacioRestanteCm < altoSubtotalCm)
                {
                    IniciarNuevaPagina();
                    AgregarEncabezadoSeccionComponentes(tabla!, grupo.Titulo, grupo.Color);
                    espacioRestanteCm -= altoEncabezadoSeccionCm;
                }

                var sub = tabla!.AddRow();
                sub.Shading.Color = MColor.Parse("#ECEFF1");
                sub.Format.Font.Bold = true;
                sub.Cells[0].MergeRight = 5;
                var pSubLbl = sub.Cells[0].AddParagraph($"Subtotal {grupo.Titulo}");
                AplicarFuenteBaseApu(pSubLbl.Format.Font, boldOverride: true);
                var pSubVal = sub.Cells[6].AddParagraph(subtotal.ToString("N2"));
                AplicarFuenteBaseApu(pSubVal.Format.Font, boldOverride: true);
                pSubVal.Format.Alignment = MParagraphAlignment.Right;
                AplicarBordeInferior(sub.Cells[0]);
                AplicarBordeInferior(sub.Cells[6]);
                costoDirecto += subtotal;
                espacioRestanteCm -= altoSubtotalCm;
            }

            if (espacioRestanteCm < 0.35)
                IniciarNuevaPagina();

            var espacio = tabla!.AddRow();
            espacio.Height = Unit.FromCentimeter(0.10);
            espacio.Cells[0].MergeRight = 6;
            espacio.Borders.Visible = false;
            espacioRestanteCm -= 0.10;

            var integracion = ApuPrecioUnitarioIntegracionHelper.Calcular(proyecto, concepto, costoDirecto);
            double altoHeaderIntegracionCm = 0.95;
            double altoPrecioUnitarioCm = 0.55;
            double altoMinimoIntegracionCm = altoHeaderIntegracionCm + EstimarAlturaFilaIntegracionCm(integracion.Lineas.First()) + altoPrecioUnitarioCm;
            AsegurarEspacio(altoMinimoIntegracionCm);
            AgregarEncabezadoIntegracion(tabla!);
            espacioRestanteCm -= altoHeaderIntegracionCm;

            for (int idx = 0; idx < integracion.Lineas.Count; idx++)
            {
                var linea = integracion.Lineas[idx];
                double altoFilaCm = EstimarAlturaFilaIntegracionCm(linea);
                double reservaCm = (idx == integracion.Lineas.Count - 1 ? altoPrecioUnitarioCm : 0.0) + 0.05;

                if (espacioRestanteCm < altoFilaCm + reservaCm)
                {
                    IniciarNuevaPagina();
                    AgregarEncabezadoIntegracion(tabla!);
                    espacioRestanteCm -= altoHeaderIntegracionCm;
                }

                var r = tabla!.AddRow();
                var pDesc = r.Cells[2].AddParagraph(linea.Etiqueta);
                AplicarFuenteBaseApu(pDesc.Format.Font);
                if (!string.IsNullOrWhiteSpace(linea.PorcentajeTexto))
                {
                    var pPct = r.Cells[4].AddParagraph(linea.PorcentajeTexto);
                    AplicarFuenteBaseApu(pPct.Format.Font);
                    pPct.Format.Alignment = MParagraphAlignment.Right;
                }
                var pMonto = r.Cells[6].AddParagraph(linea.Monto.ToString("N4"));
                AplicarFuenteBaseApu(pMonto.Format.Font);
                pMonto.Format.Alignment = MParagraphAlignment.Right;

                for (int i = 0; i < 7; i++)
                    AplicarBordeInferior(r.Cells[i]);

                espacioRestanteCm -= altoFilaCm;
            }

            if (espacioRestanteCm < altoPrecioUnitarioCm)
            {
                IniciarNuevaPagina();
                AgregarEncabezadoIntegracion(tabla!);
                espacioRestanteCm -= altoHeaderIntegracionCm;
            }

            var precioU = tabla!.AddRow();
            precioU.Shading.Color = MColor.Parse("#FFF8E1");
            precioU.Cells[2].MergeRight = 3;
            var pPrecioLbl = precioU.Cells[2].AddParagraph($"PRECIO UNITARIO (Unidad: {concepto.Unidad})");
            AplicarFuenteBaseApu(pPrecioLbl.Format.Font, boldOverride: true);
            precioU.Cells[2].Format.Alignment = MParagraphAlignment.Left;
            var pPrecioVal = precioU.Cells[6].AddParagraph(integracion.PrecioUnitario.ToString("N2"));
            AplicarFuenteBaseApu(pPrecioVal.Format.Font, boldOverride: true);
            pPrecioVal.Format.Alignment = MParagraphAlignment.Right;
            for (int i = 0; i < 7; i++)
                AplicarBordeInferior(precioU.Cells[i]);
        }

        private Table CrearTablaBaseApu(Section section)
        {
            var tabla = section.AddTable();
            tabla.Borders.Visible = false;
            tabla.TopPadding = 0;
            tabla.BottomPadding = 0;
            double[] widths = { 2.2, 3.0, 11.0, 1.7, 2.2, 2.4, 2.4 };
            foreach (var w in widths)
                tabla.AddColumn(Unit.FromCentimeter(w));
            return tabla;
        }

        private void AgregarEncabezadoSeccionComponentes(Table tabla, string titulo, string color)
        {
            var secRow = tabla.AddRow();
            secRow.KeepWith = 1;
            secRow.Shading.Color = MColor.Parse(color);
            secRow.Cells[0].MergeRight = 6;
            var pTituloSeccion = secRow.Cells[0].AddParagraph(titulo);
            AplicarFuenteBaseApu(pTituloSeccion.Format.Font, boldOverride: true);
            secRow.Cells[0].Format.Alignment = MParagraphAlignment.Left;
            secRow.Cells[0].Format.LeftIndent = 2;
            secRow.Cells[0].VerticalAlignment = VerticalAlignment.Center;
            secRow.Cells[0].Borders.Visible = false;

            var header = tabla.AddRow();
            header.KeepWith = 1;
            header.Shading.Color = MColor.Parse("#37474F");
            header.Format.Font.Color = MColor.Parse("#FFFFFF");
            header.Format.Font.Bold = true;
            string[] caps = { "Tipo", "Clave", "Descripción", "Unidad", "Cantidad", "P.U.", "Importe" };
            for (int i = 0; i < caps.Length; i++)
            {
                var c = header.Cells[i];
                var p = c.AddParagraph(caps[i]);
                AplicarFuenteBaseApu(p.Format.Font, boldOverride: true, colorOverride: "#FFFFFF");
                c.Format.Alignment = i >= 4 ? MParagraphAlignment.Right : MParagraphAlignment.Left;
                AplicarBordeInferior(c);
            }
        }

        private void AgregarEncabezadoIntegracion(Table tabla)
        {
            var resHeader = tabla.AddRow();
            resHeader.KeepWith = 1;
            resHeader.Shading.Color = MColor.Parse("#37474F");
            resHeader.Cells[0].MergeRight = 6;
            var pResHeader = resHeader.Cells[0].AddParagraph("INTEGRACIÓN DEL PRECIO UNITARIO");
            AplicarFuenteBaseApu(pResHeader.Format.Font, boldOverride: true, colorOverride: "#FFFFFF");
            resHeader.Cells[0].Format.Alignment = MParagraphAlignment.Left;
            AplicarBordeInferior(resHeader.Cells[0]);

            var resCols = tabla.AddRow();
            resCols.KeepWith = 1;
            resCols.Shading.Color = MColor.Parse("#ECEFF1");
            string[] resCaps = { "", "", "Descripción", "", "%", "", "Importe" };
            for (int i = 0; i < resCaps.Length; i++)
            {
                var c = resCols.Cells[i];
                var p = c.AddParagraph(resCaps[i]);
                AplicarFuenteBaseApu(p.Format.Font, boldOverride: true);
                c.Format.Alignment = i >= 4 ? MParagraphAlignment.Right : MParagraphAlignment.Left;
                AplicarBordeInferior(c);
            }
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

        private void AgregarBloqueIdentificadorApu(Table tabla, ConceptoPresupuesto concepto, int numero)
        {
            var titulo = tabla.AddRow();
            titulo.HeadingFormat = true;
            titulo.Cells[0].MergeRight = 6;
            var pTitulo = titulo.Cells[0].AddParagraph($"ANÁLISIS DE PRECIOS UNITARIOS #{numero}");
            pTitulo.Style = "ApuTitle";
            pTitulo.Format.Alignment = MParagraphAlignment.Center;
            pTitulo.Format.Font.Name = PdfFontHelper.NormalizeFontName(ObtenerEstiloBaseApu().ConFuente);
            pTitulo.Format.Font.Size = 14;
            pTitulo.Format.Font.Bold = true;
            pTitulo.Format.Font.Color = ParseColorSafe(ObtenerEstiloBaseApu().ConColorTexto, ReportTitleStyleHelper.StandardTextHex);
            titulo.Cells[0].Shading.Color = MColor.Parse(ReportTitleStyleHelper.StandardBackgroundHex);
            titulo.Cells[0].VerticalAlignment = VerticalAlignment.Center;
            titulo.Cells[0].Borders.Visible = false;
            titulo.Cells[0].Format.SpaceAfter = Unit.FromCentimeter(0.08);

            var info1 = tabla.AddRow();
            info1.HeadingFormat = true;
            var pClaveLbl = info1.Cells[0].AddParagraph("Clave:");
            AplicarFuenteBaseApu(pClaveLbl.Format.Font, boldOverride: true);
            var pClaveVal = info1.Cells[1].AddParagraph(concepto.Clave ?? string.Empty);
            AplicarFuenteBaseApu(pClaveVal.Format.Font);
            info1.Cells[2].MergeRight = 2;
            var pUnidadLbl = info1.Cells[5].AddParagraph("Unidad:");
            AplicarFuenteBaseApu(pUnidadLbl.Format.Font, boldOverride: true);
            var pUnidadVal = info1.Cells[6].AddParagraph(concepto.Unidad ?? string.Empty);
            AplicarFuenteBaseApu(pUnidadVal.Format.Font);
            LimpiarBordesFila(info1);

            var info2 = tabla.AddRow();
            info2.HeadingFormat = true;
            var pDescLbl = info2.Cells[0].AddParagraph("Descripción:");
            AplicarFuenteBaseApu(pDescLbl.Format.Font, boldOverride: true);
            info2.Cells[1].MergeRight = 3;
            var pDescVal = info2.Cells[1].AddParagraph(concepto.Descripcion ?? string.Empty);
            AplicarFuenteBaseApu(pDescVal.Format.Font);
            var pPuLbl = info2.Cells[5].AddParagraph("P.U.:");
            AplicarFuenteBaseApu(pPuLbl.Format.Font, boldOverride: true);
            var pPuVal = info2.Cells[6].AddParagraph(concepto.PrecioUnitario.ToString("N2"));
            AplicarFuenteBaseApu(pPuVal.Format.Font);
            info2.Cells[6].Format.Alignment = MParagraphAlignment.Left;
            LimpiarBordesFila(info2);

            var espacio = tabla.AddRow();
            espacio.HeadingFormat = true;
            espacio.Height = Unit.FromCentimeter(0.08);
            espacio.Cells[0].MergeRight = 6;
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

        private static MParagraphAlignment ConvertirAlineacion(string? alineacion)
            => (alineacion ?? "Izquierda").Trim().ToLowerInvariant() switch
            {
                "centro" or "centrado" => MParagraphAlignment.Center,
                "derecha" => MParagraphAlignment.Right,
                _ => MParagraphAlignment.Left
            };

        private static string AbreviarTipo(string titulo)
        {
            if (titulo.StartsWith("MATER", StringComparison.OrdinalIgnoreCase)) return "MAT";
            if (titulo.StartsWith("MANO", StringComparison.OrdinalIgnoreCase)) return "M.O.";
            if (titulo.StartsWith("MAQUI", StringComparison.OrdinalIgnoreCase)) return "MAQ";
            if (titulo.StartsWith("HER", StringComparison.OrdinalIgnoreCase)) return "HER";
            return "AUX";
        }

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
