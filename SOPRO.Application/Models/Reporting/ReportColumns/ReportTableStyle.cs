namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Borde neutral de tabla/celda.
/// </summary>
/// <param name="Visible">Si el borde es visible.</param>
/// <param name="ColorHex">Color del borde en hexadecimal.</param>
/// <param name="GrosorPuntos">Grosor del borde en puntos.</param>
public sealed record ReportTableBorder(
    bool Visible,
    string ColorHex,
    double GrosorPuntos);

/// <summary>
/// Estilo neutral de tabla de reporte: encabezado, contenido, bandeado y bordes
/// por defecto. Las factories reproducen los valores actuales de cada reporte.
/// </summary>
/// <param name="EstiloEncabezado">Estilo por defecto de la fila de encabezados.</param>
/// <param name="EstiloContenido">Estilo por defecto de las celdas de datos.</param>
/// <param name="FilaAlterna">Bandeado de filas alternas.</param>
/// <param name="Bordes">Borde de la grilla.</param>
public sealed record ReportTableStyle(
    ReportTextStyle EstiloEncabezado,
    ReportTextStyle EstiloContenido,
    ReportRowStyle FilaAlterna,
    ReportTableBorder Bordes)
{
    /// <summary>
    /// Default neutral del reporte de Presupuesto, equivalente al comportamiento
    /// legacy: encabezado azul (#1565C0) con texto blanco, contenido blanco con
    /// texto negro, sin bandeado (cada fila adopta su propio fondo) y sin borde
    /// de grilla (sólo hairlines inferiores por celda).
    /// </summary>
    public static ReportTableStyle LegacyPresupuesto() => new(
        EstiloEncabezado: new ReportTextStyle(
            Fuente: "Segoe UI",
            Tamano: 9f,
            Negrita: true,
            Cursiva: false,
            ColorFuente: "#FFFFFF",
            ColorFondo: "#1565C0",
            ColorFuenteEncabezado: "#FFFFFF",
            ColorFondoEncabezado: "#1565C0"),
        EstiloContenido: new ReportTextStyle(
            Fuente: "Segoe UI",
            Tamano: 9f,
            Negrita: false,
            Cursiva: false,
            ColorFuente: "#000000",
            ColorFondo: "#FFFFFF"),
        FilaAlterna: new ReportRowStyle(
            ColorFondoAlterno: null,
            ColorFuente: "#000000"),
        Bordes: new ReportTableBorder(Visible: false, ColorHex: "#E3E7EB", GrosorPuntos: 0.22));

    /// <summary>
    /// Default neutral del reporte de Catálogo de Materiales, equivalente al
    /// comportamiento legacy: encabezado gris-azulado (#4A4A6A) con texto blanco,
    /// contenido blanco con texto negro, bandeado #F5F5F5 y borde de grilla
    /// #DDDDDD de 0.25 pt.
    /// </summary>
    public static ReportTableStyle LegacyMateriales() => new(
        EstiloEncabezado: new ReportTextStyle(
            Fuente: "Segoe UI",
            Tamano: 9f,
            Negrita: true,
            Cursiva: false,
            ColorFuente: "#FFFFFF",
            ColorFondo: "#4A4A6A",
            ColorFuenteEncabezado: "#FFFFFF",
            ColorFondoEncabezado: "#4A4A6A"),
        EstiloContenido: new ReportTextStyle(
            Fuente: "Segoe UI",
            Tamano: 9f,
            Negrita: false,
            Cursiva: false,
            ColorFuente: "#000000",
            ColorFondo: "#FFFFFF"),
        FilaAlterna: new ReportRowStyle(
            ColorFondoAlterno: "#F5F5F5",
            ColorFuente: "#000000"),
        Bordes: new ReportTableBorder(Visible: true, ColorHex: "#DDDDDD", GrosorPuntos: 0.25));

    /// <summary>
    /// Default neutral COMPARTIDO por los reportes de catálogo (Mano de Obra,
    /// Herramientas, Maquinaria/Costo Horario, Explosión, Indirectos,
    /// Financiamiento, Programas de Obra e Insumos). Reproduce el look común del
    /// catálogo ya validado (encabezado #4A4A6A, bandeado #F5F5F5 y grilla
    /// #DDDDDD) para que las migraciones de la Fase 1 tengan una única línea base
    /// mientras se confirma el estilo legacy específico de cada reporte. Es
    /// aditivo: no altera <see cref="LegacyPresupuesto"/> ni <see cref="LegacyMateriales"/>.
    /// </summary>
    public static ReportTableStyle LegacyCatalogo() => new(
        EstiloEncabezado: new ReportTextStyle(
            Fuente: "Segoe UI",
            Tamano: 9f,
            Negrita: true,
            Cursiva: false,
            ColorFuente: "#FFFFFF",
            ColorFondo: "#4A4A6A",
            ColorFuenteEncabezado: "#FFFFFF",
            ColorFondoEncabezado: "#4A4A6A"),
        EstiloContenido: new ReportTextStyle(
            Fuente: "Segoe UI",
            Tamano: 9f,
            Negrita: false,
            Cursiva: false,
            ColorFuente: "#000000",
            ColorFondo: "#FFFFFF"),
        FilaAlterna: new ReportRowStyle(
            ColorFondoAlterno: "#F5F5F5",
            ColorFuente: "#000000"),
        Bordes: new ReportTableBorder(Visible: true, ColorHex: "#DDDDDD", GrosorPuntos: 0.25));
}
