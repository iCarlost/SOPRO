using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Contracts;
using SOPRO.Application.Models.Reporting.MatrixCatalog;
using SOPRO.Core.Entities;
using SOPRO.Data.Factories;

namespace SOPRO.Application.UseCases.Reporting;

/// <summary>
/// Caso de uso: construye el modelo neutral de "Catálogo de Matrices" a partir
/// de IDs de matriz, un proyecto y su plantilla.
///
/// Patrón ADR-001:181 (Application→Data transitorio, deuda explícita):
///   - Un contexto por operación (IProjectDbContextFactory).
///   - AsNoTracking en todas las lecturas.
///   - CancellationToken en cada paso de I/O.
///   - Se devuelven snapshots neutrales, nunca entidades de EF.
///
/// La aritmética pura vive en <see cref="MatrixCatalogReportModelBuilder"/> (prueable
/// sin base de datos). Este caso de uso aplica la proyección de entidades a snapshots
/// y delega al builder.
/// </summary>
public sealed class BuildMatrixCatalogReport
{
    private readonly IProjectDbContextFactory _factory;
    private readonly MatrixCatalogReportModelBuilder _builder = new();

    public BuildMatrixCatalogReport(IProjectDbContextFactory factory)
    {
        _factory = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    public async Task<Result<MatrixCatalogReportDocument>> Execute(
        ProjectSessionInfo session,
        BuildMatrixCatalogReportRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(request);
        cancellationToken.ThrowIfCancellationRequested();

        var ids = request.MatrixIds?.ToList() ?? new List<int>();

        await using var context = await _factory.CreateAsync(session.DatabasePath, cancellationToken);

        try
        {
            List<Matriz> matricesCargadas;

            if (ids.Count == 0)
            {
                matricesCargadas = new List<Matriz>();
            }
            else
            {
                matricesCargadas = await context.Matrices
                    .AsNoTracking()
                    .Include(m => m.Componentes.OrderBy(c => c.Orden))
                        .ThenInclude(c => c.Material)
                    .Include(m => m.Componentes)
                        .ThenInclude(c => c.ManoDeObra)
                    .Include(m => m.Componentes)
                        .ThenInclude(c => c.Maquinaria)
                    .Include(m => m.Componentes)
                        .ThenInclude(c => c.Herramienta)
                    .Include(m => m.Componentes)
                        .ThenInclude(c => c.Auxiliar)
                    .Where(m => ids.Contains(m.Id))
                    .OrderBy(m => m.Clave)
                    .ToListAsync(cancellationToken);
            }

            cancellationToken.ThrowIfCancellationRequested();

            Proyecto? proyecto = null;
            if (session.Project.ProjectId is int proyectoId)
            {
                proyecto = await context.Proyectos
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.Id == proyectoId, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            PlantillaReporte? plantilla = null;
            if (proyecto != null)
            {
                plantilla = await context.PlantillasReporte
                    .AsNoTracking()
                    .FirstOrDefaultAsync(p => p.ProyectoId == proyecto.Id, cancellationToken);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var settings = new MatrixCatalogReportSettings(
                MatrixCatalogSourceMapper.MapearProyecto(proyecto),
                MatrixCatalogSourceMapper.MapearPlantilla(plantilla),
                request.TitleOptions,
                request.FiltroTitulo);

            var snapshots = matricesCargadas.Select(MatrixCatalogSourceMapper.MapearMatriz).ToList();
            var doc = _builder.Build(settings, snapshots, DateTime.Now);

            return Result<MatrixCatalogReportDocument>.Ok(doc);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return Result<MatrixCatalogReportDocument>.Fail(
                AppErrorCode.Database,
                "No se pudo construir el catálogo de matrices.",
                ex.Message);
        }
    }
}