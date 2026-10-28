using System;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Aislamiento del constructor de <see cref="FormPresupuesto"/>: con el seam
/// <see cref="ProjectWorkspaceService"/> todas las rutas de datos locales
/// (índice de proyectos, uso de matrices, búsqueda de catálogo y estado del
/// panel de workspace) viven en el workspace inyectado. Las pruebas solo usan
/// rutas temporales positivas: no enumeran ni leen las carpetas reales del
/// usuario. Además verifica que los eventos estáticos a los que se suscribe el
/// form se liberan al descartarlo (sin depender de <c>Close</c>) y que el
/// proyecto global de <see cref="FormatoHelper"/> se restaura exactamente.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija estado estático (FormatoHelper); evita interacciones.
public class FormPresupuestoWorkspaceIsolationTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    /// <summary>
    /// Eventos ESTÁTICOS a los que se suscribe el constructor. El nombre del
    /// campo de respaldo coincide con el del evento (evento tipo "field-like").
    /// </summary>
    private static readonly (Type Tipo, string Evento)[] EventosEstaticos =
    {
        (typeof(FormatoHelper), nameof(FormatoHelper.ConfiguracionCambiada)),
        (typeof(FormPorcentajes), nameof(FormPorcentajes.PorcentajesActualizados)),
        (typeof(FormIndirectos), nameof(FormIndirectos.IndirectosTransferidos)),
        (typeof(FormFinanciamiento), nameof(FormFinanciamiento.FinanciamientoTransferido)),
        (typeof(FormUtilidad), nameof(FormUtilidad.UtilidadTransferida)),
        (typeof(FormDatosProyecto), nameof(FormDatosProyecto.DecimalesActualizados)),
        (typeof(FormCatalogoMateriales), nameof(FormCatalogoMateriales.InsumosModificados)),
        (typeof(FormCatalogoManoObra), nameof(FormCatalogoManoObra.InsumosModificados)),
        (typeof(FormCatalogoHerramientas), nameof(FormCatalogoHerramientas.InsumosModificados)),
        (typeof(FormCatalogoMaquinaria), nameof(FormCatalogoMaquinaria.InsumosModificados)),
        (typeof(FormEditarMatriz), nameof(FormEditarMatriz.MatrizGuardada)),
    };

    /// <summary>
    /// Construye y descarta el form con un SQLite temporal y un workspace
    /// temporal: todas las rutas deben quedar bajo el workspace inyectado, no
    /// debe quedar ningún manejador estático apuntando al form y el directorio
    /// temporal debe poder eliminarse (sin prompts modales: si el constructor
    /// mostrara un <c>MessageBox</c> la prueba se quedaría bloqueada).
    /// El proyecto global de <see cref="FormatoHelper"/> se captura antes y se
    /// restaura exactamente al salir, sin disparar eventos.
    /// </summary>
    [TestMethod]
    public void ConstructorConWorkspaceTemporal_UsaSoloRutasTemporalesYLiberaEventosEstaticos()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_workspace_{Guid.NewGuid():N}");
        var soproFolder = Path.Combine(root, "sopro");
        var localDataFolder = Path.Combine(root, "local");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();
            var proyecto = AgregarProyecto(ctx);
            var proyectoId = proyecto.Id;

            var workspace = new ProjectWorkspaceService(soproFolder, localDataFolder);

            form = new FormPresupuesto(ctx, proyecto, workspace);

            Assert.IsFalse(form.IsDisposed);
            Assert.IsNotNull(form.GridPrincipal, "El grid del presupuesto debe quedar inicializado.");
            Assert.IsTrue(form.GridPrincipal.Columns.Count > 0,
                "El constructor debe crear las columnas predeterminadas del presupuesto.");

            // Índice de proyectos: se escribe en el LocalDataFolder del workspace inyectado.
            var indexPath = Path.Combine(localDataFolder, "selector-project-index.json");
            Assert.IsTrue(File.Exists(indexPath),
                $"El índice de proyectos debe vivir en el workspace temporal: {indexPath}");
            StringAssert.StartsWith(indexPath, root);

            // Estado del panel de workspace: misma raíz (antes estaba hardcodeado a %LOCALAPPDATA%).
            Assert.AreEqual(
                Path.Combine(localDataFolder, $"presupuesto_workspace_{proyectoId}.txt"),
                form.WorkspacePanelStatePath,
                "El estado del panel debe resolverse desde el LocalDataFolder del workspace.");
            StringAssert.StartsWith(form.WorkspacePanelStatePath, localDataFolder);
            Assert.IsTrue(Directory.Exists(localDataFolder),
                "LoadWorkspacePanelState debe crear la carpeta del workspace temporal.");

            // El constructor fija el proyecto global de formato con el proyecto abierto...
            var proyectoGlobalEnForm = LeerProyectoGlobalFormato();
            Assert.IsNotNull(proyectoGlobalEnForm);
            Assert.AreEqual(proyectoId, proyectoGlobalEnForm!.Id,
                "El constructor establece el proyecto del form en FormatoHelper.");

            // Mientras vive, el form está suscrito a los eventos estáticos...
            Assert.AreEqual(EventosEstaticos.Length, ContarHandlersEstaticosConTarget(form),
                "El constructor debe suscribir los eventos estáticos esperados.");

            // ...y al descartarlo NO debe quedar retenido por ningún evento estático.
            form.Dispose();
            Assert.IsTrue(form.IsDisposed);
            Assert.AreEqual(0, ContarHandlersEstaticosConTarget(form),
                "Dispose debe liberar todas las suscripciones a eventos estáticos.");

            // La base temporal sigue siendo válida después de descartar el form.
            Assert.IsTrue(ctx.Proyectos.Any(p => p.Id == proyectoId));
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();

            // Restaura EXACTAMENTE el valor previo, sin disparar ConfiguracionCambiada.
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }

        Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");

        // "Poder limpiar": el directorio temporal ya no existe.
        Assert.IsFalse(Directory.Exists(root), "El workspace temporal debe poder eliminarse.");
    }

    /// <summary>
    /// El workspace por defecto (sin inyección) conserva EXACTAMENTE la ruta
    /// histórica del estado del panel. Se comprueba por código/propiedad, sin
    /// construir el form con el ctor público (escribiría en las carpetas del
    /// usuario) y sin enumerar el HOME.
    /// </summary>
    [TestMethod]
    public void WorkspacePorDefecto_ConservaLaRutaHistoricaDelEstadoDelPanel()
    {
        var workspacePorDefecto = new ProjectWorkspaceService();

        Assert.AreEqual(WorkspacePaths.LocalDataFolder, workspacePorDefecto.LocalDataFolder,
            "El workspace por defecto debe seguir apuntando a %LOCALAPPDATA%\\SOPRO.");

        const int proyectoId = 42;
        var rutaHistorica = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "SOPRO",
            $"presupuesto_workspace_{proyectoId}.txt");
        var rutaConSeam = Path.Combine(
            workspacePorDefecto.LocalDataFolder,
            $"presupuesto_workspace_{proyectoId}.txt");

        Assert.AreEqual(rutaHistorica, rutaConSeam,
            "La ruta por defecto no debe ganar un segmento 'SOPRO' extra ni cambiar de raíz.");
    }

    /// <summary>
    /// El ctor interno rechaza el workspace nulo ANTES de tocar el proyecto
    /// global de formato: el helper conserva exactamente su valor previo.
    /// </summary>
    [TestMethod]
    public void ConstructorInterno_RechazaWorkspaceNulo_SinTocarElProyectoGlobalDeFormato()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_workspace_null_{Guid.NewGuid():N}");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);

        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            using var contexto = new SOPROContext(dbPath);
            contexto.Database.EnsureCreated();
            var proyecto = AgregarProyecto(contexto);

            Assert.ThrowsExactly<ArgumentNullException>(
                () => new FormPresupuesto(contexto, proyecto, null!));

            Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
                "El ctor debe fallar antes de FormatoHelper.EstablecerProyecto.");
        }
        finally
        {
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }

        Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto aislamiento workspace",
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

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               BindingFlags.Static | BindingFlags.NonPublic)
           ?? throw new InvalidOperationException(
               $"No se encontró el campo FormatoHelper.{CampoProyectoGlobalFormato}.");

    private static int ContarHandlersEstaticosConTarget(FormPresupuesto form)
        => ContarHandlers(handler => ReferenceEquals(handler.Target, form));

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
