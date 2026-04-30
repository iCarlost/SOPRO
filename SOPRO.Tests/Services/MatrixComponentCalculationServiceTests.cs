using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services;

[TestClass]
public class MatrixComponentCalculationServiceTests
{
    [TestMethod]
    public void Recalculate_DebeCalcularMaterialConPrecioUnitarioVisible()
    {
        var material = new Material
        {
            Clave = "MAT-001",
            Descripcion = "Cemento",
            Unidad = "kg",
            PrecioUnitario = 13.3875m
        };

        var componentes = new List<ComponenteMatriz>
        {
            new()
            {
                TipoComponente = TipoComponenteMatriz.Material,
                Material = material,
                Cantidad = 652m
            }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(8730.28m, componentes[0].Importe);
        Assert.AreEqual(8730.28m, totales.TotalMaterial);
        Assert.AreEqual(8730.28m, totales.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_DebeCalcularPorcentajeMO_SobreBaseManoDeObraNormal()
    {
        var oficial = new ManoDeObra
        {
            Clave = "MO-001",
            Descripcion = "Oficial albañil",
            Unidad = "jor",
            SalarioReal = 500m
        };

        var caboOficio = new ManoDeObra
        {
            Clave = "MO-002",
            Descripcion = "Cabo de oficio",
            Unidad = "%MO",
            SalarioReal = 0m
        };

        var componentes = new List<ComponenteMatriz>
        {
            new()
            {
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                ManoDeObra = oficial,
                Cantidad = 2m
            },
            new()
            {
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                ManoDeObra = caboOficio,
                Cantidad = 0.10m
            }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(1000.00m, componentes[0].Importe);
        Assert.AreEqual(100.00m, componentes[1].Importe);
        Assert.AreEqual(1000.00m, totales.BaseManoObra);
        Assert.AreEqual(1100.00m, totales.TotalManoObra);
        Assert.AreEqual(1100.00m, totales.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_DebeIncluirCuadrillaEnBaseManoDeObraParaPorcentajeMO()
    {
        var cuadrilla = new Matriz
        {
            Clave = "CUA-001",
            Descripcion = "Cuadrilla albañilería",
            Unidad = "jor",
            Tipo = TipoMatriz.Cuadrilla,
            CostoDirecto = 800m
        };

        var caboOficio = new ManoDeObra
        {
            Clave = "MO-002",
            Descripcion = "Cabo de oficio",
            Unidad = "%MO"
        };

        var componentes = new List<ComponenteMatriz>
        {
            new()
            {
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                Auxiliar = cuadrilla,
                Cantidad = 1m
            },
            new()
            {
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                ManoDeObra = caboOficio,
                Cantidad = 0.10m
            }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(800.00m, componentes[0].Importe);
        Assert.AreEqual(80.00m, componentes[1].Importe);
        Assert.AreEqual(800.00m, totales.BaseManoObra);
        Assert.AreEqual(880.00m, totales.TotalManoObra);
        Assert.AreEqual(880.00m, totales.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_DebeCalcularHerramientaPorcentajeMO_SobreBaseManoDeObra()
    {
        var oficial = new ManoDeObra
        {
            Clave = "MO-001",
            Descripcion = "Oficial albañil",
            Unidad = "jor",
            SalarioReal = 500m
        };

        var herramientaMenor = new Herramienta
        {
            Clave = "HER-001",
            Descripcion = "Herramienta menor",
            Unidad = "%MO",
            PrecioUnitario = 0m
        };

        var componentes = new List<ComponenteMatriz>
        {
            new()
            {
                TipoComponente = TipoComponenteMatriz.ManoDeObra,
                ManoDeObra = oficial,
                Cantidad = 2m
            },
            new()
            {
                TipoComponente = TipoComponenteMatriz.Herramienta,
                Herramienta = herramientaMenor,
                Cantidad = 0.03m
            }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        Assert.AreEqual(1000.00m, componentes[0].Importe);
        Assert.AreEqual(30.00m, componentes[1].Importe);
        Assert.AreEqual(1000.00m, totales.BaseManoObra);
        Assert.AreEqual(1000.00m, totales.TotalManoObra);
        Assert.AreEqual(30.00m, totales.TotalHerramientas);
        Assert.AreEqual(1030.00m, totales.CostoDirectoTotal);
    }

    [TestMethod]
    public void Recalculate_DebeSepararBasicosDeCuadrillasEnTotales()
    {
        var basico = new Matriz
        {
            Clave = "BAS-001",
            Descripcion = "Mortero básico",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 125.555m
        };

        var componentes = new List<ComponenteMatriz>
        {
            new()
            {
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                Auxiliar = basico,
                Cantidad = 2m
            }
        };

        var totales = MatrixComponentCalculationService.Recalculate(componentes, decimalesImporte: 2);

        // P.U. visible: 125.56; 2 * 125.56 = 251.12.
        Assert.AreEqual(251.12m, componentes[0].Importe);
        Assert.AreEqual(251.12m, totales.TotalBasicos);
        Assert.AreEqual(0.00m, totales.BaseManoObra);
        Assert.AreEqual(251.12m, totales.CostoDirectoTotal);
    }
}
