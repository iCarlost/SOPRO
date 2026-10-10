using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Models.Reporting.Programa;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="ProgramaInsumosReportSnapshotBuilder" /> y de los
/// modelos neutrales del Programa de Insumos (<see cref="ProgramaRow" />,
/// <see cref="ProgramaPeriodColumn" /> y <see cref="ProgramaReportData" />,
/// reutilizados del paso 4.1). El builder produce snapshots deterministas con las
/// columnas estáticas más las series dinámicas por período, y materializa las filas
/// desde los datos neutrales + <see cref="GanttRenderModel"/> sin tocar el grid.
/// </summary>
[TestClass]
public class ProgramaInsumosReportSnapshotBuilderTests
{
    private static readonly ReportTableStyle EstiloEsperado = ReportTableStyle.LegacyCatalogo();

    private static readonly string[] IdentificadoresCanonicosMixto =
    {
        "colClave", "colDescripcion", "colUnidad", "colInicio", "colTermino", "colDondeSeUsa",
        "colPU", "colTotal", "colImporteTotal",
        "per_501", "acu_501", "imp_501", "iacu_501",
        "per_502", "acu_502", "imp_502", "iacu_502"
    };

    private static ProgramaInsumosResultDto Programa() => new()
    {
        NombrePrograma = "Programa de Insumos Sintético",
        Tipo = ProgramaInsumoTipo.Materiales,
        Periodos =
        {
            new ProgramaInsumoPeriodoDto { PeriodoId = 501, Orden = 1, Etiqueta = "Ene" },
            new ProgramaInsumoPeriodoDto { PeriodoId = 502, Orden = 2, Etiqueta = "Feb" },
        }
    };

    // ───────────────────────── Snapshot ─────────────────────────

    [TestMethod]
    public void Build_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(7, "Insumos", Programa(), ProgramaInsumosVista.Mixto);

        Assert.AreEqual(ProgramaInsumosReportSnapshotBuilder.TipoReporte, snapshot.TipoReporte);
        Assert.AreEqual("ProgramaInsumos", snapshot.TipoReporte);
        Assert.AreEqual(7, snapshot.ProyectoId);
        Assert.AreEqual("Insumos", snapshot.Titulo);
        Assert.AreEqual(17, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void Build_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(1, null, Programa(), ProgramaInsumosVista.Mixto);

        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void Build_SinPrograma_UsaPredeterminadasEstaticasEnMemoria()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder().Build(1, "M", null, ProgramaInsumosVista.Mixto);

        CollectionAssert.AreEqual(
            new[] { "colClave", "colDescripcion", "colUnidad", "colInicio", "colTermino", "colDondeSeUsa", "colPU", "colTotal", "colImporteTotal" },
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray());
    }

    [TestMethod]
    public void Build_UsaEstiloLegacyCatalogo()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(1, "M", Programa(), ProgramaInsumosVista.Mixto);

