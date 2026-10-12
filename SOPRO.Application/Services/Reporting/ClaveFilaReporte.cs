namespace SOPRO.Application.Services.Reporting;

/// <summary>
/// Fábrica única de claves de fila ESTABLES por reporte. Las claves se derivan de
/// identificadores de dominio (nunca del índice visual del grid), de modo que la
/// identidad de una fila sobrevive a reordenamientos, filtros y paginación.
///
/// Tokens (planificados y validados contra los Id reales del dominio):
/// <list type="bullet">
///   <item>Presupuesto: <c>C:{ConceptoPresupuesto.Id}</c>, <c>SUB:{Clave}</c>, <c>TOTAL</c>, <c>IVA</c>.</item>
///   <item>Indirectos: <c>G:{GrupoIndirecto.Id}</c>, <c>IC:{ConceptoIndirecto.Id}</c>.</item>
///   <item>APU / Matrices: <c>C:{ConceptoPresupuesto.Id}</c>, <c>COMP:{ComponenteMatriz.Id}</c>, <c>PU:{ConceptoId}</c>.</item>
/// </list>
/// </summary>
public static class ClaveFilaReporte
{
    /// <summary>Prefijo de fila de concepto del presupuesto (compartido por APU).</summary>
    public const string PrefijoConcepto = "C:";

    /// <summary>Prefijo de fila sintética de subtotal, parametrizada por la clave del agrupador.</summary>
    public const string PrefijoSubtotal = "SUB:";

    /// <summary>Prefijo de fila de grupo de indirectos.</summary>
    public const string PrefijoGrupoIndirecto = "G:";

    /// <summary>Prefijo de fila de concepto de indirectos.</summary>
    public const string PrefijoConceptoIndirecto = "IC:";

    /// <summary>Prefijo de fila de componente de matriz (insumo del APU).</summary>
    public const string PrefijoComponenteMatriz = "COMP:";

    /// <summary>Prefijo de fila sintética de precio unitario integrado del APU.</summary>
    public const string PrefijoPrecioUnitario = "PU:";

    /// <summary>Clave sintética de la fila de TOTAL del presupuesto.</summary>
    public const string Total = "TOTAL";

    /// <summary>Clave sintética de la fila de IVA del presupuesto.</summary>
    public const string Iva = "IVA";

    /// <summary>Fila de concepto del presupuesto (o de la matriz APU del concepto).</summary>
    public static string Concepto(int conceptoPresupuestoId) => PrefijoConcepto + conceptoPresupuestoId;

    /// <summary>Fila sintética de subtotal de un agrupador, parametrizada por su clave.</summary>
    public static string Subtotal(string? claveAgrupador) => PrefijoSubtotal + (claveAgrupador ?? string.Empty);

    /// <summary>Fila de grupo de indirectos.</summary>
    public static string GrupoIndirecto(int grupoIndirectoId) => PrefijoGrupoIndirecto + grupoIndirectoId;

    /// <summary>Fila de concepto de indirectos.</summary>
    public static string ConceptoIndirecto(int conceptoIndirectoId) => PrefijoConceptoIndirecto + conceptoIndirectoId;

    /// <summary>Fila de componente de matriz (insumo dentro del APU).</summary>
    public static string Componente(int componenteMatrizId) => PrefijoComponenteMatriz + componenteMatrizId;

    /// <summary>Fila sintética de precio unitario integrado del APU del concepto dado.</summary>
    public static string PrecioUnitario(int conceptoPresupuestoId) => PrefijoPrecioUnitario + conceptoPresupuestoId;
}
