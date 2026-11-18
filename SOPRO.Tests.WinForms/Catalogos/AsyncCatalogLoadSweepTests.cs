using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// Regresión #151 (lane B): las cargas de los catálogos de mano de obra y
/// maquinaria comparten el <c>DbContext</c> de la sesión. Una solicitud nueva
/// invalida la anterior (generación por formulario) y espera a que la consulta
/// en curso libere el turno, porque cancelar el token no detiene a EF. Todo
/// resultado obsoleto se descarta y un formulario ya cerrado no publica UI ni
/// muestra un modal de error.
/// </summary>
[STATestClass]
[DoNotParallelize] // Forms WinForms y contexto de sincronización sobre un hilo STA.
public class AsyncCatalogLoadSweepTests
{
    /// <summary>
    /// Contexto WinForms de la prueba. <c>Application.DoEvents</c> lo sustituye por
    /// uno genérico que despacha al pool, así que el bombeo lo reinstala para que
    /// toda continuación de <c>await</c> siga en el hilo STA del formulario.
    /// </summary>
    private SynchronizationContext? _contextoUi;

    [TestMethod]
    public void CargaSuperadaCompletadaDespuesDeLaNueva_SeDescartaYPublicaSoloLaVigente()
    {
        ConContexto((contexto, proyecto) =>
        {
            using var form = new FormCatalogoManoObra(contexto, proyecto.Id);
            _ = form.Handle;

            var primera = NuevaCarga<ManoDeObra>();
            var segunda = NuevaCarga<ManoDeObra>();
            int consultas = 0;
            form.CargadorManoDeObraOverride = _ =>
                Interlocked.Increment(ref consultas) == 1 ? primera.Task : segunda.Task;

            var cargaVieja = form.CargarManoDeObraAsync();
            var cargaNueva = form.CargarManoDeObraAsync();

            Assert.AreEqual(
                1,
                Volatile.Read(ref consultas),
                "La segunda consulta no debe iniciarse antes de que la primera libere el turno.");

            // La primera solicitud ya quedó superada: su resultado diferido debe
            // descartarse aunque sea el primero en completarse.
            primera.SetResult(new List<ManoDeObra> { CrearManoDeObra(1, "MO-VIEJA") });
            BombearHasta(
                () => Volatile.Read(ref consultas) == 2,
                "La consulta vigente debe arrancar cuando la anterior libera el turno.");

            segunda.SetResult(new List<ManoDeObra> { CrearManoDeObra(2, "MO-NUEVA") });
            BombearHasta(
                () => cargaVieja.IsCompleted && cargaNueva.IsCompleted,
                "Ambas cargas diferidas deben terminar tras resolverse sus consultas.");

            Assert.IsFalse(cargaVieja.Result, "El resultado superado no debe publicarse.");
            Assert.IsTrue(cargaNueva.Result, "El resultado vigente sí debe publicarse.");
            Assert.AreEqual(1, form.GridPrincipal.Rows.Count);
            Assert.AreEqual(
                "MO-NUEVA",
                ((ManoDeObra)form.GridPrincipal.Rows[0].DataBoundItem).Clave,
                "El grid debe mostrar el resultado de la última solicitud.");
            Assert.AreEqual("1 registro(s) encontrado(s)", LeerEstado(form));
        });
    }

    [TestMethod]
    public void CierreConCargaPendiente_NoPublicaNiMuestraModalAunqueLaConsultaFalle()
    {
        ConContexto((contexto, proyecto) =>
        {
            var form = new FormCatalogoMaquinaria(contexto, proyecto.Id);
            _ = form.Handle;

            var pendiente = NuevaCarga<Maquinaria>();
            form.CargadorMaquinariaOverride = _ => pendiente.Task;

            var errores = new List<string>();
            form.NotificarErrorCarga = (mensaje, _) => errores.Add(mensaje);

            var carga = form.CargarMaquinariaAsync();
            form.Dispose();

            // La consulta termina DESPUÉS del dispose y falla: ni modal ni UI.
            pendiente.SetException(new InvalidOperationException("fallo posterior al cierre"));
            BombearHasta(() => carga.IsCompleted, "La carga pendiente debe terminar aun con el form cerrado.");

            Assert.IsFalse(carga.Result, "Una carga de un formulario cerrado no debe publicar.");
            Assert.AreEqual(0, errores.Count, "Un formulario descartado no debe mostrar modal de error.");
        });
    }

    [TestMethod]
    public void ErrorConFormularioVivo_InformaUnaVezYLaSiguienteCargaSePublica()
    {
        ConContexto((contexto, proyecto) =>
        {
            using var form = new FormCatalogoMaquinaria(contexto, proyecto.Id);
            _ = form.Handle;

            var fallida = NuevaCarga<Maquinaria>();
            form.CargadorMaquinariaOverride = _ => fallida.Task;

            var errores = new List<string>();
            form.NotificarErrorCarga = (mensaje, _) => errores.Add(mensaje);

            var cargaFallida = form.CargarMaquinariaAsync();
            fallida.SetException(new InvalidOperationException("consulta rota"));
            BombearHasta(() => cargaFallida.IsCompleted, "La carga con error debe terminar.");

            Assert.IsFalse(cargaFallida.Result);
            Assert.AreEqual(1, errores.Count, "El error vigente se informa una sola vez.");
            StringAssert.Contains(errores[0], "consulta rota");
            Assert.AreEqual("Error al cargar maquinaria", LeerEstado(form));
            Assert.IsTrue(LeerRefrescarHabilitado(form), "Tras el error el refresco debe quedar disponible.");

            var exitosa = NuevaCarga<Maquinaria>();
            form.CargadorMaquinariaOverride = _ => exitosa.Task;
            var recarga = form.CargarMaquinariaAsync();
            exitosa.SetResult(new List<Maquinaria> { CrearMaquinaria(7, "MQ-OK") });
            BombearHasta(() => recarga.IsCompleted, "La recarga debe completarse.");

            Assert.IsTrue(recarga.Result, "El CRUD/refresco feliz debe seguir publicando.");
            Assert.AreEqual(1, form.GridPrincipal.Rows.Count);
            Assert.AreEqual("MQ-OK", ((Maquinaria)form.GridPrincipal.Rows[0].DataBoundItem).Clave);
            Assert.AreEqual("1 registro(s) encontrado(s)", LeerEstado(form));
        });
    }

