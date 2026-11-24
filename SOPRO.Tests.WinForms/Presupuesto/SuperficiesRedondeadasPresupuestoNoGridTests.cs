using System;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Prototipo de superficies redondeadas: solo el chrome que NO aloja grids
/// (panelTop, panelToolbar, workspaceToolbar) recibe un Region redondeado.
/// Los grids (dgvPresupuesto y el panel del APU) no se tocan.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del modulo (controles WinForms y estado estatico).
public class SuperficiesRedondeadasPresupuestoNoGridTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestInitialize]
    public void VerificarApartmentState()
    {
        Assert.AreEqual(ApartmentState.STA, Thread.CurrentThread.GetApartmentState(),
            "Las pruebas que crean FormPresupuesto deben ejecutarse en STA.");
    }

    [TestMethod]
    public void Constructor_RedondeaElChromeSinGrid_YDejaGridsYPanelApuIntactos()
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_superficies_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        Proyecto? previo = null;
        FormPresupuesto? form = null;
        SOPROContext? ctx = null;
        try
        {
            previo = LeerProyectoGlobalFormato();
            ctx = new SOPROContext(Path.Combine(root, "proyecto-temporal.soproproj"));
            ctx.Database.EnsureCreated();
            Proyecto escenario = CrearProyecto(ctx);

            form = new FormPresupuesto(ctx, escenario, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            AssertRegionRedondeada(BuscarPorNombre(form, "panelTop"), "panelTop");
            AssertRegionRedondeada(BuscarPorNombre(form, "panelToolbar"), "panelToolbar");
            AssertRegionRedondeada(BuscarPorNombre(form, "workspaceToolbar"), "workspaceToolbar");

            Control grid = BuscarPorNombre(form, "dgvPresupuesto");
            Assert.IsTrue(grid is DataGridView,
                "La precondicion es el grid real del presupuesto.");
            Assert.IsNull(grid.Region,
                "dgvPresupuesto no debe recibir Region: los grids no se tocan.");

            Control panelMatrices = BuscarPorNombre(form, "panelMatrices");
            Assert.IsNull(panelMatrices.Region,
                "panelMatrices aloja el APU: tampoco se recorta.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(previo);
            LimpiarDirectorio(root);
        }
    }

    private static void AssertRegionRedondeada(Control control, string nombre)
    {
        Region? region = control.Region;
        Assert.IsNotNull(region, $"{nombre} debe recibir un Region redondeado.");
        Assert.IsFalse(region!.IsVisible(0, 0),
            $"{nombre}: la esquina (0,0) debe quedar fuera del Region (esquina redondeada).");
        Assert.IsTrue(region.IsVisible(control.Width / 2, control.Height / 2),
            $"{nombre}: el centro del control debe seguir visible.");
    }

    private static Control BuscarPorNombre(Control raiz, string nombre)
        => BuscarPorNombreOpcional(raiz, nombre)
           ?? throw new InvalidOperationException($"No se encontro el control {nombre}.");

    private static Control? BuscarPorNombreOpcional(Control raiz, string nombre)
    {
        if (string.Equals(raiz.Name, nombre, StringComparison.Ordinal))
            return raiz;

        foreach (Control hijo in raiz.Controls)
        {
            Control? encontrado = BuscarPorNombreOpcional(hijo, nombre);
            if (encontrado != null)
                return encontrado;
        }

        return null;
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        Proyecto proyecto = new Proyecto
        {
            Nombre = "Proyecto superficies",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            PorcentajeIVA = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?.SetValue(null, previo);

    private static void LimpiarDirectorio(string ruta)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(ruta))
                    Directory.Delete(ruta, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                Thread.Sleep(200);
            }
        }
    }
}
