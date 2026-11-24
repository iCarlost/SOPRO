using System;
using System.Drawing;
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

/// <summary>
/// Contrato visual del panel embebido de matrices (issue #169):
/// la banda oscura de identidad, los chips APU/Básico/Cuadrilla y los botones
/// ▲▼ usan las esquinas redondeadas del tema (misma Región recalculada ante
/// tamaño/DPI que el resto del chrome), y la acción eliminar del grid muestra
/// exactamente el mismo glifo del editor de matriz (FormEditarMatriz) sin
/// alterar geometría, estilos ni comportamiento del grid. Todo verificable a
/// 96 DPI comparando la Región con la fórmula.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class PanelMatricesEmbebidoRedondeadoTests
{
    [TestMethod]
    public void CabeceraChipsFlechas_TienenEsquinasRedondeadasDelTema()
    {
        using EscenarioRedondeado escenario = new();
        var panel = escenario.Panel;

        Control cabecera = escenario.ControlPrivado("_panelHeader");
        Control anterior = escenario.ControlPrivado("_btnAnterior");
        Control siguiente = escenario.ControlPrivado("_btnSiguiente");

        var redondeados = new Control[]
        {
            cabecera,
            panel.ChipApuParaPrueba!,
            panel.ChipBasicoParaPrueba!,
            panel.ChipCuadrillaParaPrueba!,
            anterior,
            siguiente,
        };

        foreach (Control control in redondeados)
        {
            Assert.IsTrue(control.Width > 8 && control.Height > 8,
                $"Precondición: '{control.Name}' debe tener tamaño suficiente para el radio del tema ({control.Size}).");
            Assert.IsNotNull(control.Region,
                $"'{control.Name}' debe tener Región de esquinas redondeadas aplicada.");
            // La esquina (1,1) queda fuera del arco del radio aplicado (14 en la
            // cabecera como superficie, 10 en chips/flechas a 96 DPI)
            // y el centro y los puntos medios de los bordes quedan dentro: así se
            // demuestra el redondeo (y que la Región sigue cubriendo todo el borde)
            // sin depender de la apariencia en pantalla.
            Assert.IsFalse(control.Region!.IsVisible(1, 1),
                $"La esquina de '{control.Name}' debe quedar recortada por el arco del radio del tema.");
            Assert.IsTrue(control.Region.IsVisible(control.Width / 2, control.Height / 2),
                $"El interior de '{control.Name}' debe seguir visible bajo la Región.");
            Assert.IsTrue(control.Region.IsVisible(control.Width - 1, control.Height / 2),
                $"El borde derecho de '{control.Name}' debe seguir dentro de la Región.");
        }

        // El recorte sigue a los cambios de tamaño (Resize/RecalibrarAlturasCromo).
        escenario.Host.ClientSize = new Size(1000, 500);
        System.Windows.Forms.Application.DoEvents();
        Assert.IsFalse(cabecera.Region!.IsVisible(1, 1),
            "Tras el cambio de tamaño la cabecera sigue con esquinas redondeadas.");
        Assert.IsTrue(cabecera.Region.IsVisible(cabecera.Width - 1, cabecera.Height / 2),
            "La Región de la cabecera debe recalcularse y cubrir el nuevo borde derecho.");
    }

    [TestMethod]
    public void JerarquiaDeRadios_Cabecera14_YControlesPequenos10()
    {
        using EscenarioRedondeado escenario = new();
        var panel = escenario.Panel;

        Assert.IsTrue(SoproUiMetrics.SurfaceRadiusPx > SoproUiMetrics.ThemeRadiusPx,
            "Las superficies estructurales deben redondearse más que los controles pequeños.");

        Control cabecera = escenario.ControlPrivado("_panelHeader");
        Control anterior = escenario.ControlPrivado("_btnAnterior");

        // Radio 14 en la cabecera (superficie): el punto (4,4) queda fuera del
        // arco (centro (14,14), distancia ~14.14 > 14).
        Assert.IsFalse(cabecera.Region!.IsVisible(4, 4),
            "La cabecera debe usar el radio de superficie (14), no el de controles (10).");

        // Radio 10 en controles pequeños: el punto (4,4) queda dentro del arco
        // (centro (10,10), distancia ~8.49 < 10).
        Assert.IsTrue(anterior.Region!.IsVisible(4, 4),
            "La flecha debe usar el radio de controles pequeños (10).");
        Assert.IsTrue(panel.ChipApuParaPrueba!.Region!.IsVisible(4, 4),
            "El chip de tipo debe usar el radio de controles pequeños (10).");
    }

    [TestMethod]
    public void AccionEliminar_UsaElGlifoDelEditorDeMatriz_SinCambiarGeometriaNiEstilos()
    {
        using EscenarioRedondeado escenario = new();
        DataGridView grid = escenario.Panel.GridComponentesParaPrueba;

        var colEliminar = AssertarColumnaEliminar(grid);

        // Mismo glifo EXACTO que la columna del editor de matriz
        // (FormEditarMatriz: colEliminar.Text = "🗑"), sin variante de selección.
        Assert.AreEqual("🗑", colEliminar.Text,
            "La acción eliminar debe usar exactamente el glifo del editor de matriz.");
        Assert.IsTrue(colEliminar.UseColumnTextForButtonValue,
            "El glifo se muestra en todas las filas, igual que en el editor de matriz.");

        // Solo cambia el texto mostrado: geometría y estilos intactos.
        Assert.AreEqual(DataGridViewAutoSizeColumnMode.AllCells, colEliminar.AutoSizeMode,
            "El modo de dimensionado de la columna eliminar no cambia.");
        Assert.AreEqual(84, colEliminar.MinimumWidth,
            "El ancho mínimo de la columna eliminar no cambia.");
        Assert.AreEqual(FlatStyle.Flat, colEliminar.FlatStyle,
            "El estilo plano de la columna eliminar no cambia.");
        Assert.AreEqual(Color.FromArgb(150, 0, 0), colEliminar.DefaultCellStyle.ForeColor,
            "El color de la acción eliminar no cambia.");
        Assert.AreEqual(DataGridViewContentAlignment.MiddleCenter, colEliminar.DefaultCellStyle.Alignment,
            "La alineación de la acción eliminar no cambia.");

        // El glifo cabe de sobra en el ancho mínimo: la columna no se estrecha
        // (misma geometría que con el texto anterior).
        int anchoGlifo = TextRenderer.MeasureText("🗑", grid.Font).Width;
        Assert.IsTrue(colEliminar.MinimumWidth >= anchoGlifo,
            $"El ancho mínimo ({colEliminar.MinimumWidth}) debe seguir cubriendo el glifo ({anchoGlifo}px).");
    }

    [TestMethod]
    public void GridComponentes_GeometriaYColumnasQuedanIntactas()
    {
        using EscenarioRedondeado escenario = new();
        DataGridView grid = escenario.Panel.GridComponentesParaPrueba;

        var nombres = new System.Collections.Generic.List<string>();
        foreach (DataGridViewColumn columna in grid.Columns)
            nombres.Add(columna.Name);

        CollectionAssert.AreEqual(new[]
        {
            "ColTipo", "ColClave", "ColDesc", "ColUnidad", "ColCantidad",
            "ColPU", "ColImporte", "ColId", "ColEliminar",
        }, nombres, "El conjunto y el orden de columnas del grid no cambian.");

        Assert.IsFalse(grid.RowHeadersVisible,
            "La geometría compacta (sin encabezados de fila) no cambia.");
        Assert.AreEqual(28, grid.RowTemplate.Height,
            "La altura de fila compacta del panel no cambia.");
        Assert.AreEqual(SoproUiMetrics.GridHeaderHeight, grid.ColumnHeadersHeight,
            "La altura de encabezado del panel no cambia.");

        var colDesc = grid.Columns["ColDesc"]!;
        Assert.AreEqual(DataGridViewAutoSizeColumnMode.Fill, colDesc.AutoSizeMode,
            "La columna Descripción sigue absorbiendo el espacio sobrante.");

        var colId = grid.Columns["ColId"]!;
        Assert.IsFalse(colId.Visible, "La columna técnica ID sigue oculta.");

        AssertarColumnaEliminar(grid);
    }

    private static DataGridViewButtonColumn AssertarColumnaEliminar(DataGridView grid)
    {
        var colEliminar = grid.Columns["ColEliminar"] as DataGridViewButtonColumn
            ?? throw new AssertFailedException("ColEliminar debe seguir siendo una columna de botones.");
        Assert.AreEqual(0, colEliminar.HeaderText.Length,
            "La columna eliminar sigue sin encabezado visible.");
        return colEliminar;
    }

    private sealed class EscenarioRedondeado : IDisposable
    {
        private readonly string _root = Path.Combine(
            Path.GetTempPath(), $"sopro_panel_redondeado_{Guid.NewGuid():N}");
        private readonly SOPROContext _context;
        private readonly PanelMatricesEmbebido _panel;
        private readonly Form _host;

        public EscenarioRedondeado()
        {
            Directory.CreateDirectory(_root);
            _context = new SOPROContext(Path.Combine(_root, "proyecto-temporal.soproproj"));
            _context.Database.EnsureCreated();

            Proyecto proyecto = new Proyecto
            {
                Nombre = "Proyecto redondeado del panel",
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
                Location = new Point(-4000, -4000),
                ShowInTaskbar = false,
                ClientSize = new Size(800, 400),
            };
            _host.Controls.Add(_panel);
            _host.Show();
            System.Windows.Forms.Application.DoEvents();
        }

        public PanelMatricesEmbebido Panel => _panel;
        public Form Host => _host;

        public Control ControlPrivado(string nombreCampo)
        {
            FieldInfo? campo = typeof(PanelMatricesEmbebido).GetField(
                nombreCampo, BindingFlags.Instance | BindingFlags.NonPublic);
            return (Control?)campo?.GetValue(_panel)
                ?? throw new AssertFailedException($"No se encontró el control privado {nombreCampo}.");
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
            }
        }
    }
}
