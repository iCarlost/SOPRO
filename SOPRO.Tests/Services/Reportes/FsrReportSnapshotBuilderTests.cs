using System;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.Fsr;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Verificación del <see cref="FsrReportSnapshotBuilder" /> y de los modelos
/// neutrales del FSR (<see cref="FsrRow" /> y <see cref="FsrTabuladorRow" />).
/// El builder produce snapshots deterministas del AE-2(A) (4 columnas) y del
/// AE-2(C) (21 columnas) sin tocar grids ni persistir configuración; los
/// modelos de fila sustituyen a los DTOs anidados de los generadores.
/// </summary>
[TestClass]
public class FsrReportSnapshotBuilderTests
{
    private static readonly ReportTableStyle EstiloEsperado = ReportTableStyle.LegacyCatalogo();

    // ───────────────────────── Metadatos AE-2(A) ─────────────────────────

    [TestMethod]
    public void BuildAE2A_ProduceSnapshotConTipoProyectoYTitulo()
    {
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2A(7, "Tabla FSR");

        Assert.AreEqual(FsrReportSnapshotBuilder.TipoReporteAE2A, snapshot.TipoReporte);
        Assert.AreEqual("FactorSalarioReal", snapshot.TipoReporte);
        Assert.AreEqual(7, snapshot.ProyectoId);
        Assert.AreEqual("Tabla FSR", snapshot.Titulo);
        Assert.AreEqual(4, snapshot.Columnas.Count);
    }

    [TestMethod]
    public void BuildAE2A_TituloNulo_SeNormalizaAVacio()
    {
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2A(1, null);
        Assert.AreEqual(string.Empty, snapshot.Titulo);
    }

    [TestMethod]
    public void BuildAE2A_ColumnasCanonicasEnOrden()
    {
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2A(1, "M");

        CollectionAssert.AreEqual(
            new[] { "Descripcion", "Operacion", "Unidad", "Valor" },
            snapshot.Columnas.OrderBy(c => c.Orden).Select(c => c.Identificador).ToArray());

        var valor = snapshot.Columnas.Single(c => c.Identificador == "Valor");
        Assert.IsTrue(valor.EsNumerica);
        Assert.IsFalse(valor.EsMoneda);
        Assert.AreEqual("N5", valor.FormatoNumerico);
        Assert.AreEqual(ReportTextAlignment.Derecha, valor.Alineacion);
    }

    // ───────────────────────── Metadatos AE-2(C) ─────────────────────────

    [TestMethod]
    public void BuildAE2C_ProduceVeintiunColumnasCanonicas()
    {
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2C(7, "Tabulador");

        Assert.AreEqual(FsrReportSnapshotBuilder.TipoReporteAE2C, snapshot.TipoReporte);
        Assert.AreEqual("TabuladorFsr", snapshot.TipoReporte);
        Assert.AreEqual(21, snapshot.Columnas.Count);

        var ordenadas = snapshot.Columnas.OrderBy(c => c.Orden).ToList();
        Assert.AreEqual("Clave", ordenadas.First().Identificador);
        Assert.AreEqual("SalarioReal", ordenadas.Last().Identificador);
        Assert.AreEqual(0, ordenadas.First().Orden);
        Assert.AreEqual(20, ordenadas.Last().Orden);
    }

    [TestMethod]
    public void BuildAE2C_MarcaMonetariasSalarioBaseYSalarioReal()
    {
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2C(1, "M");

        var salarioBase = snapshot.Columnas.Single(c => c.Identificador == "SalarioBase");
        Assert.IsTrue(salarioBase.EsNumerica);
        Assert.IsTrue(salarioBase.EsMoneda);
        Assert.AreEqual("C2", salarioBase.FormatoNumerico);

        var salarioReal = snapshot.Columnas.Single(c => c.Identificador == "SalarioReal");
        Assert.IsTrue(salarioReal.EsMoneda);

        foreach (var id in new[] { "SalarioNominal", "FactorSbc", "CuotaFija", "Fsr" })
        {
            var col = snapshot.Columnas.Single(c => c.Identificador == id);
            Assert.IsTrue(col.EsNumerica, $"{id} es numérica.");
            Assert.IsFalse(col.EsMoneda, $"{id} no es monetaria (cantidad).");
            Assert.AreEqual("N5", col.FormatoNumerico);
        }
    }

    [TestMethod]
    public void BuildAE2C_ClaveYDescripcionSonTexto()
    {
        var snapshot = new FsrReportSnapshotBuilder().BuildAE2C(1, "M");

        Assert.IsFalse(snapshot.Columnas.Single(c => c.Identificador == "Clave").EsNumerica);
        Assert.IsFalse(snapshot.Columnas.Single(c => c.Identificador == "Descripcion").EsNumerica);
    }

