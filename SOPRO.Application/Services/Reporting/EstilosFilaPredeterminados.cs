using SOPRO.Application.Models.Reporting.ReportColumns;

namespace SOPRO.Application.Services.Reporting;

/// <summary>
/// Tabla de DEFAULTS NEUTRALES de estilo por ROL de fila del reporte de
/// Presupuesto. Es el ÚNICO origen de verdad de los colores/estilos de las filas
/// AGRUPADOR (<c>Capitulo</c>, <c>Subcapitulo</c>, <c>Nivel1..Nivel3</c>) y de las
/// filas de CIERRE (<c>Subtotal</c>, <c>Total</c>) que comparten los tres
/// renderers (grid, PDF y Excel). Reproduce los colores históricos del grid para
/// que el aspecto POR DEFECTO (sin configuración del usuario) no cambie.
///
/// La precedencia efectiva la materializa <c>ReportCellStyleResolver</c>:
/// override de celda &gt; estilo de rol del usuario &gt; DEFAULT de rol
/// (esta tabla) &gt; estilo de columna &gt; tabla. Como un override del usuario
/// puede ser PARCIAL (p. ej. sólo el color de fondo), aquí se COMBINA a nivel de
/// propiedad con el default (no se reemplaza el rol completo), de modo que las
/// propiedades no configuradas heredan el default.
///
/// No depende de UI, EF ni renderizado: sólo del contrato neutral
/// <see cref="ReportStyleOverlay"/> / <see cref="ReportRowRoleStyle"/>.
/// </summary>
public static class EstilosFilaPredeterminados
{
    /// <summary>Tipo de reporte estable al que aplican estos defaults.</summary>
    public const string TipoReportePresupuesto = "Presupuesto";

    /// <summary>Tipo de reporte estable del Cálculo de Indirectos.</summary>
    public const string TipoReporteIndirectos = "Indirectos";

    /// <summary>Superposición vacía: ninguna propiedad (todo hereda).</summary>
    private static readonly ReportStyleOverlay Vacio = new();

