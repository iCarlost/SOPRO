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

    /// <summary>Tipo de reporte estable del Programa de Obra.</summary>
    public const string TipoReporteProgramaObra = "ProgramaObra";

    /// <summary>Tipo de reporte estable del Programa de Insumos.</summary>
    public const string TipoReporteProgramaInsumos = "ProgramaInsumos";

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

    /// <summary>Tipo de reporte estable de la Explosión de Insumos.</summary>
    public const string TipoReporteExplosion = "Explosion";

    /// <summary>
    /// Defaults de la Explosión de Insumos por rol de fila. Reproducen el pintado
    /// histórico del grid de explosión: banda de sección azul acero (#4682B4), fila
    /// de Total de sección gris claro (#F0F0F0) con negrita, fila de Total General
    /// oscura (#2D2D30) con texto blanco, y filas de Referencia gris claro
    /// (#F0F0F0) con texto gris (#646464) en cursiva. Al centralizarlos aquí, PDF y
    /// Excel CONVERGEN a los mismos colores del grid (antes divergían: usaban
    /// #37474F para la sección y tonos azulados para los totales). La fila terminal
    /// (Detalle) no impone color: hereda el de su columna/tabla.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> ExplosionDefaults =
        new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Encabezado] = new ReportStyleOverlay(ColorFondo: "#4682B4", ColorFuente: "#FFFFFF", Negrita: true, TamanoFuente: 9.5f),
            [RolFilaReporte.Total] = new ReportStyleOverlay(ColorFondo: "#F0F0F0", Negrita: true, TamanoFuente: 9f),
            [RolFilaReporte.TotalGeneral] = new ReportStyleOverlay(ColorFondo: "#2D2D30", ColorFuente: "#FFFFFF", Negrita: true, TamanoFuente: 10f),
            [RolFilaReporte.Referencia] = new ReportStyleOverlay(ColorFondo: "#F0F0F0", ColorFuente: "#646464", Cursiva: true, TamanoFuente: 8.5f),
            // El detalle terminal no impone color: hereda el de su columna/tabla.
            [RolFilaReporte.Concepto] = Vacio,
        };

    /// <summary>Roles con default definido para la Explosión (en orden de la tabla).</summary>
    public static IReadOnlyList<string> RolesExplosion { get; }
        = ExplosionDefaults.Keys.ToList();

    /// <summary>
    /// Default de estilo del rol para la Explosión, o una superposición vacía si el
    /// rol no tiene default (el consumidor hereda de niveles inferiores).
    /// </summary>
    public static ReportStyleOverlay Explosion(string? rolFila)
        => rolFila is not null && ExplosionDefaults.TryGetValue(rolFila, out var overlay)
            ? overlay
            : Vacio;

    /// <summary>
    /// Combina el default del rol de Explosión con el override PARCIAL del usuario
    /// (si existe): cada propiedad no nula del usuario gana; las demás heredan el
    /// default.
    /// </summary>
    /// <param name="rolFila">Rol funcional de la fila.</param>
    /// <param name="estilosUsuario">Overrides de rol persistidos indexados por rol, o <c>null</c>.</param>
    public static ReportStyleOverlay CombinarExplosion(
        string? rolFila,
        IReadOnlyDictionary<string, ReportStyleOverlay>? estilosUsuario)
    {
        var baseStyle = Explosion(rolFila);
        if (rolFila is null || estilosUsuario is null || !estilosUsuario.TryGetValue(rolFila, out var superior))
            return baseStyle;
        return Combinar(baseStyle, superior);
    }

    /// <summary>
    /// Devuelve la lista de estilos de rol efectivos de la Explosión: TODOS los
    /// defaults (combinados con el override del usuario cuando exista) más los roles
    /// persistidos que no tienen default (se conservan tal cual). Es la que se
    /// inyecta en <see cref="ReportColumnSnapshot.EstilosFila"/> antes de renderizar
    /// PDF/Excel.
    /// </summary>
    /// <param name="estilosUsuario">Estilos de rol persistidos, o <c>null</c> si no hay.</param>
    public static IReadOnlyList<ReportRowRoleStyle> CombinarExplosion(
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
        => CombinarRoles(ExplosionDefaults, estilosUsuario);

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
    /// Defaults de los PROGRAMAS (Obra e Insumos) por rol de fila. Reproducen el
    /// pintado histórico del grid: las filas AGRUPADOR/RESUMEN usaban el gris
    /// 240,240,240 (#F0F0F0) y negrita; las filas de actividad/concepto no imponen
    /// color alguno (heredan el de su columna/tabla). Es la MISMA tabla para ambos
    /// programas, de modo que grid, PDF y Excel convergen.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> ProgramasDefaults =
        new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Resumen] = new ReportStyleOverlay(ColorFondo: "#F0F0F0", Negrita: true),
            // La fila de actividad/concepto no impone color: hereda su columna/tabla.
            [RolFilaReporte.Concepto] = Vacio,
        };

    /// <summary>Roles con default definido para los Programas (en orden de la tabla).</summary>
    public static IReadOnlyList<string> RolesProgramas { get; }
        = ProgramasDefaults.Keys.ToList();

    /// <summary>
    /// Default de estilo del rol para los Programas, o una superposición vacía si el
    /// rol no tiene default (el consumidor hereda de niveles inferiores).
    /// </summary>
    public static ReportStyleOverlay Programas(string? rolFila)
        => rolFila is not null && ProgramasDefaults.TryGetValue(rolFila, out var overlay)
            ? overlay
            : Vacio;

    /// <summary>
    /// Combina el default del rol de Programa con el override PARCIAL del usuario
    /// (si existe): cada propiedad no nula del usuario gana; las demás heredan el
    /// default.
    /// </summary>
    /// <param name="rolFila">Rol funcional de la fila.</param>
    /// <param name="estilosUsuario">Overrides de rol persistidos indexados por rol, o <c>null</c>.</param>
    public static ReportStyleOverlay CombinarProgramas(
        string? rolFila,
        IReadOnlyDictionary<string, ReportStyleOverlay>? estilosUsuario)
    {
        var baseStyle = Programas(rolFila);
        if (rolFila is null || estilosUsuario is null || !estilosUsuario.TryGetValue(rolFila, out var superior))
            return baseStyle;
        return Combinar(baseStyle, superior);
    }

    /// <summary>
    /// Devuelve la lista de estilos de rol efectivos de los Programas: TODOS los
    /// defaults (combinados con el override del usuario cuando exista) más los roles
    /// persistidos que no tienen default (se conservan tal cual). Es la que se
    /// inyecta en <see cref="ReportColumnSnapshot.EstilosFila"/> antes de renderizar
    /// PDF/Excel.
    /// </summary>
    /// <param name="estilosUsuario">Estilos de rol persistidos, o <c>null</c> si no hay.</param>
    public static IReadOnlyList<ReportRowRoleStyle> CombinarProgramas(
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
        => CombinarRoles(ProgramasDefaults, estilosUsuario);

    // ══════════════════ Financiamiento (paso 6.4) ══════════════════

    /// <summary>Tipo de reporte estable del Análisis de Financiamiento.</summary>
    public const string TipoReporteFinanciamiento = "Financiamiento";

    /// <summary>
    /// Defaults del Análisis de Financiamiento por rol de fila. Reproducen el
    /// pintado histórico de la matriz: las filas de SECCIÓN (INGRESOS/EGRESOS) iban
    /// sobre azul tenue #D9E2F3; la fila de ENCABEZADO de períodos con el
    /// gris-azulado #4A4A6A y texto blanco; y la fila de RESULTADO (% de
    /// financiamiento) sobre el amarillo tenue #FFF2CC. Grid, PDF y Excel convergen
    /// a los MISMOS valores.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> FinanciamientoDefaults =
        new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Seccion] = new ReportStyleOverlay(ColorFondo: "#D9E2F3", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Encabezado] = new ReportStyleOverlay(ColorFondo: "#4A4A6A", ColorFuente: "#FFFFFF", Negrita: true),
            [RolFilaReporte.TotalGeneral] = new ReportStyleOverlay(ColorFondo: "#FFF2CC", ColorFuente: "#000000", Negrita: true),
        };

    /// <summary>Roles con default definido para Financiamiento (en orden de la tabla).</summary>
    public static IReadOnlyList<string> RolesFinanciamiento { get; }
        = FinanciamientoDefaults.Keys.ToList();

    /// <summary>
    /// Default de estilo del rol para Financiamiento, o una superposición vacía si el
    /// rol no tiene default (el consumidor hereda de niveles inferiores).
    /// </summary>
    public static ReportStyleOverlay Financiamiento(string? rolFila)
        => rolFila is not null && FinanciamientoDefaults.TryGetValue(rolFila, out var overlay)
            ? overlay
            : Vacio;

    /// <summary>Combina el default de Financiamiento con el override PARCIAL del usuario.</summary>
    public static ReportStyleOverlay CombinarFinanciamiento(
        string? rolFila,
        IReadOnlyDictionary<string, ReportStyleOverlay>? estilosUsuario)
    {
        var baseStyle = Financiamiento(rolFila);
        if (rolFila is null || estilosUsuario is null || !estilosUsuario.TryGetValue(rolFila, out var superior))
            return baseStyle;
        return Combinar(baseStyle, superior);
    }

    /// <summary>Lista de estilos de rol efectivos de Financiamiento (defaults + usuario).</summary>
    public static IReadOnlyList<ReportRowRoleStyle> CombinarFinanciamiento(
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
        => CombinarRoles(FinanciamientoDefaults, estilosUsuario);

    // ══════════════════ Utilidad (paso 6.4) ══════════════════

    /// <summary>Tipo de reporte estable del reporte de Utilidad.</summary>
    public const string TipoReporteUtilidad = "Utilidad";

    /// <summary>
    /// Defaults del reporte de Utilidad por rol de fila. Reproducen el pintado
    /// histórico del resumen: la fila SUBTOTAL sobre amarillo tenue #FFF2CC con
    /// texto #C00000; la fila del IMPORTE DE UTILIDAD sobre verde #92D050; y la
    /// UTILIDAD NETA ESTIMADA sobre azul tenue #DDEBF7. PDF y Excel convergen.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> UtilidadDefaults =
        new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Subtotal] = new ReportStyleOverlay(ColorFondo: "#FFF2CC", ColorFuente: "#C00000", Negrita: true),
            [RolFilaReporte.Total] = new ReportStyleOverlay(ColorFondo: "#92D050", ColorFuente: "#000000", Negrita: true),
            [RolFilaReporte.Resumen] = new ReportStyleOverlay(ColorFondo: "#DDEBF7", ColorFuente: "#000000", Negrita: true),
        };

    /// <summary>Roles con default definido para Utilidad (en orden de la tabla).</summary>
    public static IReadOnlyList<string> RolesUtilidad { get; }
        = UtilidadDefaults.Keys.ToList();

    /// <summary>
    /// Default de estilo del rol para Utilidad, o una superposición vacía si el rol
    /// no tiene default (el consumidor hereda de niveles inferiores).
    /// </summary>
    public static ReportStyleOverlay Utilidad(string? rolFila)
        => rolFila is not null && UtilidadDefaults.TryGetValue(rolFila, out var overlay)
            ? overlay
            : Vacio;

    /// <summary>Combina el default de Utilidad con el override PARCIAL del usuario.</summary>
    public static ReportStyleOverlay CombinarUtilidad(
        string? rolFila,
        IReadOnlyDictionary<string, ReportStyleOverlay>? estilosUsuario)
    {
        var baseStyle = Utilidad(rolFila);
        if (rolFila is null || estilosUsuario is null || !estilosUsuario.TryGetValue(rolFila, out var superior))
            return baseStyle;
        return Combinar(baseStyle, superior);
    }

    /// <summary>Lista de estilos de rol efectivos de Utilidad (defaults + usuario).</summary>
    public static IReadOnlyList<ReportRowRoleStyle> CombinarUtilidad(
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
        => CombinarRoles(UtilidadDefaults, estilosUsuario);

    // ══════════════════ Factor de Salario Real (paso 6.4) ══════════════════

    /// <summary>
    /// Tipo de reporte estable del AE-2(A) (tabla de cálculo del FSR). Coincide con
    /// <c>FsrReportSnapshotBuilder.TipoReporteAE2A</c>.
    /// </summary>
    public const string TipoReporteFsr = "FactorSalarioReal";

    /// <summary>
    /// Tipo de reporte estable del AE-2(C) (Tabulador FSR). Coincide con
    /// <c>FsrReportSnapshotBuilder.TipoReporteAE2C</c>.
    /// </summary>
    public const string TipoReporteTabuladorFsr = "TabuladorFsr";

    /// <summary>
    /// Defaults de los reportes del FSR (AE-2A/AE-2C) por rol de fila. Son la ÚNICA
    /// fuente de verdad de las bandas de SECCIÓN/SUBSEÇÃO y de la fila de TOTAL, que
    /// antes estaban hardcodeadas en <c>GeneradorPdfFSR</c> y
    /// <c>GeneradorExcelFSR</c>. Se retiran las constantes locales y PDF y Excel
    /// convergen: sección #4A4A6A con texto blanco y negrita; subsección/título de
    /// tabla #C5CAE9; subtotal #E8EAF6; total #1A237E con texto blanco; y el título
    /// de tabla #33334C.
    /// </summary>
    private static readonly IReadOnlyDictionary<string, ReportStyleOverlay> FsrDefaults =
        new Dictionary<string, ReportStyleOverlay>(StringComparer.OrdinalIgnoreCase)
        {
            [RolFilaReporte.Encabezado] = new ReportStyleOverlay(ColorFondo: "#33334C", ColorFuente: "#FFFFFF", Negrita: true),
            [RolFilaReporte.Seccion] = new ReportStyleOverlay(ColorFondo: "#4A4A6A", ColorFuente: "#FFFFFF", Negrita: true),
            [RolFilaReporte.Referencia] = new ReportStyleOverlay(ColorFondo: "#C5CAE9", ColorFuente: "#1A237E", Negrita: true),
            [RolFilaReporte.Subtotal] = new ReportStyleOverlay(ColorFondo: "#E8EAF6", ColorFuente: "#000000", Negrita: false),
            [RolFilaReporte.Total] = new ReportStyleOverlay(ColorFondo: "#1A237E", ColorFuente: "#FFFFFF", Negrita: true),
        };

    /// <summary>Roles con default definido para el FSR (en orden de la tabla).</summary>
    public static IReadOnlyList<string> RolesFsr { get; }
        = FsrDefaults.Keys.ToList();

    /// <summary>
    /// Default de estilo del rol para el FSR, o una superposición vacía si el rol no
    /// tiene default (el consumidor hereda de niveles inferiores).
    /// </summary>
    public static ReportStyleOverlay Fsr(string? rolFila)
        => rolFila is not null && FsrDefaults.TryGetValue(rolFila, out var overlay)
            ? overlay
            : Vacio;

    /// <summary>Combina el default del FSR con el override PARCIAL del usuario.</summary>
    public static ReportStyleOverlay CombinarFsr(
        string? rolFila,
        IReadOnlyDictionary<string, ReportStyleOverlay>? estilosUsuario)
    {
        var baseStyle = Fsr(rolFila);
        if (rolFila is null || estilosUsuario is null || !estilosUsuario.TryGetValue(rolFila, out var superior))
            return baseStyle;
        return Combinar(baseStyle, superior);
    }

    /// <summary>Lista de estilos de rol efectivos del FSR (defaults + usuario).</summary>
    public static IReadOnlyList<ReportRowRoleStyle> CombinarFsr(
        IEnumerable<ReportRowRoleStyle>? estilosUsuario)
        => CombinarRoles(FsrDefaults, estilosUsuario);

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
