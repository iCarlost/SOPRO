using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using System;
using System.Collections.Generic;
using System.Linq;

namespace SOPRO.WinForms.Helpers
{
    public static class ColumnasPresupuestoHelper
    {

        /// <summary>
        /// Crea las columnas predeterminadas para un proyecto nuevo
        /// </summary>
        public static void CrearColumnasPredeterminadas(SOPROContext context, int proyectoId)
        {
            var columnas = new[]
            {
                // ============================================
                // COLUMNAS BASE DEL SISTEMA (siempre visibles)
                // ============================================
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Tipo",
                    NombreInterno = "Tipo",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 120,
                    Visible = true,
                    Orden = -7,
                    Alineacion = AlineacionColumna.Centro,
                    Formula = string.Empty,
                    FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Clave",
                    NombreInterno = "Clave",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 100,
                    Visible = true,
                    Orden = -6,
                    Alineacion = AlineacionColumna.Izquierda,
                    Formula = string.Empty,
                    FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Descripción",
                    NombreInterno = "Descripcion",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 300,
                    Visible = true,
                    Orden = -5,
                    Alineacion = AlineacionColumna.Izquierda,
                    Formula = string.Empty,
                    FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Unidad",
                    NombreInterno = "Unidad",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 80,
                    Visible = true,
                    Orden = -4,
                    Alineacion = AlineacionColumna.Centro,
                    Formula = string.Empty,
                    FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Cantidad",
                    NombreInterno = "Cantidad",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Numerico,
                    AnchoColumna = 100,
                    Visible = true,
                    Orden = -3,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = string.Empty,
                    FormatoNumerico = "N2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "P.U.",
                    NombreInterno = "PrecioUnitario",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 120,
                    Visible = true,
                    Orden = -2,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "CostoDirectoUnitario",
                    FormatoNumerico = "C4",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Importe",
                    NombreInterno = "Importe",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 140,
                    Visible = true,
                    Orden = -1,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "Cantidad * CostoDirectoUnitario",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // ============================================
                // COLUMNAS ADICIONALES (ocultas por defecto)
                // ============================================
                // Costos Indirectos
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "% Indirectos",
                    NombreInterno = "PorcentajeIndirectos",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Porcentaje,
                    AnchoColumna = 90,
                    Visible = false,
                    Orden = 1,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = string.Empty,
                    FormatoNumerico = "0.00",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Indirectos",
                    NombreInterno = "Indirectos",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 110,
                    Visible = false,
                    Orden = 2,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "CostoDirecto * (PorcentajeIndirectos / 100)",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Financiamiento
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "% Financiamiento",
                    NombreInterno = "PorcentajeFinanciamiento",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Porcentaje,
                    AnchoColumna = 110,
                    Visible = false,
                    Orden = 3,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = string.Empty,
                    FormatoNumerico = "0.00",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Financiamiento",
                    NombreInterno = "Financiamiento",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 110,
                    Visible = false,
                    Orden = 4,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "(CostoDirecto + Indirectos) * (PorcentajeFinanciamiento / 100)",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Utilidad
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "% Utilidad",
                    NombreInterno = "PorcentajeUtilidad",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Porcentaje,
                    AnchoColumna = 90,
                    Visible = false,
                    Orden = 5,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = string.Empty,
                    FormatoNumerico = "0.00",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Utilidad",
                    NombreInterno = "Utilidad",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 110,
                    Visible = false,
                    Orden = 6,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "(CostoDirecto + Indirectos + Financiamiento) * (PorcentajeUtilidad / 100)",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Cargos Adicionales
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Cargos Adicionales",
                    NombreInterno = "CargosAdicionales",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 130,
                    Visible = false,
                    Orden = 7,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = string.Empty,
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Precio Unitario (con sobrecostos)
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "P.U. Final",
                    NombreInterno = "PrecioUnitarioFinal",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 120,
                    Visible = false,
                    Orden = 8,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "CostoDirecto + Indirectos + Financiamiento + Utilidad + CargosAdicionales",
                    FormatoNumerico = "C4",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Subtotal (sin IVA)
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Subtotal",
                    NombreInterno = "Subtotal",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 120,
                    Visible = false,
                    Orden = 9,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "Cantidad * PrecioUnitarioFinal",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // IVA
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "IVA",
                    NombreInterno = "IVA",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 110,
                    Visible = false,
                    Orden = 10,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "Subtotal * 0.16",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Total Final
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Total",
                    NombreInterno = "Total",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 130,
                    Visible = false,
                    Orden = 11,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "Subtotal + IVA",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Otras columnas útiles
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Incidencia %",
                    NombreInterno = "IncidenciaPorcentaje",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Porcentaje,
                    AnchoColumna = 100,
                    Visible = false,
                    Orden = 12,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "(Importe / TotalPresupuesto) * 100",
                    FormatoNumerico = "0.00",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Observaciones",
                    NombreInterno = "Observaciones",
                    TipoColumna = TipoColumnaPersonalizada.Personal,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 200,
                    Visible = false,
                    Orden = 13,
                    Alineacion = AlineacionColumna.Izquierda,
                    Formula = string.Empty,
                    FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Importe de Mano de Obra
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Importe M.O.",
                    NombreInterno = "ImporteManoObra",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Moneda,
                    AnchoColumna = 120,
                    Visible = false,
                    Orden = 14,
                    Alineacion = AlineacionColumna.Derecha,
                    Formula = "ImporteManoObraAPU * Cantidad",
                    FormatoNumerico = "C2",
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // P.U. en Letra
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "P.U. en Letra",
                    NombreInterno = "PrecioUnitarioLetra",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 250,
                    Visible = false,
                    Orden = 15,
                    Alineacion = AlineacionColumna.Izquierda,
                    Formula = "NumeroALetra(PrecioUnitarioFinal)",
                    FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                },
                
                // Total en Letra
                new ColumnaPersonalizada
                {
                    ProyectoId = proyectoId,
                    Nombre = "Total en Letra",
                    NombreInterno = "TotalLetra",
                    TipoColumna = TipoColumnaPersonalizada.Calculada,
                    TipoDato = TipoDatoColumna.Texto,
                    AnchoColumna = 300,
                    Visible = false,
                    Orden = 16,
                    Alineacion = AlineacionColumna.Izquierda,
                    Formula = "NumeroALetra(Total)",
                    FormatoNumerico = string.Empty,
                    FormatoFecha = string.Empty,
                    CondicionTotalizacion = string.Empty
                }
            };
            
