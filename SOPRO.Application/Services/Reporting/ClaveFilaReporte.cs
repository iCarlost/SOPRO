namespace SOPRO.Application.Services.Reporting;

/// <summary>
/// Fábrica única de claves de fila ESTABLES por reporte. Las claves se derivan de
/// identificadores de dominio (nunca del índice visual del grid), de modo que la
/// identidad de una fila sobrevive a reordenamientos, filtros y paginación.
///
/// Tokens (planificados y validados contra los Id reales del dominio):
/// <list type="bullet">
///   <item>Presupuesto: <c>C:{ConceptoPresupuesto.Id}</c> (conceptos), <c>SUB:{ConceptoPresupuesto.Id}</c> (agrupadores), <c>SUB:</c> (cierre global), <c>TOTAL</c>, <c>IVA</c>.</item>
///   <item>Indirectos: <c>G:{GrupoIndirecto.Id}</c>, <c>IC:{ConceptoIndirecto.Id}</c>.</item>
///   <item>APU / Matrices: <c>C:{ConceptoPresupuesto.Id}</c>, <c>COMP:{ComponenteMatriz.Id}</c>, <c>PU:{ConceptoId}</c>.</item>
///   <item>Programa de Obra: <c>ACT:{ActividadProgramada.Id}</c>, <c>RES:{ConceptoPresupuestoId}</c> (agrupadores; fallback <c>RES:{Clave}</c> si no hay concepto).</item>
///   <item>Programa de Insumos: <c>INS:{InsumoId}</c>; las columnas de período usan el identificador estable del período (<c>per_</c>,<c>acu_</c>,<c>imp_</c>,<c>iacu_</c> + PeriodoId), NO el índice visual.</item>
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
    /// <summary>Prefijo de fila de actividad programada (Programa de Obra).</summary>
    public const string PrefijoActividad = "ACT:";

    /// <summary>Prefijo de fila de agrupador/resumen (Programa de Obra).</summary>
    public const string PrefijoResumen = "RES:";

    /// <summary>Prefijo de fila de insumo programado (Programa de Insumos).</summary>
    public const string PrefijoInsumo = "INS:";

    /// <summary>Clave sintética de la fila de TOTAL del presupuesto.</summary>
    public const string Total = "TOTAL";

    /// <summary>Clave sintética de la fila de IVA del presupuesto.</summary>
    public const string Iva = "IVA";

    /// <summary>Fila de concepto del presupuesto (o de la matriz APU del concepto).</summary>
    public static string Concepto(int conceptoPresupuestoId) => PrefijoConcepto + conceptoPresupuestoId;

    /// <summary>
    /// Fila sintética de subtotal, parametrizada por su clave. Se usa para la fila
    /// de cierre global del presupuesto (<c>SUB:</c> con clave vacía) y para los
    /// subtotales de sección de Indirectos. NO se usa ya para los agrupadores
    /// persistidos: éstos emplean <see cref="Agrupador"/> para garantizar unicidad.
    /// </summary>
    public static string Subtotal(string? claveAgrupador) => PrefijoSubtotal + (claveAgrupador ?? string.Empty);

    /// <summary>
    /// Fila de un AGRUPADOR persistido del presupuesto (Capítulo/Subcapítulo/Nivel):
    /// <c>SUB:{ConceptoPresupuestoId}</c>. Se deriva del Id de dominio (único por
    /// fila y estable ante reordenamientos) de modo que el override de una celda de
    /// un agrupador afecta SOLO a esa fila, aunque varios agrupadores compartan la
    /// misma <c>Clave</c> (o la tengan vacía). El estilo por ROL sigue existiendo y
    /// solo se aplica cuando no hay override de celda (precedencia celda &gt; rol).
    /// </summary>
    public static string Agrupador(int conceptoPresupuestoId) => PrefijoSubtotal + conceptoPresupuestoId;

    /// <summary>Fila de grupo de indirectos.</summary>
    public static string GrupoIndirecto(int grupoIndirectoId) => PrefijoGrupoIndirecto + grupoIndirectoId;

    /// <summary>Fila de concepto de indirectos.</summary>
    public static string ConceptoIndirecto(int conceptoIndirectoId) => PrefijoConceptoIndirecto + conceptoIndirectoId;

    /// <summary>Fila de componente de matriz (insumo dentro del APU).</summary>
    public static string Componente(int componenteMatrizId) => PrefijoComponenteMatriz + componenteMatrizId;

    /// <summary>Fila sintética de precio unitario integrado del APU del concepto dado.</summary>
    public static string PrecioUnitario(int conceptoPresupuestoId) => PrefijoPrecioUnitario + conceptoPresupuestoId;

    /// <summary>
    /// Fila de actividad programada del Programa de Obra: <c>ACT:{Id}</c>. Deriva del
    /// identificador de la <c>ActividadProgramada</c> (nunca del índice visual).
    /// </summary>
    public static string Actividad(int actividadId) => PrefijoActividad + actividadId;

    /// <summary>
    /// Fila de agrupador/resumen del Programa de Obra identificada por su Clave:
    /// <c>RES:{Clave}</c>. Se conserva como FALLBACK para las filas resumen sin
    /// vínculo a un concepto del presupuesto (por ejemplo, agrupadores manuales).
    /// </summary>
    public static string Resumen(string? clave) => PrefijoResumen + (clave ?? string.Empty);

    /// <summary>
    /// Fila de agrupador/resumen del Programa de Obra identificada por el Id de su
    /// concepto de presupuesto de origen: <c>RES:{ConceptoPresupuestoId}</c>. Se
    /// deriva de un Id de dominio (único por fila y ESTABLE ante la regeneración de
    /// las actividades resumen, que recrean su <c>Id</c> de actividad pero conservan
    /// el vínculo al concepto) de modo que el override de una celda de un agrupador
    /// afecta SOLO a esa fila aunque varios agrupadores compartan la misma
    /// <c>Clave</c> (o la tengan vacía). Es EXACTAMENTE la clave que persiste el
    /// grid en <c>FormatoCeldaReporte.ClaveFila</c> (ver
    /// <c>FormProgramaObra.ObtenerClaveFilaFormato</c>).
    /// </summary>
    public static string Resumen(int conceptoPresupuestoId) => PrefijoResumen + conceptoPresupuestoId;

    /// <summary>
    /// Fila de insumo programado del Programa de Insumos: <c>INS:{InsumoId}</c>.
    /// </summary>
    public static string Insumo(int insumoId) => PrefijoInsumo + insumoId;
}
