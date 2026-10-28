using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Application.Services.Reporting;

/// <summary>
/// Comando ÚNICO de escritura del formato por celda (ADR-0001). Abre un contexto
/// NUEVO por operación con <see cref="IProjectDbContextFactory"/> sobre la ruta
/// REAL de la base, envuelve el lote en UNA transacción y confirma con UN solo
/// <c>SaveChanges</c>.
///
/// Contrato de lote:
/// <list type="bullet">
///   <item>Optimistic base por dirección: antes de aplicar NADA se recorre el
///   lote comparando la base esperada contra el estado persistido. Si cualquiera
///   difiere, se aborta TODO con <see cref="BudgetOperationStatus.Conflict"/>.</item>
///   <item>Las operaciones encadenadas sobre la MISMA dirección se evalúan de
///   forma secuencial (la base de la siguiente es el resultado de la anterior),
///   de modo que borrar y recrear la misma clave única en un lote es válido y no
///   viola el índice (ProyectoId, TipoReporte, ClaveVista, ClaveFila,
///   NombreColumna): la fila se reutiliza en vez de emitir DELETE+INSERT.</item>
///   <item>El contexto de la vista NO se toca: no hay ChangeTracker compartido ni
///   <c>Clear</c> sobre él.</item>
///   <item><see cref="OverlayApplyMode.Merge"/> (default) fusiona el propuesto
///   parcial sobre la base; <see cref="OverlayApplyMode.Replace"/> fija el
///   overlay COMPLETO propuesto, de modo que Undo/Redo revierten también las
///   propiedades añadidas por la edición (y <c>null</c> borra el override).</item>
/// </list>
/// </summary>
public static class FormatoCeldasCommandService
{
    /// <summary>
    /// Aplica el lote de formato de celdas sobre la base indicada. No lanza por
    /// conflictos ni fallos de persistencia: los clasifica en el resultado.
    /// </summary>
    public static FormatoCeldasEditBatchResult ExecuteAplicarCeldasCommand(
        IProjectDbContextFactory factory,
        string databasePath,
        int proyectoId,
        string tipoReporte,
        IReadOnlyList<CambioFormatoCeldaCommand> cambios)
    {
        ArgumentNullException.ThrowIfNull(factory);
        if (string.IsNullOrWhiteSpace(databasePath))
            throw new ArgumentException("La ruta de la base del proyecto es requerida.", nameof(databasePath));
        ArgumentNullException.ThrowIfNull(cambios);
        FormatoCeldaCore.ValidarCabecera(proyectoId, tipoReporte);

        // La FORMA del comando se valida antes de abrir la base: un comando
        // malformado es un error de programación, no un fallo de persistencia.
        var normalizados = Normalizar(cambios);

        if (normalizados.Count == 0)
        {
            return new FormatoCeldasEditBatchResult(
                BudgetOperationResult.Committed(Array.Empty<int>()),
                new FormatoCeldasEditBatchSnapshot(
                    proyectoId,
                    tipoReporte,
                    new Dictionary<ReportCellAddress, ReportStyleOverlay>()),
                diagnostic: null);
        }

        var execution = BudgetWriteExecutor.Execute(
            factory,
            databasePath,
            useTransaction: true,
            context => StageAplicarCeldas(context, proyectoId, tipoReporte, normalizados));

        return new FormatoCeldasEditBatchResult(
            execution.Result, execution.Confirmed, execution.Diagnostic);
    }

    private static BudgetWriteStep<FormatoCeldasEditBatchSnapshot> StageAplicarCeldas(
        SOPROContext context,
        int proyectoId,
        string tipoReporte,
        IReadOnlyList<CambioNormalizado> cambios)
    {
        // UNA consulta, TRACKEADA para que sean las mismas instancias del único
        // SaveChanges. El índice usa la dirección estable (vista incluida).
        var filas = context.FormatosCeldaReporte
            .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == tipoReporte)
            .ToList();

        var indice = new Dictionary<ReportCellAddress, FormatoCeldaReporte>();
        var estado = new Dictionary<ReportCellAddress, ReportStyleOverlay?>();
        foreach (var fila in filas)
        {
            var direccion = FormatoCeldaCore.Direccion(fila);
            indice[direccion] = fila;
            estado[direccion] = FormatoCeldaCore.AOverlay(fila);
        }

