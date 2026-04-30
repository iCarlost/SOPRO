using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Matrices;

[TestClass]
public class MatrixCostAdjustmentServiceTests
{
    [TestMethod]
    public void GetAdjustmentBasis_SoloMateriales_DebeIdentificarCostoFijoYMinimo()
    {
        var proyecto = CrearProyecto();
        var matriz = CrearMatrizBase();

        var basis = MatrixCostAdjustmentService.GetAdjustmentBasis(
            matriz,
            proyecto,
            MatrixAdjustmentScopes.Materiales);

        Assert.IsNotNull(basis);
        Assert.AreEqual(500.00m, basis!.CurrentCost);
        Assert.AreEqual(200.00m, basis.AdjustableCost);
        Assert.AreEqual(300.00m, basis.MinCost, "Si solo se ajustan materiales, la MO queda fija como costo mínimo.");
        Assert.AreEqual(300.00m, basis.FixedCost);
    }

    [TestMethod]
    public void AdjustMatrixByTargetCost_SoloMateriales_DebeModificarCantidadMaterialSinTocarMO()
    {
        var proyecto = CrearProyecto();
        var matriz = CrearMatrizBase();
        var material = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
        var manoObra = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra);

        var result = MatrixCostAdjustmentService.AdjustMatrixByTargetCost(
            matriz,
            proyecto,
            MatrixAdjustmentScopes.Materiales,
            700.00m);

        Assert.IsTrue(result.Success, result.Message);
        Assert.AreEqual(500.00m, result.CurrentCost);
        Assert.AreEqual(700.00m, result.TargetCost);
        Assert.AreEqual(700.00m, result.AchievedCost);
        Assert.AreEqual(4.00m, material.Cantidad, "El material debe subir de 2 a 4 para pasar de $200 a $400.");
        Assert.AreEqual(1.00m, manoObra.Cantidad, "La MO no debe modificarse si el alcance solo es Materiales.");
        Assert.AreEqual(400.00m, material.Importe);
        Assert.AreEqual(300.00m, manoObra.Importe);
        Assert.AreEqual(700.00m, matriz.CostoDirecto);
    }

    [TestMethod]
    public void AdjustMatrixByTargetCost_SoloManoDeObra_DebeBajarMOYConservarMaterial()
    {
        var proyecto = CrearProyecto();
        var matriz = CrearMatrizBase();
        var material = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
        var manoObra = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra);

        var result = MatrixCostAdjustmentService.AdjustMatrixByTargetCost(
            matriz,
            proyecto,
            MatrixAdjustmentScopes.ManoDeObra,
            350.00m);

        Assert.IsTrue(result.Success, result.Message);
        Assert.AreEqual(350.00m, result.AchievedCost);
        Assert.AreEqual(2.00m, material.Cantidad, "El material queda fijo en $200.");
        Assert.AreEqual(0.50m, manoObra.Cantidad, "La MO debe bajar de $300 a $150.");
        Assert.AreEqual(200.00m, material.Importe);
        Assert.AreEqual(150.00m, manoObra.Importe);
        Assert.AreEqual(350.00m, matriz.CostoDirecto);
    }

    [TestMethod]
    public void AdjustMatrixByTargetCost_MontoMenorAlMinimo_DebeFallarSinModificarCantidades()
    {
        var proyecto = CrearProyecto();
        var matriz = CrearMatrizBase();
        var material = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
        var manoObra = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra);

        var result = MatrixCostAdjustmentService.AdjustMatrixByTargetCost(
            matriz,
            proyecto,
            MatrixAdjustmentScopes.Materiales,
            250.00m);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "mínimo alcanzable");
        Assert.AreEqual(2.00m, material.Cantidad, "La prueba protege que un fallo no deje cantidades parcialmente ajustadas.");
        Assert.AreEqual(1.00m, manoObra.Cantidad);
        Assert.AreEqual(200.00m, material.Importe);
        Assert.AreEqual(300.00m, manoObra.Importe);
    }

    [TestMethod]
    public void AdjustMatrixByFactor_FactorNegativo_DebeFallarSinModificarMatriz()
    {
        var proyecto = CrearProyecto();
        var matriz = CrearMatrizBase();
        var material = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
        var manoObra = matriz.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra);

        var result = MatrixCostAdjustmentService.AdjustMatrixByFactor(
            matriz,
            proyecto,
            MatrixAdjustmentScopes.Materiales | MatrixAdjustmentScopes.ManoDeObra,
            -1m);

        Assert.IsFalse(result.Success);
        StringAssert.Contains(result.Message, "no puede ser negativo");
        Assert.AreEqual(2.00m, material.Cantidad);
        Assert.AreEqual(1.00m, manoObra.Cantidad);
        Assert.AreEqual(500.00m, matriz.Componentes.Sum(c => c.Importe));
    }

    private static Proyecto CrearProyecto()
        => new()
        {
            Nombre = "Proyecto prueba reajuste",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };

    private static Matriz CrearMatrizBase()
    {
        var material = new Material
        {
            Clave = "MAT-001",
            Descripcion = "Material ajustable",
            Unidad = "pza",
            PrecioUnitario = 100.00m,
            Notas = string.Empty
        };

        var oficial = new ManoDeObra
        {
            Clave = "MO-001",
            Descripcion = "Oficial",
            Unidad = "jor",
            SalarioBase = 300.00m,
            FactorSalarioReal = 1m,
            SalarioReal = 300.00m,
            Notas = string.Empty
        };

        var matriz = new Matriz
        {
            Clave = "APU-REAJ",
            Descripcion = "APU para reajuste",
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };

        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            Material = material,
            Cantidad = 2.00m,
            Orden = 1,
            Notas = string.Empty
        });

        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObra = oficial,
            Cantidad = 1.00m,
            Orden = 2,
            Notas = string.Empty
        });

        MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), 2);
        matriz.CostoDirecto = matriz.Componentes.Sum(c => c.Importe);
        return matriz;
    }
}
