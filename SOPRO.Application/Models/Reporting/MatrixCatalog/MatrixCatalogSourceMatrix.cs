namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Matriz fuente (snapshot) antes de la aritmética del builder. El costo directo
/// es el valor persistido; los totales se calculan en el builder (puro).
/// </summary>
internal sealed record MatrixCatalogSourceMatrix(
    string Clave,
    string Descripcion,
    string Unidad,
    MatrixCatalogMatrixKind Kind,
    decimal CostoDirecto,
    IReadOnlyList<MatrixCatalogSourceComponent> Componentes);