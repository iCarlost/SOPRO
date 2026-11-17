using System;
using System.IO;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Propaga el sink/versión de diagnóstico desde la composición existente
/// (<see cref="FormProyecto"/> → <see cref="FormCatalogoManoObra"/> →
/// <see cref="FormEditarManoObra"/>) hasta el cálculo de FSR. Ninguna prueba
/// abre modales: los editores se construyen y se ejercita su ruta productiva.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class FsrEditorDiagnosticsTests
{
    private const string VersionPrueba = "1.11.0";

    [TestMethod]
    public void EditorManoObra_FsrInvalidoPropagadoDesdeProyecto_RegistraErrorSinModal()
    {
        EjecutarConContexto(ctx =>
        {
            var proyecto = AgregarProyecto(ctx, """{ json invalido """);
            var sink = new InMemoryDiagnosticsSink();

            using var formProyecto = new FormProyecto(ctx, proyecto, sink, VersionPrueba);
            using var catalogo = formProyecto.CrearCatalogoManoObra();
            using var editor = catalogo.CrearEditorManoObra(CrearManoDeObra(proyecto.Id));

            var evento = sink.Snapshot().Single();
            Assert.AreEqual(DiagnosticsSeverity.Error, evento.Severity);
            Assert.AreEqual("SOPRO.Application", evento.Source);
            Assert.AreEqual("CalcularFactorSalarioReal", evento.Operation);
            Assert.AreEqual(VersionPrueba, evento.ProductVersion);
            Assert.IsTrue(DiagnosticsCorrelationId.IsValid(evento.CorrelationId));
            Assert.AreEqual(typeof(System.Text.Json.JsonException).FullName, evento.ExceptionType);
        });
    }

    [TestMethod]
    public void EditorManoObra_FsrValido_NoRegistraEventos()
    {
        EjecutarConContexto(ctx =>
        {
            var proyecto = AgregarProyecto(ctx, ParametrosValidos);
            var sink = new InMemoryDiagnosticsSink();

            using var formProyecto = new FormProyecto(ctx, proyecto, sink, VersionPrueba);
            using var catalogo = formProyecto.CrearCatalogoManoObra();
            using var editor = catalogo.CrearEditorManoObra(CrearManoDeObra(proyecto.Id));

            Assert.AreEqual(0, sink.Count,
                "Un FSR calculado correctamente no es una incidencia.");
        });
    }

    [TestMethod]
    public void SelectorInsumo_DesdeElCatalogo_RecibeSinkYVersionParaElEditor()
    {
        EjecutarConContexto(ctx =>
        {
            var proyecto = AgregarProyecto(ctx, ParametrosValidos);
            var sink = new InMemoryDiagnosticsSink();

            using var selector = new FormSeleccionarInsumo(
                ctx,
                proyecto.Id,
                TipoComponenteMatriz.ManoDeObra,
                modoCatalogo: true,
                diagnosticsSink: sink,
                productVersion: VersionPrueba);

            Assert.AreSame(sink, selector.DiagnosticsSink,
                "El selector debe conservar el sink que le entrega el catálogo para su editor de mano de obra.");
            Assert.AreEqual(VersionPrueba, selector.ProductVersion);
        });
    }

    private const string ParametrosValidos = """
    {
      "SalarioMinimo":"248.93",
      "Jornada":"0",
      "Semestre":"0",
      "Anio":"2026",
      "HorasJornada":"8",
      "DiasCalendario":"365",
      "DiasAguinaldo":"15",
      "DiasVacaciones":"12",
      "PrimaVacacional":"25",
      "DiasDescanso":"52",
      "DiasFestivos":"7",
      "PctGuarderias":"1",
      "PctRetiro":"2",
      "PctRiesgos":"4.58875",
      "PctINFONAVIT":"5",
      "PctNomina":"2.4"
    }
    """;

    private static ManoDeObra CrearManoDeObra(int proyectoId) => new()
    {
        Id = 7,
        ProyectoId = proyectoId,
        Clave = "MO-FSR",
        Descripcion = "Mano de obra con FSR",
        Unidad = "jor",
        SalarioBase = 500m,
        FactorSalarioReal = 1m,
        SalarioReal = 500m,
        Notas = string.Empty,
    };

    private static Proyecto AgregarProyecto(SOPROContext ctx, string parametrosFSR)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto FSR diagnóstico",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            ParametrosFSR = parametrosFSR,
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
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fsr_diag_{Guid.NewGuid():N}");
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
