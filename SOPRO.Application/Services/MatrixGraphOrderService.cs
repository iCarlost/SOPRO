using System;
using System.Collections.Generic;
using System.Linq;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services
{
    // ╔══════════════════════════════════════════════════════════════════════════╗
    // ║  MatrixGraphOrderService — orden topológico de matrices (N7-1c).         ║
    // ║  Reemplaza el orden por tipo (Cuadrilla→Básico→APU) del recálculo:       ║
    // ║  básicos anidados exigen procesar cada auxiliar antes que sus padres.    ║
    // ║  Kahn estable: conserva el orden de entrada entre nodos listos, y        ║
    // ║  lanza diagnóstico con ruta si el conjunto contiene un ciclo.            ║
    // ╚══════════════════════════════════════════════════════════════════════════╝

    /// <summary>
    /// Ordena un conjunto de matrices de modo que cada auxiliar referenciado dentro
    /// del conjunto se procese antes que sus dependientes.
    /// </summary>
    /// <remarks>
    /// Invariantes:
    ///  - Las aristas provienen de <see cref="ComponenteMatriz.AuxiliarId"/>.
    ///  - Una referencia a un Id ausente del conjunto (auxiliar externa o sin guardar)
    ///    se trata como hoja: su costo directo es fijo y lo aporta el almacenado.
    ///  - Las matrices sin persistir (Id 0) se tratan como hojas.
    ///  - Un ciclo dentro del conjunto lanza <see cref="InvalidOperationException"/>
    ///    con la ruta del ciclo.
    /// </remarks>
    public static class MatrixGraphOrderService
    {
        /// <summary>
        /// Devuelve las matrices en orden topológico estable por dependencias de
        /// auxiliares internas; lanza si el conjunto contiene un ciclo.
        /// </summary>
        public static List<Matriz> OrdenTopologico(IEnumerable<Matriz> matrices)
        {
            if (matrices == null) throw new ArgumentNullException(nameof(matrices));

            var lista = matrices.ToList();
            var vistas = new HashSet<Matriz>();
            var porId = new Dictionary<int, Matriz>();

            foreach (var matriz in lista)
            {
                if (matriz == null)
                    throw new InvalidOperationException("El conjunto de matrices contiene una matriz nula.");
                if (!vistas.Add(matriz))
                    throw new InvalidOperationException(
                        $"La misma instancia de matriz aparece duplicada en el conjunto (Id {matriz.Id}).");
                if (matriz.Id == 0) continue;
                if (porId.TryGetValue(matriz.Id, out _))
                    throw new InvalidOperationException($"Matriz duplicada en el conjunto: Id {matriz.Id}.");
                porId.Add(matriz.Id, matriz);
            }

            var dependientes = new Dictionary<int, List<Matriz>>();
            var grados = new Dictionary<Matriz, int>();

            foreach (var matriz in lista)
            {
                var grado = 0;
                foreach (var comp in matriz.Componentes ?? (IEnumerable<ComponenteMatriz>)Array.Empty<ComponenteMatriz>())
                {
                    if (!comp.AuxiliarId.HasValue) continue;
                    if (!porId.TryGetValue(comp.AuxiliarId.Value, out var auxiliar)) continue;
                    grado++;
                    if (!dependientes.TryGetValue(auxiliar.Id, out var hijos))
                    {
                        hijos = new List<Matriz>();
                        dependientes.Add(auxiliar.Id, hijos);
                    }
                    hijos.Add(matriz);
                }
                grados[matriz] = grado;
            }

            var orden = new List<Matriz>(lista.Count);
            var listas = new Queue<Matriz>(lista.Where(m => grados[m] == 0));
            while (listas.Count > 0)
            {
                var actual = listas.Dequeue();
                orden.Add(actual);
                if (!dependientes.TryGetValue(actual.Id, out var hijos)) continue;
                foreach (var hijo in hijos)
                {
                    if (--grados[hijo] == 0)
                        listas.Enqueue(hijo);
                }
            }

            if (orden.Count == lista.Count) return orden;

            throw CicloDetectado(lista, porId, grados);
        }

        private static InvalidOperationException CicloDetectado(
            List<Matriz> lista,
            Dictionary<int, Matriz> porId,
            Dictionary<Matriz, int> grados)
        {
            var restantes = lista.Where(m => grados[m] > 0).ToList();
            var restantesPorId = new Dictionary<int, Matriz>();
            foreach (var matriz in restantes)
                if (matriz.Id != 0)
                    restantesPorId[matriz.Id] = matriz;

            var enRuta = new HashSet<Matriz>();
            var ruta = new List<Matriz>();
            var actual = restantes[0];
            while (enRuta.Add(actual))
            {
                ruta.Add(actual);
                actual = DependenciaPendiente(actual, restantesPorId)!;
            }

            var inicio = ruta.IndexOf(actual);
            var ciclo = ruta.Skip(inicio).Append(actual);
            return new InvalidOperationException(
                "Ciclo de matrices detectado. Ruta: " +
                string.Join(" -> ", ciclo.Select(m => m.Id)));
        }

        private static Matriz? DependenciaPendiente(Matriz matriz, Dictionary<int, Matriz> restantesPorId)
        {
            foreach (var comp in matriz.Componentes ?? (IEnumerable<ComponenteMatriz>)Array.Empty<ComponenteMatriz>())
            {
                if (!comp.AuxiliarId.HasValue) continue;
                if (restantesPorId.TryGetValue(comp.AuxiliarId.Value, out var auxiliar))
                    return auxiliar;
            }
            return null;
        }
    }
}
