using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Matrices;

[TestClass]
public class MatrixGraphOrderServiceTests
{
    [TestMethod]
    public void OrdenaCadaAuxiliarAntesQueSusDependientes()
    {
        var apu = Matriz(1, TipoMatriz.APU, 2, 20);
        var basicoAnidado = Matriz(2, TipoMatriz.Basico, 3);
        var basico = Matriz(3, TipoMatriz.Basico, 20);
        var cuadrilla = Matriz(20, TipoMatriz.Cuadrilla);

        var orden = MatrixGraphOrderService.OrdenTopologico(new[] { apu, basicoAnidado, basico, cuadrilla });

        CollectionAssert.AreEqual(new[] { 20, 3, 2, 1 }, orden.ConvertAll(m => m.Id));
    }

    [TestMethod]
    public void SinDependencias_ConservaElOrdenDeEntrada()
    {
        var a = Matriz(1, TipoMatriz.APU);
        var b = Matriz(2, TipoMatriz.Basico);
        var c = Matriz(3, TipoMatriz.Cuadrilla);

        var orden = MatrixGraphOrderService.OrdenTopologico(new[] { a, b, c });

        CollectionAssert.AreEqual(new[] { 1, 2, 3 }, orden.ConvertAll(m => m.Id));
    }

    [TestMethod]
    public void AuxiliarExternaAlConjunto_SeTrataComoHoja()
    {
        var apu = Matriz(1, TipoMatriz.APU, 999);

        var orden = MatrixGraphOrderService.OrdenTopologico(new[] { apu });

        CollectionAssert.AreEqual(new[] { 1 }, orden.ConvertAll(m => m.Id));
    }

    [TestMethod]
    public void MatrizSinPersistir_IdCero_SeTrataComoHoja()
    {
        var nueva = Matriz(0, TipoMatriz.Basico, 0);
        var persistida = Matriz(5, TipoMatriz.APU);

        var orden = MatrixGraphOrderService.OrdenTopologico(new[] { nueva, persistida });

        CollectionAssert.AreEqual(new[] { 0, 5 }, orden.ConvertAll(m => m.Id));
    }

    [TestMethod]
    public void CicloIndirecto_LanzaConRuta()
    {
        var a = Matriz(1, TipoMatriz.APU, 2);
        var b = Matriz(2, TipoMatriz.Basico, 1);

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => MatrixGraphOrderService.OrdenTopologico(new[] { a, b }));

        StringAssert.Contains(exception.Message, "Ciclo de matrices detectado");
        StringAssert.Contains(exception.Message, "1 -> 2 -> 1");
    }

    [TestMethod]
    public void AutoReferencia_LanzaConRutaCorta()
    {
        var a = Matriz(5, TipoMatriz.Basico, 5);

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => MatrixGraphOrderService.OrdenTopologico(new[] { a }));

        StringAssert.Contains(exception.Message, "5 -> 5");
    }

    [TestMethod]
    public void IdDuplicado_Lanza()
    {
        var a = Matriz(7, TipoMatriz.APU);
        var b = Matriz(7, TipoMatriz.Basico);

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => MatrixGraphOrderService.OrdenTopologico(new[] { a, b }));

        StringAssert.Contains(exception.Message, "Id 7");
    }

    [TestMethod]
    public void InstanciaRepetida_Lanza()
    {
        var a = Matriz(7, TipoMatriz.APU);

        var exception = Assert.ThrowsException<InvalidOperationException>(
            () => MatrixGraphOrderService.OrdenTopologico(new[] { a, a }));

        StringAssert.Contains(exception.Message, "duplicada");
    }

    [TestMethod]
    public void MatrizNula_Lanza()
    {
        Assert.ThrowsException<InvalidOperationException>(
            () => MatrixGraphOrderService.OrdenTopologico(new Matriz[] { null! }));
    }

    [TestMethod]
    public void ColeccionNula_LanzaArgumentNullException()
    {
        Assert.ThrowsException<ArgumentNullException>(
            () => MatrixGraphOrderService.OrdenTopologico(null!));
    }

    private static Matriz Matriz(int id, TipoMatriz tipo, params int[] auxiliaresIds)
    {
        var matriz = new Matriz
        {
            Id = id,
            Tipo = tipo,
            Clave = $"M{id}",
            Descripcion = $"Matriz {id}",
            Unidad = "m",
            ProyectoId = 1
        };
        foreach (var auxiliarId in auxiliaresIds)
            matriz.Componentes.Add(new ComponenteMatriz
            {
                MatrizId = id,
                TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = auxiliarId,
                Cantidad = 1m
            });
        return matriz;
    }

}
