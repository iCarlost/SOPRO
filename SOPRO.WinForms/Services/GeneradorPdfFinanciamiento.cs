using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using DrawingColor = System.Drawing.Color;
using MColor = MigraDoc.DocumentObjectModel.Color;
using MOrientation = MigraDoc.DocumentObjectModel.Orientation;
using MParagraphAlignment = MigraDoc.DocumentObjectModel.ParagraphAlignment;

namespace SOPRO.WinForms.Services
{
    public sealed class GeneradorPdfFinanciamiento
    {
        public sealed class BaseRowInfo
        {
            public int NumeroPeriodo { get; set; }
            public decimal CostoDirecto { get; set; }
            public decimal CostoIndirecto { get; set; }
        }

        private readonly ReporteService _svc;

        public GeneradorPdfFinanciamiento(ReporteService svc)
        {
            _svc = svc;
        }

        public string Generar(
            Proyecto proyecto,
            PlantillaReporte plantilla,
            List<ColumnaFinanciamiento> columnas,
            ConfiguracionFinanciamiento config,
            List<FilaFlujoCajaFinanciamiento> filas,
            List<BaseRowInfo> baseRows,
            string rutaDestino = null,
            ConfiguracionTituloReporte? tituloCfg = null)
        {
            if (proyecto == null) throw new ArgumentNullException(nameof(proyecto));
            if (plantilla == null) throw new ArgumentNullException(nameof(plantilla));
            if (columnas == null) throw new ArgumentNullException(nameof(columnas));
            if (config == null) throw new ArgumentNullException(nameof(config));
            if (filas == null) throw new ArgumentNullException(nameof(filas));
            if (baseRows == null) throw new ArgumentNullException(nameof(baseRows));

            var cols = columnas.Where(c => c.Visible).OrderBy(c => c.Orden).ToList();
            if (filas.Count == 0)
                throw new InvalidOperationException("No hay cálculo de financiamiento para exportar.");

            if (string.IsNullOrWhiteSpace(rutaDestino))
            {
                var carpeta = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments), "SOPRO", "Reportes");
                Directory.CreateDirectory(carpeta);
                rutaDestino = Path.Combine(carpeta,
                    $"Financiamiento_{SanitizarNombre(proyecto.Nombre)}_{DateTime.Now:yyyyMMdd_HHmm}.pdf");
            }

            var doc = new Document();
            DefinirEstilos(doc);

            var section = doc.AddSection();
            section.PageSetup.PageFormat = PageFormat.Letter;
            section.PageSetup.Orientation = MOrientation.Landscape;

            var headerHeightCm = Math.Max(1.8, plantilla.EncabezadoAltura / 28.0);
            var footerHeightCm = Math.Max(1.2, plantilla.PiePaginaAltura / 28.0);

            section.PageSetup.LeftMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.RightMargin = Unit.FromCentimeter(1.0);
            section.PageSetup.HeaderDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.FooterDistance = Unit.FromCentimeter(0.35);
            section.PageSetup.TopMargin = Unit.FromCentimeter(headerHeightCm + 0.8);
            section.PageSetup.BottomMargin = Unit.FromCentimeter(footerHeightCm + 0.8);

            ConstruirHeader(section, proyecto, plantilla, headerHeightCm);
            ConstruirFooter(section, proyecto, plantilla, footerHeightCm);
            ConstruirCuerpo(section, proyecto, plantilla, cols, config, filas.OrderBy(x => x.NumeroPeriodo).ToList(), baseRows, tituloCfg);

