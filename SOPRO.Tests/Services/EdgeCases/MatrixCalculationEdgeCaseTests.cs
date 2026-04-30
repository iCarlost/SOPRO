using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.EdgeCases;

[TestClass]
public class MatrixCalculationEdgeCaseTests
{
    [TestMethod]
    public void Recalculate_MatrizSinComponentes_DebeRegresarTotalesEnCero()
    {
        var componentes = new List<ComponenteMatriz>();

        var totals = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(0m, totals.TotalMaterial);
        Assert.AreEqual(0m, totals.TotalManoObra);
        Assert.AreEqual(0m, totals.TotalHerramientas);
        Assert.AreEqual(0m, totals.TotalMaquinaria);
        Assert.AreEqual(0m, totals.TotalBasicos);
        Assert.AreEqual(0m, totals.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_PorcentajeMOCero_DebeNoAgregarImporte()
    {
        var oficial = new ManoDeObra { Clave = "MO-OF", Descripcion = "Oficial", Unidad = "jor", SalarioReal = 500m };
        var cabo = new ManoDeObra { Clave = "MO-CABO", Descripcion = "Cabo", Unidad = "%MO", SalarioReal = 0m };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = oficial, Cantidad = 2m },
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = cabo, Cantidad = 0m }
        };

        var totals = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(1000.00m, totals.BaseManoObra);
        Assert.AreEqual(0.00m, componentes[1].Importe);
        Assert.AreEqual(1000.00m, totals.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_PorcentajeMOMayorA100_DebeAplicarseSobreBaseMO()
    {
        var oficial = new ManoDeObra { Clave = "MO-OF", Descripcion = "Oficial", Unidad = "jor", SalarioReal = 500m };
        var sobrecargo = new ManoDeObra { Clave = "MO-SOB", Descripcion = "Sobrecargo", Unidad = "%MO", SalarioReal = 0m };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = oficial, Cantidad = 2m },
            new() { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObra = sobrecargo, Cantidad = 1.25m }
        };

        var totals = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(1000.00m, totals.BaseManoObra);
        Assert.AreEqual(1250.00m, componentes[1].Importe);
        Assert.AreEqual(2250.00m, totals.TotalManoObra);
        Assert.AreEqual(2250.00m, totals.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_CantidadCero_DebeDejarImporteEnCeroSinAfectarTotal()
    {
        var material = new Material { Clave = "MAT-001", Descripcion = "Cemento", Unidad = "kg", PrecioUnitario = 99.99m };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 0m }
        };

        var totals = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(0.00m, componentes[0].Importe);
        Assert.AreEqual(0.00m, totals.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_PrecioUnitarioCero_DebeDejarImporteEnCeroSinAfectarTotal()
    {
        var material = new Material { Clave = "MAT-000", Descripcion = "Material sin precio", Unidad = "pza", PrecioUnitario = 0m };

        var componentes = new List<ComponenteMatriz>
        {
            new() { TipoComponente = TipoComponenteMatriz.Material, Material = material, Cantidad = 15m }
        };

        var totals = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(0.00m, componentes[0].Importe);
        Assert.AreEqual(0.00m, totals.TotalMaterial);
        Assert.AreEqual(0.00m, totals.CostoDirectoTotal);
    }
}
