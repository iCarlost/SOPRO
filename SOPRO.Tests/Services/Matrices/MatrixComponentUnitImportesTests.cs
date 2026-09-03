using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Matrices;

/// <summary>
/// [N7-3] El cálculo compartido de importes unitarios debe coincidir, componente a
/// componente, con lo que produce la canónica MatrixComponentCalculationService.
/// Recalculate; y debe tolerar navegaciones incompletas excluyéndolas (a diferencia
/// de Recalculate, que lanza).
/// </summary>
[TestClass]
public class MatrixComponentUnitImportesTests
{
    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void ImportesUnitarios_CoincideConRecalculateComponenteAComponente(int decImp)
    {
        // El escenario incluye material, MO normal, %MO sobre la base, herramienta
        // normal, herramienta %MO, maquinaria, auxiliar básico y auxiliar cuadrilla
        // con signos mixtos.
        var material = new Material { PrecioUnitario = 9.333m };
        var descuento = new Material { PrecioUnitario = -2.777m };
        var oficial = new ManoDeObra { Unidad = "jor", SalarioReal = 33.333m };
        var cabo = new ManoDeObra { Unidad = "%MO", SalarioReal = 0m };
        var palana = new Herramienta { Unidad = "pza", PrecioUnitario = 1.5m };
        var herramientaPct = new Herramienta { Unidad = "%MO", PrecioUnitario = 0m };
        var grua = new Maquinaria { CostoHorario = 77.777m };
        var basico = new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = 123.456m };
        var cuadrilla = new Matriz { Tipo = TipoMatriz.Cuadrilla, CostoDirecto = -88.888m };

        var componentes = new List<ComponenteMatriz>
        {
            Comp(TipoComponenteMatriz.Material, 2m, material: material),
            Comp(TipoComponenteMatriz.Material, 1m, material: descuento),
            Comp(TipoComponenteMatriz.ManoDeObra, 1m, manoDeObra: oficial),
            Comp(TipoComponenteMatriz.ManoDeObra, .10m, manoDeObra: cabo),
            Comp(TipoComponenteMatriz.Herramienta, 3m, herramienta: palana),
            Comp(TipoComponenteMatriz.Herramienta, .05m, herramienta: herramientaPct),
            Comp(TipoComponenteMatriz.Maquinaria, 1.5m, maquinaria: grua),
            Comp(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: basico),
            Comp(TipoComponenteMatriz.Auxiliar, 2m, auxiliar: cuadrilla)
        };

        var importes = MatrixComponentCalculationService.ImportesUnitarios(componentes, decImp);

        Assert.AreEqual(componentes.Count, importes.Count);
        Assert.IsFalse(importes.Keys.Any(c => c == null));

        // Recalculate muta los mismos importes sobre instancias gemelas equivalentes;
        // se ejecuta sobre esta lista y se compara componente a componente.
        var totals = MatrixComponentCalculationService.Recalculate(componentes, decImp);

        foreach (var comp in componentes)
        {
            Assert.AreEqual(comp.Importe, importes[comp],
                $"importe coincidente para {comp.TipoComponente} (decImp={decImp})");
        }

        Assert.AreEqual(Math.Round(importes.Values.Sum(), decImp, MidpointRounding.AwayFromZero),
            totals.CostoDirectoTotal);
    }

    [TestMethod]
    public void ImportesUnitarios_ExcluyeNavegacionesIncompletas_SinLanzar()
    {
        var material = new Material { PrecioUnitario = 10m };
        var sinMaterial = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, Cantidad = 1m };
        var sinAuxiliar = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Auxiliar, Cantidad = 1m };
        var valido = new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            Material = material,
            Cantidad = 1m
        };
        var componentes = new List<ComponenteMatriz> { sinMaterial, valido, sinAuxiliar };

        var importes = MatrixComponentCalculationService.ImportesUnitarios(componentes, 2);

        Assert.AreEqual(1, importes.Count);
        Assert.AreEqual(10m, importes[valido]);
        Assert.IsFalse(importes.ContainsKey(sinMaterial));
        Assert.IsFalse(importes.ContainsKey(sinAuxiliar));

        // Contraste: la ruta estricta sí lanza ante navegaciones incompletas.
        Assert.ThrowsException<InvalidOperationException>(
            () => MatrixComponentCalculationService.Recalculate(componentes, 2));
    }

    [TestMethod]
    public void ImportesUnitarios_ListaVacia_RetornaMapaVacio()
    {
        var importes = MatrixComponentCalculationService.ImportesUnitarios(new List<ComponenteMatriz>(), 2);

        Assert.AreEqual(0, importes.Count);
    }

    [TestMethod]
    public void ImportesUnitarios_ListaNula_Lanza()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => MatrixComponentCalculationService.ImportesUnitarios(null!, 2));
    }

    private static ComponenteMatriz Comp(
        TipoComponenteMatriz tipo,
        decimal cantidad,
        Material? material = null,
        ManoDeObra? manoDeObra = null,
        Maquinaria? maquinaria = null,
        Herramienta? herramienta = null,
        Matriz? auxiliar = null)
    {
        var comp = new ComponenteMatriz { TipoComponente = tipo, Cantidad = cantidad };
        if (material != null) comp.Material = material;
        if (manoDeObra != null) comp.ManoDeObra = manoDeObra;
        if (maquinaria != null) comp.Maquinaria = maquinaria;
        if (herramienta != null) comp.Herramienta = herramienta;
        if (auxiliar != null) comp.Auxiliar = auxiliar;
        return comp;
    }
}
