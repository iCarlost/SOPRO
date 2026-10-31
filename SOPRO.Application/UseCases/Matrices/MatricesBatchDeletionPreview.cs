namespace SOPRO.Application.UseCases.Matrices;

/// <summary>
/// Previsualización de una eliminación en lote de matrices: selección, impacto de
/// referencias auxiliares externas, recálculo del cierre y conceptos de presupuesto
/// que bloquean la operación. Solo lectura; el comando revalida lo mismo dentro de
/// su transacción.
/// </summary>
public sealed record MatricesBatchDeletionPreview(
    IReadOnlyList<int> SelectedIds,
    int ExternalAuxiliarComponentCount,
    int ExternalAuxiliarMatrixCount,
    IReadOnlyList<int> RecalculatedMatrixIds,
    int AffectedConceptCount,
    int AffectedAggregatorCount,
    IReadOnlyList<int> BlockingConceptIds,
    IReadOnlyList<string> BlockingConceptClaves)
{
    public int SelectedCount => SelectedIds.Count;

    public bool IsBlocked => BlockingConceptIds.Count > 0;

    public bool RemovesExternalAuxiliares => ExternalAuxiliarComponentCount > 0;

    /// <summary>
    /// Mensaje de la confirmación única: selección, componentes auxiliares externos
    /// que se quitarán y matrices/presupuesto que se recalcularán.
    /// </summary>
    public string BuildConfirmationMessage()
    {
        string seleccion = SelectedCount == 1
            ? "1 matriz seleccionada"
            : $"{SelectedCount} matrices seleccionadas";

        string referencias = ExternalAuxiliarComponentCount == 0
            ? "No hay referencias auxiliares externas que quitar."
            : $"Se quitarán {ExternalAuxiliarComponentCount} componente(s) auxiliar(es) de " +
              $"{ExternalAuxiliarMatrixCount} matriz/matrices fuera de la selección.";

        string recalculo = RecalculatedMatrixIds.Count == 0
            ? "No se requiere recálculo del presupuesto."
            : $"Se recalcularán {RecalculatedMatrixIds.Count} matriz/matrices, " +
              $"{AffectedConceptCount} concepto(s) del presupuesto y " +
              $"{AffectedAggregatorCount} agrupador(es).";

        return $"¿Eliminar {seleccion}?\n\n{referencias}\n{recalculo}\n\n" +
               "Esta acción no se puede deshacer.";
    }

    /// <summary>
    /// Mensaje del bloqueo por filas de presupuesto que referencian directamente
    /// una matriz seleccionada; se muestra antes de pedir confirmación.
    /// </summary>
    public string BuildBlockingMessage()
    {
        var claves = BlockingConceptClaves
            .Where(clave => !string.IsNullOrWhiteSpace(clave))
            .Select(clave => clave.Trim())
            .Take(3)
            .ToList();

        string detalle = claves.Count == 0
            ? string.Empty
            : $" ({string.Join(", ", claves)})";

        return $"No se pueden eliminar las matrices seleccionadas: " +
               $"{BlockingConceptIds.Count} concepto(s) del presupuesto las referencian{detalle}. " +
               "Elimine o reasigne esas filas del presupuesto antes de intentar.";
    }
}
