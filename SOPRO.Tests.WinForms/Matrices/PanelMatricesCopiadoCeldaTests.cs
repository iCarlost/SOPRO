using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>Regresiones de selección y copiado por celda del panel embebido (#106).</summary>
[STATestClass]
[DoNotParallelize]
public class PanelMatricesCopiadoCeldaTests
{
    [TestMethod]
    public void GridComponentes_UsaSeleccionPorCelda_SinAlterarGeometriaCompacta()
    {
        using EscenarioCopiado escenario = new EscenarioCopiado();
        DataGridView grid = escenario.Grid;

        Assert.AreEqual(DataGridViewSelectionMode.CellSelect, grid.SelectionMode);
        Assert.IsTrue(grid.MultiSelect);
        Assert.AreEqual(DataGridViewClipboardCopyMode.Disable, grid.ClipboardCopyMode,
            "El copiado nativo no debe competir con DgvCeldaHelper.");
        Assert.IsFalse(grid.RowHeadersVisible,
            "Los encabezados de fila desplazarían la geometría compacta del panel.");
    }

    [TestMethod]
    public void BloqueDeCeldas_ConstruyeTsvExacto_SinExpandirseAFila()
    {
        using EscenarioCopiado escenario = new EscenarioCopiado();
        DataGridView grid = escenario.Grid;
        escenario.AgregarFilas();

        grid.ClearSelection();
        grid.Rows[0].Cells["ColClave"].Selected = true;
        grid.Rows[0].Cells["ColDesc"].Selected = true;

        Assert.AreEqual(0, DgvCeldaHelper.FilasCompletasSeleccionadas(grid).Count,
            "Dos celdas no representan una fila completa.");
        Assert.AreEqual("MAT-001\tArena", DgvCeldaHelper.ConstruirTsvDeCeldasSeleccionadas(grid));
        Assert.AreEqual("Copiar celdas", DgvCeldaHelper.EtiquetaCopiar(grid));
    }

    [TestMethod]
    public void ClicDerecho_PreservaBloqueExistente_YFueraSeleccionaSoloLaCeldaDestino()
    {
        using EscenarioCopiado escenario = new EscenarioCopiado();
        DataGridView grid = escenario.Grid;
        escenario.AgregarFilas();

        grid.CurrentCell = grid.Rows[0].Cells["ColClave"];
        grid.ClearSelection();
        grid.Rows[0].Cells["ColClave"].Selected = true;
        grid.Rows[0].Cells["ColDesc"].Selected = true;

        escenario.DispararClicDerechoCelda(0, grid.Columns["ColDesc"].Index);

        Assert.AreEqual(2, grid.SelectedCells.Count,
            "El menú contextual no debe destruir un bloque que ya contiene la celda pulsada.");
        Assert.IsTrue(grid.Rows[0].Cells["ColClave"].Selected);
        Assert.IsTrue(grid.Rows[0].Cells["ColDesc"].Selected);

        escenario.DispararClicDerechoCelda(1, grid.Columns["ColDesc"].Index);

        Assert.AreEqual(1, grid.SelectedCells.Count,
            "Fuera del bloque previo debe quedar seleccionada únicamente la celda pulsada.");
        Assert.IsTrue(grid.Rows[1].Cells["ColDesc"].Selected);
        Assert.AreSame(grid.Rows[1].Cells["ColDesc"], grid.CurrentCell);
    }

    private sealed class EscenarioCopiado : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), $"sopro_panel_copiado_{Guid.NewGuid():N}");
        private readonly SOPROContext _context;
        private readonly PanelMatricesEmbebido _panel;
        private readonly Form _host;

        public EscenarioCopiado()
        {
            Directory.CreateDirectory(_root);
            _context = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
            _context.Database.EnsureCreated();

            Proyecto proyecto = new Proyecto
            {
                Nombre = "Proyecto copiado por celda",
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
            _context.Proyectos.Add(proyecto);
            _context.SaveChanges();

            _panel = new PanelMatricesEmbebido(_context, proyecto.Id);
            _host = new Form
            {
                StartPosition = FormStartPosition.Manual,
                Location = new System.Drawing.Point(-4000, -4000),
                ShowInTaskbar = false,
                ClientSize = new System.Drawing.Size(800, 400),
            };
            _host.Controls.Add(_panel);
            _host.Show();
            System.Windows.Forms.Application.DoEvents();
        }

        public DataGridView Grid => _panel.GridComponentesParaPrueba;

        public void AgregarFilas()
        {
            Grid.Rows.Add("Material", "MAT-001", "Arena", "m3", "2.00", "60.00", "120.00", 1, string.Empty);
            Grid.Rows.Add("Material", "MAT-002", "Cemento", "kg", "3.00", "80.00", "240.00", 2, string.Empty);
        }

        public void DispararClicDerechoCelda(int rowIndex, int columnIndex)
        {
            MethodInfo metodo = typeof(PanelMatricesEmbebido).GetMethod(
                "DgvComponentes_CellMouseDown",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("No se encontró la ruta de clic derecho del grid.");
            MouseEventArgs mouse = new MouseEventArgs(MouseButtons.Right, 1, 2, 2, 0);
            DataGridViewCellMouseEventArgs args = new DataGridViewCellMouseEventArgs(
                columnIndex, rowIndex, 2, 2, mouse);

            metodo.Invoke(_panel, new object[] { Grid, args });
        }

        public void Dispose()
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    _host.Dispose();
                    _panel.Dispose();
                    _context.Database.EnsureDeleted();
                    _context.Dispose();
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
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
    }
}
