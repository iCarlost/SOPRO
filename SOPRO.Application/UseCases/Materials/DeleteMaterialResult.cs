namespace SOPRO.Application.UseCases.Materials;

/// <summary>Resultado de la eliminación de un material.</summary>
/// <param name="DeletedComponents">Componentes de matriz eliminados en cascada.</param>
/// <param name="AffectedMatrixCount">Matrices distintas afectadas.</param>
/// <param name="TriggeredRecalculation">True si se propagó el recálculo a matrices/presupuestos.</param>
public sealed record DeleteMaterialResult(int DeletedComponents, int AffectedMatrixCount, bool TriggeredRecalculation);