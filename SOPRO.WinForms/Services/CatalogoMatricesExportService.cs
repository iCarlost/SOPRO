using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Application.UseCases.Reporting;
using SOPRO.Data.Factories;
using SOPRO.Reporting.Excel;
using SOPRO.Reporting.Pdf;

namespace SOPRO.WinForms.Services;

/// <summary>
/// Compone el documento neutral, su renderer y la escritura del catálogo.
/// </summary>
public sealed class CatalogoMatricesExportService
{
    private readonly BuildMatrixCatalogReport _build;
    private readonly CatalogoMatricesExcelRenderer _excelRenderer = new();
    private readonly CatalogoMatricesPdfRenderer _pdfRenderer = new();

    public CatalogoMatricesExportService(IProjectDbContextFactory factory)
    {
        _build = new BuildMatrixCatalogReport(factory);
    }

    public void ExportarExcel(
        ProjectSessionInfo session,
        BuildMatrixCatalogReportRequest request,
        string destinationPath)
    {
        var document = Construir(session, request);
        var bytes = _excelRenderer.Render(document);
        File.WriteAllBytes(destinationPath, bytes);
    }

    public void ExportarPdf(
        ProjectSessionInfo session,
        BuildMatrixCatalogReportRequest request,
        string destinationPath)
    {
        var document = Construir(session, request);
        var bytes = _pdfRenderer.Render(document);
        File.WriteAllBytes(destinationPath, bytes);
    }

    private MatrixCatalogReportDocument Construir(
        ProjectSessionInfo session,
        BuildMatrixCatalogReportRequest request)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);

        // Puente síncrono explícito: el build corre en un worker sin
        // SynchronizationContext para que sus awaits EF no intenten volver al hilo UI.
        var result = Task.Run(() =>
        {
            SynchronizationContext.SetSynchronizationContext(null);
            return _build.Execute(session, request, CancellationToken.None).GetAwaiter().GetResult();
        })
            .GetAwaiter()
            .GetResult();

        if (result.IsSuccess)
            return result.Value ?? throw new InvalidOperationException(
                "El catálogo de matrices se construyó sin documento.");

        var error = result.Error;
        var message = error?.Message ?? "No se pudo construir el catálogo de matrices.";
        if (!string.IsNullOrWhiteSpace(error?.Detail))
            message += Environment.NewLine + error.Detail;

        throw new InvalidOperationException(message);
    }
}
