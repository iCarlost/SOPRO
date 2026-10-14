using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Undo;

/// <summary>
/// Pruebas directas de <see cref="UndoManager"/> y de la acción de undo
/// COMPUESTA (<see cref="CompositeUndoableAction"/>): un cambio de formato de
/// rango (N celdas) debe registrarse como UNA sola entrada del stack, con
/// atomicidad respecto a la persistencia (si el callback de aplicación falla,
/// la acción no queda parcialmente apilada).
/// </summary>
[TestClass]
public class UndoManagerTests
{
    private const string Descripcion = "Formato de rango";

    /// <summary>
    /// Crea una acción compuesta con <paramref name="celdas"/> cambios y unos
    /// callbacks que registran en <paramref name="registro"/> qué estado se
    /// aplica (Undo => "undo:"+Before, Do/Redo => "redo:"+After). Simula la
    /// persistencia sin I/O real: la acción compuesta es agnóstica.
    /// </summary>
    private static CompositeUndoableAction CrearAccion(
        List<string> registro, int celdas, Action? aplicarDespuesOverride = null)
    {
        var cambios = new List<UndoChange>();
        for (int i = 0; i < celdas; i++)
            cambios.Add(new UndoChange($"antes{i}", $"despues{i}"));

        return new CompositeUndoableAction(
            Descripcion,
            cambios,
            applyBefore: cambiosAplicar =>
            {
                foreach (var c in cambiosAplicar)
                    registro.Add("undo:" + c.Before);
            },
            applyAfter: cambiosAplicar =>
            {
                aplicarDespuesOverride?.Invoke();
                foreach (var c in cambiosAplicar)
                    registro.Add("redo:" + c.After);
            });
    }

    [TestMethod]
    public void Ejecutar_SeleccionDeDiezCeldas_GeneraUnaSolaAccionEnElStack()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        var accion = CrearAccion(registro, celdas: 10);

        var aplicada = manager.Execute(accion);

        Assert.IsTrue(aplicada);
        Assert.AreEqual(1, manager.UndoCount, "10 celdas deben ser UNA sola entrada de undo.");
        Assert.AreEqual(10, accion.ChangeCount);
        // Do aplicó el estado "después" de las 10 celdas en una sola operación.
        Assert.AreEqual(10, registro.Count);
    }

    [TestMethod]
    public void Undo_RestauraLosDiezValoresAnteriores_YRedoReaplica()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        var accion = CrearAccion(registro, celdas: 10);
        manager.Execute(accion);
        registro.Clear();

        Assert.IsTrue(manager.Undo());
        Assert.AreEqual(10, registro.Count);
        for (int i = 0; i < 10; i++)
            Assert.AreEqual($"undo:antes{i}", registro[i]);

        Assert.IsFalse(manager.CanUndo);
        Assert.IsTrue(manager.CanRedo);
        Assert.AreEqual(1, manager.RedoCount);

        registro.Clear();
        Assert.IsTrue(manager.Redo());
        Assert.AreEqual(10, registro.Count);
        for (int i = 0; i < 10; i++)
            Assert.AreEqual($"redo:despues{i}", registro[i]);

        Assert.AreEqual(1, manager.UndoCount);
        Assert.AreEqual(0, manager.RedoCount);
    }

    [TestMethod]
    public void Undo_SeguidoDeAccionNueva_VaciaElRedoStack()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        manager.Execute(CrearAccion(registro, celdas: 10));
        Assert.IsTrue(manager.Undo());
        Assert.IsTrue(manager.CanRedo);

        var otraAccion = CrearAccion(registro, celdas: 3);
        Assert.IsTrue(manager.Execute(otraAccion));

        Assert.IsFalse(manager.CanRedo, "Una acción nueva debe vaciar el redo stack.");
        Assert.AreEqual(0, manager.RedoCount);
    }

    [TestMethod]
    public void FalloDePersistencia_NoApilaNiParcialmenteLaAccion()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        var accion = CrearAccion(
            registro,
            celdas: 10,
            aplicarDespuesOverride: () => throw new InvalidOperationException("commit fallido"));

        Assert.Throws<InvalidOperationException>(() => manager.Execute(accion));

        Assert.AreEqual(0, manager.UndoCount, "La acción no debe entrar al stack si el commit falla.");
        Assert.AreEqual(0, manager.RedoCount);
        Assert.IsFalse(manager.CanUndo);
        Assert.IsFalse(manager.CanRedo);
    }

    [TestMethod]
    public void AccionCompuestaSinCambios_NoEntraAlStack()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        var accion = CrearAccion(registro, celdas: 0);

        var aplicada = manager.Execute(accion);

        Assert.IsFalse(aplicada);
        Assert.IsFalse(accion.HasChanges);
        Assert.AreEqual(0, manager.UndoCount);
        Assert.AreEqual(0, manager.RedoCount);
        Assert.AreEqual(0, registro.Count, "Sin cambios no debe invocarse ningún callback.");
    }
}
