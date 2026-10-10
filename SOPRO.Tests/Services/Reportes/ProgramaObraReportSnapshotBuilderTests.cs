using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Reporting.Programa;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="ProgramaObraReportSnapshotBuilder" /> y de los
/// modelos neutrales del Programa de Obra (<see cref="ProgramaRow" />,
/// <see cref="ProgramaPeriodColumn" /> y <see cref="ProgramaReportData" />).
/// El builder produce snapshots deterministas de catorce columnas y materializa las
/// filas desde los datos neutrales + <see cref="GanttRenderModel"/> sin tocar el
/// grid ni persistir configuración.
/// </summary>
[TestClass]
public class ProgramaObraReportSnapshotBuilderTests
{
    private static readonly ReportTableStyle EstiloEsperado = ReportTableStyle.LegacyCatalogo();

    private static readonly string[] IdentificadoresCanonicos =
    {
        "colOrden", "colClave", "colDescripcion", "colUnidad", "colPredecesora",
        "colCantidad", "colFechaInicio", "colFechaFin", "colDuracionDias",
        "colRendimientoDiario", "colFrentes", "colPrecioUnitario", "colImporte", "colRutaCritica"
    };

    // ───────────────────────── Snapshot ─────────────────────────

    [TestMethod]
    public void Build_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new ProgramaObraReportSnapshotBuilder()
            .Build(7, "Programa", ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas());

