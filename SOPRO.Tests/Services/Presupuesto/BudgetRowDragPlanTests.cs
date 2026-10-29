using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Presupuesto;

/// <summary>
/// Plan puro del arrastre de filas del presupuesto: bloque a mover a partir de la
/// selección capturada antes del colapso de WinForms y colocación del bloque al
/// soltarlo arriba, abajo, al final o dentro de sí mismo.
/// </summary>
[TestClass]
public class BudgetRowDragPlanTests
{
    [TestMethod]
    public void ConstruirBloque_SeleccionMultipleContigua_DevuelveTodasLasFilasSeleccionadas()
    {
        var filas = Jerarquia("Capitulo", "Concepto", "Concepto", "Capitulo", "Concepto");

        var bloque = BudgetGridInteractionService.BuildRowDragBlock(1, new[] { 1, 2 }, filas);

        Assert.IsTrue(bloque.IsValid);
        Assert.IsTrue(bloque.IsMultiSelection);
        Assert.IsFalse(bloque.ExpandedHierarchy);
        CollectionAssert.AreEqual(new[] { 1, 2 }, bloque.Rows.ToArray());
    }

    [TestMethod]
    public void ConstruirBloque_SeleccionNoContigua_DegradaAFilaDeOrigen()
    {
        var filas = Jerarquia("Capitulo", "Concepto", "Concepto", "Capitulo", "Concepto");

        var bloque = BudgetGridInteractionService.BuildRowDragBlock(1, new[] { 1, 4 }, filas);

        Assert.IsTrue(bloque.IsValid);
        Assert.IsFalse(bloque.IsMultiSelection);
        Assert.IsFalse(bloque.ExpandedHierarchy);
        CollectionAssert.AreEqual(new[] { 1 }, bloque.Rows.ToArray());
    }

    [TestMethod]
    public void ConstruirBloque_SeleccionMultipleSinElOrigen_DegradaAFilaDeOrigen()
    {
        var filas = Jerarquia("Capitulo", "Concepto", "Concepto", "Capitulo", "Concepto");

        var bloque = BudgetGridInteractionService.BuildRowDragBlock(4, new[] { 1, 2 }, filas);

        Assert.IsTrue(bloque.IsValid);
        Assert.IsFalse(bloque.IsMultiSelection);
        CollectionAssert.AreEqual(new[] { 4 }, bloque.Rows.ToArray());
    }

    [TestMethod]
    public void ConstruirBloque_SeleccionDeCeldasSinFilasCompletas_DegradaAFilaDeOrigen()
    {
        var filas = Jerarquia("Capitulo", "Concepto", "Concepto");

        var bloque = BudgetGridInteractionService.BuildRowDragBlock(1, Array.Empty<int>(), filas);

        Assert.IsTrue(bloque.IsValid);
        Assert.IsFalse(bloque.IsMultiSelection);
        CollectionAssert.AreEqual(new[] { 1 }, bloque.Rows.ToArray());
    }