            var existentes = context.ColumnasPersonalizadas
                .Where(c => c.ProyectoId == proyectoId)
                .ToList();

            if (existentes.Count > 0)
            {
                var oficiales = new HashSet<string>(
                    columnas.Select(c => c.NombreInterno),
                    StringComparer.OrdinalIgnoreCase);

                if (EsSetInicialLegacyPresupuesto(existentes))
                {
                    context.ColumnasPersonalizadas.RemoveRange(existentes);
                    context.SaveChanges();
                    existentes.Clear();
                }
                else
                {
                    var extrasLegacy = existentes
                        .Where(c => !oficiales.Contains(c.NombreInterno ?? string.Empty))
                        .Where(c => EsColumnaLegacyNoOficial(c.NombreInterno))
                        .ToList();

                    bool soloTieneExtrasLegacyNoOficiales = extrasLegacy.Count > 0
                        && existentes.All(c => oficiales.Contains(c.NombreInterno ?? string.Empty)
                            || EsColumnaLegacyNoOficial(c.NombreInterno));

                    if (soloTieneExtrasLegacyNoOficiales)
                    {
                        context.ColumnasPersonalizadas.RemoveRange(extrasLegacy);
                        context.SaveChanges();
                        existentes = context.ColumnasPersonalizadas
                            .Where(c => c.ProyectoId == proyectoId)
                            .ToList();
                    }

                    var existentesPorNombre = new HashSet<string>(
                        existentes.Select(c => c.NombreInterno ?? string.Empty),
                        StringComparer.OrdinalIgnoreCase);

                    var faltantes = columnas
                        .Where(c => !existentesPorNombre.Contains(c.NombreInterno ?? string.Empty))
                        .ToList();

                    if (faltantes.Count == 0)
                        return;

                    context.ColumnasPersonalizadas.AddRange(faltantes);
                    context.SaveChanges();
                    return;
                }
            }

            context.ColumnasPersonalizadas.AddRange(columnas);
            context.SaveChanges();
        }

        private static bool EsSetInicialLegacyPresupuesto(List<ColumnaPersonalizada> existentes)
        {
            if (existentes.Count != 17)
                return false;

            var legacy = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "Tipo",
                "Clave",
                "Descripcion",
                "Unidad",
                "Cantidad",
                "PrecioUnitario",
                "Importe",
                "PorcentajeIndirectos",
                "Indirectos",
                "PorcentajeFinanciamiento",
                "Financiamiento",
                "PorcentajeUtilidad",
                "Utilidad",
                "PorcentajeCargosAdicionales",
                "CargosAdicionales",
                "PrecioUnitarioFinal",
                "ImporteFinal"
            };

            return existentes.All(c => legacy.Contains(c.NombreInterno ?? string.Empty));
        }

        private static bool EsColumnaLegacyNoOficial(string? nombreInterno)
        {
            return string.Equals(nombreInterno, "PorcentajeCargosAdicionales", StringComparison.OrdinalIgnoreCase)
                || string.Equals(nombreInterno, "ImporteFinal", StringComparison.OrdinalIgnoreCase);
        }
    }
}
