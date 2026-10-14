using System;
using System.Linq;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Seleccion;

/// <summary>
/// H7b — el COPIADO/SELECCIÓN de Presupuesto y Matrices NO debe romperse al usar
/// <see cref="DataGridViewSelectionMode.CellSelect"/> (formato por celda).
///
/// Bajo CellSelect, <c>SelectedRows</c> queda vacío aunque haya celdas seleccionadas
/// en varios renglones. Por eso:
///   • Las operaciones por fila resuelven sus filas con
///     <see cref="SeleccionGridHelper.FilasSeleccionadas"/> (celdas + filas).
///   • El clic derecho DENTRO de una selección múltiple no debe limpiarla
///     (<see cref="DgvCeldaHelper.ClicDentroDeSeleccion"/>).
///   • La copia de filas produce TSV con todas las columnas visibles ordenadas por
///     <see cref="DataGridViewColumn.DisplayIndex"/> (<see cref="FormPresupuesto.ConstruirTsvDeFilas"/>).
/// Se prueban criterios puros, sin depender de UI real.
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

    // ═════════════════ SeleccionGridHelper.FilasSeleccionadas ═════════════════

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

    // ═════════════════ Clic derecho dentro/fuera de la selección ═════════════════

    /// <summary>
    /// El clic derecho DENTRO de la selección no debe limpiarla; FUERA sí limpia y
    /// reselecciona la fila clickeada. Bajo CellSelect, <c>clickedRow.Selected</c> es
    /// falso aunque la fila tenga celdas seleccionadas, por lo que el menú de
    /// Presupuesto decide con <see cref="DgvCeldaHelper.ClicDentroDeSeleccion"/>.
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

    // ═════════════════ Copia de filas → TSV (Presupuesto) ═════════════════

    /// <summary>
    /// La copia de filas de Presupuesto produce una línea por fila con TODAS las
    /// columnas visibles ordenadas por <see cref="DataGridViewColumn.DisplayIndex"/>,
    /// a partir de las filas resueltas por <see cref="SeleccionGridHelper.FilasSeleccionadas"/>.
    /// </summary>
    [TestMethod]
    public void Presupuesto_CopiaFilas_MultipleFilas_ProduceTSV()
    {
        using var grid = CrearGrid(filas: 2, columnas: 3);
        grid.Rows[0].Cells[0].Value = "A";
        grid.Rows[0].Cells[1].Value = "D";
        grid.Rows[0].Cells[2].Value = "G";
        grid.Rows[1].Cells[0].Value = "C";
        grid.Rows[1].Cells[1].Value = "F";
        grid.Rows[1].Cells[2].Value = "I";

        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[1].Cells[2].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);
        var tsv = FormPresupuesto.ConstruirTsvDeFilas(grid, filas);

        Assert.AreEqual(
            $"A\tD\tG{Environment.NewLine}C\tF\tI",
            tsv,
            "Una línea por fila completa, columnas visibles ordenadas por DisplayIndex.");
    }

    /// <summary>El conteo de filas seleccionadas alimenta el label/conteo del menú.</summary>
    [TestMethod]
    public void Presupuesto_ConteoFilas_ReflejaFilasSeleccionadasPorCelda()
    {
        using var grid = CrearGrid(filas: 3, columnas: 2);
        grid.ClearSelection();
        grid.Rows[0].Cells[0].Selected = true;
        grid.Rows[1].Cells[1].Selected = true;

        var filas = SeleccionGridHelper.FilasSeleccionadas(grid);

        Assert.AreEqual(2, filas.Count, "El label 'Copiar 2 filas' se deriva de este conteo.");
        Assert.IsTrue(filas.Count > 1, "Dos filas distintas ⇒ semántica de copia múltiple.");
    }
}