    /// <summary>
    /// Defaults del Presupuesto por rol de fila. Los colores reproducen el pintado
    /// histórico del grid (#800080 Capítulo, #ADD8E6 Subcapítulo, #D3D3D3 Niveles);
    /// las filas de cierre conservan los tonos azulados ya usados por PDF/Excel.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> PresupuestoDefaults =
        new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Capitulo] = new ReportStyleOverlay(ColorFondo: "#800080", ColorFuente: "#FFFFFF", Negrita: true),
            [RolFilaReporte.Subcapitulo] = new ReportStyleOverlay(ColorFondo: "#ADD8E6", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Nivel1] = new ReportStyleOverlay(ColorFondo: "#D3D3D3", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Nivel2] = new ReportStyleOverlay(ColorFondo: "#D3D3D3", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Nivel3] = new ReportStyleOverlay(ColorFondo: "#D3D3D3", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Subtotal] = new ReportStyleOverlay(ColorFondo: "#E3F2FD", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Total] = new ReportStyleOverlay(ColorFondo: "#BBDEFB", ColorFuente: "#000000", Negrita: true),
            // El concepto terminal no impone color: hereda el de su columna/tabla.
            [RolFilaReporte.Concepto] = Vacio,
        };

    /// <summary>Roles con default definido para el Presupuesto (en orden de la tabla).</summary>
    public static IReadOnlyList<string> RolesPresupuesto { get; }
        = PresupuestoDefaults.Keys.ToList();

    /// <summary>
    /// Default de estilo del rol para el Presupuesto, o una superposición vacía si
    /// el rol no tiene default (el consumidor hereda de niveles inferiores).
    /// </summary>
    public static ReportStyleOverlay Presupuesto(string? rolFila)
        => rolFila is not null && PresupuestoDefaults.TryGetValue(rolFila, out var overlay)
            ? overlay
            : Vacio;

    /// <summary>
    /// Defaults del Cálculo de Indirectos por rol de fila. Reproducen el pintado
    /// histórico: la fila de GRUPO del grid usaba el gris 230,230,230 (#E6E6E6) y
    /// negrita; las filas de cierre (Subtotal/Resumen) conservan los tonos
    /// azulados que ya usaban PDF y Excel. La entidad Concepto no impone color:
    /// hereda el de su columna/tabla. PDF y Excel convergen así al MISMO gris que
    /// el grid (antes divergían: ambos usaban #E8E8E8 para el grupo).
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> IndirectosDefaults =
        new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Grupo] = new ReportStyleOverlay(ColorFondo: "#E6E6E6", Negrita: true),
            [RolFilaReporte.Subtotal] = new ReportStyleOverlay(ColorFondo: "#E3F2FD", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Resumen] = new ReportStyleOverlay(ColorFondo: "#BBDEFB", ColorFuente: "#000000", Negrita: true),
            // El concepto terminal no impone color: hereda el de su columna/tabla.
            [RolFilaReporte.Concepto] = Vacio,
        };

    /// <summary>Roles con default definido para Indirectos (en orden de la tabla).</summary>
    public static IReadOnlyList<string> RolesIndirectos { get; }
        = IndirectosDefaults.Keys.ToList();

    /// <summary>
    /// Default de estilo del rol para Indirectos, o una superposición vacía si el
    /// rol no tiene default (el consumidor hereda de niveles inferiores).
    /// </summary>
    public static ReportStyleOverlay Indirectos(string? rolFila)
        => rolFila is not null && IndirectosDefaults.TryGetValue(rolFila, out var overlay)
            ? overlay
            : Vacio;

    /// <summary>
    /// Combina el default del rol con el override PARCIAL del usuario (si existe):
    /// cada propiedad no nula del usuario gana; las demás heredan el default.
    /// </summary>
    /// <param name="rolFila">Rol funcional de la fila.</param>
    /// <param name="estilosUsuario">Overrides de rol persistidos indexados por rol, o <c>null</c>.</param>
    public static ReportStyleOverlay CombinarPresupuesto(
        string? rolFila,
        IReadOnlyDictionary<string, ReportStyleOverlay>? estilosUsuario)
    {
        var baseStyle = Presupuesto(rolFila);
        if (rolFila is null || estilosUsuario is null || !estilosUsuario.TryGetValue(rolFila, out var superior))
            return baseStyle;
        return Combinar(baseStyle, superior);
    }

    /// <summary>
    /// Devuelve la lista de estilos de rol efectivos del Presupuesto: TODOS los
    /// defaults (combinados con el override del usuario cuando exista) más los roles
    /// persistidos que no tienen default (se conservan tal cual). Es la que se
    /// inyecta en <see cref="ReportColumnSnapshot.EstilosFila"/> antes de renderizar
    /// PDF/Excel.
    /// </summary>
    /// <param name="estilosUsuario">Estilos de rol persistidos, o <c>null</c> si no hay.</param>
    public static IReadOnlyList<ReportRowRoleStyle> CombinarPresupuesto(
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
        => CombinarRoles(PresupuestoDefaults, estilosUsuario);

    /// <summary>
    /// Combina el default del rol de Indirectos con el override PARCIAL del usuario
    /// (si existe): cada propiedad no nula del usuario gana; las demás heredan el
    /// default.
    /// </summary>
    /// <param name="rolFila">Rol funcional de la fila.</param>
    /// <param name="estilosUsuario">Overrides de rol persistidos indexados por rol, o <c>null</c>.</param>
    public static ReportStyleOverlay CombinarIndirectos(
        string? rolFila,
        IReadOnlyDictionary<string, ReportStyleOverlay>? estilosUsuario)
    {
        var baseStyle = Indirectos(rolFila);
        if (rolFila is null || estilosUsuario is null || !estilosUsuario.TryGetValue(rolFila, out var superior))
            return baseStyle;
        return Combinar(baseStyle, superior);
    }

    /// <summary>
    /// Devuelve la lista de estilos de rol efectivos de Indirectos: TODOS los
    /// defaults (combinados con el override del usuario cuando exista) más los roles
    /// persistidos que no tienen default (se conservan tal cual). Es la que se
    /// inyecta en <see cref="ReportColumnSnapshot.EstilosFila"/> antes de renderizar
    /// PDF/Excel.
    /// </summary>
    /// <param name="estilosUsuario">Estilos de rol persistidos, o <c>null</c> si no hay.</param>
    public static IReadOnlyList<ReportRowRoleStyle> CombinarIndirectos(
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
        => CombinarRoles(IndirectosDefaults, estilosUsuario);

    /// <summary>
    /// Núcleo común de combinación de una tabla de defaults con los estilos de rol
    /// persistidos: devuelve TODOS los defaults (con el override del usuario cuando
    /// exista) más los roles persistidos sin default (conservados tal cual).
    /// </summary>
    private static IReadOnlyList<ReportRowRoleStyle> CombinarRoles(
        IReadOnlyDictionary<string, ReportStyleOverlay> defaults,
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
    {
        var usuario = new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase);
        if (estilosUsuario is not null)
        {
            foreach (var rol in estilosUsuario)
            {
                if (rol is null || string.IsNullOrWhiteSpace(rol.RolFila)) continue;
                usuario[rol.RolFila.Trim()] = rol.Estilo;
            }
        }

        var resultado = new List<ReportRowRoleStyle>(defaults.Count + usuario.Count);
        foreach (var kv in defaults)
        {
            usuario.TryGetValue(kv.Key, out var superior);
            resultado.Add(new ReportRowRoleStyle(kv.Key, Combinar(kv.Value, superior)));
        }

        // Roles persistidos sin default (p. ej. de otros reportes): no se pierden.
        foreach (var kv in usuario)
        {
            if (!defaults.ContainsKey(kv.Key))
                resultado.Add(new ReportRowRoleStyle(kv.Key, kv.Value));
        }

        return resultado;
    }

    /// <summary>
    /// Combina el estilo base con una superposición superior propiedad a propiedad:
    /// un valor no nulo/no vacío del superior gana; en caso contrario se hereda el
    /// del base. Misma semántica de normalización que el resolver neutral.
    /// </summary>
    private static ReportStyleOverlay Combinar(ReportStyleOverlay baseStyle, ReportStyleOverlay? superior)
    {
        if (superior is null) return baseStyle;

        return baseStyle with
        {
            Fuente = string.IsNullOrWhiteSpace(superior.Fuente) ? baseStyle.Fuente : superior.Fuente.Trim(),
            TamanoFuente = superior.TamanoFuente is > 0 ? superior.TamanoFuente : baseStyle.TamanoFuente,
            Negrita = superior.Negrita ?? baseStyle.Negrita,
            Cursiva = superior.Cursiva ?? baseStyle.Cursiva,
            ColorFuente = string.IsNullOrWhiteSpace(superior.ColorFuente) ? baseStyle.ColorFuente : superior.ColorFuente,
            ColorFondo = string.IsNullOrWhiteSpace(superior.ColorFondo) ? baseStyle.ColorFondo : superior.ColorFondo,
            AlineacionHorizontal = superior.AlineacionHorizontal ?? baseStyle.AlineacionHorizontal,
            AlineacionVertical = superior.AlineacionVertical ?? baseStyle.AlineacionVertical,
            Wrap = superior.Wrap ?? baseStyle.Wrap,
            FormatoNumerico = string.IsNullOrWhiteSpace(superior.FormatoNumerico) ? baseStyle.FormatoNumerico : superior.FormatoNumerico,
        };
    }
}