    [TestMethod]
    public void ObservadorDeCarga_AbsorbeFalloYaCompletadoYFalloDiferido()
    {
        // Rama síncrona: la tarea ya está fallida cuando la frontera la observa.
        var completada = Task.FromException(new InvalidOperationException("fallo previo"));
        FormCatalogoManoObra.ObservarCarga(completada);
        Assert.IsTrue(completada.IsFaulted);
        Assert.IsNotNull(completada.Exception);

        // Rama diferida: la falla llega después de registrar el observador.
        var diferida = NuevaCarga<ManoDeObra>();
        FormCatalogoMaquinaria.ObservarCarga(diferida.Task);
        diferida.SetException(new InvalidOperationException("fallo diferido"));
        BombearHasta(() => diferida.Task.IsFaulted, "La tarea diferida debe quedar fallida.");
        Assert.IsNotNull(diferida.Task.Exception);
    }

    [TestMethod]
    public void RecargaSolicitadaTrasCerrar_NoArrancaConsultaNiPublica()
    {
        ConContexto((contexto, proyecto) =>
        {
            var form = new FormCatalogoMaquinaria(contexto, proyecto.Id);
            _ = form.Handle;

            int consultas = 0;
            form.CargadorMaquinariaOverride = _ =>
            {
                Interlocked.Increment(ref consultas);
                return Task.FromResult(new List<Maquinaria>());
            };

            form.Dispose();
            form.RecargarCatalogo();
            form.RecalcularTodo();

            Assert.AreEqual(0, Volatile.Read(ref consultas),
                "Una frontera sobre un formulario cerrado no debe iniciar consultas.");
        });
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static TaskCompletionSource<List<T>> NuevaCarga<T>()
        => new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <summary>
    /// Bombea la cola de mensajes hasta que la condición se cumpla. El hilo de
    /// prueba instala un contexto WinForms para que las continuaciones de los
    /// <c>await</c> se despachen en el mismo hilo STA.
    /// <para>
    /// <c>Application.DoEvents</c> reemplaza el contexto ambiente por uno genérico
    /// que despacha al pool. Sin reinstalarlo, tras una continuación que falló la
    /// siguiente carga reanuda fuera del hilo del formulario y crea allí el handle
    /// del grid; al cerrar, <c>DestroyWindow</c> esperaría indefinidamente a ese
    /// hilo sin bucle de mensajes. Por eso se restaura antes de cada bombeo y al
    /// salir, de modo que el hilo STA siga siendo el único dueño de la UI.
    /// </para>
    /// </summary>
    private void BombearHasta(Func<bool> condicion, string mensaje)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (!condicion() && DateTime.UtcNow < limite)
        {
            RestaurarContextoUi();
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(5);
        }

        RestaurarContextoUi();
        Assert.IsTrue(condicion(), mensaje);
    }

    private void RestaurarContextoUi()
    {
        if (_contextoUi is null || ReferenceEquals(SynchronizationContext.Current, _contextoUi))
            return;

        SynchronizationContext.SetSynchronizationContext(_contextoUi);
    }

    private static string LeerEstado(Form form)
    {
        var campo = form.GetType().GetField(
            "lblStatus", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(campo, "No se encontró lblStatus en el formulario.");
        return ((ToolStripStatusLabel)campo.GetValue(form)!).Text ?? string.Empty;
    }

    private static bool LeerRefrescarHabilitado(Form form)
    {
        var campo = form.GetType().GetField(
            "btnRefrescar", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
        Assert.IsNotNull(campo, "No se encontró btnRefrescar en el formulario.");
        return ((ToolStripButton)campo.GetValue(form)!).Enabled;
    }

    private static ManoDeObra CrearManoDeObra(int id, string clave) => new()
    {
        Id = id,
        Clave = clave,
        Descripcion = clave,
        Unidad = "jor",
        SalarioBase = 100m,
        FactorSalarioReal = 1m,
        SalarioReal = 100m,
        Notas = string.Empty,
        Origen = OrigenInsumo.Proyecto,
    };

    private static Maquinaria CrearMaquinaria(int id, string clave) => new()
    {
        Id = id,
        Clave = clave,
        Descripcion = clave,
    };

    private void ConContexto(Action<SOPROContext, Proyecto> verificar)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_async_catalogo_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var contextoPrevio = SynchronizationContext.Current;
        SOPROContext? contexto = null;
        try
        {
            _contextoUi = new WindowsFormsSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(_contextoUi);

            contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureDeleted();
            contexto.Database.EnsureCreated();

            var proyecto = AgregarProyecto(contexto);
            verificar(contexto, proyecto);
        }
        finally
        {
            _contextoUi = null;
            SynchronizationContext.SetSynchronizationContext(contextoPrevio);
            contexto?.Dispose();

            // EF Core (SQLite) usa pooling de conexiones: libera el archivo antes de borrar.
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
            }
            catch (IOException)
            {
                // Mejor esfuerzo: Windows puede retener el WAL un instante.
            }
        }
    }

    private static Proyecto AgregarProyecto(SOPROContext contexto)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto cargas asíncronas",
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
        contexto.SaveChanges();
        return proyecto;
    }
}
