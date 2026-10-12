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

    // ──────────────────────── EsMoneda (paridad grid) ────────────────────────

    [TestMethod]
    public void MapearPresupuesto_EsMonedaPorTipoDatoOToken()
    {
        var monedaPorTipo = new ColumnaPersonalizada
        {
            NombreInterno = "PrecioUnitario",
            TipoDato = TipoDatoColumna.Moneda,
            FormatoNumerico = "C4"
        };
        var monedaPorToken = new ColumnaPersonalizada
        {
            NombreInterno = "Extra",
            TipoDato = TipoDatoColumna.Numerico,
            FormatoNumerico = "C2"
        };
        var cantidad = new ColumnaPersonalizada
        {
            NombreInterno = "Cantidad",
            TipoDato = TipoDatoColumna.Numerico,
            FormatoNumerico = "N2"
        };
        var explicito = new ColumnaPersonalizada
        {
            NombreInterno = "Rendimiento",
            TipoDato = TipoDatoColumna.Numerico,
            FormatoNumerico = "#,##0.0000"
        };
        var monedaSinFormato = new ColumnaPersonalizada
        {
            NombreInterno = "Importe",
            TipoDato = TipoDatoColumna.Moneda,
            FormatoNumerico = ""
        };

        Assert.IsTrue(ReportColumnDefinitionMapper.MapearPresupuesto(monedaPorTipo, null).EsMoneda,
            "TipoDato Moneda debe marcar EsMoneda.");
        Assert.IsTrue(ReportColumnDefinitionMapper.MapearPresupuesto(monedaPorToken, null).EsMoneda,
            "Token 'C2' (case-insensitive) debe marcar EsMoneda.");
        Assert.IsFalse(ReportColumnDefinitionMapper.MapearPresupuesto(cantidad, null).EsMoneda,
            "Una columna Cantidad con token 'N2' no es moneda.");
        Assert.IsFalse(ReportColumnDefinitionMapper.MapearPresupuesto(explicito, null).EsMoneda,
            "Un formato explícito '#,##0.0000' no es moneda.");
        Assert.IsTrue(ReportColumnDefinitionMapper.MapearPresupuesto(monedaSinFormato, null).EsMoneda,
            "TipoDato Moneda con formato normalizado a N2 sigue siendo moneda.");
    }

    [TestMethod]
    public void MapearMaterial_EsMonedaPorPrecioUnitarioOTokenC()
    {
        var precioC4 = new ColumnaMaterial
        {
            NombreInterno = "PrecioUnitario",
            Nombre = "Precio Unitario",
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "C4"
        };
        var precioDefault = new ColumnaMaterial
        {
            NombreInterno = "PrecioUnitario",
            Nombre = "Precio Unitario",
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "#,##0.0000"
        };
        var otroExplicito = new ColumnaMaterial
        {
            NombreInterno = "Extra",
            Nombre = "Extra",
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "#,##0.0000"
        };

        Assert.IsTrue(ReportColumnDefinitionMapper.MapearMaterial(precioC4).EsMoneda,
            "El token 'C4' de Materiales debe marcar EsMoneda.");
        Assert.IsTrue(ReportColumnDefinitionMapper.MapearMaterial(precioDefault).EsMoneda,
            "El Precio Unitario es monetario aunque use el default '#,##0.0000' (paridad con el grid).");
        Assert.IsFalse(ReportColumnDefinitionMapper.MapearMaterial(otroExplicito).EsMoneda,
            "Otra columna con formato explícito '#,##0.0000' NO es moneda.");
    }

    // ───────────── Fase 0: mapeos de los catálogos restantes ─────────────

    [TestMethod]
    public void MapearManoObra_RolMonetarioPorNombreYEstiloCatalogo()
    {
        var salario = new ColumnaManoObra
        {
            Nombre = "Salario Base",
            NombreInterno = "SalarioBase",
            Orden = 1,
            AnchoColumna = 90,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = string.Empty,
        };

        var def = ReportColumnDefinitionMapper.MapearManoObra(salario);

        Assert.AreEqual("SalarioBase", def.Identificador);
        Assert.IsTrue(def.EsNumerica, "Alineación derecha ⇒ numérica.");
        Assert.IsTrue(def.EsMoneda, "El Salario Base es un rol monetario canónico.");
        Assert.AreEqual("#4A4A6A", def.EstiloEncabezado.ColorFondo, "Debe usar el estilo neutral de catálogo.");
        Assert.AreEqual("#F5F5F5", ReportTableStyle.LegacyCatalogo().FilaAlterna.ColorFondoAlterno);
    }

    [TestMethod]
    public void MapearHerramienta_MonedaPorTokenC()
    {
        var col = new ColumnaHerramienta
        {
            Nombre = "Precio Unitario",
            NombreInterno = "PrecioUnitario",
            Orden = 2,
            AnchoColumna = 110,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "C4",
        };

        var def = ReportColumnDefinitionMapper.MapearHerramienta(col);

        Assert.AreEqual("PrecioUnitario", def.Identificador);
        Assert.IsTrue(def.EsNumerica);
        Assert.IsTrue(def.EsMoneda, "El token 'C4' marca rol monetario.");
        Assert.AreEqual("C4", def.FormatoNumerico);
    }

    [TestMethod]
    public void MapearMaquinaria_CostoHorarioEsMonetarioYPorcentajeNoLoEs()
    {
        var costo = new ColumnaMaquinaria
        {
            NombreInterno = "CostoHorario",
            Nombre = "Costo Horario",
            Orden = 1,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = string.Empty,
        };
        var porcentaje = new ColumnaMaquinaria
        {
            NombreInterno = "PorcentajeIndirectos",
            Nombre = "% Indirectos",
            Orden = 2,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "P2",
        };

        var defCosto = ReportColumnDefinitionMapper.MapearMaquinaria(costo);
        var defPorc = ReportColumnDefinitionMapper.MapearMaquinaria(porcentaje);

        Assert.IsTrue(defCosto.EsMoneda, "Costo Horario es rol monetario canónico.");
        Assert.IsFalse(defPorc.EsMoneda, "Un token 'P2' no es moneda.");
        Assert.IsTrue(defPorc.EsNumerica);
    }

    [TestMethod]
    public void MapearExplosion_ImporteEsMonetario()
    {
        var importe = new ColumnaExplosion
        {
            NombreInterno = "Importe",
            Nombre = "Importe",
            Orden = 3,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = string.Empty,
        };

        var def = ReportColumnDefinitionMapper.MapearExplosion(importe);

        Assert.AreEqual("ImporteTotal", def.Identificador, "El alias Importe se normaliza.");
        Assert.IsTrue(def.EsMoneda, "Importe es rol monetario canónico.");
    }

    [TestMethod]
    public void MapearIndirectos_ImporteTotalEsMonetario()
    {
        var col = new ColumnaIndirectos
        {
            NombreInterno = "ImporteTotal",
            Nombre = "Importe total",
            Orden = 1,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = string.Empty,
        };

        Assert.IsTrue(ReportColumnDefinitionMapper.MapearIndirectos(col).EsMoneda);
    }

    [TestMethod]
    public void MapearFinanciamiento_TotalEsMonetario()
    {
        var col = new ColumnaFinanciamiento
        {
            NombreInterno = "Total",
            Nombre = "Total",
            Orden = 1,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = string.Empty,
        };

        Assert.IsTrue(ReportColumnDefinitionMapper.MapearFinanciamiento(col).EsMoneda);
    }

    [TestMethod]
    public void MapearProgramaObra_ToleraPrefijoColYReconoceRolMonetario()
    {
        var importe = new ColumnaProgramaObra
        {
            NombreInterno = "colImporte",
            Nombre = "Importe",
            Orden = 13,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = string.Empty,
        };
        var descripcion = new ColumnaProgramaObra
        {
            NombreInterno = "colDescripcion",
            Nombre = "Descripción",
            Orden = 3,
            Alineacion = AlineacionColumna.Izquierda,
        };

        var defImporte = ReportColumnDefinitionMapper.MapearProgramaObra(importe);
        var defDesc = ReportColumnDefinitionMapper.MapearProgramaObra(descripcion);

        Assert.AreEqual("colImporte", defImporte.Identificador, "El identificador conserva el nombre interno.");
        Assert.IsTrue(defImporte.EsMoneda, "El prefijo 'col' no debe impedir el reconocimiento del rol.");
        Assert.IsFalse(defDesc.EsNumerica);
        Assert.IsFalse(defDesc.EsMoneda);
    }

    [TestMethod]
    public void MapearProgramaInsumos_CantidadEsNumericaNoMonetaria()
    {
        var col = new ColumnaProgramaInsumos
        {
            NombreInterno = "colCantidad",
            Nombre = "Cantidad",
            Orden = 6,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "N2",
        };

        var def = ReportColumnDefinitionMapper.MapearProgramaInsumos(col);

        Assert.IsTrue(def.EsNumerica);
        Assert.IsFalse(def.EsMoneda);
        Assert.AreEqual("N2", def.FormatoNumerico);
    }

    // ───────────── Fase 0: reconocimiento común de roles ─────────────

    [TestMethod]
    public void ReconocimientoDeRoles_ComunATodosLosCatalogos()
    {
        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolMonetario("PrecioUnitario", null));
        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolMonetario("colImporte", null));
        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolMonetario("Desconocido", "C4"));
        Assert.IsFalse(ReportColumnDefinitionMapper.EsRolMonetario("Descripcion", "#,##0.0000"));

        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolPorcentaje("PorcentajeIndirectos", null));
        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolPorcentaje("X", "P2"));
        Assert.IsFalse(ReportColumnDefinitionMapper.EsRolPorcentaje("ImporteTotal", null));

        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolCantidad("Cantidad", null));
        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolCantidad("colCantidad", null));
        Assert.IsTrue(ReportColumnDefinitionMapper.EsRolCantidad("X", "N2"));
        Assert.IsFalse(ReportColumnDefinitionMapper.EsRolCantidad("Descripcion", null));
    }

    [TestMethod]
    public void MapearCatalogo_EsCoherenteConMapearMaterialParaTokenC()
    {
        // El núcleo compartido debe producir la MISMA definición neutral (salvo
        // que el predicado monetario difiere): token 'C4' ⇒ monetaria en ambos.
        var material = ReportColumnDefinitionMapper.MapearMaterial(new ColumnaMaterial
        {
            NombreInterno = "Extra",
            Nombre = "Extra",
            Orden = 1,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "C4",
        });
        var manoObra = ReportColumnDefinitionMapper.MapearManoObra(new ColumnaManoObra
        {
            NombreInterno = "Extra",
            Nombre = "Extra",
            Orden = 1,
            Alineacion = AlineacionColumna.Derecha,
            FormatoNumerico = "C4",
        });

        Assert.AreEqual(material, manoObra,
            "Con los mismos campos y un token 'C4', ambos catálogos deben coincidir.");
    }
}
