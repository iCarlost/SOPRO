using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Reporting.Formatting;

namespace SOPRO.Reporting.Tests.Formatting;

/// <summary>
/// Regresión del resolver NEUTRAL de estilos por celda/fila
/// (<see cref="ReportCellStyleResolver"/>): cascada única de precedencia
/// (celda &gt; rol de fila &gt; columna &gt; banding &gt; tabla), herencia parcial
/// con <c>null</c>, normalización de colores/tamaños y retrocompatibilidad de
/// snapshots sin estilos.
/// </summary>
[TestClass]
public class ReportCellStyleResolverTests
{
    // ─────────────────────────────── Fixtures ───────────────────────────────

    private static ReportTextStyle Texto(
        string fuente = "Segoe UI",
        float tamano = 9f,
        bool negrita = false,
        bool cursiva = false,
        string colorFuente = "#000000",
        string? colorFondo = "#FFFFFF")
        => new(fuente, tamano, negrita, cursiva, colorFuente, colorFondo);

    private static ReportTableStyle Tabla(string colorFondo, string? fondoAlterno = null)
        => new(
            EstiloEncabezado: Texto(negrita: true, colorFuente: "#FFFFFF", colorFondo: "#1565C0"),
            EstiloContenido: Texto(colorFuente: "#000000", colorFondo: colorFondo),
            FilaAlterna: new ReportRowStyle(fondoAlterno, "#000000"),
            Bordes: new ReportTableBorder(Visible: false, ColorHex: "#E3E7EB", GrosorPuntos: 0.22));

    private static ReportColumnDefinition Columna(
        string identificador,
        string? colorFondo = "#FFFFFF",
        ReportTextAlignment alineacion = ReportTextAlignment.Izquierda,
        bool wrap = false,
        string formato = "N2",
        float tamano = 9f,
        bool negrita = false,
        string colorFuente = "#000000")
        => new(
            Identificador: identificador,
            Encabezado: identificador,
            Visible: true,
            Orden: 0,
            Ancho: 100,
            EstiloEncabezado: Texto(negrita: true),
            EstiloContenido: Texto(tamano: tamano, negrita: negrita, colorFuente: colorFuente, colorFondo: colorFondo),
            Alineacion: alineacion,
            AlineacionVertical: ReportVerticalAlignment.Medio,
            Wrap: wrap,
            FormatoNumerico: formato,
            EsNumerica: true);

    private static ReportColumnSnapshot Snapshot(
        ReportTableStyle tabla,
        IReadOnlyList<ReportColumnDefinition> columnas,
        IReadOnlyCollection<ReportCellStyleOverride>? estilosCelda = null,
        IReadOnlyCollection<ReportRowRoleStyle>? estilosFila = null)
        => new("Presupuesto", 1, "T", columnas, tabla)
        {
            EstilosCelda = estilosCelda,
            EstilosFila = estilosFila
        };

