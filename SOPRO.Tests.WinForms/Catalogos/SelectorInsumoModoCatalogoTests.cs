using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Modo catálogo del selector de insumos (#75): oculta la cantidad y no ofrece
/// el proyecto propio como origen, mientras que el modo matriz conserva la
/// cantidad obligatoria y la construcción de componentes.
/// </summary>
[STATestClass]
[DoNotParallelize]
public class SelectorInsumoModoCatalogoTests
{
    [TestMethod]
    public void Selector_ModoMatriz_MantieneCantidadYDevuelveComponentes()
    {
        using var temp = new DirectorioTemporal("selector_matriz");
        using var ctx = CrearContexto(temp.DbPath);
        var proyecto = AgregarProyecto(ctx);
        ctx.Materiales.Add(new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-LOCAL",
            Descripcion = "Material local",
            Unidad = "kg",
            PrecioUnitario = 10m,
            Notas = string.Empty,
        });
        ctx.SaveChanges();

        using var selector = new FormSeleccionarInsumo(
            ctx, proyecto.Id, TipoComponenteMatriz.Material,
            modoCatalogo: false, workspaceService: CrearWorkspace(temp));
        selector.Show();
        System.Windows.Forms.Application.DoEvents();

        var nudCantidad = Campo<NumericUpDown>(selector, "nudCantidad");
        Assert.IsTrue(nudCantidad.Visible, "En modo matriz la cantidad debe seguir visible.");
        Assert.IsFalse(selector.ModoCatalogo);
        StringAssert.StartsWith(selector.Text, "Seleccionar");

        var grid = Campo<DataGridView>(selector, "dgvInsumos");
        Assert.AreEqual(1, grid.Rows.Count, "El modo matriz lista los insumos del proyecto actual.");
        grid.ClearSelection();
        var celdaVisible = grid.Rows[0].Cells.Cast<DataGridViewCell>().First(c => c.Visible);
        grid.CurrentCell = celdaVisible;
        grid.Rows[0].Selected = true;
        nudCantidad.Value = 2m;

        Invocar(selector, "btnAceptar_Click", selector, EventArgs.Empty);

        Assert.AreEqual(1, selector.ComponentesSeleccionados.Count);
        Assert.AreEqual(TipoComponenteMatriz.Material, selector.ComponentesSeleccionados[0].TipoComponente);
        Assert.AreEqual(2m, selector.ComponentesSeleccionados[0].Cantidad);
        Assert.AreEqual(0, selector.CantidadImportada);
    }

    [TestMethod]
    public void Selector_ModoCatalogo_OcultaCantidadYNoOfreceElProyectoActual()
    {
        using var temp = new DirectorioTemporal("selector_catalogo");
        using var ctx = CrearContexto(temp.DbPath);
        var proyecto = AgregarProyecto(ctx);
        ctx.Materiales.Add(new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-LOCAL",
            Descripcion = "Material local",
            Unidad = "kg",
            PrecioUnitario = 10m,
            Notas = string.Empty,
        });
        ctx.SaveChanges();

        using var selector = new FormSeleccionarInsumo(
            ctx, proyecto.Id, TipoComponenteMatriz.Material,
            modoCatalogo: true, workspaceService: CrearWorkspace(temp));
        selector.Show();
        System.Windows.Forms.Application.DoEvents();

        Assert.IsTrue(selector.ModoCatalogo);
        Assert.IsFalse(Campo<NumericUpDown>(selector, "nudCantidad").Visible,
            "En modo catálogo la cantidad no aplica.");
        Assert.IsFalse(Campo<Label>(selector, "lblCantidadLabel").Visible,
            "En modo catálogo la etiqueta de cantidad no aplica.");
        Assert.IsFalse(Campo<Button>(selector, "btnNuevoInsumo").Visible,
            "En modo catálogo no se crean insumos nuevos.");
        StringAssert.StartsWith(selector.Text, "Importar");

        var grid = Campo<DataGridView>(selector, "dgvInsumos");
        Assert.AreEqual(0, grid.Rows.Count, "El proyecto propio no debe ofrecer filas para importar.");

        var combo = Campo<ComboBox>(selector, "cboProyecto");
        var textos = combo.Items.Cast<object>().Select(item => item.ToString() ?? string.Empty).ToList();
        Assert.IsFalse(textos.Any(t => t.Contains("Proyecto actual")),
            "El combo Origen no debe ofrecer el proyecto actual.");
        Assert.IsFalse(textos.Any(t => t.Contains("Todos")),
            "El combo Origen no debe ofrecer el ámbito que incluye el proyecto actual.");
        Assert.AreEqual(0, selector.CantidadImportada);
    }

    private static ProjectWorkspaceService CrearWorkspace(DirectorioTemporal temp)
        => new(Path.Combine(temp.Ruta, "sopro"), Path.Combine(temp.Ruta, "local"));

    private static SOPROContext CrearContexto(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto selector",
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
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static T Campo<T>(Form form, string nombre) where T : class
        => form.GetType()
               .GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(form) as T
           ?? throw new InvalidOperationException($"No se encontró el campo '{nombre}'.");

    private static void Invocar(Form form, string metodo, params object?[] argumentos)
    {
        var info = form.GetType().GetMethod(metodo, BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró el método '{metodo}'.");
        info.Invoke(form, argumentos);
    }

    private sealed class DirectorioTemporal : IDisposable
    {
        public string Ruta { get; }
        public string DbPath { get; }

        public DirectorioTemporal(string nombre)
        {
            Ruta = Path.Combine(Path.GetTempPath(), $"sopro_{nombre}_{Guid.NewGuid():N}");
            Directory.CreateDirectory(Ruta);
            DbPath = Path.Combine(Ruta, "proyecto-temporal.soproproj");
        }

        public void Dispose()
        {
            for (var intento = 0; intento < 5; intento++)
            {
                try
                {
                    SqliteConnection.ClearAllPools();
                    if (Directory.Exists(Ruta))
                        Directory.Delete(Ruta, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    System.Threading.Thread.Sleep(200);
                }
            }
        }
    }
}
