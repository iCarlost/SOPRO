namespace SOPRO.Application.Services.Reporting;

/// <summary>
/// Roles funcionales de fila usados por
/// <c>ConfiguracionEstiloFilaReporte.RolFila</c> y por el contrato neutral
/// <c>ReportRowRoleStyle</c>. Son etiquetas SEMÁNTICAS (no posiciones visuales):
/// un mismo rol puede aplicarse a muchas filas y sobrevivir a reordenamientos.
/// </summary>
public static class RolFilaReporte
{
    public const string Capitulo = "Capitulo";
    public const string Subcapitulo = "Subcapitulo";
    public const string Nivel1 = "Nivel1";
    public const string Nivel2 = "Nivel2";
    public const string Nivel3 = "Nivel3";
    public const string Concepto = "Concepto";
    public const string Subtotal = "Subtotal";
    public const string Total = "Total";
    public const string Grupo = "Grupo";
    public const string Resumen = "Resumen";
    public const string Encabezado = "Encabezado";
    public const string TotalGeneral = "TotalGeneral";
    public const string Referencia = "Referencia";
    public const string Seccion = "Seccion";
}
