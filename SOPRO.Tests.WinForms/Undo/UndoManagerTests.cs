using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.WinForms.Helpers;
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

    // ═══════════ Invariante Peek → aplicar → Pop/Push ═══════════

    [TestMethod]
    public void UndoConFalloRevertido_ConservaAmbosStacksYLaAccion()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        var accion = new CompositeUndoableAction(
            Descripcion,
            new List<UndoChange> { new("antes", "despues") },
            applyBefore: _ => throw new FormatoCeldasPersistenceException(
                BudgetOperationStatus.Failed, "fallo revertido", "diagnostico"),
            applyAfter: _ => registro.Add("redo"));

        Assert.IsTrue(manager.Execute(accion));
        Assert.AreEqual(1, manager.UndoCount);
        registro.Clear();

        var ex = Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => manager.Undo());

        Assert.AreEqual(BudgetOperationStatus.Failed, ex.Status);
        Assert.AreEqual(1, manager.UndoCount, "Un fallo revertido NO debe consumir la acción.");
        Assert.AreEqual(0, manager.RedoCount, "Tampoco debe moverla al stack de redo.");
        Assert.IsTrue(manager.CanUndo);
        Assert.IsFalse(manager.CanRedo);
        Assert.AreEqual(0, registro.Count, "No se aplicó ni repintó el estado de éxito.");
    }

    [TestMethod]
    public void RedoConFalloRevertido_ConservaAmbosStacksYLaAccion()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        bool fallarRedo = false;

        var accion = new CompositeUndoableAction(
            Descripcion,
            new List<UndoChange> { new("antes", "despues") },
            applyBefore: _ => registro.Add("undo"),
            applyAfter: _ =>
            {
                if (fallarRedo) throw new FormatoCeldasPersistenceException(
                    BudgetOperationStatus.Failed, "fallo revertido", "diagnostico");
                registro.Add("redo");
            });

        Assert.IsTrue(manager.Execute(accion));
        Assert.IsTrue(manager.Undo());
        Assert.AreEqual(1, manager.RedoCount);

        fallarRedo = true;
        Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => manager.Redo());

        Assert.AreEqual(1, manager.RedoCount, "Un redo fallido NO debe consumir la acción.");
        Assert.AreEqual(0, manager.UndoCount, "Tampoco debe devolverla al stack de undo.");
        Assert.IsTrue(manager.CanRedo);
        Assert.IsFalse(manager.CanUndo);
    }

    [TestMethod]
    public void ExecuteConFalloRevertido_NoApilaYNoTocaElHistorialPrevio()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        Assert.IsTrue(manager.Execute(CrearAccion(registro, celdas: 2)));

        var conFallo = CrearAccion(
            registro, celdas: 3,
            aplicarDespuesOverride: () => throw new FormatoCeldasPersistenceException(
                BudgetOperationStatus.Failed, "fallo revertido", "diagnostico"));

        Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => manager.Execute(conFallo));
        Assert.AreEqual(1, manager.UndoCount, "El historial previo sigue intacto.");
        Assert.AreEqual(0, manager.RedoCount);
    }

    // ═══════════ Resultado incierto: bloqueo hasta reconciliar ═══════════

    [TestMethod]
    public void UndoConResultadoIncierto_BloqueaYNoPierdeElHistorialHastaReconciliar()
    {
        var manager = new UndoManager();
        var registro = new List<string>();

        var accion = new CompositeUndoableAction(
            Descripcion,
            new List<UndoChange> { new("antes", "despues") },
            applyBefore: _ =>
            {
                manager.SuspendForReconciliation();
                throw new FormatoCeldasPersistenceException(
                    BudgetOperationStatus.OutcomeUnknown, "resultado incierto", "diagnostico");
            },
            applyAfter: _ => registro.Add("redo"));

        Assert.IsTrue(manager.Execute(accion));

        Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => manager.Undo());

        Assert.IsTrue(manager.RequiresReconciliation);
        Assert.AreEqual(1, manager.UndoCount, "El historial se conserva hasta decidir qué descartar.");
        Assert.IsFalse(manager.CanUndo, "Bloqueado: no se puede deshacer sin leer la base.");
        Assert.IsFalse(manager.CanRedo);
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.Undo());
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.Redo());
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.Execute(CrearAccion(registro, 1)));

        // Reconciliación explícita tras una lectura fresca: descarta el historial
        // obsoleto y libera el manager para acciones nuevas.
        manager.ReconciliarTrasLecturaFresca();

        Assert.IsFalse(manager.RequiresReconciliation);
        Assert.AreEqual(0, manager.UndoCount);
        Assert.AreEqual(0, manager.RedoCount);
        Assert.IsTrue(manager.Execute(CrearAccion(registro, 1)));
        Assert.AreEqual(1, manager.UndoCount);
    }

    [TestMethod]
    public void ExecuteConResultadoIncierto_MarcaBloqueoSinApilarLaAccion()
    {
        var manager = new UndoManager();
        var registro = new List<string>();

        var accion = new CompositeUndoableAction(
            Descripcion,
            new List<UndoChange> { new("antes", "despues") },
            applyBefore: _ => registro.Add("undo"),
            applyAfter: _ =>
            {
                manager.SuspendForReconciliation();
                throw new FormatoCeldasPersistenceException(
                    BudgetOperationStatus.OutcomeUnknown, "resultado incierto", "diagnostico");
            });

        Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() => manager.Execute(accion));

        Assert.IsTrue(manager.RequiresReconciliation);
        Assert.AreEqual(0, manager.UndoCount, "Una acción incierta no entra al stack.");
        Assert.IsFalse(manager.CanUndo);
    }

    // ═══════════ Familias: descartar una sin tocar la otra ═══════════

    [TestMethod]
    public void DescartarPorFamilia_ConservaLaOtraFamiliaYSuOrden()
    {
        var manager = new UndoManager();
        var registro = new List<string>();

        Assert.IsTrue(manager.Execute(new DelegateUndoableAction(
            "programa 1", () => registro.Add("u-prog1"), () => registro.Add("r-prog1"))));
        Assert.IsTrue(manager.Execute(CrearAccion(registro, celdas: 1)));
        Assert.IsTrue(manager.Execute(new DelegateUndoableAction(
            "programa 2", () => registro.Add("u-prog2"), () => registro.Add("r-prog2"))));
        Assert.AreEqual(3, manager.UndoCount);

        manager.DescartarPorFamilia(UndoFamily.Formato);

        Assert.AreEqual(2, manager.UndoCount, "Sólo se descarta la familia solicitada.");
        Assert.AreEqual(0, manager.RedoCount);

        // El tope sigue siendo la acción de programa más reciente.
        registro.Clear();
        Assert.IsTrue(manager.Undo());
        Assert.AreEqual("u-prog2", registro.Single());
        Assert.IsTrue(manager.Undo());
        Assert.AreEqual("u-prog1", registro.Last());
        Assert.IsFalse(manager.CanUndo);
        Assert.AreEqual(2, manager.RedoCount);

        // El redo conserva ambas acciones de programa.
        registro.Clear();
        Assert.IsTrue(manager.Redo());
        Assert.IsTrue(manager.Redo());
        CollectionAssert.AreEqual(new[] { "r-prog1", "r-prog2" }, registro);
    }

    [TestMethod]
    public void DescartarPorFamilia_NoLiberaElBloqueoPorReconciliacion()
    {
        var manager = new UndoManager();
        var registro = new List<string>();
        Assert.IsTrue(manager.Execute(CrearAccion(registro, celdas: 1)));
        Assert.IsTrue(manager.Execute(new DelegateUndoableAction(
            "programa", () => registro.Add("u"), () => registro.Add("r"))));

        manager.SuspendForReconciliation();
        manager.DescartarPorFamilia(UndoFamily.Formato);
        manager.DescartarPorFamilia(UndoFamily.Programa);

        Assert.IsTrue(manager.RequiresReconciliation,
            "Descartar historial no es reconciliar con la base.");
        Assert.AreEqual(0, manager.UndoCount);
        Assert.IsFalse(manager.CanUndo);
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.Undo());
        Assert.ThrowsExactly<InvalidOperationException>(() => manager.Execute(CrearAccion(registro, 1)));

        manager.ReconciliarTrasLecturaFresca();
        Assert.IsFalse(manager.RequiresReconciliation);
        Assert.IsTrue(manager.Execute(CrearAccion(registro, 1)));
    }
}
