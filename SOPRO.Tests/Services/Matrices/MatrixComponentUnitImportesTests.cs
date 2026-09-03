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

        // [N7-4] Contrato de no mutación afirmado explícitamente: ImportesUnitarios no
        // escribe Importe en las entidades (Recalculate, que se ejecuta justo después,
        // es la que sí muta).
        Assert.IsTrue(componentes.All(c => c.Importe == 0m),
            "ImportesUnitarios no debe mutar los Importe de los componentes");

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

    [TestMethod]
    public void DistribuirImporteProporcional_AbsorbeResiduoEnUltimoNoAuxiliar()
    {
        var engine = new Sopro.Calculation.SoproCalculationEngine(2, 2, 4);
        var materialA = new Material { PrecioUnitario = 10m };
        var materialB = new Material { PrecioUnitario = 10m };
        var auxiliar = new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = 1m };

        var compA = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, Material = materialA, Cantidad = 1m };
        var compB = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, Material = materialB, Cantidad = 1m };
        var compAux = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = auxiliar, Cantidad = 1m };
        var componentes = new List<ComponenteMatriz> { compA, compB, compAux };

        // importes 10, 10, 1 → cd 21. Base 10.00: 4.76 + 4.76 + 0.48 = 10.00 exacto.
        // Base elegida para forzar residuo: 3.33 → 1.59 + 1.59 + 0.16 = 3.34 → residuo −0.01.
        var importes = new System.Collections.Generic.Dictionary<ComponenteMatriz, decimal>
        {
            [compA] = 10m, [compB] = 10m, [compAux] = 1m
        };

        var distribucion = MatrixComponentCalculationService
            .DistribuirImporteProporcional(engine, importes, componentes, 3.33m, 21m);

        Assert.AreEqual(3, distribucion.Count);
        Assert.AreEqual(3.33m, distribucion.Sum(t => t.Importe));
        // El residuo debe caer en compB (último no auxiliar), no en el auxiliar.
        Assert.AreEqual(1.58m, distribucion.Single(t => t.Componente == compB).Importe);
        Assert.AreEqual(0.16m, distribucion.Single(t => t.Componente == compAux).Importe);
    }

    [TestMethod]
    public void DistribuirImporteProporcional_ExcluyeCerosYNulos()
    {
        var engine = new Sopro.Calculation.SoproCalculationEngine(2, 2, 4);
        var material = new Material { PrecioUnitario = 10m };
        var uno = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 1m };
        var cero = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 0m };
        var ausente = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 1m };

        var importes = new System.Collections.Generic.Dictionary<ComponenteMatriz, decimal>
        {
            [uno] = 10m, [cero] = 0m
        };

        var distribucion = MatrixComponentCalculationService
            .DistribuirImporteProporcional(engine, importes, new[] { uno, cero, ausente }, 5m, 10m);

        Assert.AreEqual(1, distribucion.Count);
        Assert.AreEqual(5m, distribucion[0].Importe);
    }

    [TestMethod]
    public void DistribuirImporteProporcional_TodosAuxiliares_AbsorbeElUltimoAunqueNoSeaNoAuxiliar()
    {
        var engine = new Sopro.Calculation.SoproCalculationEngine(2, 2, 4);
        var baseMatriz = new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = 1m };
        var aux1 = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = baseMatriz, Cantidad = 1m };
        var aux2 = new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = baseMatriz, Cantidad = 1m };

        // Cada auxiliar pesa 1 sobre cd 2; base 0.03 → Round(0.015) = 0.02 por ambos,
        // suma 0.04, residuo −0.01: sin no-auxiliares el absorbedor es el último.
        var importes = new System.Collections.Generic.Dictionary<ComponenteMatriz, decimal>
        {
            [aux1] = 1m, [aux2] = 1m
        };

        var distribucion = MatrixComponentCalculationService
            .DistribuirImporteProporcional(engine, importes, new[] { aux1, aux2 }, 0.03m, 2m);

        Assert.AreEqual(2, distribucion.Count);
        Assert.AreEqual(0.03m, distribucion.Sum(t => t.Importe));
        Assert.AreEqual(0.02m, distribucion.Single(t => t.Componente == aux1).Importe);
        Assert.AreEqual(0.01m, distribucion.Single(t => t.Componente == aux2).Importe);
    }

    [TestMethod]
    public void DistribuirImporteProporcional_CostoSiero_Lanza()
    {
        var engine = new Sopro.Calculation.SoproCalculationEngine(2, 2, 4);
        Assert.ThrowsException<ArgumentException>(() =>
            MatrixComponentCalculationService.DistribuirImporteProporcional(
                engine,
                new System.Collections.Generic.Dictionary<ComponenteMatriz, decimal>(),
                new List<ComponenteMatriz>(),
                1m,
                0m));
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
