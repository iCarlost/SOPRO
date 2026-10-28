using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;
using SOPRO.WinForms.Undo;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Salida del bloqueo por resultado INCIERTO de formato en el Programa de Obra: la
/// recarga del programa (botón Recargar → <c>CargarPrograma</c>) debe releer los
/// overrides de celda Y los estilos de fila con un contexto NUEVO y, sólo si ambas
/// lecturas terminan bien, reemplazar las cachés, descartar el historial y
/// desbloquear Undo/Redo/edición de formato.
///
/// Ejercita el formulario REAL del módulo sobre bases temporales (nunca datos del
/// usuario). No es reset ni ciclo de vida: la recarga es de SÓLO LECTURA.
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms).
public class FormatoProgramaObraRecargaTests
{
    private const string Tipo = "ProgramaObra";

    // ───────────────────── Recarga exitosa: desbloquea ─────────────────────

    [TestMethod]
    public void RecargaDesdeBase_ReemplazaCachesConLoPersistidoYDesbloqueaElHistorial()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            var direccion = new ReportCellAddress(string.Empty, "ACT:1", "colImporte");

            ctx = CrearBase(dbPath, out var proyecto);
            SembrarEstadoInicial(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);

            // 1) Lectura normal previa: la caché queda con el estado persistido inicial.
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.AreEqual(1, form.ObtenerEstilosFilaUsuario().Count);

            // 2) Historial con una acción para poder observar que se descarta.
            Assert.IsTrue(form.HistorialUndo.Execute(new DelegateUndoableAction(
                "Editar fecha en programa", () => { }, () => { })),
                "La acción inicial debe apilarse.");
            Assert.AreEqual(1, form.HistorialUndo.UndoCount);

