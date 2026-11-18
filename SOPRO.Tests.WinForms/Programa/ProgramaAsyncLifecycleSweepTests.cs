using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using WinFormsApplication = System.Windows.Forms.Application;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Issue #151 (lane A): ciclo de vida async de <see cref="FormProgramaObra"/>.
/// Cerrar la vista durante la fase asíncrona de una edición y un fallo SÍNCRONO en la
/// captura previa al guardado no pueden escapar al bucle de mensajes, ni tocar
/// controles liberados, ni dejar la ventana ocupada. Los escenarios usan el
/// formulario REAL sobre SQLite temporal, sin diálogos modales.
/// </summary>
[STATestClass]
[DoNotParallelize] // Formulario real WinForms: requiere un hilo STA dedicado.
public class ProgramaAsyncLifecycleSweepTests
{
    private SynchronizationContext? _contextoAnterior;
    private ThreadExceptionEventHandler? _manejadorExcepciones;
    private readonly List<Exception> _excepcionesNoControladas = new();
    private readonly List<string> _rutasTemporales = new();

    [TestInitialize]
    public void Inicializar()
    {
        _contextoAnterior = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _excepcionesNoControladas.Clear();

        // Sin este manejador, una excepción de una frontera async void abriría el
        // diálogo de error no controlado en el hilo STA de la prueba.
        _manejadorExcepciones = (_, args) => _excepcionesNoControladas.Add(args.Exception);
        WinFormsApplication.ThreadException += _manejadorExcepciones;
    }

    [TestCleanup]
    public void Limpiar()
    {
        if (_manejadorExcepciones is not null)
            WinFormsApplication.ThreadException -= _manejadorExcepciones;

        SynchronizationContext.SetSynchronizationContext(_contextoAnterior);

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (string ruta in _rutasTemporales)
        {
            foreach (string archivo in new[] { ruta, ruta + "-wal", ruta + "-shm" })
            {
                try
                {
                    if (File.Exists(archivo))
                        File.Delete(archivo);
                }
                catch (IOException)
                {
                }
                catch (UnauthorizedAccessException)
                {
                }
            }
        }

        _rutasTemporales.Clear();
    }

    /// <summary>
    /// Cerrar la vista mientras una edición de actividad está en su fase asíncrona:
    /// la continuación reanuda con el formulario liberado y NO debe acceder a sus
    /// controles ni escapar como excepción. El guardado ya confirmado se conserva.
    /// </summary>
    [TestMethod]
    public void CierreDuranteElAwaitDeLaEdicionDeActividad_NoEscapaNiTocaLaUiLiberada()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        var mensajes = new List<string>();
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            form.Show();
            WinFormsApplication.DoEvents();

            var actividad = PrimeraActividadHoja(form);
            var fila = BuscarFila(form, actividad.Id);

            Assert.IsTrue(
                form.IniciarEdicionDuracionDiasActividad(fila, 5),
                "La edición debe llegar a su fase asíncrona.");
            Assert.IsTrue(
                form.OperacionProgramaEnCurso,
                "Precondición: el guardado quedó esperando la fase asíncrona.");

            form.Close();
            Assert.IsTrue(form.IsDisposed,
                "Cerrar la vista la libera mientras el guardado sigue en curso.");

            BombearHasta(() => !form.OperacionProgramaEnCurso);