        Assert.AreEqual(ProgramaObraReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("ProgramaObra", snapshot.TipoReporte);
        Assert.AreEqual(7, snapshot.ProyectoId);
        Assert.AreEqual("Programa", snapshot.Titulo);
        Assert.AreEqual(14, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new ProgramaObraReportSnapshotBuilder()
            .Build(1, null, ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas());

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void Build_SinColumnas_UsaPredeterminadasEnMemoria()
    {
        var snapshot = new ProgramaObraReportSnapshotBuilder().Build(1, "M", null);

        CollectionAssert.AreEqual(
            IdentificadoresCanonicos,
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void Build_ConColumnasPersistidas_RespetaOrdenYEncabezado()
    {
        var columnas = new List<ColumnaProgramaObra>
        {
            new() { NombreInterno = "colDescripcion", Nombre = "Concepto", Orden = 1, AnchoColumna = 300, Alineacion = AlineacionColumna.Izquierda },
            new() { NombreInterno = "colImporte", Nombre = "Importe Total", Orden = 2, AnchoColumna = 120, Alineacion = AlineacionColumna.Derecha },
            new() { NombreInterno = "colClave", Nombre = "Clave", Orden = 3, AnchoColumna = 90, Alineacion = AlineacionColumna.Centro },
        };

        var snapshot = new ProgramaObraReportSnapshotBuilder().Build(1, "M", columnas);

        CollectionAssert.AreEqual(
            new[] { "colDescripcion", "colImporte", "colClave" },
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray());

        Assert.AreEqual("Concepto", snapshot.Columnas.Single(c => c.Identificador == "colDescripcion").Encabezado);
        Assert.AreEqual(300, snapshot.Columnas.Single(c => c.Identificador == "colDescripcion").Ancho);
    }

    [TestMethod]
    public void Build_UsaEstiloLegacyCatalogo()
    {
        var snapshot = new ProgramaObraReportSnapshotBuilder()
            .Build(1, "M", ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas());

        Assert.AreEqual(EstiloEsperado, snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_MarcaRolesMonetariosYCantidad()
    {
        var snapshot = new ProgramaObraReportSnapshotBuilder()
            .Build(1, "M", ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas());

        var importe = snapshot.Columnas.Single(c => c.Identificador == "colImporte");
        Assert.IsTrue(importe.EsNumerica);
        Assert.IsTrue(importe.EsMoneda);

        var pu = snapshot.Columnas.Single(c => c.Identificador == "colPrecioUnitario");
        Assert.IsTrue(pu.EsMoneda);

        var cantidad = snapshot.Columnas.Single(c => c.Identificador == "colCantidad");
        Assert.IsTrue(cantidad.EsNumerica);
        Assert.IsFalse(cantidad.EsMoneda);

        Assert.IsFalse(snapshot.Columnas.Single(c => c.Identificador == "colDescripcion").EsNumerica);
    }

    [TestMethod]
    public void Build_PropagaDecimalesExplicitos()
    {
        var snapshot = new ProgramaObraReportSnapshotBuilder()
            .Build(1, "M", ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas(), 3, 4, 6);

        Assert.AreEqual(3, snapshot.DecimalesCantidad);
        Assert.AreEqual(4, snapshot.DecimalesImporte);
        Assert.AreEqual(6, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Build_DesdeProyecto_UsaSusDecimales()
    {
        var proyecto = new Proyecto
        {
            Id = 42,
            DecimalesCantidad = 4,
            DecimalesImporte = 1,
            DecimalesPorcentaje = 5,
        };

        var snapshot = new ProgramaObraReportSnapshotBuilder()
            .Build(proyecto, "M", ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas());

        Assert.AreEqual(42, snapshot.ProyectoId);
        Assert.AreEqual(4, snapshot.DecimalesCantidad);
        Assert.AreEqual(1, snapshot.DecimalesImporte);
        Assert.AreEqual(5, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Build_ProyectoNulo_LanzaArgumentNullException()
    {
        var builder = new ProgramaObraReportSnapshotBuilder();
        Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.Build(null!, "M", ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas()));
    }

    [TestMethod]
    public void ColumnasPredeterminadas_ExponenCatorceColumnasCanonicas()
    {
        var columnas = ProgramaObraReportSnapshotBuilder.ColumnasPredeterminadas();

        Assert.AreEqual(14, columnas.Count);
        CollectionAssert.AreEqual(IdentificadoresCanonicos, columnas.Select(c => c.NombreInterno).ToArray());
        Assert.IsTrue(columnas.All(c => c.Visible));
    }

    // ───────────────────────── Filas neutrales ─────────────────────────

    [TestMethod]
    public void ConstruirFilas_MapeaTextoYNumeros()
    {
        var actividad = new ActivityGridRowDto
        {
            Id = 5,
            Clave = "A-01",
            Descripcion = "Excavación",
            Unidad = "m3",
            PredecesoraResumen = "A-00 FS",
            CantidadTotal = 12.5m,
            DuracionDiasHabiles = 3,
            RendimientoDiario = 4.2m,
            FrentesTrabajo = 2,
            PrecioUnitario = 150.75m,
            ImporteTotal = 1884.375m,
            RutaCritica = true,
            Nivel = 1,
            Orden = 7,
        };

        var fila = new ProgramaObraReportSnapshotBuilder()
            .ConstruirFilas(new[] { actividad }, new GanttRenderModel())
            .Single();

        Assert.AreEqual(ProgramaRowKind.Actividad, fila.Kind);
        Assert.AreEqual(5, fila.ItemId);
        Assert.AreEqual(1, fila.Nivel);
        Assert.AreEqual(7, fila.Orden);
        Assert.AreEqual("A-01", fila.ObtenerTexto("colClave"));
        Assert.AreEqual("Excavación", fila.ObtenerTexto("colDescripcion"));
        Assert.AreEqual("m3", fila.ObtenerTexto("colUnidad"));
        Assert.AreEqual("A-00 FS", fila.ObtenerTexto("colPredecesora"));
        Assert.AreEqual("7", fila.ObtenerTexto("colOrden"));
        Assert.AreEqual("Sí", fila.ObtenerTexto("colRutaCritica"));

        Assert.IsTrue(fila.TryObtenerNumero("colCantidad", out var cantidad));
        Assert.AreEqual(12.5m, cantidad);
        Assert.IsTrue(fila.TryObtenerNumero("colImporte", out var importe));
        Assert.AreEqual(1884.375m, importe);
        Assert.IsTrue(fila.TryObtenerNumero("colPrecioUnitario", out var pu));
        Assert.AreEqual(150.75m, pu);
    }

    [TestMethod]
    public void ConstruirFilas_CorrelacionaConElGantt()
    {
        var actividad = new ActivityGridRowDto { Id = 5, EsResumen = false };
        var segmentos = new List<GanttPeriodSegmentDto>
        {
            new() { PeriodoProgramaId = 1, EtiquetaPeriodo = "Ene", ImporteProgramado = 100m }
        };
        var gantt = new GanttRenderModel
        {
            Filas =
            {
                new GanttRowDto
                {
                    Id = 5,
                    Inicio = new DateTime(2026, 1, 5),
                    Fin = new DateTime(2026, 1, 20),
                    EsCritica = true,
                    SegmentosFinancieros = segmentos
                }
            }
        };

        var fila = new ProgramaObraReportSnapshotBuilder()
            .ConstruirFilas(new[] { actividad }, gantt)
            .Single();

        Assert.AreEqual(new DateTime(2026, 1, 5), fila.Inicio);
        Assert.AreEqual(new DateTime(2026, 1, 20), fila.Fin);
        Assert.IsTrue(fila.EsCritica);
        Assert.AreEqual(1, fila.SegmentosFinancieros.Count);
        Assert.AreEqual(100m, fila.SegmentosFinancieros[0].ImporteProgramado);
    }

    [TestMethod]
    public void ConstruirFilas_Resumen_VaciaColumnasDeDetalle()
    {
        var resumen = new ActivityGridRowDto
        {
            Id = 1,
            EsResumen = true,
            CantidadTotal = 99m,
            RendimientoDiario = 9m,
            FrentesTrabajo = 9,
            PrecioUnitario = 9m,
            ImporteTotal = 9m,
            DuracionDiasHabiles = 12,
        };

        var fila = new ProgramaObraReportSnapshotBuilder()
            .ConstruirFilas(new[] { resumen }, new GanttRenderModel())
            .Single();

        foreach (var identificador in new[] { "colCantidad", "colRendimientoDiario", "colFrentes", "colPrecioUnitario", "colImporte" })
            Assert.IsFalse(fila.TryObtenerNumero(identificador, out _), $"{identificador} debe vaciarse en resumen.");

        Assert.IsTrue(fila.TryObtenerNumero("colDuracionDias", out var dias));
        Assert.AreEqual(12m, dias);
    }

    [TestMethod]
    public void ConstruirFilas_FormateaFechasConLaCulturaActual()
    {
        var inicio = new DateTime(2026, 3, 15);
        var fin = new DateTime(2026, 4, 1);
        var actividad = new ActivityGridRowDto
        {
            Id = 2,
            FechaInicioProgramada = inicio,
            FechaFinProgramada = fin,
            RutaCritica = false,
        };

        var fila = new ProgramaObraReportSnapshotBuilder()
            .ConstruirFilas(new[] { actividad }, new GanttRenderModel())
            .Single();

        Assert.AreEqual(inicio.ToString(CultureInfo.CurrentCulture), fila.ObtenerTexto("colFechaInicio"));
        Assert.AreEqual(fin.ToString(CultureInfo.CurrentCulture), fila.ObtenerTexto("colFechaFin"));
        Assert.AreEqual("No", fila.ObtenerTexto("colRutaCritica"));
    }

    [TestMethod]
    public void ConstruirFilas_ActividadesNulas_DevuelveVacio()
    {
        var filas = new ProgramaObraReportSnapshotBuilder().ConstruirFilas(null, new GanttRenderModel());
        Assert.AreEqual(0, filas.Count);
    }

    // ───────────────────────── Periodos y carga ─────────────────────────

    [TestMethod]
    public void ConstruirPeriodos_MapeaLaEscalaDelGantt()
    {
        var gantt = new GanttRenderModel
        {
            Escala =
            {
                new GanttScaleCellDto { Etiqueta = "Ene", GrupoEtiqueta = "2026", FechaInicio = new DateTime(2026, 1, 1), FechaFin = new DateTime(2026, 1, 31) },
                new GanttScaleCellDto { Etiqueta = "Feb", GrupoEtiqueta = "2026", FechaInicio = new DateTime(2026, 2, 1), FechaFin = new DateTime(2026, 2, 28) },
            }
        };

        var periodos = ProgramaObraReportSnapshotBuilder.ConstruirPeriodos(gantt);

        Assert.AreEqual(2, periodos.Count);
        Assert.AreEqual("Ene", periodos[0].Etiqueta);
        Assert.AreEqual("2026", periodos[0].Grupo);
        Assert.AreEqual(new DateTime(2026, 2, 1), periodos[1].FechaInicio);
    }

    [TestMethod]
    public void ConstruirDatos_IncluyeFilasYPeriodos()
    {
        var gantt = new GanttRenderModel
        {
            Escala = { new GanttScaleCellDto { Etiqueta = "Ene", FechaInicio = new DateTime(2026, 1, 1), FechaFin = new DateTime(2026, 1, 31) } }
        };
        var actividades = new[] { new ActivityGridRowDto { Id = 1, Clave = "A" } };

        var datos = new ProgramaObraReportSnapshotBuilder().ConstruirDatos(actividades, gantt);

        Assert.AreEqual(1, datos.Filas.Count);
        Assert.AreEqual(1, datos.Periodos.Count);
    }

    [TestMethod]
    public void ProgramaRow_ObtenerTextoYNumero_DevuelvenVacioSiNoExiste()
    {
        var fila = new ProgramaRow();
        Assert.IsNull(fila.ObtenerTexto("colNope"));
        Assert.IsFalse(fila.TryObtenerNumero("colNope", out var valor));
        Assert.AreEqual(0m, valor);
    }

    [TestMethod]
    public void ProgramaPeriodColumn_FromEscala_NormalizaNulos()
    {
        var columnas = ProgramaPeriodColumn.FromEscala(null);
        Assert.AreEqual(0, columnas.Count);
    }
}
