using SOPRO.Application.Models.Matrices;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class MatrixUsageLookupService
    {
        public List<MatrixUsageReference> FindUsageReferences(SOPROContext context, int proyectoId, int matrizId)
        {
            var parentIds = context.Set<ComponenteMatriz>()
                .Where(c => c.AuxiliarId == matrizId)
                .Select(c => c.MatrizId)
                .Distinct()
                .ToList();

            if (parentIds.Count == 0)
                return new List<MatrixUsageReference>();

            return context.Matrices
                .Where(m => m.ProyectoId == proyectoId && parentIds.Contains(m.Id))
                .OrderBy(m => m.Tipo)
                .ThenBy(m => m.Clave)
                .ToList()
                .Select(m => new MatrixUsageReference
                {
                    MatrizId = m.Id,
                    Source = m,
                    DisplayLabel = BuildLabel(m)
                })
                .ToList();
        }

        private static string BuildLabel(Matriz matriz)
        {
            var tipoLabel = matriz.Tipo switch
            {
                TipoMatriz.APU => "[APU]",
                TipoMatriz.Cuadrilla => "[Cuadrilla]",
                TipoMatriz.Basico => "[Básico]",
                _ => string.Empty
            };

            var descripcion = matriz.Descripcion ?? string.Empty;
            if (descripcion.Length > 50)
                descripcion = descripcion[..50] + "…";

            return $"{tipoLabel} {matriz.Clave} — {descripcion}".Trim();
        }
    }
}