            Assert.AreEqual(0, _excepcionesNoControladas.Count,
                "La continuación no puede escapar al bucle de mensajes ni tocar controles liberados. "
                    + DescribirExcepciones());
            Assert.AreEqual(0, mensajes.Count,
                "El cierre durante un guardado en curso no debe producir mensajes.");

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(5,
                verif.ActividadesProgramadas.Single(a => a.Id == actividad.Id).DuracionDiasHabiles,
                "El guardado ya confirmado se conserva: el cierre no revierte ni repite la escritura.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
        }
    }

    /// <summary>
    /// Cerrar la vista mientras el guardado de dependencias está en su fase asíncrona:
    /// el núcleo Task termina sin fallar y sin tocar controles liberados ni estado de
    /// ocupado visible.
    /// </summary>
    [TestMethod]
    public void CierreDuranteElAwaitDelGuardadoDeDependencias_NoEscapaNiTocaLaUiLiberada()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            form.Show();
            WinFormsApplication.DoEvents();

            SeleccionarActividad(form, PrimeraActividadHoja(form).Id);

            Task guardado = form.GuardarDependenciasActividadAsync();
            Assert.IsTrue(form.OperacionProgramaEnCurso,
                "Precondición: el guardado de dependencias quedó en su fase asíncrona.");

            form.Close();
            Assert.IsTrue(form.IsDisposed,
                "Cerrar la vista la libera mientras el guardado sigue en curso.");

            BombearHasta(() => guardado.IsCompleted && !form.OperacionProgramaEnCurso);

            Assert.IsFalse(guardado.IsFaulted,
                "El cierre no puede hacer fallar el núcleo Task: " + guardado.Exception);
            guardado.GetAwaiter().GetResult();
            Assert.AreEqual(0, _excepcionesNoControladas.Count,
                "La continuación no puede escapar ni tocar la UI liberada. " + DescribirExcepciones());
            Assert.AreEqual(0, mensajes.Count,
                "El cierre durante un guardado en curso no debe producir mensajes.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
        }
    }

    /// <summary>
    /// Guardar dependencias con la vista YA cerrada: la captura previa (selección,
    /// proyección y estado del grid) se ejecuta como no-op dentro del try, sin tocar
    /// controles liberados, sin dejar ocupación y sin escribir nada.
    /// </summary>
    [TestMethod]
    public void CapturaDeDependenciasConLaVistaCerrada_EsNoOpSinEscapeNiEscritura()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        var mensajes = new List<string>();
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            form.Show();
            WinFormsApplication.DoEvents();

            var actividad = PrimeraActividadHoja(form);
            SeleccionarActividad(form, actividad.Id);

            form.Close();
            Assert.IsTrue(form.IsDisposed, "Precondición: la vista está cerrada.");

            // La frontera async void real del evento también debe cerrarse sin tocar la
            // UI liberada (ahora su guard de vida y todos sus returns viven bajo try).
            var frontera = typeof(FormProgramaObra).GetMethod(
                "dgvDependencias_CellEndEdit", BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.IsNotNull(frontera, "Debe existir la frontera del evento de dependencias.");
            frontera!.Invoke(form, new object?[] { null, new DataGridViewCellEventArgs(0, 0) });
            WinFormsApplication.DoEvents();

            Task guardado = form.GuardarDependenciasActividadAsync();

            Assert.IsTrue(guardado.IsCompletedSuccessfully,
                "La captura de una vista cerrada no puede hacer fallar el núcleo. "
                    + guardado.Exception);
            guardado.GetAwaiter().GetResult();

            Assert.IsFalse(form.OperacionProgramaEnCurso,
                "Una captura descartada no debe dejar la ventana ocupada.");
            Assert.IsFalse(form.ProgramaOperacionIncierta,
                "Sin intento de escritura no hay resultado incierto que reconciliar.");
            Assert.AreEqual(0, _excepcionesNoControladas.Count, DescribirExcepciones());
            Assert.AreEqual(0, mensajes.Count,
                "Una captura descartada no debe informar nada al usuario.");

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(0,
                verif.DependenciasActividad.Count(d => d.ActividadDestinoId == actividad.Id),
                "No debe persistirse ninguna dependencia a partir de una captura descartada.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
        }
    }

    /// <summary>
    /// Editar una actividad con la vista ya cerrada: la captura de fila/columna/estado
    /// ocurre dentro del try y termina como no-op, sin escapar ni escribir.
    /// </summary>
    [TestMethod]
    public void CapturaDeEdicionDeActividadConLaVistaCerrada_EsNoOpSinEscapeNiEscritura()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            var mensajes = new List<string>();
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            form.Show();
            WinFormsApplication.DoEvents();

            var actividad = PrimeraActividadHoja(form);
            var duracionOriginal = actividad.DuracionDiasHabiles;

            form.Close();
            Assert.IsTrue(form.IsDisposed, "Precondición: la vista está cerrada.");

            Task edicion = form.ProcesarFinEdicionActividadAsync(0, 0);

            Assert.IsTrue(edicion.IsCompletedSuccessfully,
                "La captura de una vista cerrada no puede hacer fallar el núcleo. "
                    + edicion.Exception);
            edicion.GetAwaiter().GetResult();

            Assert.IsFalse(form.OperacionProgramaEnCurso,
                "Una captura descartada no debe dejar la ventana ocupada.");
            Assert.AreEqual(0, _excepcionesNoControladas.Count, DescribirExcepciones());
            Assert.AreEqual(0, mensajes.Count,
                "Una captura descartada no debe informar nada al usuario.");

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(duracionOriginal,
                verif.ActividadesProgramadas.Single(a => a.Id == actividad.Id).DuracionDiasHabiles,
                "Sin captura válida no se escribe nada en la base.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
        }
    }

    /// <summary>
    /// Fallo SÍNCRONO del guardado de una actividad (el contexto CORTO no se puede
    /// abrir) antes de cualquier await: el error handler del callee no escapa, informa
    /// el fallo (nunca éxito) y la vista se reconcilia en sólo lectura.
    /// </summary>
    [TestMethod]
    public void FalloSincronoDelGuardadoDeActividad_NoEscapaYNoInformaExito()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        var mensajes = new List<string>();
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            // Creación 1 = carga inicial; la 2ª (contexto del guardado) falla.
            var factory = new FalloAlCrearContextoDeGuardadoFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            form.Show();
            WinFormsApplication.DoEvents();

            var actividad = PrimeraActividadHoja(form);
            var duracionOriginal = actividad.DuracionDiasHabiles;

            Assert.IsTrue(
                form.IniciarEdicionDuracionDiasActividad(BuscarFila(form, actividad.Id), 5),
                "La edición debe iniciar su guardado (que fallará de forma síncrona).");
            BombearHasta(() => !form.OperacionProgramaEnCurso);

            Assert.AreEqual(0, _excepcionesNoControladas.Count,
                "El error handler no puede escapar al bucle de mensajes. " + DescribirExcepciones());
            Assert.IsTrue(
                mensajes.Any(m => m.Contains("No se pudo guardar la actividad", StringComparison.Ordinal)),
                "El usuario debe ver que el guardado no se pudo confirmar. Mensajes: "
                    + string.Join(" | ", mensajes));
            Assert.IsFalse(
                mensajes.Any(m => m.Contains("actualizada.", StringComparison.OrdinalIgnoreCase)),
                "Un fallo de guardado nunca informa éxito.");
            Assert.IsFalse(form.GridPrincipal.ReadOnly,
                "La vista se reconcilia y vuelve a ser operable, sin quedar a medio bloquear.");

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(duracionOriginal,
                verif.ActividadesProgramadas.Single(a => a.Id == actividad.Id).DuracionDiasHabiles,
                "El contexto que no se pudo abrir no escribió nada.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
        }
    }

    /// <summary>
    /// Fallo SÍNCRONO del guardado de dependencias (el contexto CORTO no se puede
    /// abrir): mismo contrato que el de actividades — sin escape, sin mensaje de éxito
    /// y vista reconciliada en sólo lectura.
    /// </summary>
    [TestMethod]
    public void FalloSincronoDelGuardadoDeDependencias_NoEscapaYNoInformaExito()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        var mensajes = new List<string>();
        try
        {
            ctx = CrearBase(dbPath, out var proyecto);
            GenerarPrograma(dbPath, proyecto.Id);

            var factory = new FalloAlCrearContextoDeGuardadoFactory(dbPath);
            form = new FormProgramaObra(ctx, proyecto, factory);
            form.Notificador.Mostrar = (mensaje, _) => mensajes.Add(mensaje);
            form.Show();
            WinFormsApplication.DoEvents();

            var actividad = PrimeraActividadHoja(form);
            SeleccionarActividad(form, actividad.Id);

            Task guardado = form.GuardarDependenciasActividadAsync();

            Assert.IsTrue(guardado.IsCompletedSuccessfully,
                "El fallo del guardado se clasifica dentro del núcleo, no escapa. "
                    + guardado.Exception);
            guardado.GetAwaiter().GetResult();

            Assert.IsFalse(form.OperacionProgramaEnCurso,
                "El finally debe liberar la ocupación local.");
            Assert.AreEqual(0, _excepcionesNoControladas.Count, DescribirExcepciones());
            Assert.IsTrue(
                mensajes.Any(m => m.Contains("No se pudieron guardar las dependencias", StringComparison.Ordinal)),
                "El usuario debe ver que el guardado no se pudo confirmar. Mensajes: "
                    + string.Join(" | ", mensajes));
            Assert.IsFalse(
                mensajes.Any(m => m.Contains("actualizadas.", StringComparison.OrdinalIgnoreCase)),
                "Un fallo de guardado nunca informa éxito.");
            Assert.IsFalse(form.GridPrincipal.ReadOnly,
                "La vista se reconcilia y vuelve a ser operable.");

            using var verif = new SOPROContext(dbPath);
            Assert.AreEqual(0,
                verif.DependenciasActividad.Count(d => d.ActividadDestinoId == actividad.Id),
                "El contexto que no se pudo abrir no escribió dependencias.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
        }
    }

    /// <summary>
    /// Los guardados son núcleos <c>async Task</c>: no queda ningún helper
    /// <c>async void</c> sin observación. Las fronteras async void de los eventos son
    /// las únicas permitidas por WinForms.
    /// </summary>
    [TestMethod]
    public void NucleosDeGuardado_DevuelvenTaskYNoQuedaHelperAsyncVoid()
    {
        var tipo = typeof(FormProgramaObra);

        Assert.IsNull(
            tipo.GetMethod("GuardarDependenciasActuales", BindingFlags.Instance | BindingFlags.NonPublic),
            "El helper async void sin observación no debe existir.");

        var dependencias = tipo.GetMethod(
            "GuardarDependenciasActualesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(dependencias, "El guardado de dependencias debe tener un núcleo Task.");
        Assert.AreEqual(typeof(Task), dependencias!.ReturnType,
            "El núcleo de dependencias debe devolver Task.");
        Assert.IsNotNull(
            dependencias.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>(),
            "El núcleo de dependencias debe ser async Task, no un wrapper sincrónico.");

        var actividades = tipo.GetMethod(
            "ProcesarFinEdicionActividadAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(actividades, "La edición de actividades debe exponer su núcleo Task.");
        Assert.AreEqual(typeof(Task), actividades!.ReturnType,
            "El núcleo de actividades debe devolver Task.");
    }

    // ═══════════════════════════ Infraestructura ═══════════════════════════

    private static ActivityGridRowDto PrimeraActividadHoja(FormProgramaObra form)
        => form.ProgramaActual!.Actividades
            .Where(a => !a.EsResumen)
            .OrderBy(a => a.Orden)
            .ThenBy(a => a.Id)
            .First();

    private static void SeleccionarActividad(FormProgramaObra form, int actividadId)
    {
        var fila = BuscarFila(form, actividadId);
        Assert.IsTrue(fila >= 0, "La actividad debe estar publicada en el grid.");

        var grid = form.GridPrincipal;
        grid.Rows[fila].Selected = true;
        grid.CurrentCell = grid.Rows[fila].Cells["colClave"];
        WinFormsApplication.DoEvents();
        Assert.AreEqual(actividadId,
            (grid.Rows[fila].DataBoundItem as ActivityGridRowDto)!.Id,
            "La selección debe apuntar a la actividad indicada.");
    }

    private static int BuscarFila(FormProgramaObra form, int actividadId)
    {
        for (var fila = 0; fila < form.GridPrincipal.Rows.Count; fila++)
        {
            if (form.GridPrincipal.Rows[fila].DataBoundItem is ActivityGridRowDto dto && dto.Id == actividadId)
                return fila;
        }

        return -1;
    }

    private string DescribirExcepciones()
        => _excepcionesNoControladas.Count == 0
            ? "Sin excepciones."
            : "Excepciones: " + string.Join(" | ", _excepcionesNoControladas.Select(e => e.Message));

    private static void BombearHasta(Func<bool> condicion, int milisegundos = 10000)
    {
        long limite = Environment.TickCount64 + milisegundos;
        while (!condicion() && Environment.TickCount64 < limite)
        {
            WinFormsApplication.DoEvents();
            Thread.Sleep(1);
        }

        Assert.IsTrue(condicion(), "La operación de UI no terminó dentro del límite.");
    }

    private string CrearRuta()
    {
        var ruta = Path.Combine(Path.GetTempPath(), $"sopro_po_async_{Guid.NewGuid():N}.db");
        _rutasTemporales.Add(ruta);
        return ruta;
    }

    private static SOPROContext CrearBase(string dbPath, out Proyecto proyecto)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        proyecto = new Proyecto
        {
            Nombre = "Proyecto async lifecycle",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 5),
            FechaTermino = new DateTime(2026, 2, 27),
            PlazoEjecucion = 40,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        ctx.ConceptosPresupuesto.AddRange(
            CrearConcepto(proyecto.Id, "C-01", 10m, 100m, 1),
            CrearConcepto(proyecto.Id, "C-02", 5m, 200m, 2));
        ctx.SaveChanges();

        return ctx;
    }

    private static ConceptoPresupuesto CrearConcepto(
        int proyectoId, string clave, decimal cantidad, decimal precio, int orden)
        => new()
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = $"Concepto {clave}",
            Unidad = "m2",
            EsAgrupador = false,
            Nivel = 1,
            Orden = orden,
            Cantidad = cantidad,
            CostoDirectoUnitario = precio,
            CostoDirectoTotal = cantidad * precio,
            PrecioUnitario = precio,
            ImporteTotal = cantidad * precio,
        };

    private static void GenerarPrograma(string dbPath, int proyectoId)
    {
        var result = new ProgramacionSynchronizationService().GenerateProgram(
            new ProjectDbContextFactory(),
            dbPath,
            proyectoId,
            expectedActiveProgramaId: null,
            new DateTime(2026, 1, 5),
            TipoPeriodoPrograma.Semana);

        Assert.AreEqual(BudgetOperationStatus.Committed, result.Status, result.Diagnostic);
        Assert.IsNotNull(result.ProgramaObraId);
    }

    /// <summary>
    /// Falla al crear el contexto del guardado de una edición: la 1ª creación es la
    /// carga inicial del formulario y la 2ª el contexto CORTO del guardado.
    /// </summary>
    private sealed class FalloAlCrearContextoDeGuardadoFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private int _createCount;

        public FalloAlCrearContextoDeGuardadoFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath)
        {
            var numero = Interlocked.Increment(ref _createCount);
            if (numero == 2)
                throw new InvalidOperationException("contexto_de_guardado_simulado_fallido");

            return new SOPROContext(_dbPath);
        }

        public Task<SOPROContext> CreateAsync(
            string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }
}