            // 3) Resultado INCIERTO real: el commit se hizo pero el cierre del contexto
            //    falló, así que la persistencia no puede confirmarse.
            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "ACT:1",
                    "colImporte",
                    ExpectedOverlay: new ReportStyleOverlay(ColorFondo: "#111111"),
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#999999")),
            };

            var ex = Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath, proyecto.Id, Tipo, comandos, form.HistorialUndo, new DisposeThrowingFactory(dbPath)));

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, ex.Status);
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.HistorialUndo.CanUndo);
            Assert.IsFalse(form.HistorialUndo.CanRedo);

            // 4) Con la persistencia incierta: Undo/Redo y formato quedan bloqueados.
            Assert.ThrowsExactly<InvalidOperationException>(() => form.HistorialUndo.Undo());
            Assert.ThrowsExactly<InvalidOperationException>(() => form.HistorialUndo.Redo());
            Assert.ThrowsExactly<InvalidOperationException>(() => form.HistorialUndo.Execute(
                new DelegateUndoableAction("otra", () => { }, () => { })));

            var nueva = new ReportCellAddress(string.Empty, "ACT:2", "colImporte");
            form.AplicarOverlayCelda(nueva, new ReportStyleOverlay(ColorFondo: "#00FF00"));
            Assert.IsNull(form.ObtenerOverlayActual(nueva),
                "Bloqueado: no se acumulan overrides sobre una caché que no se sabe si coincide con la base.");
            Assert.AreEqual(0, form.ObtenerEstilosFilaUsuario().Count,
                "Bloqueado: no se publican estilos de usuario (ni caché parcial).");

            // 5) Otra escritura confirmada por fuera mientras el formulario está bloqueado.
            using (var externo = new SOPROContext(dbPath))
            {
                var celda = externo.FormatosCeldaReporte.Single(
                    f => f.ProyectoId == proyecto.Id && f.TipoReporte == Tipo);
                celda.ColorFondo = "#222222";

                var estilo = externo.ConfiguracionesEstiloFilaReporte.Single(
                    f => f.ProyectoId == proyecto.Id && f.TipoReporte == Tipo);
                estilo.ColorFondo = "#444444";

                externo.SaveChanges();
            }

            // 6) Trigger normal del usuario: recargar el programa.
            form.CargarPrograma();

            // 7) Sólo con la base releída se reemplazan AMBAS cachés y se reconcilia.
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation,
                "La recarga exitosa debe liberar el bloqueo.");
            Assert.AreEqual(0, form.HistorialUndo.UndoCount, "El historial obsoleto se descarta.");
            Assert.AreEqual(0, form.HistorialUndo.RedoCount);
            Assert.AreEqual("#222222", form.ObtenerOverlayActual(direccion)?.ColorFondo,
                "La caché de celdas se reemplaza con lo que hay en la base (lectura fresca).");

            var estilos = form.ObtenerEstilosFilaUsuario();
            Assert.AreEqual(1, estilos.Count);
            Assert.AreEqual("#444444", estilos["Total"].ColorFondo,
                "La caché de estilos de fila también se reemplaza con lo persistido.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ─────────────── Recarga fallida: mantiene bloqueo y cachés ───────────────

    [TestMethod]
    public void RecargaConLecturaDeEstilosFallida_ConservaBloqueoEHistorialSinCacheParcial()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            var direccion = new ReportCellAddress(string.Empty, "ACT:1", "colImporte");

            ctx = CrearBase(dbPath, out var proyecto);
            SembrarEstadoInicial(dbPath, proyecto.Id);

            // La lectura de celdas funcionará, la de estilos NO: así la recarga queda a
            // medias y no debe publicar ni desbloquear nada.
            ctx.Database.ExecuteSqlRaw("DROP TABLE \"ConfiguracionesEstiloFilaReporte\"");

            using (var ctxLectura = new SOPROContext(dbPath))
            {
                var servicio = new FormatoReporteService(ctxLectura);
                Assert.AreEqual(1, servicio.CargarCeldas(proyecto.Id, Tipo).Count,
                    "La lectura de celdas debe seguir siendo válida (fallo parcial).");

                var falloEstilos = false;
                try { servicio.CargarEstilosFila(proyecto.Id, Tipo); }
                catch (Exception) { falloEstilos = true; }
                Assert.IsTrue(falloEstilos, "La lectura de estilos de fila debe fallar en este escenario.");
            }

            form = new FormProgramaObra(ctx, proyecto);
            Assert.IsTrue(form.HistorialUndo.Execute(new DelegateUndoableAction(
                "Editar fecha en programa", () => { }, () => { })));
            Assert.AreEqual(1, form.HistorialUndo.UndoCount);

            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "ACT:1",
                    "colImporte",
                    ExpectedOverlay: new ReportStyleOverlay(ColorFondo: "#111111"),
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#999999")),
            };

            Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath, proyecto.Id, Tipo, comandos, form.HistorialUndo, new DisposeThrowingFactory(dbPath)));

            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);

            // La recarga NO puede confirmar la lectura completa: no desbloquea.
            Assert.IsFalse(form.RecargarFormatoPersistidoDesdeBase(),
                "Con una lectura incompleta la recarga debe informar fallo.");
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation,
                "Sigue bloqueado: no hay evidencia completa de la base.");
            Assert.AreEqual(1, form.HistorialUndo.UndoCount,
                "El historial se conserva: sin lectura completa no se descarta nada.");
            Assert.IsNull(form.ObtenerOverlayActual(direccion),
                "No se publica la caché PARCIAL de celdas de una recarga a medias.");
            Assert.AreEqual(0, form.ObtenerEstilosFilaUsuario().Count);

            // El trigger normal (recargar programa) es best-effort: al fallar la lectura
            // tampoco desbloquea ni publica estado parcial.
            form.CargarPrograma();

            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);
            Assert.IsFalse(form.HistorialUndo.CanUndo);
            Assert.AreEqual(1, form.HistorialUndo.UndoCount);
            Assert.IsNull(form.ObtenerOverlayActual(direccion));
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ─────────────────────────────── Escenario ───────────────────────────────

    private static string CrearRuta()
        => Path.Combine(Path.GetTempPath(), $"sopro_fmt_recarga_{Guid.NewGuid():N}.db");

    private static SOPROContext CrearBase(string dbPath, out Proyecto proyecto)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        proyecto = new Proyecto
        {
            Nombre = "Proyecto recarga formato",
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
        return ctx;
    }

    // ───────────────────────── Botón Recargar (UI real) ─────────────────────────

    [TestMethod]
    public void BotonRecargar_RealEnLaBarra_InvocarPerformClickDesbloqueaTrasLecturaFresca()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            var direccion = new ReportCellAddress(string.Empty, "ACT:1", "colImporte");

            ctx = CrearBase(dbPath, out var proyecto);
            SembrarEstadoInicial(dbPath, proyecto.Id);

            form = new FormProgramaObra(ctx, proyecto);
            Assert.AreEqual("#111111", form.ObtenerOverlayActual(direccion)?.ColorFondo);

            Assert.IsTrue(form.HistorialUndo.Execute(new DelegateUndoableAction(
                "Editar fecha en programa", () => { }, () => { })));

            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "ACT:1",
                    "colImporte",
                    ExpectedOverlay: new ReportStyleOverlay(ColorFondo: "#111111"),
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#999999")),
            };

            Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath, proyecto.Id, Tipo, comandos, form.HistorialUndo, new DisposeThrowingFactory(dbPath)));
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);

            using (var externo = new SOPROContext(dbPath))
            {
                var celda = externo.FormatosCeldaReporte.Single(
                    f => f.ProyectoId == proyecto.Id && f.TipoReporte == Tipo);
                celda.ColorFondo = "#222222";

                var estilo = externo.ConfiguracionesEstiloFilaReporte.Single(
                    f => f.ProyectoId == proyecto.Id && f.TipoReporte == Tipo);
                estilo.ColorFondo = "#444444";

                externo.SaveChanges();
            }

            // La propiedad Visible del ToolStripItem también depende de que la
            // jerarquía del formulario esté visible. Mostrar el form en STA prueba
            // que el usuario realmente puede ver y pulsar la acción.
            form.Show();
            System.Windows.Forms.Application.DoEvents();

            // El botón es real: está en la barra `toolPrograma`, es visible, habilitado
            // y su Click lo maneja `btnRecargar_Click` (no un evento no disparable).
            var (barra, boton) = LocalizarBotonRecargar(form);
            Assert.AreEqual("btnRecargar", boton.Name);
            Assert.AreEqual("↻ Recargar", boton.Text);
            Assert.AreSame(barra, boton.Owner, "El botón debe estar en la barra del programa.");
            Assert.IsTrue(boton.Visible, "El botón Recargar debe ser visible.");
            Assert.IsTrue(boton.Enabled, "El botón Recargar debe estar habilitado.");
            Assert.IsTrue(boton.Available, "El botón Recargar debe estar disponible para PerformClick.");
            boton.PerformClick();

            // PerformClick → btnRecargar_Click → CargarPrograma → lectura fresca.
            Assert.IsFalse(form.HistorialUndo.RequiresReconciliation,
                "El clic real en Recargar debe liberar el bloqueo.");
            Assert.AreEqual(0, form.HistorialUndo.UndoCount, "El historial obsoleto se descarta.");
            Assert.AreEqual("#222222", form.ObtenerOverlayActual(direccion)?.ColorFondo);
            Assert.AreEqual("#444444", form.ObtenerEstilosFilaUsuario()["Total"].ColorFondo);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void BotonRecargar_ConLecturaFallida_MantieneBloqueoEHistorial()
    {
        var dbPath = CrearRuta();
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            var direccion = new ReportCellAddress(string.Empty, "ACT:1", "colImporte");

            ctx = CrearBase(dbPath, out var proyecto);
            SembrarEstadoInicial(dbPath, proyecto.Id);
            ctx.Database.ExecuteSqlRaw("DROP TABLE \"ConfiguracionesEstiloFilaReporte\"");

            form = new FormProgramaObra(ctx, proyecto);
            Assert.IsTrue(form.HistorialUndo.Execute(new DelegateUndoableAction(
                "Editar fecha en programa", () => { }, () => { })));

            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(
                    string.Empty,
                    "ACT:1",
                    "colImporte",
                    ExpectedOverlay: new ReportStyleOverlay(ColorFondo: "#111111"),
                    ProposedOverlay: new ReportStyleOverlay(ColorFondo: "#999999")),
            };

            Assert.ThrowsExactly<FormatoCeldasPersistenceException>(() =>
                FormatoCeldasPersistence.Aplicar(
                    dbPath, proyecto.Id, Tipo, comandos, form.HistorialUndo, new DisposeThrowingFactory(dbPath)));
            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation);

            var (_, boton) = LocalizarBotonRecargar(form);
            Assert.IsTrue(boton.Enabled && boton.Available);

            boton.PerformClick();

            Assert.IsTrue(form.HistorialUndo.RequiresReconciliation,
                "Lectura incompleta: el clic en Recargar NO debe desbloquear.");
            Assert.IsFalse(form.HistorialUndo.CanUndo);
            Assert.AreEqual(1, form.HistorialUndo.UndoCount, "El historial se conserva.");
            Assert.IsNull(form.ObtenerOverlayActual(direccion),
                "No se publica caché parcial de celdas con la lectura de estilos caída.");
            Assert.AreEqual(0, form.ObtenerEstilosFilaUsuario().Count);
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ─────────────────────────── Localización por Name ───────────────────────────

    private static (ToolStrip Barra, ToolStripButton Boton) LocalizarBotonRecargar(FormProgramaObra form)
    {
        var barra = form.Controls.Find("toolPrograma", searchAllChildren: true)
            .OfType<ToolStrip>()
            .SingleOrDefault();
        Assert.IsNotNull(barra, "Debe existir la barra 'toolPrograma' en el formulario.");

        var boton = barra!.Items.Find("btnRecargar", searchAllChildren: false)
            .OfType<ToolStripButton>()
            .SingleOrDefault();
        Assert.IsNotNull(boton, "La barra 'toolPrograma' debe contener el botón 'btnRecargar'.");

        return (barra, boton!);
    }

    /// <summary>Estado persistido inicial: un override de celda y un estilo por rol.</summary>
    private static void SembrarEstadoInicial(string dbPath, int proyectoId)
    {
        using var ctx = new SOPROContext(dbPath);

        ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = Tipo,
            ClaveVista = string.Empty,
            ClaveFila = "ACT:1",
            NombreColumna = "colImporte",
            ColorFondo = "#111111",
        });

        ctx.ConfiguracionesEstiloFilaReporte.Add(new ConfiguracionEstiloFilaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = Tipo,
            RolFila = "Total",
            Negrita = true,
        });

        ctx.SaveChanges();
    }

    private static void Limpiar(string dbPath)
    {
        foreach (var sufijo in new[] { string.Empty, "-wal", "-shm" })
        {
            var archivo = dbPath + sufijo;
            if (!File.Exists(archivo)) continue;
            try { File.Delete(archivo); }
            catch (IOException) { }
        }
    }

    // ───────────────────────────── Seams de prueba ─────────────────────────────

    /// <summary>
    /// Simula el commit ya realizado seguido de un fallo al cerrar el contexto: el
    /// ejecutor de escritura lo clasifica como <see cref="BudgetOperationStatus.OutcomeUnknown"/>.
    /// </summary>
    private sealed class DisposeThrowingFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;
        private bool _failed;

        public DisposeThrowingFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath)
            => new DisposeThrowingContext(_dbPath, () =>
            {
                if (_failed) return false;
                _failed = true;
                return true;
            });

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class DisposeThrowingContext : SOPROContext
    {
        private readonly Func<bool> _debeFallar;

        public DisposeThrowingContext(string dbPath, Func<bool> debeFallar)
            : base(dbPath)
        {
            _debeFallar = debeFallar;
        }

        public override void Dispose()
        {
            base.Dispose();
            if (_debeFallar()) throw new InvalidOperationException("dispose_simulado");
        }
    }
}
