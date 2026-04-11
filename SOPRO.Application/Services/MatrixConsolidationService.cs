using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public sealed class MatrixConsolidationService
    {
        public ConsolidacionImpactoPreview ObtenerPreview(SOPROContext context, int proyectoId, TipoMatriz tipo, int matrizBaseId, IReadOnlyCollection<int> idsSeleccionados)
        {
            var idsAReemplazar = PrepararSeleccion(context, proyectoId, tipo, matrizBaseId, idsSeleccionados);
            var componentesAfectados = context.ComponentesMatriz
                .Where(c => c.AuxiliarId.HasValue && idsAReemplazar.Contains(c.AuxiliarId.Value))
                .Select(c => new { c.Id, c.MatrizId })
                .ToList();

            var matricesAfectadas = ObtenerMatricesAfectadas(context, proyectoId, componentesAfectados.Select(x => x.MatrizId).ToHashSet());
            var conceptosAfectados = context.ConceptosPresupuesto
                .Where(c => c.ProyectoId == proyectoId && c.MatrizId.HasValue && matricesAfectadas.Contains(c.MatrizId.Value))
                .Select(c => c.Id)
                .Distinct()
                .Count();

            return new ConsolidacionImpactoPreview
            {
                RegistrosAConsolidar = idsAReemplazar.Count,
                ComponentesAfectados = componentesAfectados.Count,
                MatricesAfectadas = matricesAfectadas.Count,
                ConceptosAfectados = conceptosAfectados
            };
        }

        public ConsolidacionResultado Consolidar(SOPROContext context, int proyectoId, TipoMatriz tipo, int matrizBaseId, IReadOnlyCollection<int> idsSeleccionados)
        {
            var idsAReemplazar = PrepararSeleccion(context, proyectoId, tipo, matrizBaseId, idsSeleccionados);
            if (idsAReemplazar.Count == 0)
                return new ConsolidacionResultado();

            var preview = ObtenerPreview(context, proyectoId, tipo, matrizBaseId, idsSeleccionados);

            using var tx = context.Database.BeginTransaction();

            var componentes = context.ComponentesMatriz
                .Where(c => c.AuxiliarId.HasValue && idsAReemplazar.Contains(c.AuxiliarId.Value))
                .ToList();

            foreach (var componente in componentes)
                componente.AuxiliarId = matrizBaseId;

            var matricesAEliminar = context.Matrices
                .Where(m => idsAReemplazar.Contains(m.Id))
                .ToList();

            if (matricesAEliminar.Count > 0)
                context.Matrices.RemoveRange(matricesAEliminar);

            context.SaveChanges();

            new RecalculoGlobalService().Ejecutar(context, proyectoId);
            tx.Commit();

            return new ConsolidacionResultado
            {
                RegistrosConsolidados = idsAReemplazar.Count,
                ComponentesActualizados = componentes.Count,
                MatricesAfectadas = preview.MatricesAfectadas,
                ConceptosAfectados = preview.ConceptosAfectados
            };
        }

        private static HashSet<int> PrepararSeleccion(SOPROContext context, int proyectoId, TipoMatriz tipo, int matrizBaseId, IReadOnlyCollection<int> idsSeleccionados)
        {
            if (tipo == TipoMatriz.APU)
                throw new InvalidOperationException("La consolidación no está disponible para matrices APU.");

            var ids = (idsSeleccionados ?? Array.Empty<int>())
                .Where(x => x > 0)
                .Distinct()
                .ToHashSet();

            if (ids.Count < 2)
                throw new InvalidOperationException("Seleccione al menos dos matrices del mismo tipo para consolidar.");

            if (!ids.Contains(matrizBaseId))
                throw new InvalidOperationException("La matriz base debe pertenecer a la selección.");

            var seleccion = context.Matrices
                .Where(m => ids.Contains(m.Id))
                .Select(m => new { m.Id, m.ProyectoId, m.Tipo })
                .ToList();

            if (seleccion.Count != ids.Count)
                throw new InvalidOperationException("No fue posible validar todas las matrices seleccionadas.");

            if (seleccion.Any(x => x.ProyectoId != proyectoId))
                throw new InvalidOperationException("La consolidación solo admite matrices del proyecto actual.");

            if (seleccion.Any(x => x.Tipo != tipo))
                throw new InvalidOperationException("La selección debe contener únicamente matrices del mismo tipo.");

            return ids.Where(id => id != matrizBaseId).ToHashSet();
        }

        private static HashSet<int> ObtenerMatricesAfectadas(SOPROContext context, int proyectoId, HashSet<int> semillas)
        {
            var afectadas = new HashSet<int>(semillas);
            if (afectadas.Count == 0)
                return afectadas;

            bool cambio;
            do
            {
                var snapshot = afectadas.ToList();
                var padres = context.ComponentesMatriz
                    .Where(c => c.AuxiliarId.HasValue && snapshot.Contains(c.AuxiliarId.Value))
                    .Join(context.Matrices.Where(m => m.ProyectoId == proyectoId), c => c.MatrizId, m => m.Id, (c, m) => m.Id)
                    .Distinct()
                    .ToList();

                cambio = false;
                foreach (var id in padres)
                    if (afectadas.Add(id))
                        cambio = true;
            }
            while (cambio);

            return afectadas;
        }
    }
}
