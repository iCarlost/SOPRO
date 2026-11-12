using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>Paridad del grid de <see cref="FormEditarMatriz"/> con el panel embebido (#109).</summary>
[STATestClass]
[DoNotParallelize]
public class FormEditarMatrizInteraccionGridTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void GridComponentes_UsaSeleccionYCopiadoPorCelda_SinOrdenamiento()
    {
        using EscenarioEditor escenario = new EscenarioEditor();
        DataGridView grid = escenario.Grid;

        Assert.AreEqual(DataGridViewSelectionMode.CellSelect, grid.SelectionMode);
        Assert.IsTrue(grid.MultiSelect);
        Assert.AreEqual(DataGridViewClipboardCopyMode.Disable, grid.ClipboardCopyMode);
        Assert.IsFalse(grid.RowHeadersVisible);
        Assert.IsTrue(grid.Columns.Cast<DataGridViewColumn>()
            .All(c => c.SortMode == DataGridViewColumnSortMode.NotSortable),
            "El editor vincula fila y componente por índice; ninguna columna puede reordenar el grid.");

        grid.ClearSelection();
        grid.Rows[4].Cells["colClave"].Selected = true;
        grid.Rows[4].Cells["colDescripcion"].Selected = true;

        Assert.AreEqual(0, DgvCeldaHelper.FilasCompletasSeleccionadas(grid).Count);
        Assert.AreEqual("MAT-001\tArena", DgvCeldaHelper.ConstruirTsvDeCeldasSeleccionadas(grid));
        Assert.AreEqual("Copiar celdas", DgvCeldaHelper.EtiquetaCopiar(grid));
    }

    [TestMethod]
    public void CantidadEnFilaAlejada_ConservaCeldaFocoYScrollTrasRecalcular()
    {
        using EscenarioEditor escenario = new EscenarioEditor();
        DataGridView grid = escenario.Grid;
        int filaEditada = 12;
        int primeraVisible = 6;

        grid.CurrentCell = grid.Rows[filaEditada].Cells["colCantidad"];
        grid.FirstDisplayedScrollingRowIndex = primeraVisible;
        grid.Focus();
        Assert.IsTrue(grid.BeginEdit(true));
        Assert.IsNotNull(grid.EditingControl);
        grid.EditingControl.Text = "3";

        grid.CurrentCell = grid.Rows[filaEditada + 1].Cells["colDescripcion"];
        System.Windows.Forms.Application.DoEvents();

        Assert.IsNotNull(grid.CurrentCell);
        Assert.AreEqual(filaEditada, grid.CurrentCell.RowIndex,
            "El repintado diferido debe volver a la fila editada, no al inicio.");
        Assert.AreEqual("colCantidad", grid.CurrentCell.OwningColumn.Name);
        Assert.AreEqual(primeraVisible, grid.FirstDisplayedScrollingRowIndex);
        Assert.IsTrue(grid.ContainsFocus, "El foco debe permanecer en el grid.");
        Assert.AreEqual(3m, Convert.ToDecimal(grid.Rows[filaEditada].Cells["colCantidad"].Value, CultureInfo.CurrentCulture));
    }

    [TestMethod]
    public void CantidadInvalida_RevierteValorYConservaCeldaYScroll()
    {
        using EscenarioEditor escenario = new EscenarioEditor();
        DataGridView grid = escenario.Grid;
        int filaEditada = 10;
        int primeraVisible = 5;
        decimal cantidadOriginal = Convert.ToDecimal(
            grid.Rows[filaEditada].Cells["colCantidad"].Value,
            CultureInfo.CurrentCulture);

        grid.CurrentCell = grid.Rows[filaEditada].Cells["colCantidad"];
        grid.FirstDisplayedScrollingRowIndex = primeraVisible;
        grid.Focus();
        Assert.IsTrue(grid.BeginEdit(true));
        grid.EditingControl!.Text = "-1";

        grid.CurrentCell = grid.Rows[filaEditada + 1].Cells["colDescripcion"];
        System.Windows.Forms.Application.DoEvents();

        Assert.IsNotNull(grid.CurrentCell);
        Assert.AreEqual(filaEditada, grid.CurrentCell.RowIndex);
        Assert.AreEqual("colCantidad", grid.CurrentCell.OwningColumn.Name);
        Assert.AreEqual(primeraVisible, grid.FirstDisplayedScrollingRowIndex);
        Assert.AreEqual(cantidadOriginal, Convert.ToDecimal(
            grid.Rows[filaEditada].Cells["colCantidad"].Value,
            CultureInfo.CurrentCulture));
    }

    [TestMethod]
    [DataRow("colDescripcion", "Descripción actualizada")]
    [DataRow("colUnidad", "lt")]
    [DataRow("colPU", "12.50")]
    public void OtrasCeldasEditables_ConservanCeldaFocoYScroll(
        string nombreColumna,
        string valorEditado)
    {
        using EscenarioEditor escenario = new EscenarioEditor();
        DataGridView grid = escenario.Grid;
        int filaEditada = 11;
        int primeraVisible = 5;

        grid.CurrentCell = grid.Rows[filaEditada].Cells[nombreColumna];
        grid.FirstDisplayedScrollingRowIndex = primeraVisible;
        grid.Focus();
        Assert.IsTrue(grid.BeginEdit(true));
        grid.EditingControl!.Text = valorEditado;

        grid.CurrentCell = grid.Rows[filaEditada + 1].Cells["colDescripcion"];
        System.Windows.Forms.Application.DoEvents();

        Assert.IsNotNull(grid.CurrentCell);
        Assert.AreEqual(filaEditada, grid.CurrentCell.RowIndex);
        Assert.AreEqual(nombreColumna, grid.CurrentCell.OwningColumn.Name);
        Assert.AreEqual(primeraVisible, grid.FirstDisplayedScrollingRowIndex);
        Assert.IsTrue(grid.ContainsFocus);
    }

    [TestMethod]
    public void ReconstruccionTrasEliminarUltimaFila_ClampeaSeleccionALaNuevaUltima()
    {
        using EscenarioEditor escenario = new EscenarioEditor();
        DataGridView grid = escenario.Grid;
        int ultimaOriginal = grid.Rows.Count - 1;
        grid.CurrentCell = grid.Rows[ultimaOriginal].Cells["colCantidad"];
        grid.FirstDisplayedScrollingRowIndex = 7;

        escenario.EliminarUltimoComponenteYReconstruir();

        Assert.AreEqual(ultimaOriginal, grid.Rows.Count,
            "La reconstrucción debe reflejar la eliminación del último componente.");
        Assert.IsNotNull(grid.CurrentCell);
        Assert.AreEqual(grid.Rows.Count - 1, grid.CurrentCell.RowIndex,
            "La fila eliminada debe clampear a la nueva última fila.");
        Assert.AreEqual("colCantidad", grid.CurrentCell.OwningColumn.Name);
        Assert.AreEqual(7, grid.FirstDisplayedScrollingRowIndex);
    }

    private sealed class EscenarioEditor : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), $"sopro_editar_matriz_grid_{Guid.NewGuid():N}");
        private readonly Proyecto? _proyectoGlobalPrevio;
        private readonly SOPROContext _context;
        private readonly FormEditarMatriz _form;

        public EscenarioEditor()
        {
            Directory.CreateDirectory(_root);
            _proyectoGlobalPrevio = LeerProyectoGlobalFormato();
            _context = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
            _context.Database.EnsureDeleted();
            _context.Database.EnsureCreated();

            Proyecto proyecto = CrearProyecto();
            _context.Proyectos.Add(proyecto);
            _context.SaveChanges();

            Material material = new Material
            {
                ProyectoId = proyecto.Id,
                Clave = "MAT-001",
                Descripcion = "Arena",
                Unidad = "m3",
                PrecioUnitario = 10m,
                Notas = string.Empty,
            };
            _context.Materiales.Add(material);
            _context.SaveChanges();

            Matriz matriz = new Matriz
            {
                ProyectoId = proyecto.Id,
                Clave = "APU-FOCO",
                Descripcion = "Matriz para conservar foco",
                Unidad = "m3",
                Tipo = TipoMatriz.APU,
                Notas = string.Empty,
            };
            _context.Matrices.Add(matriz);
            _context.SaveChanges();

            for (int i = 0; i < 40; i++)
            {
                decimal cantidad = i + 1;
                _context.ComponentesMatriz.Add(new ComponenteMatriz
                {
                    MatrizId = matriz.Id,
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = material.Id,
                    Cantidad = cantidad,
                    Importe = cantidad * material.PrecioUnitario,
                    Orden = i + 1,
                    Notas = string.Empty,
                });
            }
            matriz.CostoDirecto = 8200m;
            _context.SaveChanges();

            _form = new FormEditarMatriz(_context, proyecto.Id, matriz)
            {
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-4000, -4000),
                ShowInTaskbar = false,
            };
            _form.Show();
            System.Windows.Forms.Application.DoEvents();
        }

        public DataGridView Grid => _form.DgvComponentesParaPrueba;

        public void EliminarUltimoComponenteYReconstruir()
        {
            FieldInfo campo = typeof(FormEditarMatriz).GetField(
                "_componentesTemp",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("No se encontró la colección temporal del editor.");
            List<ComponenteMatriz> componentes =
                (List<ComponenteMatriz>)(campo.GetValue(_form)
                    ?? throw new InvalidOperationException("La colección temporal no está disponible."));
            componentes.RemoveAt(componentes.Count - 1);

            MethodInfo metodo = typeof(FormEditarMatriz).GetMethod(
                "ActualizarGridComponentes",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("No se encontró la reconstrucción del grid.");
            object?[] parametros = metodo.GetParameters().Length == 0
                ? Array.Empty<object?>()
                : new object?[] { null };
            metodo.Invoke(_form, parametros);
        }

        public void Dispose()
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    _form.Dispose();
                    _context.Database.EnsureDeleted();
                    _context.Dispose();
                    SqliteConnection.ClearAllPools();
                    RestaurarProyectoGlobalFormato(_proyectoGlobalPrevio);
                    Directory.Delete(_root, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    Thread.Sleep(200);
                }
                catch (UnauthorizedAccessException) when (intento < 4)
                {
                    Thread.Sleep(200);
                }
            }
        }

        private static Proyecto CrearProyecto()
        {
            return new Proyecto
            {
                Nombre = "Proyecto editor matriz",
                Descripcion = string.Empty,
                Ubicacion = string.Empty,
                Convocante = string.Empty,
                Contratista = string.Empty,
                ApoderadoLegal = string.Empty,
                FechaInicio = new DateTime(2026, 1, 1),
                FechaTermino = new DateTime(2026, 1, 31),
                PlazoEjecucion = 31,
                DecimalesCantidad = 2,
                DecimalesImporte = 2,
                DecimalesPorcentaje = 4,
                FechaCreacion = new DateTime(2026, 1, 1),
                FechaModificacion = new DateTime(2026, 1, 1),
            };
        }
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(
               CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");
}
