using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Catalogos;

/// <summary>
/// "Dónde se usa" del catálogo de materiales: la consulta se resuelve dentro de
/// un menú contextual vivo. Se cubren resultados vacíos/poblados, fallo de
/// fábrica y de consulta, cancelación/cierre y la respuesta obsoleta tras una
/// reapertura rápida. No se abre ningún diálogo modal.
/// </summary>
[STATestClass]
[DoNotParallelize] // Los forms de WinForms requieren un hilo STA dedicado.
public class FormCatalogoMaterialesDondeSeUsaTests
{
    private SynchronizationContext? _contextoAnterior;
    private readonly List<string> _rutasTemporalesDb = new();

    [TestInitialize]
    public void InicializarContextoDeUi()
    {
        _contextoAnterior = SynchronizationContext.Current;
        SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
    }

    [TestCleanup]
    public void RestaurarContextoDeUi()
    {
        SynchronizationContext.SetSynchronizationContext(_contextoAnterior);

        // EF Core (SQLite) usa pooling: se liberan las conexiones antes de borrar
        // los archivos temporales de cada prueba.
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        foreach (string ruta in _rutasTemporalesDb)
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
            }
        }
        _rutasTemporalesDb.Clear();
    }

    [TestMethod]
    public void DondeSeUsa_SinUso_MuestraSubmenusVaciosDeshabilitados()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-SIN");
        using var form = new FormCatalogoMateriales(context, proyecto.Id, FactoryNormal());
        using var menu = new ContextMenuStrip();
        var itemDonde = CrearItemDonde(menu);

        Task tarea = form.CargarDondeSeUsaAsync(itemDonde, material.Id, CancellationToken.None);
        BombearHasta(tarea);

        Assert.AreEqual(2, itemDonde.DropDownItems.Count);
        var matrices = (ToolStripMenuItem)itemDonde.DropDownItems[0];
        var maquinaria = (ToolStripMenuItem)itemDonde.DropDownItems[1];
        Assert.AreEqual("Matrices", matrices.Text);
        Assert.AreEqual("Maquinaria (costo horario)", maquinaria.Text);
        Assert.AreEqual("(Sin uso en matrices)", matrices.DropDownItems[0].Text);
        Assert.IsFalse(matrices.DropDownItems[0].Enabled);
        Assert.AreEqual("(Sin vínculo con maquinaria)", maquinaria.DropDownItems[0].Text);
        Assert.IsFalse(maquinaria.DropDownItems[0].Enabled);
    }

    [TestMethod]
    public void DondeSeUsa_ConMatrizYMaquinaria_MuestraLasEtiquetasEsperadas()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-USO");
        AgregarUsoEnMatriz(context, material, "APU-A", "Matriz A");
        context.Maquinaria.Add(new Maquinaria
        {
            ProyectoId = proyecto.Id,
            Clave = "EQ-A",
            Descripcion = "Equipo A",
            Notas = string.Empty,
            MaterialCombustibleId = material.Id,
        });
        context.SaveChanges();

        using var form = new FormCatalogoMateriales(context, proyecto.Id, FactoryNormal());
        using var menu = new ContextMenuStrip();
        var itemDonde = CrearItemDonde(menu);

        Task tarea = form.CargarDondeSeUsaAsync(itemDonde, material.Id, CancellationToken.None);
        BombearHasta(tarea);

        var matrices = (ToolStripMenuItem)itemDonde.DropDownItems[0];
        var maquinaria = (ToolStripMenuItem)itemDonde.DropDownItems[1];
        Assert.AreEqual(1, matrices.DropDownItems.Count);
        Assert.AreEqual("APU-A — Matriz A", matrices.DropDownItems[0].Text);
        Assert.AreEqual(1, maquinaria.DropDownItems.Count);
        Assert.AreEqual("EQ-A — Equipo A · combustible", maquinaria.DropDownItems[0].Text);
        Assert.IsFalse(maquinaria.DropDownItems[0].Enabled);
    }

    [TestMethod]
    public void DondeSeUsa_FalloDeFabrica_MuestraSoloErrorGenericoDeshabilitado()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-FAB");
        var factory = new FactoryControlada((_, _) =>
            Task.FromException<SOPROContext>(new InvalidOperationException("detalle interno de fábrica")));
        using var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
        using var menu = new ContextMenuStrip();
        var itemDonde = CrearItemDonde(menu);

        Task tarea = form.CargarDondeSeUsaAsync(itemDonde, material.Id, CancellationToken.None);
        BombearHasta(tarea);

        Assert.AreEqual(1, itemDonde.DropDownItems.Count);
        ToolStripItem error = itemDonde.DropDownItems[0];
        Assert.AreEqual(FormCatalogoMateriales.MensajeErrorDondeSeUsa, error.Text);
        Assert.IsFalse(error.Enabled);
        Assert.IsFalse((error.Text ?? string.Empty).Contains("detalle interno de fábrica", StringComparison.Ordinal),
            "El detalle técnico no debe mostrarse en el menú.");
    }

    [TestMethod]
    public void DondeSeUsa_FalloDeConsulta_MuestraSoloErrorGenericoSinDetalleCrudo()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-CONSULTA");
        string archivoInvalido = Path.Combine(
            Path.GetTempPath(), $"sopro_no_sqlite_{Guid.NewGuid():N}.db");
        File.WriteAllText(archivoInvalido, "esto no es una base SQLite");
        try
        {
            var factory = new FactoryControlada((_, _) =>
                Task.FromResult<SOPROContext>(new SOPROContext(archivoInvalido)));
            using var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
            using var menu = new ContextMenuStrip();
            var itemDonde = CrearItemDonde(menu);

            Task tarea = form.CargarDondeSeUsaAsync(itemDonde, material.Id, CancellationToken.None);
            BombearHasta(tarea);

            Assert.AreEqual(1, itemDonde.DropDownItems.Count);
            Assert.AreEqual(FormCatalogoMateriales.MensajeErrorDondeSeUsa, itemDonde.DropDownItems[0].Text);
            Assert.IsFalse(itemDonde.DropDownItems[0].Enabled);
        }
        finally
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            try
            {
                File.Delete(archivoInvalido);
            }
            catch (IOException)
            {
            }
        }
    }

    [TestMethod]
    public void DondeSeUsa_CancelacionMientrasEspera_NoMuestraError()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-CANCEL");
        var factory = new FactoryPrimeraDiferida(context.DatabasePath);
        using var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
        using var menu = new ContextMenuStrip();
        var itemDonde = CrearItemDonde(menu);
        using var cts = new CancellationTokenSource();

        Task tarea = form.CargarDondeSeUsaAsync(itemDonde, material.Id, cts.Token);
        BombearHasta(() => factory.Iniciada.Task.IsCompleted);
        cts.Cancel();
        BombearHasta(tarea);

        Assert.AreEqual(1, itemDonde.DropDownItems.Count,
            "La cancelación/cierre no se presenta como fallo y conserva el placeholder.");
        Assert.AreEqual(FormCatalogoMateriales.TextoConsultandoUsos, itemDonde.DropDownItems[0].Text);
        Assert.IsFalse(itemDonde.DropDownItems[0].Enabled);
    }

    [TestMethod]
    public void DondeSeUsa_MenuReemplazadoAntesDelResultado_NoTocaItemsLiberados()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-MENU");
        var factory = new FactoryPrimeraDiferida(context.DatabasePath);
        using var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
        var menu = new ContextMenuStrip();
        var itemDonde = CrearItemDonde(menu);
        using var cts = new CancellationTokenSource();

        Task tarea = form.CargarDondeSeUsaAsync(itemDonde, material.Id, cts.Token);
        BombearHasta(() => factory.Iniciada.Task.IsCompleted);

        cts.Cancel();
        menu.Dispose();
        factory.Primera.TrySetResult(new SOPROContext(context.DatabasePath));
        BombearHasta(tarea);

        Assert.IsTrue(itemDonde.IsDisposed);
    }

    [TestMethod]
    public void DondeSeUsa_FormularioDispuestoAntesDelResultado_ConservaElPlaceholder()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-FORM");
        var factory = new FactoryPrimeraDiferida(context.DatabasePath);
        var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
        using var menu = new ContextMenuStrip();
        var itemDonde = CrearItemDonde(menu);

        Task tarea = form.CargarDondeSeUsaAsync(itemDonde, material.Id, CancellationToken.None);
        BombearHasta(() => factory.Iniciada.Task.IsCompleted);
        form.Dispose();
        factory.Primera.TrySetResult(new SOPROContext(context.DatabasePath));
        BombearHasta(tarea);

        Assert.AreEqual(1, itemDonde.DropDownItems.Count,
            "Un formulario dispuesto no debe seguir llenando el menú.");
        Assert.AreEqual(FormCatalogoMateriales.TextoConsultandoUsos, itemDonde.DropDownItems[0].Text);
        Assert.IsFalse(itemDonde.DropDownItems[0].Enabled);
    }

    [TestMethod]
    public void DondeSeUsa_ReaperturaRapida_IgnoraLaRespuestaAnterior()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var materialA = AgregarMaterial(context, proyecto.Id, "MAT-A");
        var materialB = AgregarMaterial(context, proyecto.Id, "MAT-B");
        AgregarUsoEnMatriz(context, materialA, "APU-A", "Matriz A");
        AgregarUsoEnMatriz(context, materialB, "APU-B", "Matriz B");
        var factory = new FactoryPrimeraDiferida(context.DatabasePath);
        using var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
        using var menu = new ContextMenuStrip();
        var itemDonde = CrearItemDonde(menu);

        int solicitud = 1;
        Task consultaA = form.CargarDondeSeUsaAsync(
            itemDonde, materialA.Id, CancellationToken.None, () => solicitud == 1);
        BombearHasta(() => factory.Iniciada.Task.IsCompleted);

        solicitud = 2;
        Task consultaB = form.CargarDondeSeUsaAsync(
            itemDonde, materialB.Id, CancellationToken.None, () => solicitud == 2);
        BombearHasta(consultaB);

        // La respuesta vieja (A) llega después de la reapertura y debe ignorarse.
        factory.Primera.TrySetResult(new SOPROContext(context.DatabasePath));
        BombearHasta(consultaA);

        var matrices = (ToolStripMenuItem)itemDonde.DropDownItems[0];
        Assert.AreEqual(1, matrices.DropDownItems.Count);
        Assert.AreEqual("APU-B — Matriz B", matrices.DropDownItems[0].Text,
            "Solo la solicitud vigente puede pintar el submenú.");
    }

    [TestMethod]
    public void MenuContextual_MuestraPlaceholderMientrasConsultaYCapturaElMaterial()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var materialA = AgregarMaterial(context, proyecto.Id, "MAT-CAP-A");
        var materialB = AgregarMaterial(context, proyecto.Id, "MAT-CAP-B");
        AgregarUsoEnMatriz(context, materialA, "APU-CAP-A", "Matriz capturada");
        AgregarUsoEnMatriz(context, materialB, "APU-CAP-B", "Matriz de la selección nueva");

        var factory = new FactoryDiferible();
        using var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
        form.Show();
        try
        {
            BombearHasta(() => form.GridPrincipal.Rows.Count > 0);
            form.GridPrincipal.DataSource = new List<MaterialListItem>
            {
                ARow(materialA, proyecto.Id),
                ARow(materialB, proyecto.Id),
            };
            form.GridPrincipal.ClearSelection();
            form.GridPrincipal.Rows[0].Cells[0].Selected = true;

            factory.DiferirSiguiente = true;
            InvocarClickDerecho(form, fila: 0, columna: 0);

            var menu = Campo<ContextMenuStrip>(form, "_menuContextual");
            var itemDonde = menu.Items.OfType<ToolStripMenuItem>()
                .First(i => i.Text?.Contains("Dónde se usa", StringComparison.Ordinal) == true);

            // Simula que la selección cambió a B antes de abrir el submenú:
            // el handler debe seguir consultando el Id capturado (A).
            EstablecerMaterialSeleccionado(
                form, (MaterialListItem)form.GridPrincipal.Rows[1].DataBoundItem!);

            itemDonde.ShowDropDown();
            BombearHasta(() => factory.Iniciada.Task.IsCompleted);

            Assert.IsTrue(itemDonde.DropDown.Visible,
                "El submenú debe abrirse aunque la consulta siga pendiente.");
            Assert.AreEqual(1, itemDonde.DropDownItems.Count);
            Assert.AreEqual(FormCatalogoMateriales.TextoConsultandoUsos,
                itemDonde.DropDownItems[0].Text);
            Assert.IsFalse(itemDonde.DropDownItems[0].Enabled);

            factory.Diferida.TrySetResult(new SOPROContext(context.DatabasePath));
            BombearHasta(() => itemDonde.DropDownItems.Count >= 2);

            var matrices = (ToolStripMenuItem)itemDonde.DropDownItems[0];
            Assert.AreEqual("APU-CAP-A — Matriz capturada", matrices.DropDownItems[0].Text,
                "El Id del material se captura al construir el menú, no se relee después del await.");
        }
        finally
        {
            form.Close();
        }
    }

    [TestMethod]
    public void MenuContextual_CierreDuranteConsulta_CancelaSinError()
    {
        using var context = CrearContexto();
        var proyecto = AgregarProyecto(context);
        var material = AgregarMaterial(context, proyecto.Id, "MAT-CIERRE");
        AgregarUsoEnMatriz(context, material, "APU-CIERRE", "Matriz que no debe mostrarse");

        var factory = new FactoryDiferible();
        using var form = new FormCatalogoMateriales(context, proyecto.Id, factory);
        form.Show();
        try
        {
            BombearHasta(() => form.GridPrincipal.Rows.Count > 0);
            form.GridPrincipal.DataSource = new List<MaterialListItem>
            {
                ARow(material, proyecto.Id),
            };
            form.GridPrincipal.ClearSelection();
            form.GridPrincipal.Rows[0].Cells[0].Selected = true;

            factory.DiferirSiguiente = true;
            InvocarClickDerecho(form, fila: 0, columna: 0);

            var menu = Campo<ContextMenuStrip>(form, "_menuContextual");
            var itemDonde = menu.Items.OfType<ToolStripMenuItem>()
                .First(i => i.Text?.Contains("Dónde se usa", StringComparison.Ordinal) == true);

            itemDonde.ShowDropDown();
            BombearHasta(() => factory.Iniciada.Task.IsCompleted);
            Assert.AreEqual(FormCatalogoMateriales.TextoConsultandoUsos,
                itemDonde.DropDownItems[0].Text);

            menu.Close();
            BombearHasta(() => factory.Diferida.Task.IsCompleted);
            BombearDurante(200);

            Assert.IsFalse(itemDonde.IsDisposed,
                "El cierre del menú no debe liberar el ítem antes de la continuación.");
            Assert.AreEqual(1, itemDonde.DropDownItems.Count);
            Assert.AreEqual(FormCatalogoMateriales.TextoConsultandoUsos,
                itemDonde.DropDownItems[0].Text);
            Assert.IsFalse(itemDonde.DropDownItems[0].Enabled);
            Assert.AreNotEqual(FormCatalogoMateriales.MensajeErrorDondeSeUsa,
                itemDonde.DropDownItems[0].Text,
                "Cerrar el menú no debe presentarse como fallo.");
        }
        finally
        {
            form.Close();
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private SOPROContext CrearContexto()
    {
        string dbPath = Path.Combine(Path.GetTempPath(), $"sopro_donde_{Guid.NewGuid():N}.db");
        _rutasTemporalesDb.Add(dbPath);
        var context = new SOPROContext(dbPath);
        context.Database.EnsureDeleted();
        context.Database.EnsureCreated();
        return context;
    }

    private static Proyecto AgregarProyecto(SOPROContext context)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto dónde se usa",
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
        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static Material AgregarMaterial(SOPROContext context, int proyectoId, string clave)
    {
        var material = new Material
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Material de prueba",
            Unidad = "kg",
            PrecioUnitario = 10m,
            Notas = string.Empty,
            Origen = OrigenInsumo.Proyecto,
        };
        context.Materiales.Add(material);
        context.SaveChanges();
        return material;
    }

    private static void AgregarUsoEnMatriz(
        SOPROContext context, Material material, string clave, string descripcion)
    {
        var matriz = new Matriz
        {
            ProyectoId = material.ProyectoId!.Value,
            Clave = clave,
            Descripcion = descripcion,
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty,
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();
        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty,
        });
        context.SaveChanges();
    }

    private static MaterialListItem ARow(Material material, int proyectoId)
        => new(
            material.Id,
            material.Clave,
            material.Descripcion,
            material.Unidad,
            material.PrecioUnitario,
            material.Notas,
            material.Origen,
            proyectoId);

    private static ToolStripMenuItem CrearItemDonde(ContextMenuStrip menu)
    {
        var item = new ToolStripMenuItem("Dónde se usa");
        menu.Items.Add(item);
        return item;
    }

    private static FactoryControlada FactoryNormal()
        => new((path, _) => Task.FromResult<SOPROContext>(new SOPROContext(path)));

    private static void InvocarClickDerecho(FormCatalogoMateriales form, int fila, int columna)
    {
        var metodo = typeof(FormCatalogoMateriales).GetMethod(
            "DgvMateriales_CellMouseDown", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró el handler del clic derecho.");
        metodo.Invoke(form, new object[]
        {
            form.GridPrincipal,
            new DataGridViewCellMouseEventArgs(
                columna, fila, 0, 0, new MouseEventArgs(MouseButtons.Right, 1, 0, 0, 0)),
        });
    }

    private static T Campo<T>(object instancia, string nombre) where T : class
        => instancia.GetType()
               .GetField(nombre, BindingFlags.Instance | BindingFlags.NonPublic)
               ?.GetValue(instancia) as T
           ?? throw new InvalidOperationException($"No se encontró el campo '{nombre}'.");

    private static void EstablecerMaterialSeleccionado(
        FormCatalogoMateriales form, MaterialListItem material)
    {
        var campo = typeof(FormCatalogoMateriales).GetField(
            "_materialSeleccionado", BindingFlags.Instance | BindingFlags.NonPublic)
            ?? throw new InvalidOperationException("No se encontró la selección de material.");
        campo.SetValue(form, material);
    }

    private static void BombearHasta(Task tarea, int milisegundos = 5000)
    {
        BombearHasta(() => tarea.IsCompleted, milisegundos);
        tarea.GetAwaiter().GetResult();
    }

    private static void BombearHasta(Func<bool> condicion, int milisegundos = 5000)
    {
        long limite = Environment.TickCount64 + milisegundos;
        while (!condicion() && Environment.TickCount64 < limite)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(1);
        }

        Assert.IsTrue(condicion(), "La operación de UI no terminó dentro del límite.");
    }

    private static void BombearDurante(int milisegundos)
    {
        long limite = Environment.TickCount64 + milisegundos;
        while (Environment.TickCount64 < limite)
        {
            System.Windows.Forms.Application.DoEvents();
            Thread.Sleep(1);
        }
    }

    private sealed class FactoryControlada : IProjectDbContextFactory
    {
        private readonly Func<string, CancellationToken, Task<SOPROContext>> _crear;

        public FactoryControlada(Func<string, CancellationToken, Task<SOPROContext>> crear)
            => _crear = crear;

        public SOPROContext Create(string databasePath)
            => CreateAsync(databasePath).GetAwaiter().GetResult();

        public Task<SOPROContext> CreateAsync(
            string databasePath, CancellationToken cancellationToken = default)
            => _crear(databasePath, cancellationToken);
    }

    /// <summary>
    /// Difiera la creación indicada y deja el resto inmediatas. Permite mantener
    /// abierto el submenú mientras la fábrica sigue pendiente.
    /// </summary>
    private sealed class FactoryDiferible : IProjectDbContextFactory
    {
        public bool DiferirSiguiente { get; set; }

        public TaskCompletionSource<SOPROContext> Diferida { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Iniciada { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SOPROContext Create(string databasePath)
            => CreateAsync(databasePath).GetAwaiter().GetResult();

        public Task<SOPROContext> CreateAsync(
            string databasePath, CancellationToken cancellationToken = default)
        {
            if (!DiferirSiguiente)
                return Task.FromResult<SOPROContext>(new SOPROContext(databasePath));

            DiferirSiguiente = false;
            Iniciada.TrySetResult();
            cancellationToken.Register(() => Diferida.TrySetCanceled(cancellationToken));
            return Diferida.Task;
        }
    }

    /// <summary>
    /// Primera creación diferida y las siguientes completas: permite definir el
    /// orden de finalización sin abrir hilos de UI adicionales.
    /// </summary>
    private sealed class FactoryPrimeraDiferida : IProjectDbContextFactory
    {
        private readonly string _databasePath;
        private int _llamadas;

        public FactoryPrimeraDiferida(string databasePath) => _databasePath = databasePath;

        public TaskCompletionSource<SOPROContext> Primera { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public TaskCompletionSource Iniciada { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        public SOPROContext Create(string databasePath)
            => CreateAsync(databasePath).GetAwaiter().GetResult();

        public Task<SOPROContext> CreateAsync(
            string databasePath, CancellationToken cancellationToken = default)
        {
            if (Interlocked.Increment(ref _llamadas) == 1)
            {
                Iniciada.TrySetResult();
                cancellationToken.Register(() => Primera.TrySetCanceled(cancellationToken));
                return Primera.Task;
            }

            return Task.FromResult<SOPROContext>(new SOPROContext(_databasePath));
        }
    }
}
