using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Matrices;

[TestClass]
public class MatrixGraphSnapshotAdapterTests
{
    [TestMethod]
    public void MapeaTiposPreciosYOrden()
    {
        var componentes = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.Material, 3m, material: new Material { PrecioUnitario = 11.115m }),
            Component(TipoComponenteMatriz.Maquinaria, 1.5m, maquinaria: new Maquinaria { CostoHorario = 45.555m })
        };

        var snapshot = MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, componentes);
        var root = snapshot.Graph.Nodes.Single(n => n.Id == snapshot.RootMatrixId);
        var inputs = root.Components.OrderBy(c => c.Order).ToList();

        Assert.AreEqual(1, snapshot.Graph.Nodes.Count);
        Assert.AreEqual(MatrixComponentType.Material, inputs[0].Type);
        Assert.AreEqual(3m, inputs[0].Quantity);
        Assert.AreEqual(11.115m, inputs[0].ResolvedUnitPrice);
        Assert.AreEqual(0, inputs[0].Order);
        Assert.AreEqual(1, inputs[0].Id);
        Assert.AreEqual(MatrixComponentType.Machinery, inputs[1].Type);
        Assert.AreEqual(45.555m, inputs[1].ResolvedUnitPrice);
        Assert.AreEqual(1, inputs[1].Order);
        Assert.AreEqual(2, inputs[1].Id);
    }

    [TestMethod]
    public void ManoDeObraYHerramientaDetectanPorcentajePorUnidad()
    {
        var componentes = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.ManoDeObra, 2m, manoDeObra: new ManoDeObra { Unidad = "jor", SalarioReal = 25.5m }),
            Component(TipoComponenteMatriz.ManoDeObra, .10m, manoDeObra: new ManoDeObra { Unidad = " %mo ", SalarioReal = 0m }),
            Component(TipoComponenteMatriz.Herramienta, 2m, herramienta: new Herramienta { Unidad = "pza", PrecioUnitario = 7.777m }),
            Component(TipoComponenteMatriz.Herramienta, .03m, herramienta: new Herramienta { Unidad = "%MO", PrecioUnitario = 0m })
        };

        var root = MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, componentes).Graph.Nodes.Single();

        Assert.IsFalse(root.Components[0].IsPercentageOfLabor);
        Assert.IsTrue(root.Components[1].IsPercentageOfLabor);
        Assert.IsFalse(root.Components[2].IsPercentageOfLabor);
        Assert.IsTrue(root.Components[3].IsPercentageOfLabor);
    }

    [TestMethod]
    public void AuxiliaresSeMapeanComoHojasSegunTipo()
    {
        var componentes = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: new Matriz { Tipo = TipoMatriz.Cuadrilla, CostoDirecto = 10m }),
            Component(TipoComponenteMatriz.Auxiliar, 2m, auxiliar: new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = 20m }),
            Component(TipoComponenteMatriz.Auxiliar, 3m, auxiliar: new Matriz { Tipo = TipoMatriz.APU, CostoDirecto = 30m })
        };

        var graph = MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, componentes).Graph;
        var leaves = graph.Nodes.Where(n => n.Id != 0).ToList();

        Assert.AreEqual(3, leaves.Count);
        CollectionAssert.AreEquivalent(
            new[] { MatrixType.Crew, MatrixType.Basic, MatrixType.Basic },
            leaves.Select(l => l.Type).ToList());
        CollectionAssert.AreEquivalent(new[] { 10m, 20m, 30m }, leaves.Select(l => l.PrecomputedDirectCostTotal!.Value).ToList());
        Assert.IsTrue(leaves.All(l => l.Id < 0), "los IDs temporales de hojas deben ser negativos");
    }

    [TestMethod]
    public void AuxiliarCompartida_GeneraUnaUnicaHoja()
    {
        var compartida = new Matriz { Tipo = TipoMatriz.Basico, CostoDirecto = 15m };
        var componentes = new List<ComponenteMatriz>
        {
            Component(TipoComponenteMatriz.Auxiliar, 1m, auxiliar: compartida),
            Component(TipoComponenteMatriz.Auxiliar, 2m, auxiliar: compartida)
        };

        var graph = MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, componentes).Graph;

        Assert.AreEqual(2, graph.Nodes.Count);
        var auxComponentes = graph.Nodes.Single(n => n.Id == 0).Components;
        Assert.AreEqual(auxComponentes[0].ReferencedMatrixId, auxComponentes[1].ReferencedMatrixId);
    }

    [TestMethod]
    public void NavegacionNula_SeRechazaConPosicionYTipo()
    {
        foreach (var tipo in new[]
        {
            TipoComponenteMatriz.Material,
            TipoComponenteMatriz.ManoDeObra,
            TipoComponenteMatriz.Maquinaria,
            TipoComponenteMatriz.Herramienta,
            TipoComponenteMatriz.Auxiliar
        })
        {
            var exception = Assert.ThrowsException<InvalidOperationException>(() =>
                MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, new List<ComponenteMatriz>
                {
                    new() { TipoComponente = tipo, Cantidad = 1m }
                }), tipo.ToString());

            StringAssert.Contains(exception.Message, $"{tipo} en la posicion 0");
            StringAssert.Contains(exception.Message, "navegacion");
        }
    }

    [TestMethod]
    public void ComponenteRepetidoEnLista_SeRechaza()
    {
        var mismo = Component(TipoComponenteMatriz.Material, 1m, material: new Material { PrecioUnitario = 2m });

        Assert.ThrowsException<ArgumentException>(() =>
            MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, new List<ComponenteMatriz> { mismo, mismo }));
    }

    [TestMethod]
    public void ComponenteNuloEnLista_SeRechaza()
    {
        Assert.ThrowsException<InvalidOperationException>(() =>
            MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, new List<ComponenteMatriz> { null! }));
    }

    [TestMethod]
    public void ListaNula_LanzaArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(() =>
            MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, null!));
    }

    [TestMethod]
    public void TipoComponenteNoSoportado_SeRechaza()
    {
        var exception = Assert.ThrowsException<InvalidOperationException>(() =>
            MatrixGraphSnapshotAdapter.BuildRootSnapshot(0, new List<ComponenteMatriz>
            {
                new() { TipoComponente = (TipoComponenteMatriz)99, Cantidad = 1m }
            }));

        StringAssert.Contains(exception.Message, "Tipo de componente no soportado: 99");
    }

    private static ComponenteMatriz Component(
        TipoComponenteMatriz tipo,
        decimal cantidad,
        Material? material = null,
        ManoDeObra? manoDeObra = null,
        Maquinaria? maquinaria = null,
        Herramienta? herramienta = null,
        Matriz? auxiliar = null)
    {
        var componente = new ComponenteMatriz { TipoComponente = tipo, Cantidad = cantidad };
        if (material != null) componente.Material = material;
        if (manoDeObra != null) componente.ManoDeObra = manoDeObra;
        if (maquinaria != null) componente.Maquinaria = maquinaria;
        if (herramienta != null) componente.Herramienta = herramienta;
        if (auxiliar != null) componente.Auxiliar = auxiliar;
        return componente;
    }
}
