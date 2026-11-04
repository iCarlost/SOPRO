using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Ciclo de vida de <see cref="FormMatrices"/> cuando se embebe en las pestañas de
/// <see cref="FormProyecto"/> (<c>TopLevel=false</c>): el cierre del proyecto puede
/// descartarlo sin disparar <c>FormClosed</c>. Si las suscripciones a eventos
/// estáticos quedaran vivas, el manejador obsoleto conservaría el form y su
/// contexto ya liberado; una guarda de matriz en otro proyecto dispararía
/// <see cref="FormEditarMatriz.MatrizGuardada"/> y lanzaría
/// <c>ObjectDisposedException</c> dentro del guardado (falso "Error al guardar").
/// La prueba no abre <c>MessageBox</c>: solo construye, descarta y dispara el
/// evento estático con el contexto ya liberado.
/// </summary>
[STATestClass]
[DoNotParallelize] // Manipula eventos estáticos y contextos temporales.
public class FormMatricesLifecycleIsolationTests
{
    /// <summary>
    /// Eventos ESTÁTICOS a los que se suscribe el constructor (5 insumos + decimales
    /// + guarda de matriz). El nombre del campo de respaldo coincide con el evento.
    /// </summary>
    private static readonly (Type Tipo, string Evento)[] EventosEstaticos =
    {
        (typeof(FormDatosProyecto), nameof(FormDatosProyecto.DecimalesActualizados)),
        (typeof(FormCatalogoMateriales), nameof(FormCatalogoMateriales.InsumosModificados)),
        (typeof(FormCatalogoManoObra), nameof(FormCatalogoManoObra.InsumosModificados)),
        (typeof(FormCatalogoHerramientas), nameof(FormCatalogoHerramientas.InsumosModificados)),
        (typeof(FormCatalogoMaquinaria), nameof(FormCatalogoMaquinaria.InsumosModificados)),
        (typeof(FormEditarMatriz), nameof(FormEditarMatriz.MatrizGuardada)),
    };

    /// <summary>
    /// Descartar el form embebido sin <c>Close</c> libera TODAS sus suscripciones
    /// estáticas. Después de liberar su contexto, disparar <c>MatrizGuardada</c>
    /// (la ruta real de guardado de matrices) no debe invocar ningún manejador
    /// obsoleto ni lanzar <c>ObjectDisposedException</c>.
    /// </summary>
    [TestMethod]
    public void DisposeSinClose_LiberaSuscripcionesEstaticas_YNoDejaManejadorObsoleto()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fm_lifecycle_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        SOPROContext? ctx = null;
        FormMatrices? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
            var proyectoId = AgregarProyecto(ctx).Id;

            form = new FormMatrices(ctx, proyectoId, modoEmbebido: true);

            Assert.IsFalse(form.IsDisposed);
            Assert.IsNotNull(form.GridPrincipal, "El grid de matrices debe quedar inicializado.");
            Assert.AreEqual(EventosEstaticos.Length, ContarHandlersEstaticosConTarget(form),
                "El constructor debe suscribir los eventos estáticos esperados.");

            // Cierre del proyecto: el form embebido se descarta sin pasar por Close/OnFormClosed.
            form.Dispose();
            form.Dispose(); // idempotente: la limpieza no debe fallar al repetirse.

            Assert.IsTrue(form.IsDisposed);
            Assert.AreEqual(0, ContarHandlersEstaticosConTarget(form),
                "Dispose debe liberar todas las suscripciones a eventos estáticos.");

            // El contexto del proyecto anterior también se libera. Si quedara un
            // manejador obsoleto, disparar el evento tocaría _context y lanzaría
            // ObjectDisposedException, que el guardado real reportaría como error.
            ctx.Dispose();
            ctx = null;

