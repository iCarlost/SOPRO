using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;

namespace SOPRO.Tests.Services.Presupuesto;

/// <summary>
/// Simulación pura del pegado sobre instantáneas del grid: orden de columnas aceptado,
/// offsets de fila, omisión parcial de celdas inválidas/restricciones por tipo y rechazo
/// temprano del lote cuando un destino no es una fila persistida con Tipo.
/// </summary>
[TestClass]
public class BudgetClipboardPasteOperationTests
{
    [TestMethod]
    public void BuildOperations_DosFilasConOffset_OmiteCantidadInvalidaSinPerderElResto()
    {
        var plan = BudgetClipboardPasteService.BuildPlan(
            "Clave",
            "APU-002\tAcero\tkg\t7\nAPU-001\t\t\t0");

        var targets = new Dictionary<int, BudgetClipboardPasteTarget?>
        {
            [5] = Target(5, "Concepto", currentKey: "C-001", currentQuantity: "10"),
            [6] = Target(6, "Concepto", currentKey: "C-002", currentQuantity: "3")
        };

        var operations = BudgetClipboardPasteService.BuildOperations(plan, 5, targets);

        Assert.IsTrue(operations.IsValid, operations.ErrorMessage);
        Assert.AreEqual(2, operations.Operations.Count);
        Assert.AreEqual(1, operations.SkippedCells, "La cantidad 0 se omite como celda inválida.");

        var primera = operations.Operations[0];
        Assert.AreEqual(5, primera.RowIndex);
        Assert.AreEqual("APU-002", primera.Clave);
        Assert.AreEqual("Acero", primera.Descripcion);
        Assert.AreEqual("kg", primera.Unidad);
        Assert.AreEqual(7m, primera.Cantidad);

        var segunda = operations.Operations[1];
        Assert.AreEqual(6, segunda.RowIndex);
        Assert.AreEqual("APU-001", segunda.Clave);
        Assert.AreEqual(string.Empty, segunda.Descripcion, "Una descripción vacía sí es un valor pegado.");
        Assert.AreEqual(string.Empty, segunda.Unidad);
        Assert.IsNull(segunda.Cantidad, "La cantidad inválida no participa de la operación.");
    }

    [TestMethod]
    public void BuildOperations_IniciandoEnDescripcion_MapeaColumnasSiguientes()
    {
        var plan = BudgetClipboardPasteService.BuildPlan("Descripcion", "Texto\tm3\t5");

        var targets = new Dictionary<int, BudgetClipboardPasteTarget?>
        {
            [0] = Target(0, "Concepto", currentKey: "C-001", currentQuantity: "1")
        };

        var operations = BudgetClipboardPasteService.BuildOperations(plan, 0, targets);

        Assert.IsTrue(operations.IsValid, operations.ErrorMessage);
        var operacion = operations.Operations.Single();
        Assert.IsNull(operacion.Clave, "La clave no participó del bloque.");
        Assert.AreEqual("Texto", operacion.Descripcion);
        Assert.AreEqual("m3", operacion.Unidad);
        Assert.AreEqual(5m, operacion.Cantidad);
    }

    [TestMethod]
    public void BuildOperations_Agrupador_OmiteUnidadYCantidad()
    {
        var plan = BudgetClipboardPasteService.BuildPlan("Clave", "CAP-1\tCapítulo pegado\tm3\t5");

        var targets = new Dictionary<int, BudgetClipboardPasteTarget?>
        {
            [2] = Target(2, "Capitulo", currentKey: "CAP-1", currentQuantity: string.Empty)
        };

        var operations = BudgetClipboardPasteService.BuildOperations(plan, 2, targets);

        Assert.IsTrue(operations.IsValid, operations.ErrorMessage);
        Assert.AreEqual(2, operations.SkippedCells, "Un agrupador solo admite Clave y Descripción.");

        var operacion = operations.Operations.Single();
        Assert.AreEqual("CAP-1", operacion.Clave);
        Assert.AreEqual("Capítulo pegado", operacion.Descripcion);
        Assert.IsNull(operacion.Unidad);
        Assert.IsNull(operacion.Cantidad);
    }

    [TestMethod]
    public void BuildOperations_DestinoSinConceptoPersistido_RechazaElLoteCompletoAntesDeEscribir()
    {
        var plan = BudgetClipboardPasteService.BuildPlan("Clave", "APU-002\nAPU-001");

        var targets = new Dictionary<int, BudgetClipboardPasteTarget?>
        {
            [5] = Target(5, "Concepto", conceptId: 0),
            [6] = Target(6, "Concepto")
        };

        var operations = BudgetClipboardPasteService.BuildOperations(plan, 5, targets);

        Assert.IsFalse(operations.IsValid);
        Assert.AreEqual(0, operations.Operations.Count,
            "El lote no se normaliza parcialmente cuando un destino no es representable.");
        StringAssert.Contains(operations.ErrorMessage, "fila 6 del presupuesto");
        StringAssert.Contains(operations.ErrorMessage, "No se modificó nada");
    }

    [TestMethod]
    public void BuildOperations_DestinoFueraDelGrid_RechazaElLoteCompleto()
    {
        var plan = BudgetClipboardPasteService.BuildPlan("Clave", "APU-002\nAPU-001");

        var targets = new Dictionary<int, BudgetClipboardPasteTarget?>
        {
            [5] = Target(5, "Concepto")
        };

        var operations = BudgetClipboardPasteService.BuildOperations(plan, 5, targets);

        Assert.IsFalse(operations.IsValid);
        Assert.AreEqual(0, operations.Operations.Count);
        StringAssert.Contains(operations.ErrorMessage, "fila 7 del presupuesto");
    }

    [TestMethod]
    public void BuildOperations_DestinoSinTipo_RechazaElLoteCompleto()
    {
        var plan = BudgetClipboardPasteService.BuildPlan("Clave", "APU-002");

        var targets = new Dictionary<int, BudgetClipboardPasteTarget?>
        {
            [3] = Target(3, tipo: string.Empty)
        };

        var operations = BudgetClipboardPasteService.BuildOperations(plan, 3, targets);

        Assert.IsFalse(operations.IsValid);
        Assert.AreEqual(0, operations.Operations.Count);
        StringAssert.Contains(operations.ErrorMessage, "Tipo asignado");
    }

    [TestMethod]
    public void BuildOperations_TodasLasCeldasInvalidas_DevuelvePlanValidoSinOperaciones()
    {
        var plan = BudgetClipboardPasteService.BuildPlan("Cantidad", "0");

        var targets = new Dictionary<int, BudgetClipboardPasteTarget?>
        {
            [0] = Target(0, "Concepto", currentKey: "C-001", currentQuantity: "10")
        };

        var operations = BudgetClipboardPasteService.BuildOperations(plan, 0, targets);

        Assert.IsTrue(operations.IsValid, operations.ErrorMessage);
        Assert.AreEqual(0, operations.Operations.Count,
            "Una fila sin ningún valor aceptado no se representa como operación.");
        Assert.AreEqual(1, operations.SkippedCells);
    }

    private static BudgetClipboardPasteTarget Target(
        int rowIndex,
        string tipo,
        string currentKey = "C-001",
        string currentQuantity = "1",
        int conceptId = 10)
    {
        var concepto = new ConceptoPresupuesto
        {
            Id = conceptId,
            Clave = currentKey,
            Descripcion = "Concepto de prueba",
            EsAgrupador = !string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase),
            Orden = rowIndex
        };

        return new BudgetClipboardPasteTarget(
            rowIndex,
            tipo,
            currentKey,
            currentQuantity,
            conceptId > 0 ? concepto : null);
    }
}
