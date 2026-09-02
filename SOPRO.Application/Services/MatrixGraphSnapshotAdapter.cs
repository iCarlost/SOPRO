using System;
using System.Collections.Generic;
using Sopro.Calculation.Matrices;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  MatrixGraphSnapshotAdapter — puente entidades → grafo puro (N7-1b).     ║
    // ║  Materializa la lista plana del editor como un grafo de un solo nodo raiz ║
    // ║  con hojas precalculadas para las auxiliares (costo directo almacenado,  ║
    // ║  mismo contrato que usaba el servicio canónico). La aritmética vive en    ║
    // ║  MatrixGraphCalculator; aquí solo hay mapeo.                              ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Convierte componentes de entidades en un <see cref="MatrixGraphInput"/> evaluable
    /// por el motor puro, sin aritmética propia.
    /// </summary>
    public static class MatrixGraphSnapshotAdapter
    {
        /// <summary>
        /// Resultado de materializar una lista plana de componentes.
        /// </summary>
        public sealed class Snapshot
        {
            internal Snapshot(
                MatrixGraphInput graph,
                int rootMatrixId,
                IReadOnlyDictionary<ComponenteMatriz, int> componentIds)
            {
                Graph = graph;
                RootMatrixId = rootMatrixId;
                ComponentIds = componentIds;
            }

            /// <summary>Grafo materializado listo para el evaluador.</summary>
            public MatrixGraphInput Graph { get; }
            /// <summary>Identificador del nodo raiz dentro del grafo.</summary>
            public int RootMatrixId { get; }
            /// <summary>Claves sinteticas asignadas a cada componente de la lista.</summary>
            public IReadOnlyDictionary<ComponenteMatriz, int> ComponentIds { get; }
        }

        /// <summary>
        /// Materializa la lista plana de componentes de una matriz (orden de origen
        /// conservado en <c>Order</c>). Las auxiliares se mapean como hojas con su
        /// costo directo almacenado, replicando el contrato del calculador canonico:
        /// el propagador garantiza el postorden antes de recalcular padres.
        /// Requiere navegaciones completas: una entidad de insumo o auxiliar sin cargar
        /// lanza <see cref="InvalidOperationException"/> en lugar de propagar importes
        /// obsoletos en silencio.
        /// </summary>
        public static Snapshot BuildRootSnapshot(int rootMatrixId, IList<ComponenteMatriz> componentes)
        {
            if (componentes == null) throw new ArgumentNullException(nameof(componentes));

            var componentIds = new Dictionary<ComponenteMatriz, int>();
            var inputs = new List<MatrixComponentInput>(componentes.Count);
            var leafNodeIds = new Dictionary<Matriz, int>();
            var leaves = new List<MatrixNodeInput>();
            var nextTempNodeId = 0;

            for (var index = 0; index < componentes.Count; index++)
            {
                var comp = componentes[index];
                if (comp == null)
                    throw new InvalidOperationException($"El componente en la posicion {index} es nulo.");
                componentIds.Add(comp, index + 1);

                var id = index + 1;
                MatrixComponentInput input;
                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        input = new MatrixComponentInput(
                            id, index, MatrixComponentType.Material, comp.Cantidad,
                            Require(comp.Material, index, comp.TipoComponente, nameof(comp.Material)).PrecioUnitario, false);
                        break;

                    case TipoComponenteMatriz.ManoDeObra:
                    {
                        var manoDeObra = Require(comp.ManoDeObra, index, comp.TipoComponente, nameof(comp.ManoDeObra));
                        input = new MatrixComponentInput(
                            id, index, MatrixComponentType.Labor, comp.Cantidad,
                            manoDeObra.SalarioReal, manoDeObra.EsPorcentajeMO);
                        break;
                    }

                    case TipoComponenteMatriz.Maquinaria:
                        input = new MatrixComponentInput(
                            id, index, MatrixComponentType.Machinery, comp.Cantidad,
                            Require(comp.Maquinaria, index, comp.TipoComponente, nameof(comp.Maquinaria)).CostoHorario, false);
                        break;

                    case TipoComponenteMatriz.Herramienta:
                    {
                        var herramienta = Require(comp.Herramienta, index, comp.TipoComponente, nameof(comp.Herramienta));
                        input = new MatrixComponentInput(
                            id, index, MatrixComponentType.Tool, comp.Cantidad,
                            herramienta.PrecioUnitario, herramienta.EsPorcentajeMO);
                        break;
                    }

                    case TipoComponenteMatriz.Auxiliar:
                    {
                        var auxiliar = Require(comp.Auxiliar, index, comp.TipoComponente, nameof(comp.Auxiliar));
                        if (!leafNodeIds.TryGetValue(auxiliar, out var auxNodeId))
                        {
                            auxNodeId = --nextTempNodeId;
                            leafNodeIds.Add(auxiliar, auxNodeId);
                            leaves.Add(new MatrixNodeInput(
                                auxNodeId,
                                auxiliar.Tipo == TipoMatriz.Cuadrilla ? MatrixType.Crew : MatrixType.Basic,
                                Array.Empty<MatrixComponentInput>(),
                                auxiliar.CostoDirecto));
                        }

                        input = new MatrixComponentInput(
                            id, index, MatrixComponentType.Auxiliary, comp.Cantidad,
                            0m, false, auxNodeId);
                        break;
                    }

                    default:
                        throw new InvalidOperationException(
                            $"Tipo de componente no soportado: {(int)comp.TipoComponente} (posicion {index}).");
                }

                inputs.Add(input);
            }

            var nodes = new List<MatrixNodeInput>(leaves.Count + 1)
            {
                new MatrixNodeInput(rootMatrixId, MatrixType.Apu, inputs)
            };
            nodes.AddRange(leaves);

            return new Snapshot(new MatrixGraphInput(rootMatrixId, nodes), rootMatrixId, componentIds);
        }

        private static T Require<T>(T? navigation, int index, TipoComponenteMatriz tipo, string name)
            where T : class
            => navigation ?? throw new InvalidOperationException(
                $"El componente {tipo} en la posicion {index} no tiene la navegacion {name} cargada. " +
                "El snapshot del grafo requiere navegaciones completas.");
    }
}
