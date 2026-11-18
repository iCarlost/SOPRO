using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Diagnostics;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Services;
using WinFormsApplication = System.Windows.Forms.Application;

namespace SOPRO.Tests.WinForms.Diagnostics;

/// <summary>
/// Barrido de seguridad async de WinForms (issue #151): ciclo de vida de la carga
/// del catálogo de materiales, guard compartido del editor de maquinaria y
/// verificación del updater de arranque. Los formularios no se muestran y ninguna
/// prueba abre diálogos modales ni usa red real.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los formularios WinForms requieren un hilo STA dedicado.
public class AsyncWinFormsSafetySweepTests
{
    private const string VersionPrueba = "1.0.0.0";

    private SynchronizationContext? _contextoAnterior;
    private readonly List<Exception> _excepcionesNoControladas = new();
    private readonly List<string> _rutasTemporales = new();

    [TestInitialize]
    public void InicializarContextoDeUi()
    {
        _contextoAnterior = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
        _excepcionesNoControladas.Clear();
    }

    [TestCleanup]
    public void Limpiar()
    {
        SynchronizationContext.SetSynchronizationContext(_contextoAnterior);

        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (string ruta in _rutasTemporales)
        {
            foreach (string archivo in new[] { ruta, ruta + "-wal", ruta + "-shm" })
            {
                try
                {
                    if (File.Exists(archivo)) File.Delete(archivo);
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

    // ═════════════════════════ Catálogo de materiales ═════════════════════════

    [TestMethod]
    public void CatalogoMateriales_AlCerrarCancelaLaCargaPendienteYNoTocaControles()
    {
        var handler = new ThreadExceptionEventHandler((_, args) => _excepcionesNoControladas.Add(args.Exception));
        WinFormsApplication.ThreadException += handler;

        try
        {
            using var context = CrearContexto();
            var factory = new FabricaDiferida();
            var form = new FormCatalogoMateriales(context, null, factory);

            try
            {
                form.RecargarCatalogo();

                Assert.IsTrue(factory.Iniciada.Task.IsCompleted,
                    "La carga debe alcanzar la fábrica antes de cerrar el formulario.");
                var cargaLock = (SemaphoreSlim)Campo(form, "_cargaLock");
                Assert.AreEqual(0, cargaLock.CurrentCount,
                    "La carga pendiente debe retener el candado de ESTE formulario.");

                form.Close();
                form.Dispose();

                BombearHasta(() => cargaLock.CurrentCount == 1);
                Assert.IsTrue(factory.Cancelada.Task.IsCompleted,
                    "Cerrar el formulario debe cancelar la carga pendiente.");
                Assert.AreEqual(0, _excepcionesNoControladas.Count,
                    "La continuación de una carga cancelada no debe tocar controles liberados.");
            }
            finally
            {
                form.Dispose();
            }
        }
        finally
        {
            WinFormsApplication.ThreadException -= handler;
        }
    }

    [TestMethod]
    public void CatalogoMateriales_NoExponeCargaAsyncVoidYElNucleoEsTask()
    {
        var tipo = typeof(FormCatalogoMateriales);

        Assert.IsNull(
            tipo.GetMethod(
                "CargarMateriales",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            "La carga no debe existir como método void/async void: el núcleo es CargarMaterialesAsync.");

        var nucleo = tipo.GetMethod("CargarMaterialesAsync", BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(nucleo, "El núcleo de carga debe ser CargarMaterialesAsync.");
        Assert.AreEqual(typeof(Task), nucleo!.ReturnType,
            "El núcleo debe devolver Task para que los handlers puedan await-earlo.");
        Assert.IsNotNull(
            nucleo.GetCustomAttribute<System.Runtime.CompilerServices.AsyncStateMachineAttribute>(),
            "El núcleo debe ser async Task, no un wrapper sincrónico.");
    }

    [TestMethod]
    public void CatalogoMateriales_NucleoAsync_CargaYCompletaComoTask()
    {
        using var context = CrearContexto();
        using var form = new FormCatalogoMateriales(context, null, new FabricaInmediata());

        Task carga = form.CargarMaterialesAsync();
        BombearHasta(carga);

        Assert.IsTrue(carga.IsCompletedSuccessfully);
        Assert.IsNotNull(form.GridPrincipal.DataSource,
            "El núcleo async debe completar la carga y enlazar el grid.");
        Assert.AreEqual(0, _excepcionesNoControladas.Count);
    }

    [TestMethod]
    public void CatalogosIndependientes_CarganEnParaleloSinSerializarse()
    {
        var handler = new ThreadExceptionEventHandler((_, args) => _excepcionesNoControladas.Add(args.Exception));
        WinFormsApplication.ThreadException += handler;

        try
        {
            using var context = CrearContexto();
            var factoryLenta = new FabricaDiferida();
            using var lento = new FormCatalogoMateriales(context, null, factoryLenta);
            using var rapido = new FormCatalogoMateriales(context, null, new FabricaInmediata());

            lento.RecargarCatalogo();
            Assert.IsTrue(factoryLenta.Iniciada.Task.IsCompleted);

            rapido.RecargarCatalogo();

            BombearHasta(() => rapido.GridPrincipal.DataSource is not null);
            Assert.IsNotNull(rapido.GridPrincipal.DataSource,
                "Un catálogo con carga lenta no debe serializar la carga de otro formulario.");
            Assert.AreEqual(0, _excepcionesNoControladas.Count);
        }
        finally
        {
            WinFormsApplication.ThreadException -= handler;
        }
    }

    // ═══════════════════════ Editor de maquinaria ═══════════════════════

    [TestMethod]
    public void FormEditarMaquinaria_GuardCompartido_BloqueaCalculoYGuardado()
    {
        using var context = CrearContexto();
        using var form = new FormEditarMaquinaria(context, null, CrearMaquinaria(calculado: true));

        var iniciar = MetodoPrivado(form, "IniciarOperacion");
        var finalizar = MetodoPrivado(form, "FinalizarOperacion");
        var btnGuardar = Campo<Button>(form, "btnGuardar");
        var btnCalcular = Campo<Button>(form, "btnCalcular");

        Assert.IsTrue((bool)iniciar.Invoke(form, null)!,
            "La primera operación debe tomar el guard.");
        Assert.IsFalse((bool)iniciar.Invoke(form, null)!,
            "El guard compartido debe rechazar una segunda operación concurrente.");
        Assert.IsFalse(btnGuardar.Enabled);
        Assert.IsFalse(btnCalcular.Enabled);

        // Ambos handlers usan el mismo guard: con una operación en curso no deben
        // validar, persistir ni abrir el diálogo de cálculo.
        MetodoPrivado(form, "btnGuardar_Click").Invoke(form, new object?[] { form, EventArgs.Empty });
        MetodoPrivado(form, "btnCalcular_Click").Invoke(form, new object?[] { form, EventArgs.Empty });

        Assert.IsTrue((bool)Campo(form, "_operacionEnCurso"),
            "Invitar guardar/calcular no debe liberar el guard de la operación en curso.");
        Assert.IsFalse(btnGuardar.Enabled);

        finalizar.Invoke(form, null);

        Assert.IsFalse((bool)Campo(form, "_operacionEnCurso"));
        Assert.IsTrue(btnGuardar.Enabled, "El finally debe rehabilitar Guardar en un formulario vivo.");
        Assert.IsTrue(btnCalcular.Enabled,
            "El finally debe restaurar Calcular según el modo de costo vigente.");
    }

    [TestMethod]
    public void FormEditarMaquinaria_GuardCompartido_RespetaModoManualAlFinalizar()
    {
        using var context = CrearContexto();
        using var form = new FormEditarMaquinaria(context, null, CrearMaquinaria(calculado: false));

        var iniciar = MetodoPrivado(form, "IniciarOperacion");
        var finalizar = MetodoPrivado(form, "FinalizarOperacion");
        var btnCalcular = Campo<Button>(form, "btnCalcular");

        Assert.IsTrue((bool)iniciar.Invoke(form, null)!);
        finalizar.Invoke(form, null);

        Assert.IsFalse(btnCalcular.Enabled,
            "En modo manual el botón Calcular debe seguir deshabilitado tras liberar el guard.");
    }

    [TestMethod]
    public void FormEditarMaquinaria_FinalizarTrasDispose_NoTocaControles()
    {
        using var context = CrearContexto();
        var form = new FormEditarMaquinaria(context, null, CrearMaquinaria(calculado: true));

        var iniciar = MetodoPrivado(form, "IniciarOperacion");
        var finalizar = MetodoPrivado(form, "FinalizarOperacion");

        Assert.IsTrue((bool)iniciar.Invoke(form, null)!);
        form.Dispose();

        finalizar.Invoke(form, null);

        Assert.IsTrue(form.IsDisposed);
        Assert.IsFalse((bool)Campo(form, "_operacionEnCurso"));
    }

    // ═════════════════════════ Updater de arranque ═════════════════════════

    [TestMethod]
    public async Task Updater_ConCheckerQueFalla_NoPropagaYRegistraCorrelacionValida()
    {
        var raiz = Path.Combine(
            Path.GetTempPath(),
            "sopro-async-sweep-updater",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(raiz);

        try
        {
            var workspace = new ProjectWorkspaceService(
                Path.Combine(raiz, "sopro"),
                Path.Combine(raiz, "local"));
            var sink = new InMemoryDiagnosticsSink();

            using var form = new FormPrincipal(
                workspace,
                new ProjectLifecycleService(workspace),
                initialProjectPath: null,
                diagnosticsSink: sink,
                productVersion: VersionPrueba,
                updateChecker: (_, _) => throw new InvalidOperationException("fallo simulado"),
                updateConfirmation: (_, _) => false,
                installerLauncher: _ => { });

            await form.CheckForUpdatesOnStartupAsync();

            var eventos = sink.Snapshot();
            Assert.AreEqual(1, eventos.Count);
            Assert.AreEqual(DiagnosticsSeverity.Error, eventos[0].Severity);
            Assert.IsTrue(DiagnosticsCorrelationId.IsValid(eventos[0].CorrelationId),
                "La comprobación debe registrar una correlación válida aun al fallar.");
        }
        finally
        {
            try
            {
                Directory.Delete(raiz, recursive: true);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }

    // ═════════════════════════ Infraestructura ═════════════════════════

    private SOPROContext CrearContexto()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"sopro_async_sweep_{Guid.NewGuid():N}.db");
        _rutasTemporales.Add(dbPath);
        var context = new SOPROContext(dbPath);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    private static Maquinaria CrearMaquinaria(bool calculado)
        => new()
        {
            Clave = "MAQ-SWEEP",
            Descripcion = "Maquinaria de prueba",
            PotenciaNominal = 100m,
            TipoCombustible = TipoCombustible.Diesel,
            CostoHorario = calculado ? 500m : 250m,
            EsCostoCalculado = calculado,
            Origen = OrigenInsumo.Maestro,
            Notas = string.Empty,
        };

    private static MethodInfo MetodoPrivado(object instancia, string nombre)
        => instancia.GetType().GetMethod(nombre, BindingFlags.Instance | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException($"No existe el método '{nombre}'.");

    private static object Campo(object instancia, string nombre)
        => instancia.GetType().GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(instancia)
           ?? throw new InvalidOperationException($"No existe o no está inicializado el campo '{nombre}'.");

    private static T Campo<T>(object instancia, string nombre) where T : class
        => Campo(instancia, nombre) as T
           ?? throw new InvalidOperationException($"El campo '{nombre}' no es de tipo {typeof(T).Name}.");

    private static void BombearHasta(Func<bool> condicion, int milisegundos = 5000)
    {
        long limite = Environment.TickCount64 + milisegundos;
        while (!condicion() && Environment.TickCount64 < limite)
        {
            WinFormsApplication.DoEvents();
            Thread.Sleep(1);
        }

        Assert.IsTrue(condicion(), "La operación de UI no terminó dentro del límite.");
    }

    private static void BombearHasta(Task tarea, int milisegundos = 5000)
    {
        BombearHasta(() => tarea.IsCompleted, milisegundos);
        tarea.GetAwaiter().GetResult();
    }

    private sealed class FabricaInmediata : IProjectDbContextFactory
    {
        public SOPROContext Create(string databasePath) => new(databasePath);

        public Task<SOPROContext> CreateAsync(
            string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(new SOPROContext(databasePath));
    }

    /// <summary>
    /// Primera creación diferida: permite mantener una carga pendiente mientras se
    /// cierra el formulario y observar si la cancelación llegó a la fábrica.
    /// </summary>
    private sealed class FabricaDiferida : IProjectDbContextFactory
    {
        private readonly TaskCompletionSource<SOPROContext> _contexto =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Iniciada { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Cancelada { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SOPROContext Create(string databasePath)
            => CreateAsync(databasePath).GetAwaiter().GetResult();

        public Task<SOPROContext> CreateAsync(
            string databasePath, CancellationToken cancellationToken = default)
        {
            Iniciada.TrySetResult();
            cancellationToken.Register(() =>
            {
                Cancelada.TrySetResult();
                _contexto.TrySetCanceled(cancellationToken);
            });

            return _contexto.Task;
        }
    }
}
