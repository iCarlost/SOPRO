using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Seleccion;

/// <summary>
/// H8 — la selección/copiado de los grids debe seguir la semántica de Excel:
///   • Si hay CELDAS seleccionadas (una o varias, incluso en filas distintas) se
///     copia EXACTAMENTE ese bloque de celdas como TSV.
///   • Si se seleccionó(n) FILA(S) COMPLETA(S) por el ENCABEZADO de fila se copian
///     las filas completas (todas las columnas visibles).
///   • NO se expande a fila completa cuando sólo hay celdas seleccionadas.
///
/// Bajo <see cref="DataGridViewSelectionMode.CellSelect"/> WinForms deja
/// <c>SelectedRows</c> SIEMPRE vacío: la fila completa se representa seleccionando
/// todas sus celdas (lo que hace el encabezado). Se prueban las funciones puras sin
/// depender de UI real.
/// </summary>
[TestClass]
public class CopiadoSeleccionGridTests
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

    /// <summary>Simula la selección de una fila COMPLETA (como hace el encabezado).</summary>
    private static void SeleccionarFilaCompleta(DataGridView grid, int rowIndex)
    {
        for (int c = 0; c < grid.ColumnCount; c++)
            grid.Rows[rowIndex].Cells[c].Selected = true;
    }

    // ═══════════════════════ Bloque de celdas ═══════════════════════

    /// <summary>
    /// 2 celdas en 2 filas distintas ⇒ TSV con 2 líneas de 1 valor (bloque de celdas),
    /// NUNCA filas completas. Regresión del bug: antes se expandía a 2 filas enteras.
    /// </summary>
    [TestMethod]
    public void CeldasEnDosFilas_CopianElBloqueDeCeldas_NoFilasCompletas()
    {
        using var grid = CrearGrid(filas: 3, columnas: 3);
        grid.Rows[0].Cells[0].Value = "A";
        grid.Rows[2].Cells[1].Value = "X";

        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[2].Cells[1].Selected = true;

        Assert.AreEqual(0, DgvCeldaHelper.FilasCompletasSeleccionadas(grid).Count,
            "Celdas sueltas ⇒ ninguna fila completa.");

        var tsv = DgvCeldaHelper.ConstruirTsvDeCeldasSeleccionadas(grid);
        Assert.AreEqual("A\nX", tsv, "Bloque exacto: 2 líneas, 1 valor cada una.");
        Assert.AreEqual(2, tsv.Split('\n').Length);
        Assert.IsTrue(tsv.Split('\n').All(l => !l.Contains('\t')),
            "Una sola columna seleccionada por fila ⇒ sin TAB.");
    }

    /// <summary>2 celdas en la MISMA fila ⇒ una línea con 2 valores separados por TAB.</summary>
    [TestMethod]
    public void DosCeldasMismaFila_CopianUnaLineaDosValores()
    {
        using var grid = CrearGrid(filas: 2, columnas: 3);
        grid.Rows[1].Cells[0].Value = "A";
        grid.Rows[1].Cells[2].Value = "C";

        grid.ClearSelection();
        grid.Rows[1].Cells[0].Selected = true;
        grid.Rows[1].Cells[2].Selected = true;

        Assert.AreEqual("A\tC", DgvCeldaHelper.ConstruirTsvDeCeldasSeleccionadas(grid));
        Assert.AreEqual(0, DgvCeldaHelper.FilasCompletasSeleccionadas(grid).Count);
    }

    /// <summary>1 celda ⇒ copia esa celda.</summary>
    [TestMethod]
    public void UnaCelda_CopiaEsaCelda()
    {
        using var grid = CrearGrid(filas: 3, columnas: 3);
        grid.Rows[1].Cells[1].Value = "B";

        grid.ClearSelection();
        grid.Rows[1].Cells[1].Selected = true;

        Assert.AreEqual("B", DgvCeldaHelper.ConstruirTsvDeCeldasSeleccionadas(grid));
        Assert.AreEqual(0, DgvCeldaHelper.FilasCompletasSeleccionadas(grid).Count);
    }

    // ═══════════════════════ Filas completas ═══════════════════════

    /// <summary>
    /// Filas completas seleccionadas (todas sus celdas, como al clicar el encabezado)
    /// ⇒ se copian las filas COMPLETAS, una por línea, con todas las columnas.
    /// </summary>
    [TestMethod]
    public void FilasCompletasSeleccionadas_CopianFilasCompletas()
    {
        using var grid = CrearGrid(filas: 3, columnas: 3);
        grid.Rows[0].Cells[0].Value = "A";
        grid.Rows[0].Cells[1].Value = "D";
        grid.Rows[0].Cells[2].Value = "G";
        grid.Rows[2].Cells[0].Value = "C";
        grid.Rows[2].Cells[1].Value = "F";
        grid.Rows[2].Cells[2].Value = "I";

        grid.ClearSelection();
        SeleccionarFilaCompleta(grid, 0);
        SeleccionarFilaCompleta(grid, 2);

        var filas = DgvCeldaHelper.FilasCompletasSeleccionadas(grid);
        CollectionAssert.AreEqual(new[] { 0, 2 }, filas.Select(f => f.Index).ToArray(),
            "Filas completas ordenadas por índice.");

        var tsv = DgvCeldaHelper.ConstruirTsvDeFilas(grid, filas);
        Assert.AreEqual($"A\tD\tG{Environment.NewLine}C\tF\tI", tsv,
            "Una línea por fila con todas las columnas visibles.");
    }

    // ═══════════════════════ Etiqueta ═══════════════════════

    /// <summary>La etiqueta del ítem de copia refleja celdas vs fila(s) completa(s).</summary>
    [TestMethod]
    public void EtiquetaCopiar_SegunLaSeleccion()
    {
        using var grid = CrearGrid(filas: 3, columnas: 3);

        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        Assert.AreEqual("Copiar celda", DgvCeldaHelper.EtiquetaCopiar(grid));

        grid.Rows[0].Cells[2].Selected = true;
        Assert.AreEqual("Copiar celdas", DgvCeldaHelper.EtiquetaCopiar(grid));

        grid.ClearSelection();
        SeleccionarFilaCompleta(grid, 0);
        Assert.AreEqual("Copiar fila", DgvCeldaHelper.EtiquetaCopiar(grid));

        grid.ClearSelection();
        SeleccionarFilaCompleta(grid, 0);
        SeleccionarFilaCompleta(grid, 1);
        Assert.AreEqual("Copiar 2 filas", DgvCeldaHelper.EtiquetaCopiar(grid));
    }

    // ═══════════════════════ SeleccionGridHelper (se conserva) ═══════════════════════

    /// <summary>
    /// Celdas sueltas en varias filas (selección por celda) resuelven las filas
    /// COMPLETAS y ordenadas; <c>SelectedRows</c> permanece vacío bajo CellSelect.
    /// </summary>
    [TestMethod]
    public void FilasSeleccionadas_DesdeCeldasEnVariasFilas_DevuelveFilasCompletas()
    {
        using var grid = CrearGrid(filas: 3, columnas: 3);
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[1].Cells[2].Selected = true;
        grid.Rows[2].Cells[1].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(0, grid.SelectedRows.Count, "CellSelect por celdas no puebla SelectedRows.");
        CollectionAssert.AreEqual(new[] { 0, 1, 2 }, filas.Select(f => f.Index).ToArray(),
            "Devuelve las filas distintas, ordenadas por índice ascendente.");
        Assert.IsTrue(filas.All(f => f.Cells.Count == grid.ColumnCount),
            "Cada elemento es la fila COMPLETA (acceso a todas sus celdas).");
    }

    /// <summary>
    /// El clic derecho DENTRO de la selección no debe limpiarla; FUERA sí limpia. Bajo
    /// CellSelect se decide con <see cref="DgvCeldaHelper.ClicDentroDeSeleccion"/>.
    /// </summary>
    [TestMethod]
    public void ClicDerechoDentroDeLaSeleccion_NoSeLimpiaria()
    {
        using var grid = CrearGrid(filas: 3, columnas: 2);
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[2].Cells[1].Selected = true;

        Assert.IsTrue(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 2),
            "El clic cayó DENTRO de la selección ⇒ NO debe ejecutarse ClearSelection.");
        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 1),
            "El clic cayó FUERA de la selección ⇒ se limpia y se selecciona la fila clickeada.");
    }
}
