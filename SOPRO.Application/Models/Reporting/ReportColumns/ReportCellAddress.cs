namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Referencia ESTABLE a una celda de reporte: vista lógica + fila + columna. NO
/// usa índices visuales del grid (posiciones), de modo que la identidad de la
/// celda sobrevive a reordenamientos de columnas, al banding y a la paginación.
/// </summary>
/// <param name="ClaveVista">
/// Clave de la vista/sección del reporte (ej. "Principal", "Insumos"). Puede ir
/// vacía para actuar como comodín de vista en <c>EstilosCelda</c>.
/// </param>
/// <param name="ClaveFila">Clave estable de la fila (ej. el id/clave de negocio).</param>
/// <param name="NombreColumna">
/// Identificador de la columna (<see cref="ReportColumnDefinition.Identificador"/>).
/// </param>
public sealed record ReportCellAddress(
    string ClaveVista,
    string ClaveFila,
    string NombreColumna);