    [TestMethod]
    public void ConstruirBloque_AgrupadorSeleccionado_ExpandeHijosYLosUneAlBloque()
    {
        var filas = Jerarquia("Capitulo", "Concepto", "Concepto", "Capitulo", "Concepto");

        var bloque = BudgetGridInteractionService.BuildRowDragBlock(0, new[] { 0, 1 }, filas);

        Assert.IsTrue(bloque.IsValid);
        Assert.IsTrue(bloque.IsMultiSelection);
        Assert.IsTrue(bloque.ExpandedHierarchy,
            "El agrupador seleccionado debe arrastrar a sus descendientes.");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, bloque.Rows.ToArray());
    }

    [TestMethod]
    public void ConstruirBloque_AgrupadorConHijoSeparadoPorFilaVacia_IncluyeElRangoFisico()
    {
        var filas = Jerarquia("Capitulo", "", "Concepto");

        var bloque = BudgetGridInteractionService.BuildRowDragBlock(0, new[] { 0 }, filas);

        Assert.IsTrue(bloque.IsValid);
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, bloque.Rows.ToArray(),
            "El bloque físico incluye la fila vacía intermedia para no partir el grupo.");
    }

    [TestMethod]
    public void ResolverSoltar_ArribaDelBloque_InsertaAntesDeLaFilaApuntada()
    {
        var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 3, 4 }, dropRow: 1, lastContentRow: 5, rowCount: 6);

        Assert.IsTrue(ubicacion.IsValid);
        Assert.IsFalse(ubicacion.IsNoOp);
        Assert.AreEqual(1, ubicacion.InsertionIndex);
        Assert.AreEqual(1, ubicacion.IndicatorRow);
    }

    [TestMethod]
    public void ResolverSoltar_AbajoDelBloque_InsertaDespuesDeLaFilaApuntada()
    {
        var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 1, 2 }, dropRow: 4, lastContentRow: 5, rowCount: 6);

        Assert.IsTrue(ubicacion.IsValid);
        Assert.IsFalse(ubicacion.IsNoOp);
        Assert.AreEqual(3, ubicacion.InsertionIndex,
            "Tras quitar 2 filas, insertar después de la fila 4 cae en el índice 3.");
        Assert.AreEqual(5, ubicacion.IndicatorRow);
    }

    [TestMethod]
    public void ResolverSoltar_AlFinalDelContenido_InsertaTrasLaUltimaFila()
    {
        var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 1, 2 }, dropRow: 5, lastContentRow: 5, rowCount: 8);

        Assert.IsTrue(ubicacion.IsValid);
        Assert.IsFalse(ubicacion.IsNoOp);
        Assert.AreEqual(4, ubicacion.InsertionIndex);
        Assert.AreEqual(6, ubicacion.IndicatorRow);
    }

    [TestMethod]
    public void ResolverSoltar_DentroDelPropioBloque_EsNoOpSinIndicador()
    {
        var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 1, 2 }, dropRow: 2, lastContentRow: 5, rowCount: 6);

        Assert.IsTrue(ubicacion.IsValid);
        Assert.IsTrue(ubicacion.IsNoOp);
        Assert.AreEqual(-1, ubicacion.IndicatorRow,
            "El indicador nunca debe quedar dentro del bloque arrastrado.");
    }

    [TestMethod]
    public void ResolverSoltar_EnElBordeFinalDelMismoBloque_EsNoOp()
    {
        var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 4, 5 }, dropRow: 6, lastContentRow: 5, rowCount: 7);

        Assert.IsTrue(ubicacion.IsValid);
        Assert.IsTrue(ubicacion.IsNoOp,
            "Soltar sobre las filas vacías finales cuando el bloque ya está al final no cambia el orden.");
        Assert.AreEqual(-1, ubicacion.IndicatorRow);
    }

    [TestMethod]
    public void ResolverSoltar_FilaDestinoFueraDelGrid_EsInvalido()
    {
        // dropRow == rowCount: no es una fila del grid; recortarla a la última fila
        // movería el bloque a un destino que el usuario no apuntó.
        var enElLimite = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 1, 2 }, dropRow: 6, lastContentRow: 5, rowCount: 6);

        Assert.IsFalse(enElLimite.IsValid,
            "Una fila destino igual a rowCount no es una posición de inserción válida.");
        Assert.AreEqual(-1, enElLimite.InsertionIndex);
        Assert.AreEqual(-1, enElLimite.IndicatorRow);

        var fuera = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 1, 2 }, dropRow: 9, lastContentRow: 5, rowCount: 6);

        Assert.IsFalse(fuera.IsValid,
            "Una fila destino mayor que el grid tampoco es válida.");
        Assert.AreEqual(-1, fuera.InsertionIndex);
    }

    [TestMethod]
    public void ResolverSoltar_UltimaFilaValida_SiguePermitiendoInsertarAlFinal()
    {
        // La semántica de "final del contenido" se conserva con una fila VÁLIDA.
        var ubicacion = BudgetGridInteractionService.ResolveRowDragPlacement(
            new[] { 1, 2 }, dropRow: 5, lastContentRow: 5, rowCount: 6);

        Assert.IsTrue(ubicacion.IsValid);
        Assert.IsFalse(ubicacion.IsNoOp);
        Assert.AreEqual(4, ubicacion.InsertionIndex);
        Assert.AreEqual(6, ubicacion.IndicatorRow);
    }

    private static List<BudgetHierarchyRow> Jerarquia(params string[] tipos)
    {
        var filas = new List<BudgetHierarchyRow>(tipos.Length);
        for (int i = 0; i < tipos.Length; i++)
        {
            string tipo = tipos[i];
            filas.Add(new BudgetHierarchyRow
            {
                RowIndex = i,
                Tipo = string.IsNullOrEmpty(tipo) ? "Concepto" : tipo,
                HasContent = !string.IsNullOrEmpty(tipo),
                IsConcept = string.Equals(tipo, "Concepto", StringComparison.OrdinalIgnoreCase)
            });
        }

        return filas;
    }
}
