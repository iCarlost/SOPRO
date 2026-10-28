using System.Collections.Generic;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Tests.WinForms.TestInfrastructure;

/// <summary>
/// Atajo de pruebas para ejecutar el comando REAL de formato por celda sobre la
/// ruta real de la base del <see cref="SOPROContext"/> compartido del escenario.
/// No escribe a través del contexto del escenario: abre un contexto nuevo como
/// la aplicación.
/// </summary>
internal static class FormatoTestWriter
{
    public static FormatoCeldasEditBatchResult Aplicar(
        SOPROContext context,
        int proyectoId,
        string tipoReporte,
        IReadOnlyList<CambioFormatoCeldaCommand> cambios)
        => FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
            new ProjectDbContextFactory(),
            context.DatabasePath,
            proyectoId,
            tipoReporte,
            cambios);
}
