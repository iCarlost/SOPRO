namespace SOPRO.Application.Models.Reporting.MatrixCatalog;

/// <summary>
/// Componente fuente (snapshot) antes de la aritmética del builder.
/// <c>CostoUnitario</c> es el costo unitario crudo del insumo (precio/salario
/// real/costo horario/costo directo según <c>Kind</c>).
/// </summary>
internal sealed record MatrixCatalogSourceComponent(
    MatrixCatalogComponentKind Kind,
    int Orden,
    string Clave,
    string Descripcion,
    string Unidad,
    decimal Cantidad,
    decimal CostoUnitario,
    bool EsPorcentajeMo,
    bool EsCuadrillaAuxiliar);