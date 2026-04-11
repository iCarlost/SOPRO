using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public enum ConsolidacionInsumoTipo
    {
        Material = 0,
        ManoDeObra = 1,
        Herramienta = 2,
        Maquinaria = 3
    }

    public sealed class ConsolidacionImpactoPreview
    {
        public int RegistrosSeleccionados { get; init; }
        public int RegistrosAConsolidar { get; init; }
        public int ComponentesAfectados { get; init; }
        public int MatricesAfectadas { get; init; }
        public int ConceptosAfectados { get; init; }
    }

    public sealed class ConsolidacionResultado
    {
        public int RegistrosConsolidados { get; init; }
        public int ComponentesActualizados { get; init; }
        public int MatricesAfectadas { get; init; }
        public int ConceptosAfectados { get; init; }
    }

    public sealed class InsumoConsolidationService
    {
        public ConsolidacionImpactoPreview ObtenerPreview(SOPROContext context, int proyectoId, ConsolidacionInsumoTipo tipo, int insumoBaseId, IReadOnlyCollection<int> idsSeleccionados)
        {
            var ids = NormalizarSeleccion(idsSeleccionados, insumoBaseId);
            var idsAReemplazar = ids.Where(x => x != insumoBaseId).ToList();
            var q = QueryComponentesPorTipo(context, tipo, idsAReemplazar);
            var matrizIds = q.Select(c => c.MatrizId).Distinct().ToList();
            var conceptos = matrizIds.Count == 0
                ? 0
                : context.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId && c.MatrizId.HasValue && matrizIds.Contains(c.MatrizId.Value));

            return new ConsolidacionImpactoPreview
            {
                RegistrosSeleccionados = ids.Count,
                RegistrosAConsolidar = idsAReemplazar.Count,
                ComponentesAfectados = q.Count(),
                MatricesAfectadas = matrizIds.Count,
                ConceptosAfectados = conceptos
            };
        }

        public ConsolidacionResultado Consolidar(SOPROContext context, int proyectoId, ConsolidacionInsumoTipo tipo, int insumoBaseId, IReadOnlyCollection<int> idsSeleccionados)
        {
            var ids = NormalizarSeleccion(idsSeleccionados, insumoBaseId);
            var idsAReemplazar = ids.Where(x => x != insumoBaseId).ToList();
            if (idsAReemplazar.Count == 0)
                return new ConsolidacionResultado();

            ValidarSeleccion(context, proyectoId, tipo, ids);

            var componentes = QueryComponentesPorTipo(context, tipo, idsAReemplazar).ToList();
            var matrizIds = componentes.Select(c => c.MatrizId).Distinct().ToList();
            var conceptosAfectados = matrizIds.Count == 0
                ? 0
                : context.ConceptosPresupuesto.Count(c => c.ProyectoId == proyectoId && c.MatrizId.HasValue && matrizIds.Contains(c.MatrizId.Value));

            foreach (var comp in componentes)
            {
                switch (tipo)
                {
                    case ConsolidacionInsumoTipo.Material:
                        comp.MaterialId = insumoBaseId;
                        comp.Material = null;
                        break;
                    case ConsolidacionInsumoTipo.ManoDeObra:
                        comp.ManoDeObraId = insumoBaseId;
                        comp.ManoDeObra = null;
                        break;
                    case ConsolidacionInsumoTipo.Herramienta:
                        comp.HerramientaId = insumoBaseId;
                        comp.Herramienta = null;
                        break;
                    case ConsolidacionInsumoTipo.Maquinaria:
                        comp.MaquinariaId = insumoBaseId;
                        comp.Maquinaria = null;
                        break;
                }
            }

            context.SaveChanges();

            EliminarSustituidos(context, tipo, idsAReemplazar);
            context.SaveChanges();

            if (matrizIds.Count > 0)
                RecalculationCoordinatorService.RecalculateAfterInsumoDeletion(context, matrizIds);

            return new ConsolidacionResultado
            {
                RegistrosConsolidados = idsAReemplazar.Count,
                ComponentesActualizados = componentes.Count,
                MatricesAfectadas = matrizIds.Count,
                ConceptosAfectados = conceptosAfectados
            };
        }

        private static List<int> NormalizarSeleccion(IReadOnlyCollection<int> idsSeleccionados, int insumoBaseId)
        {
            var ids = (idsSeleccionados ?? Array.Empty<int>()).Where(x => x > 0).Distinct().ToList();
            if (!ids.Contains(insumoBaseId))
                ids.Add(insumoBaseId);
            if (ids.Count < 2)
                throw new InvalidOperationException("Seleccione al menos dos insumos para consolidar.");
            return ids;
        }

        private static IQueryable<ComponenteMatriz> QueryComponentesPorTipo(SOPROContext context, ConsolidacionInsumoTipo tipo, IReadOnlyCollection<int> ids)
        {
            if (ids == null || ids.Count == 0)
                return context.ComponentesMatriz.Where(c => false);

            return tipo switch
            {
                ConsolidacionInsumoTipo.Material => context.ComponentesMatriz.Where(c => c.MaterialId.HasValue && ids.Contains(c.MaterialId.Value)),
                ConsolidacionInsumoTipo.ManoDeObra => context.ComponentesMatriz.Where(c => c.ManoDeObraId.HasValue && ids.Contains(c.ManoDeObraId.Value)),
                ConsolidacionInsumoTipo.Herramienta => context.ComponentesMatriz.Where(c => c.HerramientaId.HasValue && ids.Contains(c.HerramientaId.Value)),
                ConsolidacionInsumoTipo.Maquinaria => context.ComponentesMatriz.Where(c => c.MaquinariaId.HasValue && ids.Contains(c.MaquinariaId.Value)),
                _ => context.ComponentesMatriz.Where(c => false)
            };
        }

        private static void ValidarSeleccion(SOPROContext context, int proyectoId, ConsolidacionInsumoTipo tipo, IReadOnlyCollection<int> ids)
        {
            switch (tipo)
            {
                case ConsolidacionInsumoTipo.Material:
                    ValidarRegistros(context.Materiales.Where(x => ids.Contains(x.Id)).ToList(), proyectoId, ids.Count, x => x.ProyectoId, x => x.Origen);
                    break;
                case ConsolidacionInsumoTipo.ManoDeObra:
                    ValidarRegistros(context.ManoDeObra.Where(x => ids.Contains(x.Id)).ToList(), proyectoId, ids.Count, x => x.ProyectoId, x => x.Origen);
                    break;
                case ConsolidacionInsumoTipo.Herramienta:
                    ValidarRegistros(context.Herramientas.Where(x => ids.Contains(x.Id)).ToList(), proyectoId, ids.Count, x => (int?)x.ProyectoId, x => x.Origen);
                    break;
                case ConsolidacionInsumoTipo.Maquinaria:
                    ValidarRegistros(context.Maquinaria.Where(x => ids.Contains(x.Id)).ToList(), proyectoId, ids.Count, x => x.ProyectoId, x => x.Origen);
                    break;
            }
        }

        private static void ValidarRegistros<T>(List<T> rows, int proyectoId, int expectedCount, Func<T, int?> proyectoAccessor, Func<T, OrigenInsumo> origenAccessor)
        {
            if (rows.Count != expectedCount)
                throw new InvalidOperationException("No se encontraron todos los insumos seleccionados para consolidar.");
            if (rows.Any(x => proyectoAccessor(x) != proyectoId))
                throw new InvalidOperationException("La consolidación solo está disponible para insumos del proyecto actual.");
            if (rows.Any(x => origenAccessor(x) == OrigenInsumo.Maestro))
                throw new InvalidOperationException("No se pueden consolidar registros del catálogo maestro desde un proyecto.");
        }

        private static void EliminarSustituidos(SOPROContext context, ConsolidacionInsumoTipo tipo, IReadOnlyCollection<int> ids)
        {
            switch (tipo)
            {
                case ConsolidacionInsumoTipo.Material:
                    context.Materiales.RemoveRange(context.Materiales.Where(x => ids.Contains(x.Id)).ToList());
                    break;
                case ConsolidacionInsumoTipo.ManoDeObra:
                    context.ManoDeObra.RemoveRange(context.ManoDeObra.Where(x => ids.Contains(x.Id)).ToList());
                    break;
                case ConsolidacionInsumoTipo.Herramienta:
                    context.Herramientas.RemoveRange(context.Herramientas.Where(x => ids.Contains(x.Id)).ToList());
                    break;
                case ConsolidacionInsumoTipo.Maquinaria:
                    context.Maquinaria.RemoveRange(context.Maquinaria.Where(x => ids.Contains(x.Id)).ToList());
                    break;
            }
        }
    }
}
