using System;
using System.Collections.Generic;
using SOPRO.Application.Models.Reporting.ReportColumns;

namespace SOPRO.Reporting.Formatting;

/// <summary>
/// Combina los estilos de una celda de reporte en UNA sola cascada de
/// precedencia, neutral y sin dependencias de UI/renderizado (ni System.Drawing,
/// ni WinForms, ni MigraDoc, ni ClosedXML). La precedencia, de mayor a menor:
/// <list type="number">
///   <item>override de celda (<see cref="ReportColumnSnapshot.EstilosCelda"/>);</item>
///   <item>estilo semántico de fila por rol (<see cref="ReportColumnSnapshot.EstilosFila"/>);</item>
///   <item>estilo de la columna (<see cref="ReportColumnDefinition"/>);</item>
///   <item>fila alterna / banding (<see cref="ReportTableStyle.FilaAlterna"/>);</item>
///   <item>estilo de tabla (<see cref="ReportTableStyle.EstiloContenido"/>).</item>
/// </list>
/// En cada nivel, una propiedad <c>null</c> hereda del nivel inferior y un valor
/// no nulo gana. Los colores se normalizan a "#RRGGBB"/"#AARRGGBB" en mayúsculas
/// y los valores inválidos se descartan (se comportan como <c>null</c>).
/// </summary>
public static class ReportCellStyleResolver
{
    /// <summary>
    /// Resuelve el estilo efectivo de la celda (<paramref name="claveFila"/>,
    /// <paramref name="nombreColumna"/>) dentro de <paramref name="claveVista"/>.
    /// El resultado es un <see cref="ReportStyleOverlay"/> con los valores
    /// ganadores; las propiedades que ningún nivel aportó quedan en <c>null</c>.
    /// </summary>
    /// <param name="snapshot">Snapshot de columnas y estilos del reporte.</param>
    /// <param name="claveVista">Vista/sección lógica (comodín si va vacía).</param>
    /// <param name="claveFila">Clave estable de la fila.</param>
    /// <param name="nombreColumna">Identificador de la columna.</param>
    /// <param name="rolFila">Rol funcional de la fila, o <c>null</c> si no aplica.</param>
    /// <param name="esFilaAlterna">Si la fila es la banda alterna de la tabla.</param>
    public static ReportStyleOverlay Resolve(
        ReportColumnSnapshot snapshot,
        string claveVista,
        string claveFila,
        string nombreColumna,
        string? rolFila = null,
        bool esFilaAlterna = false)
    {
        ArgumentNullException.ThrowIfNull(snapshot);

        // Nivel más bajo: estilo de contenido de la tabla.
        var estilo = DesdeTexto(snapshot.EstiloTabla.EstiloContenido);

        // Banding: sólo aplica en la fila alterna y cuando la tabla lo define.
        if (esFilaAlterna && snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno is not null)
        {
            estilo = Aplicar(estilo, new ReportStyleOverlay(
                ColorFondo: snapshot.EstiloTabla.FilaAlterna.ColorFondoAlterno,
                ColorFuente: snapshot.EstiloTabla.FilaAlterna.ColorFuente));
        }

        // Estilo de la columna.
        var columna = BuscarColumna(snapshot, nombreColumna);
        if (columna is not null)
            estilo = Aplicar(estilo, DesdeColumna(columna));

        // Estilo semántico por rol de fila.
        var rol = BuscarRol(snapshot, rolFila);
        if (rol is not null)
            estilo = Aplicar(estilo, rol.Estilo);

        // Override puntual de celda: máxima precedencia.
        var celda = BuscarCelda(snapshot, claveVista, claveFila, nombreColumna);
        if (celda is not null)
            estilo = Aplicar(estilo, celda.Estilo);

        return estilo;
    }

    /// <summary>
    /// Sobrecarga de conveniencia que toma la referencia estable de la celda.
    /// </summary>
    public static ReportStyleOverlay Resolve(
        ReportColumnSnapshot snapshot,
        ReportCellAddress direccion,
        string? rolFila = null,
        bool esFilaAlterna = false)
    {
        ArgumentNullException.ThrowIfNull(direccion);
        return Resolve(snapshot, direccion.ClaveVista, direccion.ClaveFila, direccion.NombreColumna, rolFila, esFilaAlterna);
    }

    /// <summary>
    /// Combina dos niveles ADYACENTES de la cascada con la MISMA semántica que
    /// <see cref="Resolve"/>: <paramref name="superior"/> gana propiedad a propiedad
    /// y un valor <c>null</c> (o inválido) hereda de <paramref name="baseStyle"/>.
    ///
    /// Es el punto ÚNICO para recomponer niveles FUERA de un
    /// <see cref="ReportColumnSnapshot"/>: el grid ya trae el estilo de columna en la
    /// celda y sólo necesita fundir el estilo EFECTIVO de rol con el override
    /// puntual de celda, de modo que la precedencia NO se duplique en la UI.
    /// </summary>
    /// <param name="baseStyle">Nivel inferior (se hereda), o <c>null</c> para vacío.</param>
    /// <param name="superior">Nivel superior (gana), o <c>null</c> para no cambiar nada.</param>
    public static ReportStyleOverlay Combinar(
        ReportStyleOverlay? baseStyle,
        ReportStyleOverlay? superior)
        => Aplicar(baseStyle ?? new ReportStyleOverlay(), superior);

