namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Estilo SEMÁNTICO de una fila por su rol funcional (ej. "Subtotal", "Total",
/// "Título", "Detalle"). El resolver lo aplica a todas las celdas de las filas
/// cuyo rol coincide, por debajo del override de celda y por encima del estilo de
/// columna.
/// </summary>
/// <param name="RolFila">Rol funcional de la fila.</param>
/// <param name="Estilo">Superposición de estilo que aporta el rol.</param>
public sealed record ReportRowRoleStyle(
    string RolFila,
    ReportStyleOverlay Estilo);
