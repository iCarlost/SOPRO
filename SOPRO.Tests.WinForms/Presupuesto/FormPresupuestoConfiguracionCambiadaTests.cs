using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// <see cref="FormPresupuesto"/> NO debe mutar valores económicos ni escribir con
/// <c>GuardarCambios</c> desde el manejador de <see cref="FormatoHelper.ConfiguracionCambiada"/>:
/// los decimales se persisten upstream (FormProyecto + RecalculoGlobalService) y aquí
/// solo se reconcilia la vista. Se invoca directamente el núcleo
/// <see cref="FormPresupuesto.ProcesarCambioConfiguracionFormato"/> porque el manejador
/// real exige el form visible (los构造 de pruebas no lo muestran).
/// </summary>
[STATestClass]
[DoNotParallelize] // El núcleo toca FormatoHelper estático; evita interacciones.
public class FormPresupuestoConfiguracionCambiadaTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void CambioDeDecimales_ReconciliaSolo_SinEscribirEconomia()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_cfg_{Guid.NewGuid():N}");
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
            var concepto = AgregarConcepto(ctx, proyecto);
            decimal puAntes = concepto.PrecioUnitario;
            decimal importeAntes = concepto.ImporteTotal;
            decimal cantidadAntes = concepto.Cantidad;

            form = new FormPresupuesto(ctx, proyecto, new ProjectWorkspaceService(root, root));

            // Simular el módulo upstream: cambió los decimales EN LA BASE.
            int nuevoDecimalesCantidad;
            using (var ctxUpstream = new SOPROContext(dbPath))
            {
                var proy = ctxUpstream.Proyectos.First(p => p.Id == proyecto.Id);
                proy.DecimalesCantidad = 4;
                ctxUpstream.SaveChanges();
                nuevoDecimalesCantidad = proy.DecimalesCantidad;
            }

            using (var ctxLectura = new SOPROContext(dbPath))
            {
                var conceptoEnBd = ctxLectura.ConceptosPresupuesto.First(c => c.Id == concepto.Id);
                Assert.AreEqual(cantidadAntes, conceptoEnBd.Cantidad);
                Assert.AreEqual(importeAntes, conceptoEnBd.ImporteTotal);
            }

            form.ProcesarCambioConfiguracionFormato();

            // El form reconcilia (solo lectura): sync de decimales + reetiquetado de columna.
            Assert.AreEqual(4, form.ProyectoActual.DecimalesCantidad,
                "El form debe adoptar los decimales persistidos upstream.");
            Assert.AreEqual($"N{nuevoDecimalesCantidad}", FormatoColumnaCantidad(form),
                "La columna Cantidad debe reetiquetarse con el nuevo formato.");

            // Nada de la reconciliación escribe económicas: los valores persistidos
            // del concepto siguen exactamente iguales (la mutación+GuardarCambios
            // heredada habría cambiado ImporteTotal y PU).
            using (var ctxLectura = new SOPROContext(dbPath))
            {
                var conceptoEnBd = ctxLectura.ConceptosPresupuesto.AsNoTracking().First(c => c.Id == concepto.Id);
                Assert.AreEqual(cantidadAntes, conceptoEnBd.Cantidad,
                    "La cantidad persistida no debe cambiar al reconciliar.");
                Assert.AreEqual(puAntes, conceptoEnBd.PrecioUnitario,
                    "El P.U. persistido no debe cambiar al reconciliar.");
                Assert.AreEqual(importeAntes, conceptoEnBd.ImporteTotal,
                    "El importe persistido no debe cambiar al reconciliar.");
            }

            // El contexto compartido del form no queda con escrituras pendientes.
            ctx.ChangeTracker.DetectChanges();
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El manejador no debe dejar cambios pendientes en el contexto compartido.");

            // Segundo disparo SIN nuevo cambio de decimales: idempotente, no reescribe.
            form.ProcesarCambioConfiguracionFormato();
            using (var ctxLectura = new SOPROContext(dbPath))
            {
                var conceptoEnBd = ctxLectura.ConceptosPresupuesto.AsNoTracking().First(c => c.Id == concepto.Id);
                Assert.AreEqual(importeAntes, conceptoEnBd.ImporteTotal);
            }
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }

        Assert.AreSame(proyectoGlobalPrevio, LeerProyectoGlobalFormato(),
            "La prueba no debe dejar alterado el proyecto global de FormatoHelper.");

        // Limpieza mejor esfuerzo: el WAL de SQLite puede retener el archivo unos
        // instantes tras Dispose; es un detalle del entorno de prueba, no de la
        // ruta bajo prueba, y no invalida los invariantes ya verificados.
        EsperarEliminacionDirectorio(root, TimeSpan.FromSeconds(5));
    }

    private static bool EsperarEliminacionDirectorio(string ruta, TimeSpan tiempoMaximo)
    {
        var limite = DateTime.UtcNow + tiempoMaximo;
        Exception? ultimaExcepcion = null;
        while (DateTime.UtcNow < limite)
        {
            try
            {
                if (!Directory.Exists(ruta)) return true;
                Directory.Delete(ruta, recursive: true);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or AccessViolationException)
            {
                ultimaExcepcion = ex;
                foreach (var archivo in SafeEnumerar(ruta))
                    Console.WriteLine($"BLOQUEADO?: {archivo}");
            }
            GC.Collect();
            GC.WaitForPendingFinalizers();
            System.Threading.Thread.Sleep(100);
        }
        Console.WriteLine($"No se pudo eliminar {ruta}: {ultimaExcepcion?.Message}");
        return !Directory.Exists(ruta);
    }

    private static IEnumerable<string> SafeEnumerar(string ruta)
    {
        if (!Directory.Exists(ruta)) yield break;
        foreach (var f in Directory.EnumerateFiles(ruta, "*", SearchOption.AllDirectories))
            yield return f;
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private static Proyecto AgregarProyecto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto configuracion cambiada",
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

    private static ConceptoPresupuesto AgregarConcepto(SOPROContext ctx, Proyecto proyecto)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Concepto de prueba",
            Unidad = "m2",
            Nivel = 1,
            EsAgrupador = false,
            Orden = 1,
            Cantidad = 10.123m,
            PrecioUnitario = 100.5487m,
            ImporteTotal = 1017.8000m,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();
        return concepto;
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)typeof(FormatoHelper)
            .GetField(CampoProyectoGlobalFormato, BindingFlags.Static | BindingFlags.NonPublic)
            ?.GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => typeof(FormatoHelper)
            .GetField(CampoProyectoGlobalFormato, BindingFlags.Static | BindingFlags.NonPublic)
            ?.SetValue(null, previo);

    private static string? FormatoColumnaCantidad(FormPresupuesto form)
        => form.GridPrincipal.Columns.Cast<System.Windows.Forms.DataGridViewColumn>()
            .FirstOrDefault(c => c.Tag is ColumnaPersonalizada colDef &&
                string.Equals(colDef.NombreInterno, "Cantidad", StringComparison.OrdinalIgnoreCase))
            ?.DefaultCellStyle.Format;

    private static void EliminarDirectorio(string ruta)
    {
        try { if (Directory.Exists(ruta)) Directory.Delete(ruta, recursive: true); }
        catch (IOException) { /* mejor esfuerzo */ }
        catch (UnauthorizedAccessException) { /* mejor esfuerzo */ }
    }
}
