using System;
using System.IO;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Recalculo;

[STATestClass]
[DoNotParallelize]
public class FormProyectoRefrescoPostRecalculoTests
{
    [TestMethod]
    public void Refresco_CuentaPestanasFallidasYContinuaConLasRestantes()
    {
        EjecutarConContexto(ctx =>
        {
            using FormProyecto form = new FormProyecto(ctx, CrearProyecto());
            TabControl tabControl = LeerCampo<TabControl>(form, "tabControl");

            FakeRecalculable fallida = new FakeRecalculable(lanzarAlRefrescar: true);
            FakeRecalculable correcta = new FakeRecalculable(lanzarAlRefrescar: false);
            tabControl.TabPages.Add(CrearPestana("Fallida", fallida));
            tabControl.TabPages.Add(CrearPestana("Correcta", correcta));

            RefrescoPostRecalculoResultado resultado = form.EjecutarRefrescoPostRecalculo();

            Assert.AreEqual(1, resultado.PestanasFallidas, "Debe contarse la pestaña que lanzó.");
            Assert.IsNull(resultado.Error, "Un fallo por pestaña no es un error del refresco completo.");
            Assert.IsTrue(resultado.TieneFallos);
            Assert.AreEqual(1, fallida.Refrescos, "La pestaña fallida se intenta una vez.");
            Assert.AreEqual(1, correcta.Refrescos, "Las pestañas restantes deben seguir refrescándose.");
        });
    }

    [TestMethod]
    public void Refresco_SinPestanasRecalculables_NoReportaFallos()
    {
        EjecutarConContexto(ctx =>
        {
            using FormProyecto form = new FormProyecto(ctx, CrearProyecto());
            TabControl tabControl = LeerCampo<TabControl>(form, "tabControl");
            tabControl.TabPages.Add(CrearPestana("Simple", new Form()));

            RefrescoPostRecalculoResultado resultado = form.EjecutarRefrescoPostRecalculo();

            Assert.AreEqual(0, resultado.PestanasFallidas);
            Assert.IsNull(resultado.Error);
            Assert.IsFalse(resultado.TieneFallos);
        });
    }

    [TestMethod]
    public void RestaurarFoco_SinHandle_NoLanza()
    {
        EjecutarConContexto(ctx =>
        {
            using FormProyecto form = new FormProyecto(ctx, CrearProyecto());
            MethodInfo metodo = typeof(FormProyecto).GetMethod(
                "RestaurarFocoTrasRecalculo",
                BindingFlags.Instance | BindingFlags.NonPublic)
                ?? throw new InvalidOperationException("No existe RestaurarFocoTrasRecalculo.");

            metodo.Invoke(form, new object?[] { null, null, null });
        });
    }

    private static Proyecto CrearProyecto() => new Proyecto
    {
        Id = 1,
        Nombre = "Proyecto de prueba",
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
    };

    private static TabPage CrearPestana(string titulo, Form contenido)
    {
        TabPage pestana = new TabPage(titulo);
        contenido.TopLevel = false;
        contenido.FormBorderStyle = FormBorderStyle.None;
        pestana.Controls.Add(contenido);
        return pestana;
    }

    private static T LeerCampo<T>(Form form, string nombre) where T : class
    {
        FieldInfo? campo = form.GetType().GetField(
            nombre,
            BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);

        if (campo == null)
            throw new InvalidOperationException($"No existe el campo '{nombre}' en {form.GetType().Name}.");

        return campo.GetValue(form) as T
            ?? throw new InvalidOperationException($"El campo '{nombre}' de {form.GetType().Name} no está inicializado.");
    }

    private static void EjecutarConContexto(Action<SOPROContext> prueba)
    {
        string root = Path.Combine(Path.GetTempPath(), $"sopro_recalculo_refresco_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        string dbPath = Path.Combine(root, "catalogo-temporal.db");

        SOPROContext? contexto = null;
        try
        {
            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();
            prueba(contexto);
        }
        finally
        {
            contexto?.Dispose();
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();

            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    private sealed class FakeRecalculable : Form, IRecalculable
    {
        private readonly bool _lanzarAlRefrescar;

        internal FakeRecalculable(bool lanzarAlRefrescar)
        {
            _lanzarAlRefrescar = lanzarAlRefrescar;
        }

        internal int Refrescos { get; private set; }

        public void RecalcularTodo()
        {
            Refrescos++;

            if (_lanzarAlRefrescar)
                throw new InvalidOperationException("fallo simulado de refresco");
        }
    }
}