        Assert.AreEqual(EstiloEsperado, snapshot.EstiloTabla);
    }

    [TestMethod]
    public void Build_MarcaRolesDeCantidadEImporte()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(1, "M", Programa(), ProgramaInsumosVista.Mixto);

        var pu = snapshot.Columnas.Single(c => c.Identificador == "colPU");
        Assert.IsTrue(pu.EsNumerica);
        Assert.IsTrue(pu.EsMoneda);

        var importeTotal = snapshot.Columnas.Single(c => c.Identificador == "colImporteTotal");
        Assert.IsTrue(importeTotal.EsMoneda);

        // "Total" es cantidad (suma de cantidades), no importe.
        var total = snapshot.Columnas.Single(c => c.Identificador == "colTotal");
        Assert.IsTrue(total.EsNumerica);
        Assert.IsFalse(total.EsMoneda);

        var cantidad = snapshot.Columnas.Single(c => c.Identificador == "per_501");
        Assert.IsTrue(cantidad.EsNumerica);
        Assert.IsFalse(cantidad.EsMoneda);

        var importe = snapshot.Columnas.Single(c => c.Identificador == "imp_501");
        Assert.IsTrue(importe.EsMoneda);

        Assert.IsFalse(snapshot.Columnas.Single(c => c.Identificador == "colDescripcion").EsNumerica);
    }

    [TestMethod]
    public void Build_ConTipoMaquinaria_IncluyeRendimiento()
    {
        var programa = Programa();
        programa.Tipo = ProgramaInsumoTipo.Maquinaria;

        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(1, "M", programa, ProgramaInsumosVista.Mixto);

        var rendimiento = snapshot.Columnas.Single(c => c.Identificador == "colRendimiento");
        Assert.IsTrue(rendimiento.EsNumerica);
        Assert.IsFalse(rendimiento.EsMoneda);
    }

    [TestMethod]
    public void Build_VistaCantidades_OmiteColumnasDeImporte()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(1, "M", Programa(), ProgramaInsumosVista.Cantidades);

        var ids = snapshot.Columnas.Select(c => c.Identificador).ToList();
        CollectionAssert.DoesNotContain(ids, "colPU");
        CollectionAssert.DoesNotContain(ids, "colImporteTotal");
        CollectionAssert.DoesNotContain(ids, "imp_501");
        CollectionAssert.Contains(ids, "per_501");
        CollectionAssert.Contains(ids, "acu_501");
    }

    [TestMethod]
    public void Build_VistaImportes_OmiteColumnasDeCantidad()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(1, "M", Programa(), ProgramaInsumosVista.Importes);

        var ids = snapshot.Columnas.Select(c => c.Identificador).ToList();
        CollectionAssert.DoesNotContain(ids, "per_501");
        CollectionAssert.DoesNotContain(ids, "acu_501");
        CollectionAssert.Contains(ids, "imp_501");
        CollectionAssert.Contains(ids, "iacu_501");
        CollectionAssert.Contains(ids, "colPU");
    }

    [TestMethod]
    public void Build_PropagaDecimalesExplicitos()
    {
        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(1, "M", Programa(), ProgramaInsumosVista.Mixto, 3, 4, 6);

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

        var snapshot = new ProgramaInsumosReportSnapshotBuilder()
            .Build(proyecto, "M", Programa(), ProgramaInsumosVista.Mixto);

        Assert.AreEqual(42, snapshot.ProyectoId);
        Assert.AreEqual(4, snapshot.DecimalesCantidad);
        Assert.AreEqual(1, snapshot.DecimalesImporte);
        Assert.AreEqual(5, snapshot.DecimalesPorcentaje);
    }

    [TestMethod]
    public void Build_ProyectoNulo_LanzaArgumentNullException()
    {
        var builder = new ProgramaInsumosReportSnapshotBuilder();
        Assert.ThrowsExactly<ArgumentNullException>(
            () => builder.Build(null!, "M", Programa(), ProgramaInsumosVista.Mixto));
    }

    [TestMethod]
    public void ColumnasPredeterminadas_ExponenLasColumnasCanonicasMixto()
    {
        var columnas = ProgramaInsumosReportSnapshotBuilder.ColumnasPredeterminadas(Programa(), ProgramaInsumosVista.Mixto);

        CollectionAssert.AreEqual(IdentificadoresCanonicosMixto, columnas.Select(c => c.NombreInterno).ToArray());
        Assert.IsTrue(columnas.All(c => c.Visible));
    }

    // ───────────────────────── Filas neutrales ─────────────────────────

    [TestMethod]
    public void ConstruirFilas_MapeaTextoNumerosYSeriesPorPeriodo()
    {
        var programa = Programa();
        programa.Rows.Add(new ProgramaInsumoRowDto
        {
            InsumoId = 5,
            Clave = "MAT-01",
            Descripcion = "Cemento",
            Unidad = "ton",
            FechaInicio = new DateTime(2026, 1, 5),
            FechaFin = new DateTime(2026, 2, 20),
            DondeSeUsa = "C-01",
            PrecioUnitario = 150.75m,
            Total = 12.5m,
            ImporteTotal = 1884.375m,
            CantidadesPorPeriodo = { [501] = 4m, [502] = 8.5m },
            AcumuladosPorPeriodo = { [501] = 4m, [502] = 12.5m },
            ImportesPorPeriodo = { [501] = 603m, [502] = 1281.375m },
            ImportesAcumuladosPorPeriodo = { [501] = 603m, [502] = 1884.375m },
        });

        var fila = new ProgramaInsumosReportSnapshotBuilder()
            .ConstruirFilas(programa, new GanttRenderModel())
            .Single();

        Assert.AreEqual(ProgramaRowKind.Actividad, fila.Kind);
        Assert.AreEqual(5, fila.ItemId);
        Assert.AreEqual("MAT-01", fila.ObtenerTexto("colClave"));
        Assert.AreEqual("Cemento", fila.ObtenerTexto("colDescripcion"));
        Assert.AreEqual("ton", fila.ObtenerTexto("colUnidad"));
        Assert.AreEqual("C-01", fila.ObtenerTexto("colDondeSeUsa"));
        Assert.AreEqual(new DateTime(2026, 1, 5).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.CurrentCulture), fila.ObtenerTexto("colInicio"));
        Assert.AreEqual(new DateTime(2026, 2, 20).ToString("dd/MM/yyyy", System.Globalization.CultureInfo.CurrentCulture), fila.ObtenerTexto("colTermino"));

        Assert.IsTrue(fila.TryObtenerNumero("colPU", out var pu));
        Assert.AreEqual(150.75m, pu);
        Assert.IsTrue(fila.TryObtenerNumero("colTotal", out var total));
        Assert.AreEqual(12.5m, total);
        Assert.IsTrue(fila.TryObtenerNumero("imp_502", out var imp502));
        Assert.AreEqual(1281.375m, imp502);
        Assert.IsTrue(fila.TryObtenerNumero("acu_502", out var acu502));
        Assert.AreEqual(12.5m, acu502);
    }

    [TestMethod]
    public void ConstruirFilas_CerosDeSeriesSeOmiten()
    {
        var programa = Programa();
        programa.Rows.Add(new ProgramaInsumoRowDto
        {
            InsumoId = 9,
            CantidadesPorPeriodo = { [501] = 0m, [502] = 3m },
            ImportesPorPeriodo = { [501] = 0m, [502] = 100m },
        });

        var fila = new ProgramaInsumosReportSnapshotBuilder()
            .ConstruirFilas(programa, new GanttRenderModel())
            .Single();

        Assert.IsFalse(fila.TryObtenerNumero("per_501", out _), "El cero se omite (celda vacía).");
        Assert.IsTrue(fila.TryObtenerNumero("per_502", out var per502));
        Assert.AreEqual(3m, per502);
        Assert.IsFalse(fila.TryObtenerNumero("imp_501", out _));
    }

    [TestMethod]
    public void ConstruirFilas_CorrelacionaConElGantt()
    {
        var programa = Programa();
        programa.Rows.Add(new ProgramaInsumoRowDto { InsumoId = 5 });
        var segmentos = new List<GanttPeriodSegmentDto>
        {
            new() { PeriodoProgramaId = 501, EtiquetaPeriodo = "Ene", ImporteProgramado = 100m }
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
                    EsCritica = false,
                    SegmentosFinancieros = segmentos
                }
            }
        };

        var fila = new ProgramaInsumosReportSnapshotBuilder()
            .ConstruirFilas(programa, gantt)
            .Single();

        Assert.AreEqual(new DateTime(2026, 1, 5), fila.Inicio);
        Assert.AreEqual(new DateTime(2026, 1, 20), fila.Fin);
        Assert.AreEqual(1, fila.SegmentosFinancieros.Count);
        Assert.AreEqual(100m, fila.SegmentosFinancieros[0].ImporteProgramado);
    }

    [TestMethod]
    public void ConstruirFilas_ProgramaNulo_DevuelveVacio()
    {
        var filas = new ProgramaInsumosReportSnapshotBuilder().ConstruirFilas(null, new GanttRenderModel());
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

        var periodos = ProgramaInsumosReportSnapshotBuilder.ConstruirPeriodos(gantt);

        Assert.AreEqual(2, periodos.Count);
        Assert.AreEqual("Ene", periodos[0].Etiqueta);
        Assert.AreEqual("2026", periodos[0].Grupo);
        Assert.AreEqual(new DateTime(2026, 2, 1), periodos[1].FechaInicio);
    }

    [TestMethod]
    public void ConstruirDatos_IncluyeFilasYPeriodos()
    {
        var programa = Programa();
        programa.Rows.Add(new ProgramaInsumoRowDto { InsumoId = 1, Clave = "A" });
        var gantt = new GanttRenderModel
        {
            Escala = { new GanttScaleCellDto { Etiqueta = "Ene", FechaInicio = new DateTime(2026, 1, 1), FechaFin = new DateTime(2026, 1, 31) } }
        };

        var datos = new ProgramaInsumosReportSnapshotBuilder().ConstruirDatos(programa, gantt);

        Assert.AreEqual(1, datos.Filas.Count);
        Assert.AreEqual(1, datos.Periodos.Count);
    }

    [TestMethod]
    public void IdentificadoresDePeriodo_SiguenElPrefijoCanonico()
    {
        Assert.AreEqual("per_7", ProgramaInsumosReportSnapshotBuilder.IdentificadorPeriodo(7));
        Assert.AreEqual("acu_7", ProgramaInsumosReportSnapshotBuilder.IdentificadorAcumulado(7));
        Assert.AreEqual("imp_7", ProgramaInsumosReportSnapshotBuilder.IdentificadorImporte(7));
        Assert.AreEqual("iacu_7", ProgramaInsumosReportSnapshotBuilder.IdentificadorImporteAcumulado(7));
    }
}
