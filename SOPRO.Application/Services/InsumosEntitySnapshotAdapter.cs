using System.Collections.Generic;
using System.Linq;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services;

/// <summary>
/// Materializa un grafo local de matrices para los calculos de insumos.
/// EF queda en la frontera: los servicios nunca enlazan navegaciones sobre
/// instancias devueltas por el contexto.
/// </summary>
internal static class InsumosEntitySnapshotAdapter
{
    public static List<Matriz> CloneAndLink(IEnumerable<Matriz> source)
    {
        var clones = source.ToDictionary(m => m.Id, CloneMatrix);
        foreach (var original in source)
        {
            var clone = clones[original.Id];
            foreach (var component in clone.Componentes)
            {
                if (component.AuxiliarId is int auxiliaryId && clones.TryGetValue(auxiliaryId, out var auxiliary))
                    component.Auxiliar = auxiliary;
            }
        }

        return clones.Values.ToList();
    }

    private static Matriz CloneMatrix(Matriz source)
    {
        var clone = new Matriz
        {
            Id = source.Id,
            Clave = source.Clave,
            Descripcion = source.Descripcion,
            Unidad = source.Unidad,
            Tipo = source.Tipo,
            CostoDirecto = source.CostoDirecto,
            ProyectoId = source.ProyectoId,
            MatrizMaestraId = source.MatrizMaestraId,
            Origen = source.Origen,
            Notas = source.Notas,
            FechaCreacion = source.FechaCreacion,
            FechaModificacion = source.FechaModificacion,
            FechaUltimoCalculo = source.FechaUltimoCalculo
        };

        foreach (var sourceComponent in source.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id))
        {
            clone.Componentes.Add(new ComponenteMatriz
            {
                Id = sourceComponent.Id,
                MatrizId = sourceComponent.MatrizId,
                TipoComponente = sourceComponent.TipoComponente,
                MaterialId = sourceComponent.MaterialId,
                Material = sourceComponent.Material,
                ManoDeObraId = sourceComponent.ManoDeObraId,
                ManoDeObra = sourceComponent.ManoDeObra,
                MaquinariaId = sourceComponent.MaquinariaId,
                Maquinaria = sourceComponent.Maquinaria,
                AuxiliarId = sourceComponent.AuxiliarId,
                HerramientaId = sourceComponent.HerramientaId,
                Herramienta = sourceComponent.Herramienta,
                Cantidad = sourceComponent.Cantidad,
                Rendimiento = sourceComponent.Rendimiento,
                Importe = sourceComponent.Importe,
                Orden = sourceComponent.Orden,
                Notas = sourceComponent.Notas
            });
        }

        return clone;
    }
}
