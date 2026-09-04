using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Insumos;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Application.Services
{
    public static class InsumoSelectionService
    {
        public static List<SelectableInsumoDto> GetSelectableInsumos(
            SOPROContext context,
            int proyectoId,
            TipoComponenteMatriz tipoComponente,
            string? filtro,
            bool incluirManoDeObraIndividual = true,
            bool incluirCuadrillas = true)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));

            filtro = string.IsNullOrWhiteSpace(filtro) ? null : filtro.Trim().ToLowerInvariant();

            return tipoComponente switch
            {
                TipoComponenteMatriz.Material => GetMateriales(context, proyectoId, filtro),
                TipoComponenteMatriz.ManoDeObra => GetManoDeObra(context, proyectoId, filtro, incluirManoDeObraIndividual, incluirCuadrillas),
                TipoComponenteMatriz.Maquinaria => GetMaquinaria(context, proyectoId, filtro),
                TipoComponenteMatriz.Auxiliar => GetBasicos(context, proyectoId, filtro),
                TipoComponenteMatriz.Herramienta => GetHerramientas(context, proyectoId, filtro),
                _ => new List<SelectableInsumoDto>()
            };
        }

        public static List<ComponenteMatriz> BuildSelectedComponents(
            SOPROContext context,
            TipoComponenteMatriz tipoComponente,
            IEnumerable<int> selectedIds,
            IReadOnlyDictionary<int, string>? tagsById,
            decimal cantidad)
        {
            if (context == null) throw new ArgumentNullException(nameof(context));
            if (selectedIds == null) throw new ArgumentNullException(nameof(selectedIds));

            var ids = selectedIds.Distinct().ToList();
            var componentes = new List<ComponenteMatriz>();
            if (ids.Count == 0) return componentes;

            switch (tipoComponente)
            {
                case TipoComponenteMatriz.Material:
                    var materiales = context.Materiales.Where(m => ids.Contains(m.Id)).ToList();
                    componentes.AddRange(materiales.Select(material => new ComponenteMatriz
                    {
                        MaterialId = material.Id,
                        Material = material,
                        TipoComponente = TipoComponenteMatriz.Material,
                        Cantidad = cantidad,
                        Importe = material.PrecioUnitario * cantidad
                    }));
                    break;

                case TipoComponenteMatriz.ManoDeObra:
                    foreach (var id in ids)
                    {
                        string? tag = null;
                        tagsById?.TryGetValue(id, out tag);
                        if (string.Equals(tag, "Cuadrilla", StringComparison.OrdinalIgnoreCase))
                        {
                            var cuadrilla = context.Matrices.FirstOrDefault(m => m.Id == id);
                            if (cuadrilla != null)
                            {
                                componentes.Add(new ComponenteMatriz
                                {
                                    AuxiliarId = cuadrilla.Id,
                                    Auxiliar = cuadrilla,
                                    TipoComponente = TipoComponenteMatriz.Auxiliar,
                                    Cantidad = cantidad,
                                    Importe = cuadrilla.CostoDirecto * cantidad
                                });
                            }
                        }
                        else
                        {
                            var manoDeObra = context.ManoDeObra.FirstOrDefault(m => m.Id == id);
                            if (manoDeObra != null)
                            {
                                componentes.Add(new ComponenteMatriz
                                {
                                    ManoDeObraId = manoDeObra.Id,
                                    ManoDeObra = manoDeObra,
                                    TipoComponente = TipoComponenteMatriz.ManoDeObra,
                                    Cantidad = cantidad,
                                    Importe = 0
                                });
                            }
                        }
                    }
                    break;

                case TipoComponenteMatriz.Maquinaria:
                    var maquinaria = context.Maquinaria.Where(m => ids.Contains(m.Id)).ToList();
                    componentes.AddRange(maquinaria.Select(item => new ComponenteMatriz
                    {
                        MaquinariaId = item.Id,
                        Maquinaria = item,
                        TipoComponente = TipoComponenteMatriz.Maquinaria,
                        Cantidad = cantidad,
                        Rendimiento = Maquinaria.CalcularRendimiento(cantidad),
                        Importe = item.CostoHorario * cantidad
                    }));
                    break;

                case TipoComponenteMatriz.Auxiliar:
                    var auxiliares = context.Matrices.Where(m => ids.Contains(m.Id)).ToList();
                    componentes.AddRange(auxiliares.Select(item => new ComponenteMatriz
                    {
                        AuxiliarId = item.Id,
                        Auxiliar = item,
                        TipoComponente = TipoComponenteMatriz.Auxiliar,
                        Cantidad = cantidad,
                        Importe = item.CostoDirecto * cantidad
                    }));
                    break;

                case TipoComponenteMatriz.Herramienta:
                    var herramientas = context.Herramientas.Where(h => ids.Contains(h.Id)).ToList();
                    componentes.AddRange(herramientas.Select(item => new ComponenteMatriz
                    {
                        HerramientaId = item.Id,
                        Herramienta = item,
                        TipoComponente = TipoComponenteMatriz.Herramienta,
                        Cantidad = cantidad,
                        Importe = 0
                    }));
                    break;
            }

            return componentes;
        }

        private static List<SelectableInsumoDto> GetMateriales(SOPROContext context, int proyectoId, string? filtro)
        {
            return context.Materiales
                .Where(m => m.ProyectoId == proyectoId)
                .OrderBy(m => m.Clave)
                .ToList()
                .Where(m => Matches(filtro, m.Clave, m.Descripcion))
                .Select(m => new SelectableInsumoDto
                {
                    Id = m.Id,
                    TipoComponente = TipoComponenteMatriz.Material,
                    Clave = m.Clave,
                    Descripcion = m.Descripcion,
                    Unidad = m.Unidad,
                    PrecioUnitario = m.PrecioUnitario,
                    PrecioMostrado = m.PrecioUnitario.ToString("C2")
                })
                .ToList();
        }

        private static List<SelectableInsumoDto> GetManoDeObra(SOPROContext context, int proyectoId, string? filtro, bool incluirManoDeObraIndividual, bool incluirCuadrillas)
        {
            var items = new List<SelectableInsumoDto>();

            if (incluirManoDeObraIndividual)
            {
                items.AddRange(context.ManoDeObra
                    .Where(m => m.ProyectoId == proyectoId)
                    .OrderBy(m => m.Clave)
                    .ToList()
                    .Where(m => Matches(filtro, m.Clave, m.Descripcion))
                    .Select(m => new SelectableInsumoDto
                    {
                        Id = m.Id,
                        TipoComponente = TipoComponenteMatriz.ManoDeObra,
                        Clave = m.Clave,
                        Descripcion = m.Descripcion,
                        Unidad = m.Unidad,
                        PrecioUnitario = m.SalarioReal,
                        PrecioMostrado = m.SalarioReal.ToString("C2"),
                        Tag = "Individual"
                    }));
            }

            if (incluirCuadrillas)
            {
                items.AddRange(context.Matrices
                    .Where(m => m.ProyectoId == proyectoId && m.Tipo == TipoMatriz.Cuadrilla)
                    .OrderBy(m => m.Clave)
                    .ToList()
                    .Where(m => Matches(filtro, m.Clave, m.Descripcion))
                    .Select(m => new SelectableInsumoDto
                    {
                        Id = m.Id,
                        TipoComponente = TipoComponenteMatriz.Auxiliar,
                        Clave = m.Clave,
                        Descripcion = "👷 " + m.Descripcion,
                        Unidad = m.Unidad,
                        PrecioUnitario = m.CostoDirecto,
                        PrecioMostrado = m.CostoDirecto.ToString("C2"),
                        Tag = "Cuadrilla"
                    }));
            }

            return items.OrderBy(i => i.Clave).ToList();
        }

        private static List<SelectableInsumoDto> GetMaquinaria(SOPROContext context, int proyectoId, string? filtro)
        {
            return context.Maquinaria
                .Where(m => m.ProyectoId == proyectoId)
                .OrderBy(m => m.Clave)
                .ToList()
                .Where(m => Matches(filtro, m.Clave, m.Descripcion))
                .Select(m => new SelectableInsumoDto
                {
                    Id = m.Id,
                    TipoComponente = TipoComponenteMatriz.Maquinaria,
                    Clave = m.Clave,
                    Descripcion = m.Descripcion,
                    Unidad = "hora",
                    PrecioUnitario = m.CostoHorario,
                    PrecioMostrado = m.CostoHorario.ToString("C2")
                })
                .ToList();
        }

        private static List<SelectableInsumoDto> GetBasicos(SOPROContext context, int proyectoId, string? filtro)
        {
            return context.Matrices
                .Where(m => m.ProyectoId == proyectoId && m.Tipo == TipoMatriz.Basico)
                .OrderBy(m => m.Clave)
                .ToList()
                .Where(m => Matches(filtro, m.Clave, m.Descripcion))
                .Select(m => new SelectableInsumoDto
                {
                    Id = m.Id,
                    TipoComponente = TipoComponenteMatriz.Auxiliar,
                    Clave = m.Clave,
                    Descripcion = m.Descripcion,
                    Unidad = m.Unidad,
                    PrecioUnitario = m.CostoDirecto,
                    PrecioMostrado = m.CostoDirecto.ToString("C4")
                })
                .ToList();
        }

        private static List<SelectableInsumoDto> GetHerramientas(SOPROContext context, int proyectoId, string? filtro)
        {
            return context.Herramientas
                .Where(h => h.ProyectoId == proyectoId)
                .OrderBy(h => h.Clave)
                .ToList()
                .Where(h => Matches(filtro, h.Clave, h.Descripcion))
                .Select(h => new SelectableInsumoDto
                {
                    Id = h.Id,
                    TipoComponente = TipoComponenteMatriz.Herramienta,
                    Clave = h.Clave,
                    Descripcion = h.Descripcion,
                    Unidad = h.Unidad,
                    PrecioUnitario = h.PrecioUnitario,
                    PrecioMostrado = h.EsPorcentajeMO ? $"{h.PrecioUnitario:N2}%" : h.PrecioUnitario.ToString("C2")
                })
                .ToList();
        }

        private static bool Matches(string? filtro, string? clave, string? descripcion)
        {
            if (filtro == null) return true;
            return (clave?.ToLowerInvariant().Contains(filtro) == true)
                || (descripcion?.ToLowerInvariant().Contains(filtro) == true);
        }
    }
}
