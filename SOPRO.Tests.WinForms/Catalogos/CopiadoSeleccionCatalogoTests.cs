using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// H7a — el copiado/selección de los grids de catálogo NO debe romperse al pasar a
/// <see cref="DataGridViewSelectionMode.CellSelect"/> (formato por celda).
///
/// Bajo CellSelect, <c>SelectedRows</c> queda vacío aunque haya celdas seleccionadas
/// en varios renglones. Por eso:
///   • Ctrl+C resuelve las filas con <see cref="SeleccionGridHelper.FilasSeleccionadas"/>
///     (celdas + filas) y sólo copia la CELDA activa cuando hay una sola fila.
///   • El clic derecho DENTRO de una selección múltiple no debe limpiarla.
/// Se prueban los criterios puros extraídos en <see cref="DgvCeldaHelper"/> y el
/// helper <see cref="SeleccionGridHelper.FilasSeleccionadas"/>, sin depender de UI.
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

    // ═════════════════════════ Ctrl+C: celda vs filas ═════════════════════════

    /// <summary>
    /// Una sola celda (una sola fila) ⇒ Ctrl+C copia la CELDA activa, no las filas.
    /// Regresión: bajo CellSelect antes se copiaba siempre una celda; aquí se valida
    /// que una única fila mantiene esa semántica.
    /// </summary>
    [TestMethod]
    public void CtrlC_UnaCelda_CopiaSoloLaCelda()
    {
        using var grid = CrearGrid(filas: 3);
        grid.ClearSelection();
        grid.Rows[1].Cells[0].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(0, grid.SelectedRows.Count, "CellSelect por celdas no puebla SelectedRows.");
        Assert.AreEqual(1, filas.Count);
        Assert.IsFalse(DgvCeldaHelper.DebeCopiarFilas(filas.Count, clicEnRowHeader: false),
            "Una sola fila ⇒ Ctrl+C copia la CELDA activa.");
    }

    /// <summary>
    /// Celdas en dos filas distintas ⇒ Ctrl+C copia las FILAS completas como TSV
    /// (una línea por fila, todas las columnas). El helper ignora que
    /// <c>SelectedRows</c> esté vacío al resolver las filas desde <c>SelectedCells</c>.
    /// </summary>
    [TestMethod]
    public void CtrlC_CeldasEnDosFilas_CopiaFilasCompletas()
    {
        using var grid = CrearGrid(filas: 3);
        grid.Rows[0].Cells[0].Value = "A";
        grid.Rows[0].Cells[1].Value = "D";
        grid.Rows[0].Cells[2].Value = "G";
        grid.Rows[2].Cells[0].Value = "C";
        grid.Rows[2].Cells[1].Value = "F";
        grid.Rows[2].Cells[2].Value = "I";

        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[2].Cells[1].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(0, grid.SelectedRows.Count, "CellSelect por celdas no puebla SelectedRows.");
        Assert.AreEqual(2, filas.Count);
        CollectionAssert.AreEqual(new[] { 0, 2 }, filas.Select(f => f.Index).ToArray());
        Assert.IsTrue(DgvCeldaHelper.DebeCopiarFilas(filas.Count, clicEnRowHeader: false),
            "Dos filas distintas ⇒ Ctrl+C copia las FILAS completas.");

        // Semántica TSV: una línea por fila con TODAS las columnas (filas completas).
        var tsv = string.Join("\n", filas.Select(r => string.Join("\t",
            Enumerable.Range(0, grid.ColumnCount).Select(i => r.Cells[i].Value?.ToString() ?? ""))));

        Assert.AreEqual("A\tD\tG\nC\tF\tI", tsv, "TSV con las filas completas seleccionadas.");
    }

    /// <summary>
    /// Un clic en el RowHeader fuerza la copia de la fila completa aunque no haya
    /// más de una fila seleccionada (semántica previa conservada).
    /// </summary>
    [TestMethod]
    public void CtrlC_ClicEnRowHeader_CopiaFilaCompleta()
    {
        Assert.IsTrue(DgvCeldaHelper.DebeCopiarFilas(filasSeleccionadas: 1, clicEnRowHeader: true),
            "Clic en RowHeader ⇒ copia la fila completa.");
        Assert.IsTrue(DgvCeldaHelper.DebeCopiarFilas(filasSeleccionadas: 0, clicEnRowHeader: true));
        Assert.IsFalse(DgvCeldaHelper.DebeCopiarFilas(filasSeleccionadas: 1, clicEnRowHeader: false));
    }

    /// <summary>El label del menú contextual refleja si se copian filas o una sola.</summary>
    [TestMethod]
    public void EtiquetaCopiarFilas_ReflejaElNumeroDeFilas()
    {
        Assert.AreEqual("Copiar fila", DgvCeldaHelper.EtiquetaCopiarFilas(0));
        Assert.AreEqual("Copiar fila", DgvCeldaHelper.EtiquetaCopiarFilas(1));
        Assert.AreEqual("Copiar 3 filas", DgvCeldaHelper.EtiquetaCopiarFilas(3));
    }

    // ═════════════════ Clic derecho dentro/fuera de la selección ═════════════════

    /// <summary>
    /// El clic derecho DENTRO de la selección no debe limpiarla; FUERA sí limpia y
    /// reselecciona la fila clickeada (comportamiento previo). Bajo CellSelect,
    /// `clickedRow.Selected` es falso aunque la fila tenga celdas seleccionadas, por
    /// lo que el catálogo debe decidir con <see cref="SeleccionGridHelper.FilasSeleccionadas"/>.
    /// </summary>
    [TestMethod]
    public void ClicDerechoDentroDeLaSeleccion_NoLimpiaLaSeleccion()
    {
        using var grid = CrearGrid(filas: 3);
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[2].Cells[1].Selected = true;

        // Fila 2 YA pertenece a la selección → clic DENTRO: no limpiar.
        Assert.IsTrue(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 2),
            "El clic cayó DENTRO de la selección ⇒ no debe ejecutarse ClearSelection.");

        // Fila 1 NO pertenece a la selección → clic FUERA: limpiar y reseleccionar.
        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 1),
            "El clic cayó FUERA de la selección ⇒ se limpia y se selecciona la fila clickeada.");
    }

    /// <summary>Sin selección (o índice inválido) el criterio nunca marca "dentro".</summary>
    [TestMethod]
    public void ClicDentroDeSeleccion_SinSeleccion_DevuelveFalse()
    {
        using var grid = CrearGrid(filas: 2);
        grid.ClearSelection();

        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(grid, 0));
        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(grid, -1));
        Assert.IsFalse(DgvCeldaHelper.ClicDentroDeSeleccion(null!, 0));
    }
}
