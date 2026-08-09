using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Models.Explosion;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Services;

namespace SOPRO.WinForms.Forms
{
    /// <summary>
    /// Explosión de matrices y acumulación de insumos.
    /// </summary>
    public partial class FormExplosionInsumos
    {

        private void ExplotarMatriz(
            Matriz matriz,
            decimal importeBase,       // importe guardado en el nivel padre (ya × cantidadConcepto)
            decimal cantidadBase,      // cantidad física del nivel padre
            Dictionary<int, DatosInsumo> materiales,
            Dictionary<int, DatosInsumo> manoObra,
            Dictionary<int, DatosInsumo> maquinaria,
            Dictionary<int, DatosInsumo> herramientas)
        {
            if (matriz?.Componentes == null || matriz.Componentes.Count == 0) return;

            // Suma de importes guardados en esta matriz
            decimal sumaImportesMatriz = matriz.Componentes.Sum(c => c.Importe);
            if (sumaImportesMatriz == 0) return;

            // Factor de escala: ajusta los sub-importes para que sumen exactamente importeBase
            decimal escala = importeBase / sumaImportesMatriz;

            foreach (var comp in matriz.Componentes)
            {
                decimal importe = comp.Importe * escala;
                if (importe == 0) continue;

                // Cantidad física real: cantidadBase × comp.Cantidad (del insumo en el APU)
                decimal cantFisica = cantidadBase * comp.Cantidad;

                switch (comp.TipoComponente)
                {
                    case TipoComponenteMatriz.Material:
                        if (comp.Material != null)
                            AcumularImporte(materiales, comp.MaterialId.Value,
                                comp.Material.Clave, comp.Material.Descripcion,
                                comp.Material.Unidad, importe, cantFisica, comp.Material.PrecioUnitario);
                        break;

                    case TipoComponenteMatriz.ManoDeObra:
                        if (comp.ManoDeObra != null)
                            AcumularImporte(manoObra, comp.ManoDeObraId.Value,
                                comp.ManoDeObra.Clave, comp.ManoDeObra.Descripcion,
                                comp.ManoDeObra.Unidad, importe, cantFisica,
                                comp.ManoDeObra.EsPorcentajeMO ? 0m : comp.ManoDeObra.SalarioReal);
                        break;

                    case TipoComponenteMatriz.Maquinaria:
                        if (comp.Maquinaria != null)
                            AcumularImporte(maquinaria, comp.MaquinariaId.Value,
                                comp.Maquinaria.Clave, comp.Maquinaria.Descripcion,
                                "hr", importe, cantFisica, comp.Maquinaria.CostoHorario);
                        break;

                    case TipoComponenteMatriz.Herramienta:
                        if (comp.Herramienta != null)
                            AcumularImporte(herramientas, comp.HerramientaId.Value,
                                comp.Herramienta.Clave, comp.Herramienta.Descripcion,
                                comp.Herramienta.Unidad, importe, cantFisica,
                                comp.Herramienta.EsPorcentajeMO ? 0m : comp.Herramienta.PrecioUnitario);
                        break;

                    case TipoComponenteMatriz.Auxiliar:
                        // Recursivo: el importeBase y cantidadBase del auxiliar ya están escalados
                        if (comp.Auxiliar != null)
                            ExplotarMatriz(comp.Auxiliar, importe, cantFisica,
                                materiales, manoObra, maquinaria, herramientas);
                        break;
                }
            }
        }

        private static void Acumular(Dictionary<int, DatosInsumo> dic, int key,
            string clave, string desc, string unidad, decimal cant, decimal pu)
        {
            // Mantener compatibilidad - convierte a importe y llama AcumularImporte
            // cantidadFisica = cant (ya es cantidad física en este path)
            AcumularImporte(dic, key, clave, desc, unidad, cant * pu, cant, pu);
        }

        /// <summary>
        /// Acumula por IMPORTE (método OPUS PLANET).
        /// PU=0 indica insumo %MO cuyo PU varía por concepto — la cantidad se mostrará como "—".
        /// </summary>
        private static void AcumularImporte(Dictionary<int, DatosInsumo> dic, int key,
            string clave, string desc, string unidad, decimal importe, decimal cantidadFisica, decimal pu)
        {
            if (dic.TryGetValue(key, out var existing))
            {
                existing.Cantidad       += importe;         // Importe acumulado
                existing.CantidadFisica += cantidadFisica;  // Cantidad física real
                dic[key] = existing;
            }
            else
            {
                dic[key] = new DatosInsumo
                { Clave = clave, Descripcion = desc, Unidad = unidad,
                  Cantidad = importe,
                  CantidadFisica = cantidadFisica,
                  PrecioUnitario = pu };
            }
        }
    }
}