    /// <summary>
    /// Normaliza un color a "#RRGGBB" o "#AARRGGBB" en mayúsculas. Devuelve
    /// <c>null</c> si la entrada es nula, vacía o no es un hexadecimal válido
    /// precedido por '#'. Un <c>null</c> en la cascada significa "heredar".
    /// </summary>
    public static string? NormalizarColorHex(string? colorHex)
    {
        if (string.IsNullOrWhiteSpace(colorHex)) return null;

        var valor = colorHex.Trim();
        if (valor.Length == 0 || valor[0] != '#') return null;

        var hex = valor[1..];
        if (hex.Length != 6 && hex.Length != 8) return null;

        foreach (var c in hex)
        {
            if (!Uri.IsHexDigit(c)) return null;
        }

        return "#" + hex.ToUpperInvariant();
    }

    /// <summary>
    /// Normaliza el tamaño de fuente: devuelve <c>null</c> para valores nulos, no
    /// finitos o no positivos (se tratan como "heredar"); en otro caso devuelve el
    /// valor tal cual.
    /// </summary>
    public static float? NormalizarTamanoFuente(float? tamano)
    {
        if (tamano is null) return null;
        var valor = tamano.Value;
        if (!float.IsFinite(valor) || valor <= 0f) return null;
        return valor;
    }

    /// <summary>
    /// Normaliza el nombre de la tipografía: recorta espacios y devuelve
    /// <c>null</c> para entradas nulas o en blanco (se tratan como "heredar").
    /// </summary>
    public static string? NormalizarFuente(string? fuente)
        => string.IsNullOrWhiteSpace(fuente) ? null : fuente.Trim();

    // ─────────────────────────── Búsqueda de niveles ───────────────────────────

    private static ReportColumnDefinition? BuscarColumna(ReportColumnSnapshot snapshot, string nombreColumna)
    {
        if (string.IsNullOrWhiteSpace(nombreColumna)) return null;
        foreach (var columna in snapshot.Columnas)
        {
            if (columna is not null && Coincide(columna.Identificador, nombreColumna))
                return columna;
        }
        return null;
    }

    private static ReportRowRoleStyle? BuscarRol(ReportColumnSnapshot snapshot, string? rolFila)
    {
        if (string.IsNullOrWhiteSpace(rolFila)) return null;
        var roles = snapshot.EstilosFila;
        if (roles is null) return null;

        foreach (var rol in roles)
        {
            if (rol is not null && Coincide(rol.RolFila, rolFila))
                return rol;
        }
        return null;
    }

    private static ReportCellStyleOverride? BuscarCelda(
        ReportColumnSnapshot snapshot,
        string claveVista,
        string claveFila,
        string nombreColumna)
    {
        var overrides = snapshot.EstilosCelda;
        if (overrides is null || overrides.Count == 0) return null;

        ReportCellStyleOverride? comodin = null;
        foreach (var overrideCelda in overrides)
        {
            if (overrideCelda?.Direccion is null) continue;
            var direccion = overrideCelda.Direccion;
            if (!Coincide(direccion.NombreColumna, nombreColumna)) continue;
            if (!Coincide(direccion.ClaveFila, claveFila)) continue;

            // Vista exacta gana; una vista vacía actúa como comodín.
            if (Coincide(direccion.ClaveVista, claveVista)) return overrideCelda;
            if (string.IsNullOrEmpty(direccion.ClaveVista)) comodin = overrideCelda;
        }

        return comodin;
    }

    // ─────────────────────────── Conversión y cascada ───────────────────────────

    private static ReportStyleOverlay DesdeTexto(ReportTextStyle estilo) => new(
        Fuente: estilo.Fuente,
        TamanoFuente: estilo.Tamano,
        Negrita: estilo.Negrita,
        Cursiva: estilo.Cursiva,
        ColorFuente: estilo.ColorFuente,
        ColorFondo: estilo.ColorFondo);

    private static ReportStyleOverlay DesdeColumna(ReportColumnDefinition columna) => new(
        Fuente: columna.EstiloContenido.Fuente,
        TamanoFuente: columna.EstiloContenido.Tamano,
        Negrita: columna.EstiloContenido.Negrita,
        Cursiva: columna.EstiloContenido.Cursiva,
        ColorFuente: columna.EstiloContenido.ColorFuente,
        ColorFondo: columna.EstiloContenido.ColorFondo,
        AlineacionHorizontal: columna.Alineacion,
        AlineacionVertical: columna.AlineacionVertical,
        Wrap: columna.Wrap,
        FormatoNumerico: columna.FormatoNumerico);

    private static ReportStyleOverlay Aplicar(ReportStyleOverlay baseStyle, ReportStyleOverlay superior)
        => baseStyle with
        {
            Fuente = NormalizarFuente(superior.Fuente) ?? baseStyle.Fuente,
            TamanoFuente = NormalizarTamanoFuente(superior.TamanoFuente) ?? baseStyle.TamanoFuente,
            Negrita = superior.Negrita ?? baseStyle.Negrita,
            Cursiva = superior.Cursiva ?? baseStyle.Cursiva,
            ColorFuente = NormalizarColorHex(superior.ColorFuente) ?? baseStyle.ColorFuente,
            ColorFondo = NormalizarColorHex(superior.ColorFondo) ?? baseStyle.ColorFondo,
            AlineacionHorizontal = superior.AlineacionHorizontal ?? baseStyle.AlineacionHorizontal,
            AlineacionVertical = superior.AlineacionVertical ?? baseStyle.AlineacionVertical,
            Wrap = superior.Wrap ?? baseStyle.Wrap,
            FormatoNumerico = string.IsNullOrWhiteSpace(superior.FormatoNumerico)
                ? baseStyle.FormatoNumerico
                : superior.FormatoNumerico
        };

    private static bool Coincide(string? a, string? b)
        => string.Equals(a ?? string.Empty, b ?? string.Empty, StringComparison.OrdinalIgnoreCase);
}
