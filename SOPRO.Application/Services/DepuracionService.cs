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

            var snapshot = CargarSnapshot(context, proyectoId);
            var candidatos = CalcularCandidatos(snapshot);

            return new DepuracionPreview
            {
                MatricesApuCandidatas = candidatos.Count(m => m.Tipo == TipoMatriz.APU),
                MatricesBasicasCandidatas = candidatos.Count(m => m.Tipo == TipoMatriz.Basico),
                CuadrillasCandidatas = candidatos.Count(m => m.Tipo == TipoMatriz.Cuadrilla),
                MaterialesCandidatos = ContarMaterialesSinUso(context, proyectoId),
                ManoDeObraCandidata = ContarManoDeObraSinUso(context, proyectoId),
                HerramientasCandidatas = ContarHerramientasSinUso(context, proyectoId),
                MaquinariaCandidata = ContarMaquinariaSinUso(context, proyectoId)
            };
        }

        public static DepuracionResultado Ejecutar(SOPROContext context, int proyectoId)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            var snapshot = CargarSnapshot(context, proyectoId);
            var candidatos = CalcularCandidatos(snapshot);

            var ids = candidatos.Select(x => x.Id).ToHashSet();

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

            var materiales = ObtenerMaterialesSinUso(context, proyectoId);
            if (materiales.Count > 0)
                context.Materiales.RemoveRange(materiales);

            var manoDeObra = ObtenerManoDeObraSinUso(context, proyectoId);
            if (manoDeObra.Count > 0)
                context.ManoDeObra.RemoveRange(manoDeObra);

            var herramientas = ObtenerHerramientasSinUso(context, proyectoId);
            if (herramientas.Count > 0)
                context.Herramientas.RemoveRange(herramientas);

            var maquinaria = ObtenerMaquinariaSinUso(context, proyectoId);
            if (maquinaria.Count > 0)
                context.Maquinaria.RemoveRange(maquinaria);

            context.SaveChanges();

            return new DepuracionResultado
            {
                MatricesApuEliminadas = candidatos.Count(m => m.Tipo == TipoMatriz.APU),
                MatricesBasicasEliminadas = candidatos.Count(m => m.Tipo == TipoMatriz.Basico),
                CuadrillasEliminadas = candidatos.Count(m => m.Tipo == TipoMatriz.Cuadrilla),
                MaterialesEliminados = materiales.Count,
                ManoDeObraEliminada = manoDeObra.Count,
                HerramientasEliminadas = herramientas.Count,
                MaquinariaEliminada = maquinaria.Count
            };
        }

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

        private static int ContarMaterialesSinUso(SOPROContext context, int proyectoId)
            => ObtenerMaterialesSinUso(context, proyectoId).Count;

        private static int ContarManoDeObraSinUso(SOPROContext context, int proyectoId)
            => ObtenerManoDeObraSinUso(context, proyectoId).Count;

        private static int ContarHerramientasSinUso(SOPROContext context, int proyectoId)
            => ObtenerHerramientasSinUso(context, proyectoId).Count;

        private static int ContarMaquinariaSinUso(SOPROContext context, int proyectoId)
            => ObtenerMaquinariaSinUso(context, proyectoId).Count;

        private static List<Material> ObtenerMaterialesSinUso(SOPROContext context, int proyectoId)
        {
            var usados = context.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.MaterialId.HasValue)
                .Select(c => c.MaterialId!.Value)
                .Distinct()
                .ToHashSet();

            return context.Materiales
                .Where(m => m.ProyectoId == proyectoId && !usados.Contains(m.Id))
                .ToList();
        }

        private static List<ManoDeObra> ObtenerManoDeObraSinUso(SOPROContext context, int proyectoId)
        {
            var usados = context.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.ManoDeObraId.HasValue)
                .Select(c => c.ManoDeObraId!.Value)
                .Distinct()
                .ToHashSet();

            return context.ManoDeObra
                .Where(m => m.ProyectoId == proyectoId && !usados.Contains(m.Id))
                .ToList();
        }

        private static List<Herramienta> ObtenerHerramientasSinUso(SOPROContext context, int proyectoId)
        {
            var usados = context.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.HerramientaId.HasValue)
                .Select(c => c.HerramientaId!.Value)
                .Distinct()
                .ToHashSet();

            return context.Herramientas
                .Where(h => h.ProyectoId == proyectoId && !usados.Contains(h.Id))
                .ToList();
        }

        private static List<Maquinaria> ObtenerMaquinariaSinUso(SOPROContext context, int proyectoId)
        {
            var usados = context.ComponentesMatriz
                .AsNoTracking()
                .Where(c => c.MaquinariaId.HasValue)
                .Select(c => c.MaquinariaId!.Value)
                .Distinct()
                .ToHashSet();

            return context.Maquinaria
                .Where(m => m.ProyectoId == proyectoId && !usados.Contains(m.Id))
                .ToList();
        }
    }
}