            InvocarEventoEstatico(typeof(FormEditarMatriz), nameof(FormEditarMatriz.MatrizGuardada));
            Assert.AreEqual(0, ContarHandlersEstaticosConTarget(form),
                "El manejador obsoleto no debe reaparecer tras disparar el evento estático.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            EliminarDirectorio(root);
        }
    }

    /// <summary>
    /// La limpieza de una instancia descartada no debe desuscribir a otra instancia
    /// viva: cada form usa su propio delegado y el remover es por referencia.
    /// </summary>
    [TestMethod]
    public void DisposeDeUnaInstancia_NoLiberaLasSuscripcionesDeOtra()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fm_lifecycle_dos_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        SOPROContext? ctx = null;
        FormMatrices? formA = null;
        FormMatrices? formB = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
            var proyectoId = AgregarProyecto(ctx).Id;

            formA = new FormMatrices(ctx, proyectoId, modoEmbebido: true);
            formB = new FormMatrices(ctx, proyectoId, modoEmbebido: true);

            Assert.AreEqual(EventosEstaticos.Length, ContarHandlersEstaticosConTarget(formA));
            Assert.AreEqual(EventosEstaticos.Length, ContarHandlersEstaticosConTarget(formB));

            formA.Dispose();

            Assert.AreEqual(0, ContarHandlersEstaticosConTarget(formA),
                "La instancia descartada debe quedar sin suscripciones.");
            Assert.AreEqual(EventosEstaticos.Length, ContarHandlersEstaticosConTarget(formB),
                "La otra instancia viva debe conservar sus suscripciones.");
        }
        finally
        {
            formA?.Dispose();
            formB?.Dispose();
            ctx?.Dispose();
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto ciclo de vida matrices",
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
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    private static int ContarHandlersEstaticosConTarget(FormMatrices form)
        => ContarHandlers(handler => ReferenciaA(handler, form));

    private static int ContarHandlers(Func<Delegate, bool> predicado)
    {
        int total = 0;
        foreach (var (tipo, evento) in EventosEstaticos)
        {
            var campo = tipo.GetField(evento, BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(campo, $"No se encontró el campo de respaldo del evento {tipo.Name}.{evento}.");
            if (campo.GetValue(null) is not Delegate manejadores)
                continue;

            foreach (var manejador in manejadores.GetInvocationList())
                if (predicado(manejador)) total++;
        }

        return total;
    }

    private static void InvocarEventoEstatico(Type tipo, string evento)
    {
        var campo = tipo.GetField(evento, BindingFlags.Static | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException($"No se encontró el campo de respaldo del evento {tipo.Name}.{evento}.");
        if (campo.GetValue(null) is not Delegate manejadores)
            return;

        manejadores.DynamicInvoke(null, EventArgs.Empty);
    }

    /// <summary>
    /// Un delegado de instancia apunta al form directamente o a un closure
    /// generado por el compilador que lo captura.
    /// </summary>
    private static bool ReferenciaA(Delegate manejador, object objetivo)
    {
        var target = manejador.Target;
        if (target is null)
            return false;

        return ReferenceEquals(target, objetivo) || EsClosureSobre(target, objetivo);
    }

    private static bool EsClosureSobre(object closure, object objetivo)
    {
        if (!closure.GetType().IsDefined(typeof(CompilerGeneratedAttribute), inherit: false))
            return false;

        foreach (var campo in closure.GetType().GetFields(
                     BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            var valor = campo.GetValue(closure);
            if (valor is null) continue;
            if (ReferenceEquals(valor, objetivo)) return true;
            if (EsClosureSobre(valor, objetivo)) return true;
        }

        return false;
    }

    private static void EliminarDirectorio(string root)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
                // EF Core (SQLite) usa pooling de conexiones: libera el archivo antes de borrar.
                Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
                if (Directory.Exists(root))
                    Directory.Delete(root, recursive: true);
                return;
            }
            catch (IOException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
            catch (UnauthorizedAccessException) when (intento < 4)
            {
                System.Threading.Thread.Sleep(200);
            }
        }
    }
}
