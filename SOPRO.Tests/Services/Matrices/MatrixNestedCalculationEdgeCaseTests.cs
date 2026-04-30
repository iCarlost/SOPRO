using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Matrices;

[TestClass]
public class MatrixNestedCalculationEdgeCaseTests
{
    [TestMethod]
    public void Recalculate_APUConBasicoYCuadrilla_DebeSepararTotalesYCalcularCostoDirecto()
    {
        var basico = new Matriz
        {
            Clave = "BAS-MORT",
            Descripcion = "Mortero básico",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 125.555m
        };

        var cuadrilla = new Matriz
        {
            Clave = "CUA-ALB",
            Descripcion = "Cuadrilla albañilería",
            Unidad = "jor",
            Tipo = TipoMatriz.Cuadrilla,
            CostoDirecto = 800m
        };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = basico, Cantidad = 2m },
            new() { TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = cuadrilla, Cantidad = 1.5m }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(251.12m, componentes[0].Importe, "El básico debe usar su P.U. visible: 125.56 x 2.");
        Assert.AreEqual(1200.00m, componentes[1].Importe, "La cuadrilla debe formar parte de mano de obra.");
        Assert.AreEqual(251.12m, totales.TotalBasicos);
        Assert.AreEqual(1200.00m, totales.BaseManoObra);
        Assert.AreEqual(1200.00m, totales.TotalManoObra);
        Assert.AreEqual(1451.12m, totales.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_PorcentajeMOConCuadrillaYMONormal_DebeUsarBaseMOSumada()
    {
        var oficial = new ManoDeObra
        {
            Clave = "MO-OF",
            Descripcion = "Oficial",
            Unidad = "jor",
            SalarioReal = 500m
        };

        var cuadrilla = new Matriz
        {
            Clave = "CUA-01",
            Descripcion = "Cuadrilla",
            Unidad = "jor",
            Tipo = TipoMatriz.Cuadrilla,
            CostoDirecto = 800m
        };

        var cabo = new ManoDeObra
        {
            Clave = "MO-CABO",
            Descripcion = "Cabo de oficio",
            Unidad = "%MO",
            SalarioReal = 0m
        };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = oficial, Cantidad = 2m },
            new() { TipoComponente = TipoComponenteMatriz.Auxiliar, Auxiliar = cuadrilla, Cantidad = 1m },
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = cabo, Cantidad = 0.10m }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(1000.00m, componentes[0].Importe);
        Assert.AreEqual(800.00m, componentes[1].Importe);
        Assert.AreEqual(1800.00m, totales.BaseManoObra, "La base MO debe incluir MO normal + cuadrilla.");
        Assert.AreEqual(180.00m, componentes[2].Importe, "%MO debe calcularse sobre toda la base MO.");
        Assert.AreEqual(1980.00m, totales.TotalManoObra);
        Assert.AreEqual(1980.00m, totales.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_HerramientaPorcentajeMO_NoDebeDuplicarseEnTotalManoObra()
    {
        var oficial = new ManoDeObra
        {
            Clave = "MO-OF",
            Descripcion = "Oficial",
            Unidad = "jor",
            SalarioReal = 500m
        };

        var herramientaMenor = new Herramienta
        {
            Clave = "HER-MEN",
            Descripcion = "Herramienta menor",
            Unidad = "%MO",
            PrecioUnitario = 0m
        };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = oficial, Cantidad = 2m },
            new() { TipoComponente = TipoComponenteMatriz.Herramienta, Herramienta = herramientaMenor, Cantidad = 0.03m }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(1000.00m, totales.TotalManoObra, "TotalManoObra no debe incluir herramienta.");
        Assert.AreEqual(30.00m, totales.TotalHerramientas, "La herramienta %MO se reporta como herramienta.");
        Assert.AreEqual(1030.00m, totales.TotalManoObraResumen, "El resumen MO sí puede mostrar MO + herramienta %MO.");
        Assert.AreEqual(1030.00m, totales.CostoDirectoTotal);
    }
}
