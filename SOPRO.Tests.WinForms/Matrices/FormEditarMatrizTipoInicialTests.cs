using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Tipo inicial del editor de matrices (#95): crear una matriz nueva con
/// <c>tipoInicial</c> Básico o Cuadrilla deja marcado el radio correcto y
/// aplica su configuración visual, mientras que sin tipo inicial se conserva
/// el APU por defecto.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormEditarMatrizTipoInicialTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void Constructor_NuevoConTipoInicialBasico_SeleccionaRbBasico()
    {
        EjecutarConEditor(TipoMatriz.Basico, form =>
        {
            Assert.IsTrue(Rb(form, "rbBasico").Checked, "El editor de un Básico debe iniciar en rbBasico.");
            Assert.IsFalse(Rb(form, "rbAPU").Checked, "rbAPU no debe quedar marcado al crear un Básico.");
            Assert.IsFalse(Rb(form, "rbCuadrilla").Checked, "rbCuadrilla no debe quedar marcado al crear un Básico.");

            Assert.AreEqual("Editor de Básicos", form.Text,
                "La identidad visual debe corresponder al tipo Básico.");
        });
    }

    [TestMethod]
    public void Constructor_NuevoConTipoInicialCuadrilla_SeleccionaRbCuadrilla()
    {
        EjecutarConEditor(TipoMatriz.Cuadrilla, form =>
        {
            Assert.IsTrue(Rb(form, "rbCuadrilla").Checked,
                "El editor de una Cuadrilla (camino existente) debe iniciar en rbCuadrilla.");
            Assert.IsFalse(Rb(form, "rbAPU").Checked, "rbAPU no debe quedar marcado al crear una Cuadrilla.");
            Assert.IsFalse(Rb(form, "rbBasico").Checked, "rbBasico no debe quedar marcado al crear una Cuadrilla.");

            Assert.AreEqual("Editor de Cuadrillas", form.Text,
                "La identidad visual debe corresponder al tipo Cuadrilla.");
            Assert.AreEqual("jor", Campo<ComboBox>(form, "cboUnidad").Text,
                "La Cuadrilla sugiere la unidad 'jor'.");
        });
    }

    [TestMethod]
    public void Constructor_NuevoSinTipoInicial_ConservaApuPorDefecto()
    {
        EjecutarConEditor(tipoInicial: null, form =>
        {
            Assert.IsTrue(Rb(form, "rbAPU").Checked, "Sin tipo inicial el editor debe quedar en APU.");
            Assert.IsFalse(Rb(form, "rbBasico").Checked, "Sin tipo inicial no debe marcarse rbBasico.");
            Assert.IsFalse(Rb(form, "rbCuadrilla").Checked, "Sin tipo inicial no debe marcarse rbCuadrilla.");

            Assert.AreEqual("Editor de Matriz (APU)", form.Text,
                "La identidad visual por defecto debe seguir siendo APU.");
            Assert.AreEqual(string.Empty, Campo<ComboBox>(form, "cboUnidad").Text,
                "El APU no debe imponer una unidad sugerida.");
        });
    }

    [TestMethod]
    public void Constructor_NuevoConTipoInicialApu_SeleccionaRbApu()
    {
        EjecutarConEditor(TipoMatriz.APU, form =>
        {
            Assert.IsTrue(Rb(form, "rbAPU").Checked, "El tipo APU explícito debe iniciar en rbAPU.");
            Assert.IsFalse(Rb(form, "rbBasico").Checked, "rbBasico no debe quedar marcado con tipo APU.");
            Assert.IsFalse(Rb(form, "rbCuadrilla").Checked, "rbCuadrilla no debe quedar marcado con tipo APU.");
        });
    }

    private static void EjecutarConEditor(TipoMatriz? tipoInicial, Action<FormEditarMatriz> verificar)
    {
        using DirectorioTemporal temp = new DirectorioTemporal("editar_matriz_tipo");
        using SOPROContext ctx = CrearContexto(temp.DbPath);
        Proyecto proyecto = AgregarProyecto(ctx);
        Proyecto? proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            using FormEditarMatriz form = new FormEditarMatriz(ctx, proyecto.Id, tipoInicial: tipoInicial);
            verificar(form);
        }
        finally
        {
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
        }

        Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");
    }

    private static RadioButton Rb(Form form, string nombre) => Campo<RadioButton>(form, nombre);

    private static T Campo<T>(Form form, string nombre) where T : class
        => form.GetType()
               .GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(form) as T
           ?? throw new InvalidOperationException($"No se encontró el campo '{nombre}'.");

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");

    private static SOPROContext CrearContexto(string dbPath)
    {
        SqliteConnection.ClearAllPools();
        SOPROContext ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        return ctx;
    }

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        Proyecto proyecto = new Proyecto
        {
            Nombre = "Proyecto tipo inicial matriz",
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
            for (int intento = 0; intento < 5; intento++)
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