            var renderer = new PdfDocumentRenderer(true) { Document = doc };
            renderer.RenderDocument();
            renderer.PdfDocument.Save(rutaDestino);
            return rutaDestino;
        }

        private static void DefinirEstilos(Document doc)
        {
            var normal = doc.Styles["Normal"];
            normal.Font.Name = PdfFontHelper.NormalizeFontName("Segoe UI");
            normal.Font.Size = 8;

            var title = doc.Styles.AddStyle("FinTitle", "Normal");
            title.Font.Bold = true;
            title.Font.Size = 12;
            title.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var project = doc.Styles.AddStyle("FinProject", "Normal");
            project.Font.Size = 9.5;
            project.ParagraphFormat.Alignment = MParagraphAlignment.Center;

            var section = doc.Styles.AddStyle("FinSection", "Normal");
            section.Font.Bold = true;
            section.Font.Size = 9.2;

            var header = doc.Styles.AddStyle("FinHeader", "Normal");
            header.Font.Bold = true;
            header.ParagraphFormat.Alignment = MParagraphAlignment.Center;
        }

        private void ConstruirHeader(Section section, Proyecto proyecto, PlantillaReporte plantilla, double headerHeightCm)
        {
            ConstruirTablaPlantilla(section.Headers.Primary.AddTable(), proyecto, plantilla, headerHeightCm, false);
            ConstruirTablaPlantilla(section.Headers.FirstPage.AddTable(), proyecto, plantilla, headerHeightCm, false);
        }

        private void ConstruirFooter(Section section, Proyecto proyecto, PlantillaReporte plantilla, double footerHeightCm)
        {
            ConstruirTablaPlantilla(section.Footers.Primary.AddTable(), proyecto, plantilla, footerHeightCm, true);
            ConstruirTablaPlantilla(section.Footers.FirstPage.AddTable(), proyecto, plantilla, footerHeightCm, true);
        }

        private void ConstruirTablaPlantilla(Table table, Proyecto proyecto, PlantillaReporte plantilla, double altoCm, bool esPie)
        {
            table.Borders.Visible = false;
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));
            table.AddColumn(Unit.FromCentimeter(8.6));

            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(altoCm);

            if (!esPie)
            {
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
            else
            {
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
                img.Height = Unit.FromCentimeter(1.5);
                return;
            }

            var p = cell.AddParagraph();
            p.Format.Alignment = ConvertirAlineacionTexto(alineacion);
            p.Format.SpaceAfter = 0;
            p.Format.SpaceBefore = 0;
            PdfFontHelper.ApplyFont(p.Format.Font, fuente, tamano <= 0 ? 9 : tamano, negrita, cursiva);

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
            texto = texto ?? string.Empty;
            int index = 0;
            while (index < texto.Length)
            {
                int posPagina = texto.IndexOf("{pagina}", index, StringComparison.OrdinalIgnoreCase);
                int posTotal = texto.IndexOf("{total_paginas}", index, StringComparison.OrdinalIgnoreCase);
                int next = new[] { posPagina, posTotal }.Where(x => x >= 0).DefaultIfEmpty(-1).Min();
                if (next < 0)
                {
                    p.AddText(texto.Substring(index));
                    break;
                }

                if (next > index)
                    p.AddText(texto.Substring(index, next - index));

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

        private void ConstruirCuerpo(
            Section section,
            Proyecto proyecto,
            PlantillaReporte plantilla,
            List<ColumnaFinanciamiento> cols,
            ConfiguracionFinanciamiento config,
            List<FilaFlujoCajaFinanciamiento> filas,
            List<BaseRowInfo> baseRows, ConfiguracionTituloReporte? tituloCfg)
        {
            var tituloTable = section.AddTable();
            tituloTable.Borders.Visible = false;
            tituloTable.AddColumn(Unit.FromCentimeter(25.94));
            var rowTitulo = tituloTable.AddRow();
            rowTitulo.Shading.Color = ParseColor(ReportTitleStyleHelper.StandardBackgroundHex);
            rowTitulo.HeightRule = RowHeightRule.AtLeast;
            rowTitulo.Height = Unit.FromCentimeter(0.75);
            var pTitulo = rowTitulo.Cells[0].AddParagraph(ReportTitleStyleHelper.ObtenerTexto(tituloCfg, "ANÁLISIS DE FINANCIAMIENTO"));
            pTitulo.Style = "FinTitle";
            ReportTitleStyleHelper.ApplyToParagraph(pTitulo, tituloCfg, "ANÁLISIS DE FINANCIAMIENTO");
            rowTitulo.Cells[0].Format.Alignment = MParagraphAlignment.Center;
            rowTitulo.Cells[0].VerticalAlignment = VerticalAlignment.Center;

            var proyectoTable = section.AddTable();
            proyectoTable.Borders.Visible = false;
            proyectoTable.AddColumn(Unit.FromCentimeter(25.94));
            var rowProyecto = proyectoTable.AddRow();
            rowProyecto.Shading.Color = ParseColor("#E3F2FD");
            rowProyecto.HeightRule = RowHeightRule.AtLeast;
            rowProyecto.Height = Unit.FromCentimeter(0.58);
            var pProyecto = rowProyecto.Cells[0].AddParagraph(proyecto.Nombre ?? string.Empty);
            pProyecto.Style = "FinProject";
            rowProyecto.Cells[0].VerticalAlignment = VerticalAlignment.Center;

            section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.05);

            ConstruirDatos(section, proyecto, config, filas, baseRows);
            ConstruirMatriz(section, proyecto, cols, config, filas, baseRows);
        }

        private void ConstruirDatos(Section section, Proyecto proyecto, ConfiguracionFinanciamiento config,
            List<FilaFlujoCajaFinanciamiento> filas, List<BaseRowInfo> baseRows)
        {
            var table = section.AddTable();
            table.Borders.Visible = false;
            table.Rows.LeftIndent = 0;

            double[] widths = { 4.2, 0.4, 2.4, 0.4, 0.4, 3.2, 0.5, 1.8, 2.0 };
            foreach (var width in widths)
                table.AddColumn(Unit.FromCentimeter(width));

            int durMeses = proyecto.PlazoEjecucion > 0 ? (int)Math.Ceiling(proyecto.PlazoEjecucion / 30.0) : 1;
            decimal totalCD = baseRows.Sum(x => x.CostoDirecto);
            decimal totalCI = baseRows.Sum(x => x.CostoIndirecto);

            AgregarDato(table, "COSTO DIRECTO", totalCD.ToString("N2"), "INDICADOR ECONÓMICO", "TIIE", config.TasaTIIE.ToString("N4") + "%");
            AgregarDato(table, "COSTO INDIRECTO = " + (proyecto.PorcentajeIndirectosCentral + proyecto.PorcentajeIndirectosCampo).ToString("N2") + "%",
                totalCI.ToString("N2"), "TASA DE INTERÉS ANUAL", string.Empty, config.TasaEfectiva.ToString("N4") + "%");
            AgregarDato(table, "% ANTICIPO", (config.PorcentajeAnticipo / 100m).ToString("0.0000%"),
                "TASA DE INTERÉS PERIODO BASE", string.Empty,
                (filas.Count > 0 ? GetTasaPeriodoLabel(config, filas[0].DiasPeriodo) : 0m).ToString("N4") + "%");
            AgregarDato(table, "DESFASE DE COBRO", config.DesfaseCobro.ToString(),
                "BASE DE CÁLCULO", string.Empty, config.BaseCalculo ?? string.Empty);

            section.AddParagraph().Format.SpaceAfter = Unit.FromCentimeter(0.10);
        }

        private void AgregarDato(Table table, string etiquetaIzq, string valorIzq, string etiquetaDer, string subEtiquetaDer, string valorDer)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.52);

            row.Cells[0].AddParagraph(etiquetaIzq).Format.Font.Bold = true;
            row.Cells[2].AddParagraph(valorIzq).Format.Alignment = MParagraphAlignment.Right;
            row.Cells[5].AddParagraph(etiquetaDer).Format.Font.Bold = true;
            if (!string.IsNullOrWhiteSpace(subEtiquetaDer))
                row.Cells[7].AddParagraph(subEtiquetaDer).Format.Font.Bold = true;
            row.Cells[8].AddParagraph(valorDer).Format.Alignment = MParagraphAlignment.Right;

            row.Cells[0].Shading.Color = ParseColor("#F5F5F5");
            row.Cells[5].Shading.Color = ParseColor("#F5F5F5");
        }

        private void ConstruirMatriz(Section section, Proyecto proyecto, List<ColumnaFinanciamiento> cols, ConfiguracionFinanciamiento config,
            List<FilaFlujoCajaFinanciamiento> filas, List<BaseRowInfo> baseRows)
        {
            var table = section.AddTable();
            table.Rows.LeftIndent = 0;
            table.Borders.Visible = false;

            double pageWidthCm = 25.94;
            double fixedConceptCm = 4.9;
            double fixedGapCm = 0.6;
            double remainingCm = Math.Max(10.0, pageWidthCm - fixedConceptCm - fixedGapCm);
            double periodWidthCm = Math.Max(1.35, remainingCm / Math.Max(1, filas.Count));

            table.AddColumn(Unit.FromCentimeter(fixedConceptCm));
            table.AddColumn(Unit.FromCentimeter(fixedGapCm));
            for (int i = 0; i < filas.Count; i++)
                table.AddColumn(Unit.FromCentimeter(periodWidthCm));

            var cfgPeriodo = cols.FirstOrDefault(c => string.Equals(c.NombreInterno, "colPeriodo", StringComparison.OrdinalIgnoreCase));

            var header = table.AddRow();
            header.HeadingFormat = true;
            header.HeightRule = RowHeightRule.AtLeast;
            header.Height = Unit.FromCentimeter(0.74);
            header.Cells[0].AddParagraph("CONCEPTO").Style = "FinHeader";
            header.Cells[1].AddParagraph(string.Empty);
            AplicarHeaderCell(header.Cells[0]);
            AplicarHeaderCell(header.Cells[1]);
            for (int i = 0; i < filas.Count; i++)
            {
                var p = header.Cells[i + 2].AddParagraph(filas[i].Etiqueta);
                p.Style = "FinHeader";
                p.Format.Alignment = MParagraphAlignment.Center;
                AplicarHeaderCell(header.Cells[i + 2]);
            }
            AplicarBordeInferior(header.Cells[0], "#4A4A6A", 0.02);
            header.Cells[0].Borders.Bottom.Visible = false;

            var baseRowsMap = baseRows.ToDictionary(x => x.NumeroPeriodo, x => x);
            decimal totalBase = baseRows.Sum(x => x.CostoDirecto + x.CostoIndirecto);
            decimal[] avanceProgramado = filas.Select(x =>
            {
                decimal basePeriodo = baseRowsMap.TryGetValue(x.NumeroPeriodo, out var b) ? b.CostoDirecto + b.CostoIndirecto : 0m;
                return totalBase > 0m ? decimal.Round(basePeriodo / totalBase, 4, MidpointRounding.AwayFromZero) : 0m;
            }).ToArray();

            var ingresosAcum = new List<decimal>();
            var egresosAcum = new List<decimal>();
            decimal ingresoAcum = 0m;
            decimal egresoAcum = 0m;
            var _motorFin = new MotorCalculoSopro(proyecto);
            foreach (var f in filas)
            {
                ingresoAcum += f.AnticipoRecibido + f.EstimacionCobrada - f.AmortizacionAnticipo;
                egresoAcum += f.Egresos;
                ingresosAcum.Add(_motorFin.RedondearImporte(ingresoAcum));
                egresosAcum.Add(_motorFin.RedondearImporte(egresoAcum));
            }

            AgregarFilaValores(table, "AVANCE PROGRAMADO", filas, x => avanceProgramado[x], cols, "colPeriodo", "colEgresos", "0.0000%");

            AgregarFilaEspaciador(table);

            AgregarFilaSeccion(table, "INGRESOS");
            AgregarFilaValores(table, "ESTIMACIONES DE OBRA (CD + CI)", filas, x => filas[x].EstimacionCobrada, cols, "colPeriodo", "colEstim", "#,##0.00");
            AgregarFilaValores(table, "AMORTIZACIÓN ANTICIPO", filas, x => filas[x].AmortizacionAnticipo, cols, "colPeriodo", "colAmort", "#,##0.00");
            AgregarFilaValores(table, "COBRO NETO", filas, x => filas[x].EstimacionCobrada - filas[x].AmortizacionAnticipo, cols, "colPeriodo", "colCobro", "#,##0.00");
            AgregarFilaValores(table, "ANTICIPOS (CD + CI)", filas, x => filas[x].AnticipoRecibido, cols, "colPeriodo", "colAnticipo", "#,##0.00");
            AgregarFilaValores(table, "INGRESOS ACUMULADOS", filas, x => ingresosAcum[x], cols, "colPeriodo", "colCobro", "#,##0.00");

            AgregarFilaEspaciador(table);

            AgregarFilaSeccion(table, "EGRESOS");
            AgregarFilaValores(table, "COSTO DIRECTO", filas, x => baseRowsMap.TryGetValue(filas[x].NumeroPeriodo, out var b) ? b.CostoDirecto : 0m, cols, "colPeriodo", "colCD", "#,##0.00");
            AgregarFilaValores(table, "COSTO INDIRECTO", filas, x => baseRowsMap.TryGetValue(filas[x].NumeroPeriodo, out var b) ? b.CostoIndirecto : 0m, cols, "colPeriodo", "colCI", "#,##0.00");
            AgregarFilaValores(table, "C.D. + C.I.", filas, x => filas[x].Egresos, cols, "colPeriodo", "colEgresos", "#,##0.00");
            AgregarFilaValores(table, "EGRESOS ACUMULADOS", filas, x => egresosAcum[x], cols, "colPeriodo", "colEgresos", "#,##0.00");

            AgregarFilaEspaciador(table);

            AgregarFilaValores(table, "EGRESOS ACUM - INGRESOS ACUM", filas, x => (egresosAcum[x] - ingresosAcum[x]), cols, "colPeriodo", "colSaldo", "#,##0.00");
            AgregarFilaValores(table, "TASA PERÍODO", filas, x => GetTasaPeriodoLabel(config, filas[x].DiasPeriodo) / 100m, cols, "colPeriodo", "colTasa", "0.0000%");
            AgregarFilaValores(table, "COSTO FINANC. PARCIAL (INTERESES)", filas, x => filas[x].InteresPeriodo, cols, "colPeriodo", "colInteres", "#,##0.0000");
            decimal interesAcum = 0m;
            AgregarFilaValores(table, "COSTO FINANC. ACUMULADO", filas, x =>
            {
                interesAcum += filas[x].InteresPeriodo;
                return interesAcum;
            }, cols, "colPeriodo", "colInteres", "#,##0.0000");

            AgregarFilaEspaciador(table);

            var result = table.AddRow();
            result.HeightRule = RowHeightRule.AtLeast;
            result.Height = Unit.FromCentimeter(0.58);
            result.Cells[0].AddParagraph("PORCENTAJE DE FINANCIAMIENTO").Format.Font.Bold = true;
            result.Cells[0].MergeRight = Math.Max(0, filas.Count - 1);
            result.Cells[filas.Count].AddParagraph("RESULTADO").Format.Font.Bold = true;
            result.Cells[filas.Count + 1].AddParagraph((config.PorcentajeCalculado / 100m).ToString("0.00000%")).Format.Alignment = MParagraphAlignment.Right;
            result.Cells[filas.Count + 1].Shading.Color = ParseColor("#FFF2CC");
            result.Cells[filas.Count + 1].Format.Font.Bold = true;
            AplicarBordeSuperior(result.Cells[0], "#B7B7B7", 0.02);
            AplicarBordeSuperior(result.Cells[filas.Count], "#B7B7B7", 0.02);
            AplicarBordeSuperior(result.Cells[filas.Count + 1], "#B7B7B7", 0.02);
        }

        private void AgregarFilaSeccion(Table table, string titulo)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.54);
            row.Shading.Color = ParseColor("#D9E2F3");
            row.Cells[0].MergeRight = table.Columns.Count - 1;
            var p = row.Cells[0].AddParagraph(titulo);
            p.Style = "FinSection";
            p.Format.Alignment = MParagraphAlignment.Left;
        }

        private void AgregarFilaEspaciador(Table table)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.Exactly;
            row.Height = Unit.FromCentimeter(0.14);
            for (int i = 0; i < table.Columns.Count; i++)
            {
                row.Cells[i].Borders.Visible = false;
                row.Cells[i].Shading.Color = ParseColor("#FFFFFF");
            }
        }

        private void AgregarFilaValores(
            Table table,
            string concepto,
            List<FilaFlujoCajaFinanciamiento> filas,
            Func<int, decimal> selector,
            List<ColumnaFinanciamiento> columnas,
            string keyPeriodo,
            string keyValor,
            string formato)
        {
            var row = table.AddRow();
            row.HeightRule = RowHeightRule.AtLeast;
            row.Height = Unit.FromCentimeter(0.52);

            var cellConcepto = row.Cells[0];
            var pConcepto = cellConcepto.AddParagraph(concepto);
            pConcepto.Format.Font.Bold = true;
            AplicarFormatoCelda(cellConcepto, columnas.FirstOrDefault(c => string.Equals(c.NombreInterno, keyPeriodo, StringComparison.OrdinalIgnoreCase)), true, false);

            row.Cells[1].AddParagraph(string.Empty);

            var cfgValor = columnas.FirstOrDefault(c => string.Equals(c.NombreInterno, keyValor, StringComparison.OrdinalIgnoreCase));

            for (int i = 0; i < filas.Count; i++)
            {
                var valor = selector(i);
                var cell = row.Cells[i + 2];
                if (valor != 0m)
                    cell.AddParagraph(FormatearValor(valor, formato));
                cell.Format.Alignment = ConvertirAlineacion(cfgValor != null ? cfgValor.Alineacion : AlineacionColumna.Derecha);
                AplicarFormatoCelda(cell, cfgValor, false, false);
            }
        }

        private void AplicarHeaderCell(Cell cell)
        {
            cell.Shading.Color = ParseColor("#4A4A6A");
            cell.Format.Font.Color = ParseColor("#FFFFFF");
            cell.Format.Font.Bold = true;
            cell.Format.Alignment = MParagraphAlignment.Center;
            cell.VerticalAlignment = VerticalAlignment.Center;
            cell.Borders.Visible = false;
        }

        private void AplicarFormatoCelda(Cell cell, ColumnaFinanciamiento cfg, bool esConcepto, bool esEncabezado)
        {
            cell.Borders.Visible = false;
            cell.VerticalAlignment = VerticalAlignment.Center;

            if (cfg == null)
                return;

            PdfFontHelper.ApplyFont(cell.Format.Font, cfg.NombreFuente, cfg.TamanoFuente > 0 ? cfg.TamanoFuente : 8,
                cfg.Negrita || esConcepto || esEncabezado, cfg.Cursiva);

            if (!string.IsNullOrWhiteSpace(cfg.ColorFuente))
                cell.Format.Font.Color = ParseColor(cfg.ColorFuente);

            if (!esEncabezado && !string.IsNullOrWhiteSpace(cfg.ColorFondo))
            {
                var fondo = NormalizarColor(cfg.ColorFondo);
                if (!string.Equals(fondo, "#FFFFFF", StringComparison.OrdinalIgnoreCase))
                    cell.Shading.Color = ParseColor(fondo);
            }

            if (!esConcepto)
                cell.Format.Alignment = ConvertirAlineacion(cfg.Alineacion);
        }

        private static string FormatearValor(decimal valor, string formato)
        {
            if (string.IsNullOrWhiteSpace(formato))
                return valor.ToString("N2");

            if (string.Equals(formato, "0.0000%", StringComparison.OrdinalIgnoreCase))
                return valor.ToString("0.0000%");
            if (string.Equals(formato, "0.00000%", StringComparison.OrdinalIgnoreCase))
                return valor.ToString("0.00000%");
            if (string.Equals(formato, "#,##0.0000", StringComparison.OrdinalIgnoreCase))
                return valor.ToString("N4");
            return valor.ToString("N2");
        }

        private static decimal GetTasaPeriodoLabel(ConfiguracionFinanciamiento config, int diasPeriodo)
        {
            decimal tasaAnual = (config.TasaTIIE + config.PuntosAdicionales) / 100m;
            if (tasaAnual <= 0m || diasPeriodo <= 0)
                return 0m;
            return decimal.Round(tasaAnual * diasPeriodo / 365m * 100m, 4, MidpointRounding.AwayFromZero);
        }

        private static void AplicarBordeSuperior(Cell cell, string colorHex, double widthPt)
        {
            cell.Borders.Top.Visible = true;
            cell.Borders.Top.Color = ParseColor(colorHex);
            cell.Borders.Top.Width = Unit.FromPoint(widthPt);
        }

        private static void AplicarBordeInferior(Cell cell, string colorHex, double widthPt)
        {
            cell.Borders.Bottom.Visible = true;
            cell.Borders.Bottom.Color = ParseColor(colorHex);
            cell.Borders.Bottom.Width = Unit.FromPoint(widthPt);
        }

        private static MParagraphAlignment ConvertirAlineacion(AlineacionColumna alineacion)
        {
            return alineacion switch
            {
                AlineacionColumna.Centro => MParagraphAlignment.Center,
                AlineacionColumna.Derecha => MParagraphAlignment.Right,
                _ => MParagraphAlignment.Left
            };
        }

        private static MParagraphAlignment ConvertirAlineacionTexto(string alineacion)
        {
            return (alineacion ?? string.Empty) switch
            {
                "Centro" => MParagraphAlignment.Center,
                "Derecha" => MParagraphAlignment.Right,
                _ => MParagraphAlignment.Left
            };
        }

        private static MColor ParseColor(string value)
        {
            try
            {
                var c = DrawingColorTranslator(value);
                return new MColor(c.R, c.G, c.B);
            }
            catch
            {
                return MColor.Parse("#000000");
            }
        }

        private static DrawingColor DrawingColorTranslator(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
                return DrawingColor.Black;
            try
            {
                return System.Drawing.ColorTranslator.FromHtml(value);
            }
            catch
            {
                return DrawingColor.Black;
            }
        }

        private static string NormalizarColor(string value)
        {
            var c = DrawingColorTranslator(value);
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }

        private static string SanitizarNombre(string nombre)
        {
            if (string.IsNullOrWhiteSpace(nombre)) return "Proyecto";
            foreach (var c in Path.GetInvalidFileNameChars()) nombre = nombre.Replace(c, '_');
            return nombre.Length > 40 ? nombre.Substring(0, 40) : nombre;
        }
    }
}
