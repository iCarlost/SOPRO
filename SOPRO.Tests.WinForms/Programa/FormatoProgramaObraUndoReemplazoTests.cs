using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Reporting.Formatting;
using SOPRO.WinForms.Forms;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// BLOQUEANTE Oracle: el undo de formato por celda debe dejar la base EXACTAMENTE
/// en el estado anterior completo. Con una celda que YA tenía ColorFondo, editar
/// añadiendo Negrita y deshacer tiene que quitar la Negrita (antes se fusionaba el
/// "antes" parcial y la propiedad añadida sobrevivía, dejando la persistencia
/// divergente de la caché). Se ejercita el formulario REAL del Programa de Obra en
/// STA, con el flush/undo/redo reales y bases temporales (nunca datos del usuario).
/// </summary>
[STATestClass]
[DoNotParallelize] // Construye el formulario real del módulo (controles WinForms).
public class FormatoProgramaObraUndoReemplazoTests
{
    private const string Tipo = "ProgramaObra";

    [TestMethod]
    public void UndoQuitaLaPropiedadAnadidaYRedoLaRestaura_BaseYCacheCoinciden()
    {
        var dbPath = Path.Combine(Path.GetTempPath(), $"sopro_fmt_undo_po_{Guid.NewGuid():N}.db");
        SOPROContext? ctx = null;
        FormProgramaObra? form = null;
        try
        {
            var direccion = new ReportCellAddress(string.Empty, "ACT:7", "colImporte");
            var antes = new ReportStyleOverlay(ColorFondo: "#111111");
            // Snapshot COMPLETO después de la edición: el coordinador fusiona el delta
            // del ribbon (Negrita) con el overlay previo de la celda (ColorFondo).
            var despues = ReportCellStyleResolver.Combinar(antes, new ReportStyleOverlay(Negrita: true));
            Assert.AreEqual("#111111", despues.ColorFondo, "La edición conserva el fondo previo.");

            ctx = CrearBase(dbPath, out var proyecto);
            SembrarCeldaPrevia(dbPath, proyecto.Id, antes);

            form = new FormProgramaObra(ctx, proyecto);
            Assert.AreEqual(antes, form.ObtenerOverlayActual(direccion),
                "Precondición: la caché arranca con el estado persistido (sólo ColorFondo).");

            form.Show();
            System.Windows.Forms.Application.DoEvents();

            // Edición: añade Negrita conservando el ColorFondo preexistente.
            form.AplicarOverlayCelda(direccion, despues);
            System.Windows.Forms.Application.DoEvents();

            Assert.AreEqual(1, form.HistorialUndo.UndoCount, "El flush apila UNA acción compuesta.");
            AssertPersistido(dbPath, proyecto.Id, "#111111", negrita: true);
            Assert.AreEqual(despues, form.ObtenerOverlayActual(direccion), "Caché = base tras editar.");

            // Undo: vuelve al estado anterior COMPLETO (sin Negrita).
            Assert.IsTrue(form.HistorialUndo.Undo());
            AssertPersistido(dbPath, proyecto.Id, "#111111", negrita: false);
            Assert.AreEqual(antes, form.ObtenerOverlayActual(direccion),
                "La caché coincide con la base: el undo no dejó Negrita en ninguno de los dos.");

            // Redo: restaura el estado posterior completo.
            Assert.IsTrue(form.HistorialUndo.Redo());
            AssertPersistido(dbPath, proyecto.Id, "#111111", negrita: true);
            Assert.AreEqual(despues, form.ObtenerOverlayActual(direccion),
                "La caché vuelve a coincidir con la base tras rehacer.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            Limpiar(dbPath);
        }
    }

    // ─────────────────────────────── Escenario ───────────────────────────────

    private static SOPROContext CrearBase(string dbPath, out Proyecto proyecto)
    {
        var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        proyecto = new Proyecto
        {
            Nombre = "Proyecto undo reemplazo formato",
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

    /// <summary>Fila de formato PREEXISTENTE con sólo ColorFondo (el escenario del bug).</summary>
    private static void SembrarCeldaPrevia(string dbPath, int proyectoId, ReportStyleOverlay overlay)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.FormatosCeldaReporte.Add(new FormatoCeldaReporte
        {
            ProyectoId = proyectoId,
            TipoReporte = Tipo,
            ClaveVista = string.Empty,
            ClaveFila = "ACT:7",
            NombreColumna = "colImporte",
            ColorFondo = overlay.ColorFondo,
        });
        ctx.SaveChanges();
    }

    private static void AssertPersistido(
        string dbPath, int proyectoId, string colorFondoEsperado, bool negrita)
    {
        using var ctx = new SOPROContext(dbPath);
        var fila = ctx.FormatosCeldaReporte.AsNoTracking()
            .SingleOrDefault(f => f.ProyectoId == proyectoId && f.TipoReporte == Tipo);

        Assert.IsNotNull(fila, "El override debe seguir existiendo (sólo cambia su contenido).");
        Assert.AreEqual(colorFondoEsperado, fila!.ColorFondo, "El fondo preexistente no se pierde.");
        Assert.AreEqual(negrita, fila.Negrita ?? false, "Negrita debe coincidir con el estado esperado.");
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
}
