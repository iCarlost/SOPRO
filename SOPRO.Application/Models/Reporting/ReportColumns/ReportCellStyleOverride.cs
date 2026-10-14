namespace SOPRO.Application.Models.Reporting.ReportColumns;

/// <summary>
/// Override puntual del estilo de UNA celda: la <see cref="Direccion"/> identifica
/// la celda de forma estable y <see cref="Estilo"/> es la superposición a aplicar.
/// Tiene la MÁXIMA precedencia en la cascada de <c>ReportCellStyleResolver</c>.
/// </summary>
/// <param name="Direccion">Referencia estable de la celda destino.</param>
/// <param name="Estilo">Superposición de estilo de la celda.</param>
public sealed record ReportCellStyleOverride(
    ReportCellAddress Direccion,
    ReportStyleOverlay Estilo);
