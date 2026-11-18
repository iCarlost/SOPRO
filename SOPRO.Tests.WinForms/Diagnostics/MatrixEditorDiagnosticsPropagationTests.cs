using System;
using System.IO;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Issue #144: la composición real (<see cref="FormPrincipal"/> →
/// <see cref="FormProyecto"/> → <see cref="FormMatrices"/> →
/// <see cref="FormEditarMatriz"/>) propaga el sink/versión de diagnóstico hasta
/// el editor que ejecuta la guarda. Ninguna prueba abre modales: se ejercitan los
/// seams de composición y se verifica la instancia recibida.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class MatrixEditorDiagnosticsPropagationTests
{
    private const string VersionPrueba = "1.19.2";

    [TestMethod]
    public void CatalogoMatrices_DesdeProyecto_RecibeSinkYVersion()
    {
        EjecutarConContexto(ctx =>
        {
            var proyecto = AgregarProyecto(ctx);
            var sink = new InMemoryDiagnosticsSink();

            using var formProyecto = new FormProyecto(ctx, proyecto, sink, VersionPrueba);
            using var matrices = formProyecto.CrearCatalogoMatrices();

            Assert.AreSame(sink, matrices.DiagnosticsSink,
                "El catálogo de matrices debe recibir el sink de la sesión.");
            Assert.AreEqual(VersionPrueba, matrices.ProductVersion);
        });
    }

    [TestMethod]
    public void EditorMatriz_DesdeElCatalogo_RecibeSinkYVersionParaLaGuardaYElSelector()
    {
        EjecutarConContexto(ctx =>
        {
            var proyecto = AgregarProyecto(ctx);
            var sink = new InMemoryDiagnosticsSink();

            using var formProyecto = new FormProyecto(ctx, proyecto, sink, VersionPrueba);
            using var matrices = formProyecto.CrearCatalogoMatrices();
            using var editor = matrices.CrearEditorMatriz();

            Assert.AreSame(sink, editor.DiagnosticsSink,
                "El editor debe conservar el sink para la guarda y el selector de insumos.");
            Assert.AreEqual(VersionPrueba, editor.ProductVersion);
        });
    }

    [TestMethod]
    public void EditoresDesdeMenusContextualesDeCatalogosDeProyecto_RecibenSinkYVersion()
    {
        EjecutarConContexto(ctx =>
        {
            var proyecto = AgregarProyecto(ctx);
            var sink = new InMemoryDiagnosticsSink();

            using var formProyecto = new FormProyecto(ctx, proyecto, sink, VersionPrueba);
            using var materiales = formProyecto.CrearCatalogoMateriales();
            using var manoObra = formProyecto.CrearCatalogoManoObra();
            using var maquinaria = formProyecto.CrearCatalogoMaquinaria();
            using var herramientas = formProyecto.CrearCatalogoHerramientas();
            using var editorMaterial = materiales.CrearEditorMatriz();
            using var editorManoObra = manoObra.CrearEditorMatriz();
            using var editorMaquinaria = maquinaria.CrearEditorMatriz();
            using var editorHerramienta = herramientas.CrearEditorMatriz();

            foreach (var editor in new[] { editorMaterial, editorManoObra, editorMaquinaria, editorHerramienta })
            {
                Assert.AreSame(sink, editor.DiagnosticsSink);
                Assert.AreEqual(VersionPrueba, editor.ProductVersion);
            }
        });
    }

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto propagación diagnóstico",
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

    private static void EjecutarConContexto(Action<SOPROContext> prueba)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_matriz_diag_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);

        SOPROContext? contexto = null;
        try
        {
            contexto = new SOPROContext(Path.Combine(root, "proyecto.soproproj"));
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
                    Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
