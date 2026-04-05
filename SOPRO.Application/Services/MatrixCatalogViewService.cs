using SOPRO.Application.Models.Catalogs;
using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class MatrixCatalogViewService
    {
        private readonly MatrixCatalogLoadService _loadService = new();

        public async Task<MatrixCatalogLoadResult> LoadAsync(SOPROContext context, MatrixCatalogFilterInput filter)
        {
            var matrices = await _loadService.LoadMatricesAsync(context, filter);
            return new MatrixCatalogLoadResult
            {
                Rows = matrices.Select(MapRow).ToList(),
                StatusText = $"{matrices.Count} matriz/matrices encontrada(s)"
            };
        }

        private static MatrixGridRowDisplay MapRow(Matriz matriz)
        {
            return new MatrixGridRowDisplay
            {
                MatrizId = matriz.Id,
                Clave = matriz.Clave ?? string.Empty,
                Descripcion = matriz.Descripcion ?? string.Empty,
                Unidad = matriz.Unidad ?? string.Empty,
                Tipo = matriz.Tipo,
                TipoTexto = matriz.Tipo switch
                {
                    TipoMatriz.APU => "📊 APU",
                    TipoMatriz.Basico => "🧩 Básico",
                    TipoMatriz.Cuadrilla => "👷 Cuadrilla",
                    _ => "❓"
                },
                CostoDirecto = matriz.CostoDirecto,
                NumInsumos = matriz.Componentes?.Count ?? 0,
                OrigenDetalle = ImportOriginStampService.BuildOriginDisplay(matriz.Notas),
                Source = matriz
            };
        }
    }
}
