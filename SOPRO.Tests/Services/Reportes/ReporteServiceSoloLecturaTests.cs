using System;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Services;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Hallazgo bloqueante Critic #2: ninguna ruta de EXPORTACIÓN puede escribir en
/// la base de datos. Estas pruebas demuestran que las variantes de SOLO LECTURA
/// de <see cref="ReporteService"/> y <see cref="ConfiguracionTituloReporteService"/>
/// devuelven un default EN MEMORIA cuando no hay configuración persistida, sin
/// agregar entidades al <see cref="SOPROContext"/> y sin invocar
/// <c>SaveChanges</c> (contado con un contexto espía).
/// </summary>
[TestClass]
public class ReporteServiceSoloLecturaTests
{
    private const string Modulo = "Presupuesto";

    private static SaveChangesSpyContext CreateSpyContext()
    {
        var dbPath = System.IO.Path.Combine(
            System.IO.Path.GetTempPath(), $"sopro_sololec_{Guid.NewGuid():N}.db");

        var ctx = new SaveChangesSpyContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();
        // EnsureCreated no debe contar como escritura del caso bajo prueba.
        ctx.ResetSaveChangesCount();
        return ctx;
    }

    /// <summary>Entidades en el ChangeTracker que NO están en estado Unchanged.</summary>
    private static int EntidadesModificadas(SOPROContext ctx)
        => ctx.ChangeTracker.Entries().Count(e => e.State != EntityState.Unchanged);

    private static Proyecto CreateProyecto(SOPROContext ctx, string nombre)
    {
        var proyecto = new Proyecto
        {
            Nombre = nombre,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();
        return proyecto;
    }

    // ── ReporteService.ObtenerPlantillaSoloLectura ─────────────────────────

    [TestMethod]
    public void ObtenerPlantillaSoloLectura_SinPlantilla_DevuelveDefaultYNoEscribe()
    {
        using var ctx = CreateSpyContext();
        var svc = new ReporteService(ctx);

        var plantilla = svc.ObtenerPlantillaSoloLectura(proyectoId: 42);

        // (a) default no-null
        Assert.IsNotNull(plantilla);
        Assert.AreEqual(42, plantilla.ProyectoId);

        // (b) cero entidades Added/Modified y (c) cero SaveChanges
        Assert.AreEqual(0, EntidadesModificadas(ctx));
        Assert.IsFalse(ctx.ChangeTracker.Entries<PlantillaReporte>()
            .Any(e => e.State == EntityState.Added));
        Assert.AreEqual(0, ctx.SaveChangesCount);

        // Nada quedó persistido
        Assert.AreEqual(0, ctx.PlantillasReporte.Count());
    }

    [TestMethod]
    public void ObtenerPlantillaSoloLectura_ConPlantillaPersistida_LaDevuelveSinEscribir()
    {
        using var ctx = CreateSpyContext();
        var proyecto = CreateProyecto(ctx, "Proyecto persistido");
        var existente = new PlantillaReporte
        {
            ProyectoId = proyecto.Id,
            EncabezadoCenContenido = "Proyecto persistido"
        };
        ctx.PlantillasReporte.Add(existente);
        ctx.SaveChanges();
        ctx.ResetSaveChangesCount();

        var svc = new ReporteService(ctx);
        var plantilla = svc.ObtenerPlantillaSoloLectura(proyectoId: proyecto.Id);

        Assert.AreSame(existente, plantilla);
        Assert.AreEqual(0, EntidadesModificadas(ctx));
        Assert.AreEqual(0, ctx.SaveChangesCount);
        Assert.AreEqual(1, ctx.PlantillasReporte.Count());
    }

    // ── ConfiguracionTituloReporteService.ObtenerSoloLectura ───────────────

    [TestMethod]
    public void ObtenerSoloLectura_SinConfiguracion_DevuelveDefaultConTituloYNoEscribe()
    {
        using var ctx = CreateSpyContext();
        var svc = new ConfiguracionTituloReporteService(ctx);

        var cfg = svc.ObtenerSoloLectura(proyectoId: 5, modulo: Modulo, tituloDefault: "PRESUPUESTO DEMO");

        // (a) default no-null con el título sugerido como fallback
        Assert.IsNotNull(cfg);
        Assert.AreEqual(5, cfg.ProyectoId);
        Assert.AreEqual(Modulo, cfg.Modulo);
        Assert.AreEqual("PRESUPUESTO DEMO", cfg.TextoTitulo);

        // (b) cero entidades Added/Modified y (c) cero SaveChanges
        Assert.AreEqual(0, EntidadesModificadas(ctx));
        Assert.IsFalse(ctx.ChangeTracker.Entries<ConfiguracionTituloReporte>()
            .Any(e => e.State == EntityState.Added));
        Assert.AreEqual(0, ctx.SaveChangesCount);

        Assert.AreEqual(0, ctx.ConfiguracionesTituloReporte.Count());
    }

    [TestMethod]
    public void ObtenerSoloLectura_ConConfiguracionPersistida_LaDevuelveSinEscribir()
    {
        using var ctx = CreateSpyContext();
        var proyecto = CreateProyecto(ctx, "Proyecto título");
        var existente = new ConfiguracionTituloReporte
        {
            ProyectoId = proyecto.Id,
            Modulo = Modulo,
            TextoTitulo = "TÍTULO GUARDADO"
        };
        ctx.ConfiguracionesTituloReporte.Add(existente);
        ctx.SaveChanges();
        ctx.ResetSaveChangesCount();

        var svc = new ConfiguracionTituloReporteService(ctx);
        var cfg = svc.ObtenerSoloLectura(proyectoId: proyecto.Id, modulo: Modulo, tituloDefault: "OTRO");

        Assert.AreSame(existente, cfg);
        Assert.AreEqual("TÍTULO GUARDADO", cfg.TextoTitulo);
        Assert.AreEqual(0, EntidadesModificadas(ctx));
        Assert.AreEqual(0, ctx.SaveChangesCount);
        Assert.AreEqual(1, ctx.ConfiguracionesTituloReporte.Count());
    }

    /// <summary>
    /// Contexto espía que cuenta las invocaciones a <c>SaveChanges</c> para
    /// demostrar que las variantes de solo lectura no persisten nada. El
    /// <see cref="DbContext.SaveChanges()"/> sin argumentos despacha a la
    /// sobrecarga virtual <see cref="DbContext.SaveChanges(bool)"/>.
    /// </summary>
    private sealed class SaveChangesSpyContext : SOPROContext
    {
        public SaveChangesSpyContext(string dbPath) : base(dbPath) { }

        public int SaveChangesCount { get; private set; }

        public void ResetSaveChangesCount() => SaveChangesCount = 0;

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
        {
            SaveChangesCount++;
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
    }
}
