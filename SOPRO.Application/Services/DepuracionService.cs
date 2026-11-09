using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class DepuracionService
    {
        public sealed class DepuracionResultado
        {
            public int MatricesApuEliminadas { get; set; }
            public int MatricesBasicasEliminadas { get; set; }
            public int CuadrillasEliminadas { get; set; }
            public int MaterialesEliminados { get; set; }
            public int ManoDeObraEliminada { get; set; }
            public int HerramientasEliminadas { get; set; }
            public int MaquinariaEliminada { get; set; }

            public int TotalEliminado => MatricesApuEliminadas + MatricesBasicasEliminadas + CuadrillasEliminadas +
                                         MaterialesEliminados + ManoDeObraEliminada + HerramientasEliminadas +
                                         MaquinariaEliminada;
        }

        public sealed class DepuracionPreview
        {
            public int MatricesApuCandidatas { get; set; }
            public int MatricesBasicasCandidatas { get; set; }
            public int CuadrillasCandidatas { get; set; }
            public int MaterialesCandidatos { get; set; }
            public int ManoDeObraCandidata { get; set; }
            public int HerramientasCandidatas { get; set; }
            public int MaquinariaCandidata { get; set; }
            public int TotalCandidatas => MatricesApuCandidatas + MatricesBasicasCandidatas + CuadrillasCandidatas +
                                          MaterialesCandidatos + ManoDeObraCandidata + HerramientasCandidatas +
                                          MaquinariaCandidata;
        }

        public static DepuracionPreview ObtenerPreview(SOPROContext context, int proyectoId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var plan = CrearPlan(context, proyectoId);

            return new DepuracionPreview
            {
                MatricesApuCandidatas = plan.Matrices.Count(m => m.Tipo == TipoMatriz.APU),
                MatricesBasicasCandidatas = plan.Matrices.Count(m => m.Tipo == TipoMatriz.Basico),
                CuadrillasCandidatas = plan.Matrices.Count(m => m.Tipo == TipoMatriz.Cuadrilla),
                MaterialesCandidatos = plan.MaterialIds.Count,
                ManoDeObraCandidata = plan.ManoDeObraIds.Count,
                HerramientasCandidatas = plan.HerramientaIds.Count,
                MaquinariaCandidata = plan.MaquinariaIds.Count
            };
        }

        public static DepuracionResultado Ejecutar(SOPROContext context, int proyectoId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var plan = CrearPlan(context, proyectoId);

            var ids = plan.Matrices.Select(x => x.Id).ToHashSet();

            var componentes = ids.Count == 0
                ? new List<ComponenteMatriz>()
                : context.ComponentesMatriz
                    .Where(c => ids.Contains(c.MatrizId))
                    .ToList();

            if (componentes.Count > 0)
                context.ComponentesMatriz.RemoveRange(componentes);

            var matrices = ids.Count == 0
                ? new List<Matriz>()
                : context.Matrices
                    .Where(m => ids.Contains(m.Id))
                    .ToList();

            if (matrices.Count > 0)
                context.Matrices.RemoveRange(matrices);

            context.SaveChanges();

            var maquinaria = plan.MaquinariaIds.Count == 0 ? new List<Maquinaria>() :
                context.Maquinaria.Where(m => plan.MaquinariaIds.Contains(m.Id)).ToList();
            if (maquinaria.Count > 0)
                context.Maquinaria.RemoveRange(maquinaria);
            if (maquinaria.Count > 0)
                context.SaveChanges();

            var materiales = plan.MaterialIds.Count == 0 ? new List<Material>() :
                context.Materiales.Where(m => plan.MaterialIds.Contains(m.Id)).ToList();
            if (materiales.Count > 0)
                context.Materiales.RemoveRange(materiales);

            var manoDeObra = plan.ManoDeObraIds.Count == 0 ? new List<ManoDeObra>() :
                context.ManoDeObra.Where(m => plan.ManoDeObraIds.Contains(m.Id)).ToList();
            if (manoDeObra.Count > 0)
                context.ManoDeObra.RemoveRange(manoDeObra);

            var herramientas = plan.HerramientaIds.Count == 0 ? new List<Herramienta>() :
                context.Herramientas.Where(h => plan.HerramientaIds.Contains(h.Id)).ToList();
            if (herramientas.Count > 0)
                context.Herramientas.RemoveRange(herramientas);

            context.SaveChanges();

            return new DepuracionResultado
            {
                MatricesApuEliminadas = plan.Matrices.Count(m => m.Tipo == TipoMatriz.APU),
                MatricesBasicasEliminadas = plan.Matrices.Count(m => m.Tipo == TipoMatriz.Basico),
                CuadrillasEliminadas = plan.Matrices.Count(m => m.Tipo == TipoMatriz.Cuadrilla),
                MaterialesEliminados = materiales.Count,
                ManoDeObraEliminada = manoDeObra.Count,
                HerramientasEliminadas = herramientas.Count,
                MaquinariaEliminada = maquinaria.Count
            };
        }

        private static CleanupPlan CrearPlan(SOPROContext context, int proyectoId)
        {
            List<MatrixNode> matrices = CalcularCandidatos(CargarSnapshot(context, proyectoId));
            HashSet<int> matrixIds = matrices.Select(m => m.Id).ToHashSet();
            List<ComponenteMatriz> componentesRestantes = context.ComponentesMatriz.AsNoTracking()
                .Where(c => !matrixIds.Contains(c.MatrizId)).ToList();

            HashSet<int> maquinariaEnUso = componentesRestantes.Where(c => c.MaquinariaId.HasValue)
                .Select(c => c.MaquinariaId!.Value).ToHashSet();
            List<int> maquinariaIds = context.Maquinaria.AsNoTracking()
                .Where(m => m.ProyectoId == proyectoId && !maquinariaEnUso.Contains(m.Id))
                .Select(m => m.Id).ToList();
            HashSet<int> maquinariaCandidataIds = maquinariaIds.ToHashSet();

            HashSet<int> materialEnUso = componentesRestantes.Where(c => c.MaterialId.HasValue)
                .Select(c => c.MaterialId!.Value).ToHashSet();
            HashSet<int> manoDeObraEnUso = componentesRestantes.Where(c => c.ManoDeObraId.HasValue)
                .Select(c => c.ManoDeObraId!.Value).ToHashSet();
            List<Maquinaria> maquinariaRetenida = context.Maquinaria.AsNoTracking()
                .Where(m => m.ProyectoId == proyectoId && !maquinariaCandidataIds.Contains(m.Id)).ToList();
            foreach (Maquinaria maquinaria in maquinariaRetenida)
            {
                if (maquinaria.MaterialCombustibleId.HasValue)
                    materialEnUso.Add(maquinaria.MaterialCombustibleId.Value);
                if (maquinaria.MaterialAceiteId.HasValue)
                    materialEnUso.Add(maquinaria.MaterialAceiteId.Value);
                if (maquinaria.ManoDeObraOperadorId.HasValue)
                    manoDeObraEnUso.Add(maquinaria.ManoDeObraOperadorId.Value);
            }

            List<int> materialIds = context.Materiales.AsNoTracking()
                .Where(m => m.ProyectoId == proyectoId && !materialEnUso.Contains(m.Id))
                .Select(m => m.Id).ToList();
            List<int> manoDeObraIds = context.ManoDeObra.AsNoTracking()
                .Where(m => m.ProyectoId == proyectoId && !manoDeObraEnUso.Contains(m.Id))
                .Select(m => m.Id).ToList();
            HashSet<int> herramientaEnUso = componentesRestantes.Where(c => c.HerramientaId.HasValue)
                .Select(c => c.HerramientaId!.Value).ToHashSet();
            List<int> herramientaIds = context.Herramientas.AsNoTracking()
                .Where(h => h.ProyectoId == proyectoId && !herramientaEnUso.Contains(h.Id))
                .Select(h => h.Id).ToList();

            return new CleanupPlan(matrices, maquinariaIds, materialIds, manoDeObraIds, herramientaIds);
        }

        private sealed record CleanupPlan(
            List<MatrixNode> Matrices,
            List<int> MaquinariaIds,
            List<int> MaterialIds,
            List<int> ManoDeObraIds,
            List<int> HerramientaIds);

        private static Snapshot CargarSnapshot(SOPROContext context, int proyectoId)
        {
            var matrices = context.Matrices
                .AsNoTracking()
                .Where(m => m.ProyectoId == proyectoId)
                .Select(m => new MatrixNode
                {
                    Id = m.Id,
                    Tipo = m.Tipo
                })
                .ToList();

            var conceptoMatrixIds = context.ConceptosPresupuesto
                .AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId && c.MatrizId.HasValue)
                .Select(c => c.MatrizId!.Value)
                .Distinct()
                .ToHashSet();

            var referenciasAuxiliares = context.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.AuxiliarId.HasValue)
                .Select(c => new ReferenceEdge { MatrizId = c.MatrizId, AuxiliarId = c.AuxiliarId!.Value })
                .ToList();

            return new Snapshot
            {
                Matrices = matrices,
                MatricesAsignadasAPresupuesto = conceptoMatrixIds,
                ReferenciasAuxiliares = referenciasAuxiliares
            };
        }

        private static List<MatrixNode> CalcularCandidatos(Snapshot snapshot)
        {
            var remaining = snapshot.Matrices.ToDictionary(m => m.Id);
            var assignedToBudget = snapshot.MatricesAsignadasAPresupuesto;
            var references = snapshot.ReferenciasAuxiliares;
            var candidatos = new List<MatrixNode>();

            bool removedAny;
            do
            {
                removedAny = false;
                var remainingIds = remaining.Keys.ToHashSet();
                var referencedAuxIds = references
                    .Where(r => remainingIds.Contains(r.MatrizId) && remainingIds.Contains(r.AuxiliarId))
                    .Select(r => r.AuxiliarId)
                    .ToHashSet();

                var currentCandidates = remaining.Values
                    .Where(m => EsCandidata(m, assignedToBudget, referencedAuxIds))
                    .OrderBy(m => m.Tipo)
                    .ThenBy(m => m.Id)
                    .ToList();

                if (currentCandidates.Count == 0)
                    break;

                foreach (var candidate in currentCandidates)
                {
                    if (remaining.Remove(candidate.Id))
                    {
                        candidatos.Add(candidate);
                        removedAny = true;
                    }
                }
            }
            while (removedAny);

            return candidatos;
        }

        private static bool EsCandidata(MatrixNode matrix, HashSet<int> assignedToBudget, HashSet<int> referencedAuxIds)
        {
            return matrix.Tipo switch
            {
                TipoMatriz.APU => !assignedToBudget.Contains(matrix.Id),
                TipoMatriz.Basico => !referencedAuxIds.Contains(matrix.Id),
                TipoMatriz.Cuadrilla => !referencedAuxIds.Contains(matrix.Id),
                _ => false
            };
        }

        private sealed class Snapshot
        {
            public List<MatrixNode> Matrices { get; set; } = new();
            public HashSet<int> MatricesAsignadasAPresupuesto { get; set; } = new();
            public List<ReferenceEdge> ReferenciasAuxiliares { get; set; } = new();
        }

        private sealed class MatrixNode
        {
            public int Id { get; set; }
            public TipoMatriz Tipo { get; set; }
        }

        private sealed class ReferenceEdge
        {
            public int MatrizId { get; set; }
            public int AuxiliarId { get; set; }
        }

    }
}
