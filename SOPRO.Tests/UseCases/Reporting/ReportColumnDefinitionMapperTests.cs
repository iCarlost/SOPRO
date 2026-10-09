using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.UseCases.Reporting;

/// <summary>
/// Verificación de precedencia del contrato neutral de columnas de reporte:
///   - Presupuesto: la <c>ColumnaPersonalizada</c> gana; <c>ConfigColumnaReporte</c>
///     sólo sobreescribe el estilo del encabezado.
///   - Alias: <c>Importe</c> se normaliza a <c>ImporteTotal</c>.
///   - Exclusión de columnas internas (<c>Tipo</c>, <c>colRelleno</c>).
///   - Materiales: <c>ColumnaMaterial</c> es la única fuente y usa el default neutral.
/// </summary>
[TestClass]
public class ReportColumnDefinitionMapperTests
{
    [TestMethod]
    public void Presupuesto_EntidadPrimaria_GanaSalvoEncabezadoOverlay()
    {
        var columna = new ColumnaPersonalizada
        {
            Nombre = "Descripcion entidad",
            NombreInterno = "Descripcion",
            Visible = false,
            Orden = 3,
            AnchoColumna = 250,
            WrapTexto = true,
            TipoDato = TipoDatoColumna.Texto,
            Alineacion = AlineacionColumna.Izquierda,
            NombreFuente = "Consolas",
            TamanoFuente = 12,
            ColorFuente = "#010203",
            ColorFondo = "#040506",
            Negrita = true,
            Cursiva = true
        };

        var overlay = new ConfigColumnaReporte
        {
            NombreInterno = "Descripcion",
            Encabezado = "ENCABEZADO DEL OVERLAY",
            EncFuente = "Arial",
            EncTamaño = 11f,
            EncNegrita = false,
            EncCursiva = true,
            EncColorFondo = "#123456",
            EncColorTexto = "#ABCDEF"
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(7, "Presupuesto X", new[] { columna }, new[] { overlay });

        var def = snapshot.Columnas.Single();

        // Entidad primaria: identificador, encabezado, visible, orden, ancho, wrap y contenido.
        Assert.AreEqual("Descripcion", def.Identificador);
        Assert.AreEqual("Descripcion entidad", def.Encabezado, "El encabezado lo aporta la entidad, no el overlay.");
        Assert.IsFalse(def.Visible);
        Assert.AreEqual(3, def.Orden);
        Assert.AreEqual(250, def.Ancho);
        Assert.IsTrue(def.Wrap);
        Assert.AreEqual("Consolas", def.EstiloContenido.Fuente);
        Assert.AreEqual(12f, def.EstiloContenido.Tamano);
        Assert.AreEqual("#010203", def.EstiloContenido.ColorFuente);
        Assert.AreEqual("#040506", def.EstiloContenido.ColorFondo);
        Assert.IsTrue(def.EstiloContenido.Negrita);
        Assert.IsTrue(def.EstiloContenido.Cursiva);

        // Overlay: sólo el estilo del encabezado.
        Assert.AreEqual("Arial", def.EstiloEncabezado.Fuente);
        Assert.AreEqual(11f, def.EstiloEncabezado.Tamano);
        Assert.IsFalse(def.EstiloEncabezado.Negrita);
        Assert.IsTrue(def.EstiloEncabezado.Cursiva);
        Assert.AreEqual("#ABCDEF", def.EstiloEncabezado.ColorFuente);
        Assert.AreEqual("#123456", def.EstiloEncabezado.ColorFondo);

        Assert.AreEqual(ReportTableStyle.LegacyPresupuesto().Bordes, snapshot.EstiloTabla.Bordes);
    }

    [TestMethod]
    public void Presupuesto_SinOverlay_UsaDefaultLegacyEncabezado()
    {
        var columna = new ColumnaPersonalizada { Nombre = "Clave", NombreInterno = "Clave" };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", new[] { columna });
        var def = snapshot.Columnas.Single();

        Assert.AreEqual("#1565C0", def.EstiloEncabezado.ColorFondo);
        Assert.AreEqual("#FFFFFF", def.EstiloEncabezado.ColorFuente);
        Assert.IsTrue(def.EstiloEncabezado.Negrita);
    }

    [TestMethod]
    public void Presupuesto_AliasImporte_SeNormalizaYMatchOverlayImporteTotal()
    {
        var columna = new ColumnaPersonalizada
        {
            Nombre = "Importe",
            NombreInterno = "Importe",
            TipoDato = TipoDatoColumna.Moneda,
            Alineacion = AlineacionColumna.Izquierda,
            AnchoColumna = 140,
            FormatoNumerico = "C2"
        };

        var overlay = new ConfigColumnaReporte
        {
            NombreInterno = "ImporteTotal",
            EncColorFondo = "#000000",
            EncColorTexto = "#EEEEEE"
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", new[] { columna }, new[] { overlay });
        var def = snapshot.Columnas.Single();

        Assert.AreEqual("ImporteTotal", def.Identificador, "El alias Importe debe normalizarse a ImporteTotal.");
        Assert.IsTrue(def.EsNumerica);
        Assert.AreEqual(ReportTextAlignment.Derecha, def.Alineacion, "Una columna numérica con alineación por defecto cae a Derecha.");
        Assert.AreEqual("C2", def.FormatoNumerico);
        Assert.AreEqual("#000000", def.EstiloEncabezado.ColorFondo, "El overlay ImporteTotal debe coincidir por identificador normalizado.");
    }

    [TestMethod]
    public void Presupuesto_ExcluyeColumnasInternas()
    {
        var columnas = new[]
        {
            new ColumnaPersonalizada { NombreInterno = "Tipo", Nombre = "Tipo", Orden = -1 },
            new ColumnaPersonalizada { NombreInterno = "colRelleno", Nombre = "", Orden = 99 },
            new ColumnaPersonalizada { NombreInterno = "Clave", Nombre = "Clave", Orden = 0 }
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", columnas);

        Assert.AreEqual(1, snapshot.Columnas.Count);
        Assert.AreEqual("Clave", snapshot.Columnas.Single().Identificador);
    }

    [TestMethod]
    public void Presupuesto_FormatoVacioNumerico_SeNormalizaAN2()
    {
        var columna = new ColumnaPersonalizada
        {
            NombreInterno = "Cantidad",
            Nombre = "Cantidad",
            TipoDato = TipoDatoColumna.Numerico,
            FormatoNumerico = ""
        };

        var snapshot = new PresupuestoReportSnapshotBuilder().Build(1, "P", new[] { columna });

        Assert.AreEqual("N2", snapshot.Columnas.Single().FormatoNumerico);
    }

    [TestMethod]
    public void Materiales_UsaDefaultLegacyYEntidadComoUnicaFuente()
    {
        var columnas = new[]
        {
            new ColumnaMaterial { Nombre = "Clave", NombreInterno = "Clave", Orden = 0, Alineacion = AlineacionColumna.Centro },
            new ColumnaMaterial { Nombre = "Precio Unitario", NombreInterno = "PrecioUnitario", Orden = 1, Alineacion = AlineacionColumna.Derecha, FormatoNumerico = "C4" },
            new ColumnaMaterial { Nombre = "Descripción", NombreInterno = "Descripcion", Orden = 2, Alineacion = AlineacionColumna.Izquierda, WrapTexto = true }
        };

        var snapshot = new MaterialesReportSnapshotBuilder().Build(5, "Materiales X", columnas);

        Assert.AreEqual("Materiales", snapshot.TipoReporte);
        Assert.AreEqual(3, snapshot.Columnas.Count);

        var precio = snapshot.Columnas.Single(c => c.Identificador == "PrecioUnitario");
        Assert.IsTrue(precio.EsNumerica);
        Assert.AreEqual(ReportTextAlignment.Derecha, precio.Alineacion);
        Assert.AreEqual("C4", precio.FormatoNumerico);

        var descripcion = snapshot.Columnas.Single(c => c.Identificador == "Descripcion");
        Assert.IsFalse(descripcion.EsNumerica);
        Assert.AreEqual(ReportTextAlignment.Izquierda, descripcion.Alineacion);
        Assert.IsTrue(descripcion.Wrap);

        // Encabezado neutral LegacyMateriales.
        Assert.AreEqual("#4A4A6A", snapshot.EstiloTabla.EstiloEncabezado.ColorFondo);
        Assert.AreEqual("#FFFFFF", snapshot.EstiloTabla.EstiloEncabezado.ColorFuente);
        Assert.AreEqual("#F5F5F5", snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno);
        Assert.IsTrue(snapshot.EstiloTabla.Bordes.Visible);
        Assert.AreEqual("#DDDDDD", snapshot.EstiloTabla.Bordes.ColorHex);
    }
}