    // ───────────────────────── Estilo y decimales ─────────────────────────

    [TestMethod]
    public void Build_UsaEstiloLegacyCatalogo()
    {
        var builder = new FsrReportSnapshotBuilder();
        Assert.AreEqual(EstiloEsperado, builder.BuildAE2A(1, "M").EstiloTabla);
        Assert.AreEqual(EstiloEsperado, builder.BuildAE2C(1, "M").EstiloTabla);
    }

    [TestMethod]
    public void Build_SinDecimalesExplicitos_UsaDefaultsDelFsr()
    {
        var builder = new FsrReportSnapshotBuilder();
        var ae2a = builder.BuildAE2A(1, "M");
        var ae2c = builder.BuildAE2C(1, "M");

        Assert.AreEqual(FsrReportSnapshotBuilder.DecimalesCantidadFsr, ae2a.DecimalesCantidad);
        Assert.AreEqual(5, ae2a.DecimalesCantidad);
        Assert.AreEqual(2, ae2a.DecimalesImporte);
        Assert.AreEqual(4, ae2a.DecimalesPorcentaje);
        Assert.AreEqual(5, ae2c.DecimalesCantidad);
    }

    [TestMethod]
    public void Build_PropagaDecimalesExplicitos()
    {
        var snapshot = new FsrReportSnapshotBuilder()
            .BuildAE2C(1, "M", decimalesCantidad: 3, decimalesImporte: 4, decimalesPorcentaje: 6);

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

        var builder = new FsrReportSnapshotBuilder();
        var ae2a = builder.BuildAE2A(proyecto, "M");
        var ae2c = builder.BuildAE2C(proyecto, "M");

        Assert.AreEqual(42, ae2a.ProyectoId);
        Assert.AreEqual(4, ae2a.DecimalesCantidad);
        Assert.AreEqual(1, ae2a.DecimalesImporte);
        Assert.AreEqual(5, ae2a.DecimalesPorcentaje);
        Assert.AreEqual(42, ae2c.ProyectoId);
        Assert.AreEqual(4, ae2c.DecimalesCantidad);
    }

    [TestMethod]
    public void Build_ProyectoNulo_LanzaArgumentNullException()
    {
        var builder = new FsrReportSnapshotBuilder();
        Assert.ThrowsExactly<ArgumentNullException>(() => builder.BuildAE2A(null!, "M"));
        Assert.ThrowsExactly<ArgumentNullException>(() => builder.BuildAE2C(null!, "M"));
    }

    [TestMethod]
    public void DefaultColumns_ExponenLasMismasDefiniciones()
    {
        Assert.AreEqual(4, FsrReportSnapshotBuilder.DefaultColumnsAE2A().Count);
        Assert.AreEqual(21, FsrReportSnapshotBuilder.DefaultColumnsAE2C().Count);
        Assert.AreEqual("Valor", FsrReportSnapshotBuilder.DefaultColumnsAE2A().Single(c => c.Identificador == "Valor").Identificador);
    }

    // ───────────────────────── Modelos de fila ─────────────────────────

    [TestMethod]
    public void FsrRow_FactoriesConstruyenFilasCorrectas()
    {
        var seccion = FsrRow.Seccion("DATOS");
        Assert.AreEqual(FsrRowType.Seccion, seccion.Tipo);
        Assert.AreEqual("DATOS", seccion.Descripcion);

        var sub = FsrRow.Subseccion("Sub");
        Assert.AreEqual(FsrRowType.Subseccion, sub.Tipo);

        var numero = FsrRow.Numero("Días", "op", "días", 12.5m);
        Assert.AreEqual(FsrRowType.Dato, numero.Tipo);
        Assert.AreEqual(12.5m, numero.Valor);
        Assert.IsNull(numero.ValorTexto);

        var texto = FsrRow.Texto("Límite", "", "", "19,378");
        Assert.AreEqual("19,378", texto.ValorTexto);
        Assert.IsNull(texto.Valor);
    }

    [TestMethod]
    public void FsrTabuladorRow_ValoresAlineadosConColumnasNumericas()
    {
        var fila = new FsrTabuladorRow
        {
            Clave = "MO-01",
            Descripcion = "Peón",
            Valores = Enumerable.Range(1, 19).Select(i => (decimal)i).ToArray(),
        };

        Assert.AreEqual("MO-01", fila.Clave);
        Assert.AreEqual(19, fila.Valores.Count);
        Assert.IsFalse(fila.SinCalculo);

        var sinCalculo = new FsrTabuladorRow { Clave = "MO-02", SinCalculo = true };
        Assert.IsTrue(sinCalculo.SinCalculo);
        Assert.AreEqual(0, sinCalculo.Valores.Count);
    }
}