        // ── 1) Validar TODAS las bases esperadas sobre el estado simulado ─────
        // Nada se aplica a las entidades hasta que el lote completo es válido.
        var tocadas = new List<ReportCellAddress>();
        var vistas = new HashSet<ReportCellAddress>();
        foreach (var cambio in cambios)
        {
            estado.TryGetValue(cambio.Direccion, out var actual);
            var actualCanonico = FormatoCeldaCore.NormalizarEstado(actual);
            var esperadoCanonico = FormatoCeldaCore.NormalizarEstado(cambio.Esperado);

            if (!Equals(actualCanonico, esperadoCanonico))
            {
                return BudgetWriteStep<FormatoCeldasEditBatchSnapshot>.Abort(
                    BudgetOperationResult.Conflict(
                        $"El formato de la celda {cambio.Direccion.ClaveFila} · " +
                        $"{cambio.Direccion.NombreColumna} cambió en la base desde que se cargó; " +
                        "la vista debe recargarse antes de reintentar."));
            }

            var propuesto = FormatoCeldaCore.NormalizarEstado(cambio.Propuesto);
            // Resultado simulado: la base de la siguiente operación de la MISMA
            // dirección es lo que dejó la anterior (operaciones secuenciales).
            // Merge = delta (conserva lo no informado); Replace = estado exacto
            // (lo no informado queda nulo, p. ej. undo de una propiedad añadida).
            estado[cambio.Direccion] = propuesto is null
                ? null
                : cambio.Modo == OverlayApplyMode.Replace
                    ? propuesto
                    : FormatoCeldaCore.Combinar(actualCanonico, propuesto);

            if (vistas.Add(cambio.Direccion))
                tocadas.Add(cambio.Direccion);
        }

        // ── 2) Materializar SOLO las direcciones tocadas ──────────────────────
        var confirmados = new Dictionary<ReportCellAddress, ReportStyleOverlay>();
        foreach (var direccion in tocadas)
        {
            estado.TryGetValue(direccion, out var overlayFinal);
            indice.TryGetValue(direccion, out var entidad);

            if (overlayFinal is null)
            {
                if (entidad is not null)
                    context.FormatosCeldaReporte.Remove(entidad);
                continue;
            }

            if (entidad is null)
            {
                entidad = new FormatoCeldaReporte
                {
                    ProyectoId = proyectoId,
                    TipoReporte = tipoReporte,
                    ClaveVista = direccion.ClaveVista,
                    ClaveFila = direccion.ClaveFila,
                    NombreColumna = direccion.NombreColumna,
                };
                FormatoCeldaCore.EstablecerOverlay(entidad, overlayFinal);
                context.FormatosCeldaReporte.Add(entidad);
            }
            else
            {
                // Reutiliza la fila existente incluso si una operación anterior del
                // lote la había borrado: nunca se emiten DELETE+INSERT de la misma
                // clave única dentro del mismo SaveChanges. El estado se fija
                // COMPLETO para que un borrar+recrear no arrastre propiedades viejas.
                FormatoCeldaCore.EstablecerOverlay(entidad, overlayFinal);
            }

            confirmados[direccion] = FormatoCeldaCore.AOverlay(entidad);
        }

        var snapshot = new FormatoCeldasEditBatchSnapshot(
            proyectoId,
            tipoReporte,
            new Dictionary<ReportCellAddress, ReportStyleOverlay>(confirmados));

        return BudgetWriteStep<FormatoCeldasEditBatchSnapshot>.Commit(
            snapshot, Array.Empty<int>());
    }

    private static List<CambioNormalizado> Normalizar(IReadOnlyList<CambioFormatoCeldaCommand> cambios)
    {
        var resultado = new List<CambioNormalizado>(cambios.Count);
        foreach (var cambio in cambios)
        {
            ArgumentNullException.ThrowIfNull(cambio);

            resultado.Add(new CambioNormalizado(
                new ReportCellAddress(
                    FormatoCeldaCore.NormalizarVista(cambio.ClaveVista),
                    FormatoCeldaCore.NormalizarClaveFila(cambio.ClaveFila),
                    FormatoCeldaCore.NormalizarColumna(cambio.NombreColumna)),
                cambio.ExpectedOverlay,
                cambio.ProposedOverlay,
                cambio.ApplyMode));
        }

        return resultado;
    }

    private sealed record CambioNormalizado(
        ReportCellAddress Direccion,
        ReportStyleOverlay? Esperado,
        ReportStyleOverlay? Propuesto,
        OverlayApplyMode Modo);
}
