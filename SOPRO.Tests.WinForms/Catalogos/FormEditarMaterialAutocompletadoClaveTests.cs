using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Regresión del autocompletado por clave de <see cref="FormEditarMaterial"/>
/// (issue #127): la consulta anterior podía completar campos o abrir la
/// confirmación de duplicado después de cerrar el editor o de que el usuario
/// cambiara la clave. Los casos usan una fábrica de contextos cuya creación se
/// libera a voluntad para fijar el orden de finalización, y el seam de diálogos
/// por instancia evita cualquier MessageBox modal que colgaría el hilo STA.
/// </summary>
[STATestClass]
[DoNotParallelize] // WinForms requiere un hilo STA dedicado y el orden de los bombeos.
public class FormEditarMaterialAutocompletadoClaveTests
{
    private const string ClaveA = "MAT-A";
    private const string DescA = "Material A";
    private const string UnidadA = "kg";
    private const decimal PrecioA = 11.1111m;
    private const string NotasA = "notas A";

    private const string ClaveB = "MAT-B";
    private const string DescB = "Material B";
    private const string UnidadB = "m";
    private const decimal PrecioB = 22.2222m;
    private const string NotasB = "notas B";

    private const string ClaveEdit = "MAT-EDIT";
    private const string DescEdit = "Material en edición";
    private const string UnidadEdit = "pza";
    private const decimal PrecioEdit = 33.3333m;
    private const string NotasEdit = "notas edit";

    private SynchronizationContext? _contextoPrevio;
    private BombaSincronizacion? _bomba;

    [TestInitialize]
    public void GuardarContextoDePrueba() => _contextoPrevio = SynchronizationContext.Current;

    [TestCleanup]
    public void RestaurarContextoDePrueba()
    {
        _bomba = null;
        SynchronizationContext.SetSynchronizationContext(_contextoPrevio);
    }

