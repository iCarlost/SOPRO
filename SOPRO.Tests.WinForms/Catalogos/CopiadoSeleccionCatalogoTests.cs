using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// H8 — el menú contextual de copia de los catálogos debe seguir la semántica de
/// Excel: bloque exacto de celdas vs fila(s) completa(s) por encabezado. Bajo
/// <see cref="DataGridViewSelectionMode.CellSelect"/> <c>SelectedRows</c> queda
/// vacío, por lo que la fila completa se detecta por sus celdas (el encabezado las
/// selecciona todas vía <see cref="DgvCeldaHelper.SeleccionarFilaCompleta"/>).
/// Se prueban las funciones puras y el criterio del clic derecho, sin UI real.
/// </summary>
[TestClass]
public class CopiadoSeleccionCatalogoTests
{
    private static DataGridView CrearGrid(int filas, int columnas = 3)
    {
        var grid = new DataGridView { AllowUserToAddRows = false };
        grid.BindingContext = new BindingContext();
        for (int i = 0; i < columnas; i++)
            grid.Columns.Add(new DataGridViewTextBoxColumn { Name = $"col{i}" });
        grid.RowCount = filas;
        grid.SelectionMode = DataGridViewSelectionMode.CellSelect;
        grid.MultiSelect = true;
        return grid;
    }

    // ═════════════════════════ Copia: celdas vs fila completa ═════════════════════════

    /// <summary>
    /// Celdas en dos filas distintas ⇒ bloque de celdas (2 líneas de 1 valor); NO se
    /// expande a filas completas. Regresión del bug reportado.
    /// </summary>
    [TestMethod]
    public void CeldasEnDosFilas_CopianElBloqueDeCeldas_NoFilasCompletas()
    {
        using var grid = CrearGrid(filas: 3, columnas: 3);
        grid.Rows[0].Cells[0].Value = "A";
        grid.Rows[2].Cells[0].Value = "C";

        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[2].Cells[0].Selected = true;

        Assert.AreEqual(0, DgvCeldaHelper.FilasCompletasSeleccionadas(grid).Count);
        Assert.AreEqual("A\nC", DgvCeldaHelper.ConstruirTsvDeCeldasSeleccionadas(grid));
        Assert.AreEqual("Copiar celdas", DgvCeldaHelper.EtiquetaCopiar(grid));
    }

    /// <summary>
    /// Una fila completa (todas sus columnas seleccionadas, como al clicar el
    /// encabezado) ⇒ se copia la fila COMPLETA con todas las columnas.
    /// </summary>
    [TestMethod]
    public void FilaCompletaPorEncabezado_CopiaLaFilaCompleta()
    {
        using var grid = CrearGrid(filas: 2, columnas: 3);
        grid.Rows[0].Cells[0].Value = "A";
        grid.Rows[0].Cells[1].Value = "D";
        grid.Rows[0].Cells[2].Value = "G";

        grid.ClearSelection();
        DgvCeldaHelper.SeleccionarFilaCompleta(grid, 0);

        var filas = DgvCeldaHelper.FilasCompletasSeleccionadas(grid);
        Assert.AreEqual(1, filas.Count);
        Assert.AreEqual(0, filas[0].Index);
        Assert.AreEqual("A\tD\tG", DgvCeldaHelper.ConstruirTsvDeFilas(grid, filas));
        Assert.AreEqual("Copiar fila", DgvCeldaHelper.EtiquetaCopiar(grid));
    }

    /// <summary>Varias filas completas ⇒ "Copiar N filas".</summary>
    [TestMethod]
    public void VariasFilasCompletas_EtiquetaCopiarNFilas()
    {
        using var grid = CrearGrid(filas: 3, columnas: 2);
        grid.ClearSelection();
        DgvCeldaHelper.SeleccionarFilaCompleta(grid, 0);
        DgvCeldaHelper.SeleccionarFilaCompleta(grid, 1);

        Assert.AreEqual(2, DgvCeldaHelper.FilasCompletasSeleccionadas(grid).Count);
        Assert.AreEqual("Copiar 2 filas", DgvCeldaHelper.EtiquetaCopiar(grid));
    }

    /// <summary>Una sola celda ⇒ "Copiar celda".</summary>
    [TestMethod]
    public void UnaCelda_EtiquetaCopiarCelda()
    {
        using var grid = CrearGrid(filas: 2, columnas: 3);
        grid.ClearSelection();
        grid.Rows[1].Cells[1].Selected = true;

        Assert.AreEqual("Copiar celda", DgvCeldaHelper.EtiquetaCopiar(grid));
    }

    // ═════════════════ Clic derecho dentro/fuera de la selección ═════════════════

    /// <summary>
    /// El clic derecho DENTRO de la selección no debe limpiarla; FUERA sí limpia. Bajo
    /// CellSelect, `clickedRow.Selected` es falso aunque la fila tenga celdas
    /// seleccionadas, por lo que el catálogo decide con
    /// <see cref="DgvCeldaHelper.ClicDentroDeSeleccion"/>.
    /// </summary>
    [TestMethod]
    public void ClicDerechoDentroDeLaSeleccion_NoLimpiaLaSeleccion()
    {
        using var grid = CrearGrid(filas: 3, columnas: 3);
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[2].Cells[1].Selected = true;

        Assert.IsTrue(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 2),
            "El clic cayó DENTRO de la selección ⇒ no debe ejecutarse ClearSelection.");
        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 1),
            "El clic cayó FUERA de la selección ⇒ se limpia y se selecciona la fila clickeada.");
    }

    /// <summary>Sin selección (o índice inválido) el criterio nunca marca "dentro".</summary>
    [TestMethod]
    public void ClicDentroDeSeleccion_SinSeleccion_DevuelveFalse()
    {
        using var grid = CrearGrid(filas: 2, columnas: 3);
        grid.ClearSelection();

        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 0));
        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(grid, -1));
        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(null!, 0));
    }
}