    /// <summary>
    /// Snapshot con los CINCO niveles definidos con un fondo distinto:
    /// tabla #AAAAAA, banding #BBBBBB, columna #CCCCCC, rol #DDDDDD, celda #EEEEEE.
    /// </summary>
    private static (ReportColumnSnapshot Snapshot, ReportCellStyleOverride Celda, ReportRowRoleStyle Rol) FixturePrecedencia()
    {
        var tabla = Tabla(colorFondo: "#AAAAAA", fondoAlterno: "#BBBBBB");
        var columna = Columna("Importe", colorFondo: "#CCCCCC");
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress("Principal", "F1", "Importe"),
            new ReportStyleOverlay(ColorFondo: "#EEEEEE"));
        var rol = new ReportRowRoleStyle("Subtotal", new ReportStyleOverlay(ColorFondo: "#DDDDDD"));
        var snapshot = Snapshot(
            tabla,
            new[] { columna },
            estilosCelda: new[] { celda },
            estilosFila: new[] { rol });
        return (snapshot, celda, rol);
    }

    // ─────────────────────────── Precedencia ───────────────────────────

    [TestMethod]
    public void PrecedenciaCompleta_TodosLosNiveles_CeldaGana()
    {
        var (snapshot, _, _) = FixturePrecedencia();

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, "Principal", "F1", "Importe", rolFila: "Subtotal", esFilaAlterna: true);

        Assert.AreEqual("#EEEEEE", resultado.ColorFondo, "El override de celda tiene la máxima precedencia.");
    }

    [TestMethod]
    public void PrecedenciaSinCelda_RolGanaSobreColumnaBandingYTabla()
    {
        var tabla = Tabla("#AAAAAA", "#BBBBBB");
        var columna = Columna("Importe", colorFondo: "#CCCCCC");
        var rol = new ReportRowRoleStyle("Subtotal", new ReportStyleOverlay(ColorFondo: "#DDDDDD"));
        var snapshot = Snapshot(tabla, new[] { columna }, estilosFila: new[] { rol });

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, "Principal", "F1", "Importe", rolFila: "Subtotal", esFilaAlterna: true);

        Assert.AreEqual("#DDDDDD", resultado.ColorFondo, "Sin override de celda, gana el rol de fila.");
    }

    [TestMethod]
    public void PrecedenciaSinCeldaNiRol_ColumnaGanaSobreBandingYTabla()
    {
        var tabla = Tabla("#AAAAAA", "#BBBBBB");
        var columna = Columna("Importe", colorFondo: "#CCCCCC");
        var snapshot = Snapshot(tabla, new[] { columna });

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, "Principal", "F1", "Importe", rolFila: null, esFilaAlterna: true);

        Assert.AreEqual("#CCCCCC", resultado.ColorFondo,
            "Sin celda ni rol, gana la columna incluso sobre el banding.");
    }

    [TestMethod]
    public void PrecedenciaSinCeldaRolNiColumna_BandingGanaSobreTabla()
    {
        var tabla = Tabla("#AAAAAA", "#BBBBBB");
        var snapshot = Snapshot(tabla, Array.Empty<ReportColumnDefinition>());

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, "Principal", "F1", "Importe", rolFila: null, esFilaAlterna: true);

        Assert.AreEqual("#BBBBBB", resultado.ColorFondo, "El banding gana sobre el estilo de tabla.");
    }

    [TestMethod]
    public void PrecedenciaSoloTabla_DevuelveElEstiloDeContenidoDeLaTabla()
    {
        var tabla = Tabla("#AAAAAA", "#BBBBBB");
        var snapshot = Snapshot(tabla, Array.Empty<ReportColumnDefinition>());

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, "Principal", "F1", "Importe", rolFila: null, esFilaAlterna: false);

        Assert.AreEqual("#AAAAAA", resultado.ColorFondo, "Sin fila alterna, cae al estilo de tabla.");
    }

    // ─────────────────────────── Herencia parcial ───────────────────────────

    [TestMethod]
    public void HerenciaParcial_OverrideDeCeldaSoloConColorFondo_ConservaElEstiloDeColumna()
    {
        var tabla = Tabla("#FFFFFF");
        var columna = Columna(
            "PrecioUnitario", colorFondo: "#222222", tamano: 11f, negrita: true,
            colorFuente: "#111111", alineacion: ReportTextAlignment.Derecha, wrap: true);
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress("Principal", "F1", "PrecioUnitario"),
            new ReportStyleOverlay(ColorFondo: "#ABCDEF"));
        var snapshot = Snapshot(tabla, new[] { columna }, estilosCelda: new[] { celda });

        var resultado = ReportCellStyleResolver.Resolve(snapshot, "Principal", "F1", "PrecioUnitario");

        Assert.AreEqual("#ABCDEF", resultado.ColorFondo, "El único valor no nulo del override gana.");
        Assert.AreEqual("#111111", resultado.ColorFuente, "El color de texto se hereda de la columna.");
        Assert.AreEqual("Segoe UI", resultado.Fuente, "La tipografía se hereda de la columna.");
        Assert.AreEqual((float?)11f, resultado.TamanoFuente, "El tamaño se hereda de la columna.");
        Assert.AreEqual((bool?)true, resultado.Negrita, "La negrita se hereda de la columna.");
        Assert.AreEqual((bool?)false, resultado.Cursiva, "La cursiva se hereda de la columna.");
        Assert.AreEqual((ReportTextAlignment?)ReportTextAlignment.Derecha, resultado.AlineacionHorizontal);
        Assert.AreEqual((ReportVerticalAlignment?)ReportVerticalAlignment.Medio, resultado.AlineacionVertical);
        Assert.AreEqual((bool?)true, resultado.Wrap, "El wrap se hereda de la columna.");
    }

    [TestMethod]
    public void Resolve_TamanoFuenteInvalidoEnOverride_HeredaDeLaColumna()
    {
        var tabla = Tabla("#FFFFFF");
        var columna = Columna("Importe", tamano: 9f);
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress("Principal", "F1", "Importe"),
            new ReportStyleOverlay(TamanoFuente: 0f));
        var snapshot = Snapshot(tabla, new[] { columna }, estilosCelda: new[] { celda });

        var resultado = ReportCellStyleResolver.Resolve(snapshot, "Principal", "F1", "Importe");

        Assert.AreEqual((float?)9f, resultado.TamanoFuente, "Un tamaño no positivo se descarta y hereda.");
    }

    [TestMethod]
    public void Resolve_CeldaConVistaComodin_AplicaACualquierVista()
    {
        var tabla = Tabla("#FFFFFF");
        var columna = Columna("Importe", colorFondo: "#222222");
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress(string.Empty, "F1", "Importe"),
            new ReportStyleOverlay(ColorFondo: "#123456"));
        var snapshot = Snapshot(tabla, new[] { columna }, estilosCelda: new[] { celda });

        var resultado = ReportCellStyleResolver.Resolve(snapshot, "OtraVista", "F1", "Importe");

        Assert.AreEqual("#123456", resultado.ColorFondo, "Una vista vacía actúa como comodín.");
    }

    [TestMethod]
    public void Resolve_ConDireccionDeCelda_EquivaleALaFirmaPorComponentes()
    {
        var (snapshot, _, _) = FixturePrecedencia();

        var resultado = ReportCellStyleResolver.Resolve(
            snapshot, new ReportCellAddress("Principal", "F1", "Importe"), rolFila: "Subtotal", esFilaAlterna: true);

        Assert.AreEqual("#EEEEEE", resultado.ColorFondo);
    }

    [TestMethod]
    public void Resolve_SnapshotNulo_LanzaArgumentNullException()
    {
        Assert.ThrowsExactly<ArgumentNullException>(() =>
            ReportCellStyleResolver.Resolve(null!, "Principal", "F1", "Importe"));
    }

    // ─────────────────────────── Normalización ───────────────────────────

    [TestMethod]
    public void NormalizarColorHex_EntradaValida_SeAceptaYNormalizaAMayusculas()
    {
        Assert.AreEqual("#AABBCC", ReportCellStyleResolver.NormalizarColorHex("#aabbcc"));
        Assert.AreEqual("#AABBCC", ReportCellStyleResolver.NormalizarColorHex("  #AaBbCc  "));
        Assert.AreEqual("#AABBCCDD", ReportCellStyleResolver.NormalizarColorHex("#aabbccdd"));
    }

    [TestMethod]
    public void NormalizarColorHex_EntradaInvalida_DevuelveNull()
    {
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex(null));
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex(string.Empty));
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex("   "));
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex("red"));
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex("aabbcc"), "Sin '#' no es válido.");
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex("#12345"));
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex("#1234567"));
        Assert.IsNull(ReportCellStyleResolver.NormalizarColorHex("#GGGGGG"));
    }

    [TestMethod]
    public void Resolve_ColorInvalidoEnOverride_SeDescartaYHeredaDeLaColumna()
    {
        var tabla = Tabla("#FFFFFF");
        var columna = Columna("Importe", colorFondo: "#222222");
        var celda = new ReportCellStyleOverride(
            new ReportCellAddress("Principal", "F1", "Importe"),
            new ReportStyleOverlay(ColorFondo: "no-es-color"));
        var snapshot = Snapshot(tabla, new[] { columna }, estilosCelda: new[] { celda });

        var resultado = ReportCellStyleResolver.Resolve(snapshot, "Principal", "F1", "Importe");

        Assert.AreEqual("#222222", resultado.ColorFondo, "Un color inválido se descarta y hereda de la columna.");
    }

    [TestMethod]
    public void NormalizarTamanoFuente_ValoresNoPositivosONoFinitos_DevuelveNull()
    {
        Assert.IsNull(ReportCellStyleResolver.NormalizarTamanoFuente(null));
        Assert.IsNull(ReportCellStyleResolver.NormalizarTamanoFuente(0f));
        Assert.IsNull(ReportCellStyleResolver.NormalizarTamanoFuente(-3f));
        Assert.IsNull(ReportCellStyleResolver.NormalizarTamanoFuente(float.NaN));
        Assert.IsNull(ReportCellStyleResolver.NormalizarTamanoFuente(float.PositiveInfinity));
        Assert.AreEqual((float?)12.5f, ReportCellStyleResolver.NormalizarTamanoFuente(12.5f));
    }

    [TestMethod]
    public void NormalizarFuente_EntradasVacias_DevuelvenNull()
    {
        Assert.IsNull(ReportCellStyleResolver.NormalizarFuente(null));
        Assert.IsNull(ReportCellStyleResolver.NormalizarFuente(string.Empty));
        Assert.IsNull(ReportCellStyleResolver.NormalizarFuente("   "));
        Assert.AreEqual("Segoe UI", ReportCellStyleResolver.NormalizarFuente("  Segoe UI  "));
    }

    // ─────────────────── Retrocompatibilidad sin estilos ───────────────────

    [TestMethod]
    public void SnapshotSinEstilos_DevuelveElEstiloDeColumnaYTablaOriginal()
    {
        var tabla = Tabla("#FFFFFF");
        var columna = Columna(
            "Importe", colorFondo: "#123456", tamano: 10f, negrita: true,
            colorFuente: "#000000", alineacion: ReportTextAlignment.Derecha, wrap: true, formato: "C2");

        // Snapshot construido a la antigua: SIN las nuevas propiedades de estilos.
        var snapshot = new ReportColumnSnapshot("Presupuesto", 1, "T", new[] { columna }, tabla);

        Assert.IsNull(snapshot.EstilosCelda, "El snapshot legacy no define overrides de celda.");
        Assert.IsNull(snapshot.EstilosFila, "El snapshot legacy no define estilos de rol.");

        var resultado = ReportCellStyleResolver.Resolve(snapshot, "Principal", "F1", "Importe");

        Assert.AreEqual("#123456", resultado.ColorFondo);
        Assert.AreEqual("#000000", resultado.ColorFuente);
        Assert.AreEqual("Segoe UI", resultado.Fuente);
        Assert.AreEqual((float?)10f, resultado.TamanoFuente);
        Assert.AreEqual((bool?)true, resultado.Negrita);
        Assert.AreEqual((bool?)false, resultado.Cursiva);
        Assert.AreEqual((ReportTextAlignment?)ReportTextAlignment.Derecha, resultado.AlineacionHorizontal);
        Assert.AreEqual((ReportVerticalAlignment?)ReportVerticalAlignment.Medio, resultado.AlineacionVertical);
        Assert.AreEqual((bool?)true, resultado.Wrap);
        Assert.AreEqual("C2", resultado.FormatoNumerico);
    }

    [TestMethod]
    public void SnapshotSinEstilosSinColumna_CaeAlEstiloDeTabla()
    {
        var tabla = Tabla("#FFFFFF");
        var snapshot = new ReportColumnSnapshot("Presupuesto", 1, "T", Array.Empty<ReportColumnDefinition>(), tabla);

        var resultado = ReportCellStyleResolver.Resolve(snapshot, "Principal", "F1", "Importe");

        Assert.AreEqual("#FFFFFF", resultado.ColorFondo);
        Assert.AreEqual("#000000", resultado.ColorFuente);
        Assert.AreEqual("Segoe UI", resultado.Fuente);
        Assert.IsNull(resultado.AlineacionHorizontal, "Sin columna, la alineación no la aporta la tabla.");
    }
}