    [TestMethod]
    public void ConsultaExitosaEnMaterialNuevo_RellenaCamposSinDialogos()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(escenario, null, fabrica);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");
            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);

            Assert.AreEqual(1, fabrica.Solicitudes.Count, "La salida de la clave debe iniciar una consulta.");
            fabrica.Solicitudes[0].Completar();

            BombearHasta(
                () => LeerCampo<TextBox>(formulario, "txtDescripcion").Text == DescA,
                "La respuesta exitosa no autocompletó la descripción.");

            Assert.AreEqual(UnidadA, LeerCampo<TextBox>(formulario, "txtUnidad").Text);
            Assert.AreEqual(PrecioA, LeerCampo<NumericUpDown>(formulario, "nudPrecio").Value);
            Assert.AreEqual(NotasA, LeerCampo<TextBox>(formulario, "txtNotas").Text);
            Assert.AreEqual(ClaveA, txtClave.Text.Trim(), "La clave capturada no debe alterarse.");
            Assert.AreEqual(0, dialogos.Count, "El autocompletado exitoso es silencioso.");
            Assert.IsFalse(formulario.IsDisposed);
        }
        finally
        {
            formulario.Dispose();
        }
    }

    [TestMethod]
    public void CerrarConConsultaPendiente_IgnoraRespuestaTardiaSinDialogos()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(escenario, null, fabrica);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");
            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);
            Assert.AreEqual(1, fabrica.Solicitudes.Count);

            Solicitud solicitud = fabrica.Solicitudes[0];
            formulario.Close();
            Assert.IsTrue(formulario.IsDisposed, "Cerrar el editor debe descartarlo.");

            // La consulta termina DESPUÉS del cierre: no debe tocar controles
            // dispuestos ni presentar mensajes.
            solicitud.Completar();
            BombearHasta(
                () => solicitud.Contexto!.Liberado.IsCompleted,
                "La consulta pendiente no terminó después de cerrar el editor.");
            ProcesarMensajes();

            Assert.AreEqual(0, dialogos.Count, "Una respuesta tardía tras el cierre no debe mostrar errores.");
            Assert.IsTrue(formulario.IsDisposed);
        }
        finally
        {
            formulario.Dispose();
        }
    }

    [TestMethod]
    public void CambioDeClaveConConsultaPendiente_IgnoraRespuestaVieja()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(escenario, null, fabrica);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");

            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);

            txtClave.Text = ClaveB;
            DispararLeave(formulario, txtClave);
            Assert.AreEqual(2, fabrica.Solicitudes.Count);

            // La consulta vigente (B) termina primero.
            fabrica.Solicitudes[1].Completar();
            BombearHasta(
                () => LeerCampo<TextBox>(formulario, "txtDescripcion").Text == DescB,
                "La consulta vigente no autocompletó el material B.");

            // La consulta vieja (A) termina después: no debe pisar los campos de B.
            fabrica.Solicitudes[0].Completar();
            BombearHasta(
                () => fabrica.Solicitudes[0].Contexto!.Liberado.IsCompleted,
                "La consulta vieja no terminó.");
            ProcesarMensajes();

            Assert.AreEqual(DescB, LeerCampo<TextBox>(formulario, "txtDescripcion").Text);
            Assert.AreEqual(UnidadB, LeerCampo<TextBox>(formulario, "txtUnidad").Text);
            Assert.AreEqual(PrecioB, LeerCampo<NumericUpDown>(formulario, "nudPrecio").Value);
            Assert.AreEqual(NotasB, LeerCampo<TextBox>(formulario, "txtNotas").Text);
            Assert.AreEqual(0, dialogos.Count, "La respuesta vieja no debe abrir confirmaciones.");
        }
        finally
        {
            formulario.Dispose();
        }
    }

    [TestMethod]
    public void SecuenciaABA_NoAceptaLaRespuestaViejaDeA()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(
            escenario, escenario.MaterialEnEdicion, fabrica);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");

            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);
            txtClave.Text = ClaveB;
            DispararLeave(formulario, txtClave);
            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);
            Assert.AreEqual(3, fabrica.Solicitudes.Count);

            // La consulta vigente (la segunda A) confirma el duplicado y se aplica.
            fabrica.Solicitudes[2].Completar();
            BombearHasta(
                () => LeerCampo<TextBox>(formulario, "txtDescripcion").Text == DescA && dialogos.Count == 1,
                "La consulta vigente no aplicó el material A.");
            Assert.AreEqual("Clave Duplicada - Confirmar Reemplazo", dialogos[0].Titulo);
            Assert.AreEqual(MessageBoxButtons.YesNo, dialogos[0].Botones);
            Assert.AreEqual(MessageBoxIcon.Question, dialogos[0].Icono);

            // La primera A termina después con la MISMA clave visible: sin
            // generación volvería a confirmar. Debe descartarse por obsoleta.
            fabrica.Solicitudes[0].Completar();
            BombearHasta(
                () => fabrica.Solicitudes[0].Contexto!.Liberado.IsCompleted,
                "La primera consulta A no terminó.");
            ProcesarMensajes();
            Assert.AreEqual(1, dialogos.Count, "La respuesta vieja de A no debe repetir la confirmación.");

            // La B intermedia también es obsoleta.
            fabrica.Solicitudes[1].Completar();
            BombearHasta(
                () => fabrica.Solicitudes[1].Contexto!.Liberado.IsCompleted,
                "La consulta B no terminó.");
            ProcesarMensajes();

            Assert.AreEqual(1, dialogos.Count);
            Assert.AreEqual(DescA, LeerCampo<TextBox>(formulario, "txtDescripcion").Text);
            Assert.AreEqual(ClaveA, txtClave.Text.Trim());
        }
        finally
        {
            formulario.Dispose();
        }
    }

    [TestMethod]
    public void ConfirmacionDeDuplicadoNo_RestauraLaClaveYNoAplicaCampos()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(
            escenario, escenario.MaterialEnEdicion, fabrica, DialogResult.No);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");
            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);
            fabrica.Solicitudes[0].Completar();

            BombearHasta(() => dialogos.Count == 1, "No se presentó la confirmación de duplicado.");
            ProcesarMensajes();

            Assert.AreEqual(DescEdit, LeerCampo<TextBox>(formulario, "txtDescripcion").Text);
            Assert.AreEqual(UnidadEdit, LeerCampo<TextBox>(formulario, "txtUnidad").Text);
            Assert.AreEqual(PrecioEdit, LeerCampo<NumericUpDown>(formulario, "nudPrecio").Value);
            Assert.AreEqual(NotasEdit, LeerCampo<TextBox>(formulario, "txtNotas").Text);
            Assert.AreEqual(ClaveEdit, txtClave.Text.Trim(), "Al rechazar se restaura la clave del material editado.");
        }
        finally
        {
            formulario.Dispose();
        }
    }

    [TestMethod]
    public void ResultadoFallidoDelLookup_SeReportaYElEditorSigueUtilizable()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(escenario, null, fabrica);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");

            fabrica.FallarSiguiente(new InvalidOperationException("fallo simulado de consulta"));
            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);

            BombearHasta(() => dialogos.Count == 1, "El fallo inesperado del lookup no se reportó.");
            Assert.AreEqual(MessageBoxIcon.Warning, dialogos[0].Icono);
            StringAssert.Contains(dialogos[0].Titulo, "Búsqueda");
            StringAssert.Contains(dialogos[0].Mensaje, "No se pudo buscar la clave");
            Assert.IsFalse(formulario.IsDisposed, "Un fallo recuperable no debe cerrar el editor.");
            Assert.IsTrue(LeerCampo<Button>(formulario, "btnGuardar").Enabled);

            // El editor sigue utilizable: una consulta posterior exitosa completa.
            txtClave.Text = ClaveB;
            DispararLeave(formulario, txtClave);
            Assert.AreEqual(1, fabrica.Solicitudes.Count);
            fabrica.Solicitudes[0].Completar();
            BombearHasta(
                () => LeerCampo<TextBox>(formulario, "txtDescripcion").Text == DescB,
                "El editor no pudo completar otra consulta después del fallo.");

            Assert.AreEqual(UnidadB, LeerCampo<TextBox>(formulario, "txtUnidad").Text);
            Assert.AreEqual(1, dialogos.Count, "La consulta posterior no debe añadir mensajes.");
        }
        finally
        {
            formulario.Dispose();
        }
    }

    [TestMethod]
    public void CancelacionDeLaConsulta_NoSeReportaComoFallo()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(escenario, null, fabrica);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");

            fabrica.FallarSiguiente(new OperationCanceledException("consulta cancelada"));
            txtClave.Text = ClaveA;
            DispararLeave(formulario, txtClave);
            ProcesarMensajes();

            Assert.AreEqual(0, dialogos.Count, "La cancelación esperada no debe mostrarse como error.");
            Assert.IsFalse(formulario.IsDisposed);
        }
        finally
        {
            formulario.Dispose();
        }
    }

    [TestMethod]
    public void ClaveNoEncontrada_PermaneceSilenciosa()
    {
        using Escenario escenario = Escenario.Crear();
        var fabrica = new FabricaDiferida();
        (FormEditarMaterial formulario, List<Dialogo> dialogos) = AbrirFormulario(escenario, null, fabrica);
        try
        {
            TextBox txtClave = LeerCampo<TextBox>(formulario, "txtClave");
            txtClave.Text = "NO-EXISTE";
            DispararLeave(formulario, txtClave);

            fabrica.Solicitudes[0].Completar();
            BombearHasta(
                () => fabrica.Solicitudes[0].Contexto!.Liberado.IsCompleted,
                "La consulta sin resultado no terminó.");
            ProcesarMensajes();

            Assert.AreEqual(0, dialogos.Count, "'No encontrado' es un éxito silencioso.");
            Assert.AreEqual(string.Empty, LeerCampo<TextBox>(formulario, "txtDescripcion").Text);
            Assert.AreEqual("NO-EXISTE", txtClave.Text.Trim());
            Assert.IsFalse(formulario.IsDisposed);
        }
        finally
        {
            formulario.Dispose();
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private (FormEditarMaterial Formulario, List<Dialogo> Dialogos) AbrirFormulario(
        Escenario escenario,
        MaterialListItem? material,
        FabricaDiferida fabrica,
        DialogResult respuestaDuplicado = DialogResult.Yes)
    {
        InstalarBombaDeContinuaciones();

        var dialogos = new List<Dialogo>();
        var formulario = new FormEditarMaterial(escenario.Sesion, material!, fabrica);
        formulario.MostrarDialogo = (mensaje, titulo, botones, icono) =>
        {
            var resultado = botones == MessageBoxButtons.YesNo ? respuestaDuplicado : DialogResult.OK;
            dialogos.Add(new Dialogo(mensaje, titulo, botones, icono, resultado));
            return resultado;
        };

        // Evita que el foco inicial del editor dispare una consulta antes de que
        // la prueba prepare la clave que quiere ejercitar.
        LeerCampo<TextBox>(formulario, "txtClave").Text = string.Empty;
        formulario.Show();
        ProcesarMensajes();

        Assert.AreEqual(0, fabrica.Solicitudes.Count, "Mostrar el editor no debe iniciar consultas por sí solo.");
        return (formulario, dialogos);
    }

    private static void DispararLeave(FormEditarMaterial formulario, TextBox txtClave)
    {
        MethodInfo? metodo = typeof(FormEditarMaterial).GetMethod(
            "txtClave_Leave",
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (metodo == null)
            throw new InvalidOperationException("No existe el manejador txtClave_Leave.");

        metodo.Invoke(formulario, new object?[] { txtClave, EventArgs.Empty });
    }

    /// <summary>
    /// Sustituye el contexto de sincronización por una cola que la prueba drena
    /// explícitamente. Así toda continuación de <c>async void</c> se ejecuta en el
    /// mismo hilo STA del test, sin depender de que un bucle de mensajes WinForms
    /// mantenga instalado su contexto.
    /// </summary>
    private void InstalarBombaDeContinuaciones()
    {
        _bomba = new BombaSincronizacion();
        SynchronizationContext.SetSynchronizationContext(_bomba);
    }

    private void BombearHasta(Func<bool> condicion, string mensaje, int milisegundos = 5000)
    {
        DateTime limite = DateTime.UtcNow.AddMilliseconds(milisegundos);
        while (!condicion())
        {
            if (DateTime.UtcNow >= limite)
                Assert.Fail(mensaje);

            bool proceso = _bomba?.Procesar() ?? false;
            System.Windows.Forms.Application.DoEvents();
            if (!proceso)
                Thread.Sleep(5);
        }
    }

    private void ProcesarMensajes(int vueltas = 10)
    {
        for (int i = 0; i < vueltas; i++)
        {
            _bomba?.Procesar();
            System.Windows.Forms.Application.DoEvents();
        }
    }

    private static T LeerCampo<T>(Form formulario, string nombre) where T : class
    {
        FieldInfo? campo = typeof(FormEditarMaterial).GetField(
            nombre,
            BindingFlags.Instance | BindingFlags.NonPublic);
        if (campo == null)
            throw new InvalidOperationException($"No existe el campo '{nombre}' en {nameof(FormEditarMaterial)}.");

        return campo.GetValue(formulario) as T
            ?? throw new InvalidOperationException($"El campo '{nombre}' no está inicializado.");
    }

    private sealed record Dialogo(
        string Mensaje,
        string Titulo,
        MessageBoxButtons Botones,
        MessageBoxIcon Icono,
        DialogResult Resultado);

    /// <summary>Cola de continuaciones que el test drena en el hilo STA.</summary>
    private sealed class BombaSincronizacion : SynchronizationContext
    {
        private readonly ConcurrentQueue<(SendOrPostCallback Callback, object? State)> _pendientes = new();

        public override void Post(SendOrPostCallback d, object? state)
            => _pendientes.Enqueue((d, state));

        public override void Send(SendOrPostCallback d, object? state)
        {
            if (SynchronizationContext.Current == this)
                d(state);
            else
                _pendientes.Enqueue((d, state));
        }

        public bool Procesar()
        {
            if (!_pendientes.TryDequeue(out var pendiente))
                return false;

            pendiente.Callback(pendiente.State);
            return true;
        }
    }

    private sealed class FabricaDiferida : IProjectDbContextFactory
    {
        private readonly List<Solicitud> _solicitudes = new();
        private readonly Queue<Exception> _fallos = new();

        public IReadOnlyList<Solicitud> Solicitudes => _solicitudes;

        public void FallarSiguiente(Exception error) => _fallos.Enqueue(error);

        public SOPROContext Create(string databasePath) => new(databasePath);

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
        {
            // El caso de uso convierte un fallo de fábrica en Result fallido; la
            // prueba usa este punto para forzar el camino visible de error.
            if (_fallos.Count > 0)
                throw _fallos.Dequeue();

            Solicitud solicitud = new(databasePath);
            _solicitudes.Add(solicitud);
            return solicitud.Tarea;
        }
    }

    private sealed class Solicitud
    {
        private readonly TaskCompletionSource<SOPROContext> _tcs =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public Solicitud(string databasePath) => DatabasePath = databasePath;

        public string DatabasePath { get; }

        public Task<SOPROContext> Tarea => _tcs.Task;

        public ContextoObservable? Contexto { get; private set; }

        public void Completar()
        {
            Contexto = new ContextoObservable(DatabasePath);
            if (!_tcs.TrySetResult(Contexto))
                throw new InvalidOperationException("La solicitud diferida ya se había completado.");
        }
    }

    /// <summary>Contexto que avisa cuándo el caso de uso terminó de liberarlo.</summary>
    private sealed class ContextoObservable : SOPROContext
    {
        private readonly TaskCompletionSource _liberado =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public ContextoObservable(string databasePath) : base(databasePath)
        {
        }

        public Task Liberado => _liberado.Task;

        public override void Dispose()
        {
            base.Dispose();
            _liberado.TrySetResult();
        }

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            _liberado.TrySetResult();
        }
    }

    private sealed class Escenario : IDisposable
    {
        private Escenario(string root, string dbPath, ProjectSessionInfo sesion, int idEdit)
        {
            Root = root;
            DbPath = dbPath;
            Sesion = sesion;
            IdMaterialEdit = idEdit;
        }

        public string Root { get; }

        public string DbPath { get; }

        public ProjectSessionInfo Sesion { get; }

        public int IdMaterialEdit { get; }

        public MaterialListItem MaterialEnEdicion =>
            new(IdMaterialEdit, ClaveEdit, DescEdit, UnidadEdit, PrecioEdit, NotasEdit, OrigenInsumo.Proyecto);

        public static Escenario Crear()
        {
            string root = Path.Combine(Path.GetTempPath(), $"sopro_mat_auto_{Guid.NewGuid():N}");
            Directory.CreateDirectory(root);
            string dbPath = Path.Combine(root, "proyecto-temporal.soproproj");

            using var contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureDeleted();
            contexto.Database.EnsureCreated();

            Proyecto proyecto = AgregarProyecto(contexto);
            contexto.SaveChanges();

            AgregarMaterial(contexto, proyecto.Id, ClaveA, DescA, UnidadA, PrecioA, NotasA);
            AgregarMaterial(contexto, proyecto.Id, ClaveB, DescB, UnidadB, PrecioB, NotasB);
            Material materialEdit = AgregarMaterial(
                contexto, proyecto.Id, ClaveEdit, DescEdit, UnidadEdit, PrecioEdit, NotasEdit);
            contexto.SaveChanges();

            ProjectSessionInfo sesion = LegacySessionBridge.FromLegacy(contexto, proyecto.Id);
            return new Escenario(root, dbPath, sesion, materialEdit.Id);
        }

        public void Dispose()
        {
            for (int intento = 0; intento < 5; intento++)
            {
                try
                {
                    Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                    if (Directory.Exists(Root))
                        Directory.Delete(Root, recursive: true);
                    return;
                }
                catch (IOException) when (intento < 4)
                {
                    Thread.Sleep(100);
                }
                catch (UnauthorizedAccessException) when (intento < 4)
                {
                    Thread.Sleep(100);
                }
            }
        }

        private static Proyecto AgregarProyecto(SOPROContext contexto)
        {
            var proyecto = new Proyecto
            {
                Nombre = "Proyecto autocompletado por clave",
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
            contexto.Proyectos.Add(proyecto);
            return proyecto;
        }

        private static Material AgregarMaterial(
            SOPROContext contexto,
            int proyectoId,
            string clave,
            string descripcion,
            string unidad,
            decimal precio,
            string notas)
        {
            var material = new Material
            {
                ProyectoId = proyectoId,
                Clave = clave,
                Descripcion = descripcion,
                Unidad = unidad,
                PrecioUnitario = precio,
                Notas = notas,
                Origen = OrigenInsumo.Proyecto,
            };
            contexto.Materiales.Add(material);
            return material;
        }
    }
}
