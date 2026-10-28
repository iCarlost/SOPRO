using System;
using System.IO;
using System.Linq;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;

namespace SOPRO.Tests.WinForms.Programa;

/// <summary>
/// Contrato del recálculo atómico para el flujo posterior a editar el calendario: el
/// enlace del calendario al programa (sólo si aún no tenía uno) ocurre DENTRO de la
/// misma transacción del recálculo. La UI ya no hace un SaveChanges previo suelto.
/// </summary>
[TestClass]
public class ProgramacionCalendarioAtomicoTests
{
    [TestMethod]
    public void RecalculateProgram_EnlazaCalendarioSoloSiFaltaEnLaMismaTransaccion()
    {
        var dbPath = CrearRuta();
        try
        {
            var proyectoId = CrearProyectoConPrograma(dbPath, out var programaId);
            int calibradoId;
            int otroCalendarioId;

            using (var ctx = new SOPROContext(dbPath))
            {
                // El programa nace sin calendario: es el caso que la UI debe enlazar.
                var programa = ctx.ProgramasObra.Single(p => p.Id == programaId);
                programa.CalendarioLaboralId = null;
                ctx.SaveChanges();

                calibradoId = CrearCalendario(ctx, proyectoId, "Calendario editado");
                otroCalendarioId = CrearCalendario(ctx, proyectoId, "Calendario posterior");
                ctx.SaveChanges();
            }

            var service = new ProgramacionSynchronizationService();

            var resultado = service.RecalculateProgram(
                new ProjectDbContextFactory(),
                dbPath,
                proyectoId,
                programaId,
                tipoPeriodo: null,
                calendarioLaboralId: calibradoId);

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Diagnostic);

            using (var ctx = new SOPROContext(dbPath))
            {
                Assert.AreEqual(calibradoId,
                    ctx.ProgramasObra.Single(p => p.Id == programaId).CalendarioLaboralId,
                    "El calendario se enlaza dentro de la transacción del recálculo.");
            }

            // Ya tiene calendario: un id distinto NO lo pisa (semántica «sólo si falta»).
            var segundo = service.RecalculateProgram(
                new ProjectDbContextFactory(),
                dbPath,
                proyectoId,
                programaId,
                tipoPeriodo: null,
                calendarioLaboralId: otroCalendarioId);

            Assert.AreEqual(BudgetOperationStatus.Committed, segundo.Status, segundo.Diagnostic);

            using (var ctx = new SOPROContext(dbPath))
            {
                Assert.AreEqual(calibradoId,
                    ctx.ProgramasObra.Single(p => p.Id == programaId).CalendarioLaboralId,
                    "El enlace existente se conserva.");
            }
        }
        finally
        {
            Limpiar(dbPath);
        }
    }

    [TestMethod]
    public void RecalculateProgram_ConflictoNoEnlazaNiEscribeElCalendario()
    {
        var dbPath = CrearRuta();
        try
        {
            var proyectoId = CrearProyectoConPrograma(dbPath, out var programaId);
            int calendarioId;

            using (var ctx = new SOPROContext(dbPath))
            {
                var programa = ctx.ProgramasObra.Single(p => p.Id == programaId);
                programa.CalendarioLaboralId = null;
                ctx.SaveChanges();

                calendarioId = CrearCalendario(ctx, proyectoId, "Calendario editado");
                ctx.SaveChanges();
            }

            // Otra ventana reemplaza el programa activo antes del comando.
            int nuevoProgramaId;
            using (var ctx = new SOPROContext(dbPath))
            {
                foreach (var activo in ctx.ProgramasObra.Where(p => p.ProyectoId == proyectoId && p.Activo))
                    activo.Activo = false;

                var nuevo = new ProgramaObra
                {
                    ProyectoId = proyectoId,
                    Nombre = "Programa de otra ventana",
                    FechaInicioPrograma = new DateTime(2026, 1, 5),
                    FechaFinPrograma = new DateTime(2026, 1, 16),
                    TipoPeriodo = TipoPeriodoPrograma.Semana,
                    DuracionPeriodoDias = 7,
                    Activo = true,
                };
                ctx.ProgramasObra.Add(nuevo);
                ctx.SaveChanges();
                nuevoProgramaId = nuevo.Id;
            }

            var resultado = new ProgramacionSynchronizationService().RecalculateProgram(
                new ProjectDbContextFactory(),
                dbPath,
                proyectoId,
                programaId,
                tipoPeriodo: null,
                calendarioLaboralId: calendarioId);

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado.Status, resultado.Diagnostic);

            using var verif = new SOPROContext(dbPath);
            Assert.IsNull(verif.ProgramasObra.Single(p => p.Id == nuevoProgramaId).CalendarioLaboralId,
                "Un conflicto no escribe el enlace en el programa de otra ventana.");
            Assert.IsNull(verif.ProgramasObra.Single(p => p.Id == programaId).CalendarioLaboralId,
                "El programa esperado tampoco se modifica sin confirmación.");
        }
        finally
        {
            Limpiar(dbPath);
        }
    }

    // ─────────────────────────────── Escenario ───────────────────────────────

    private static string CrearRuta()
        => Path.Combine(Path.GetTempPath(), $"sopro_cal_atomico_{Guid.NewGuid():N}.db");

    private static int CrearProyectoConPrograma(string dbPath, out int programaId)
    {
        using var ctx = new SOPROContext(dbPath);
        ctx.Database.EnsureDeleted();
        ctx.Database.EnsureCreated();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto calendario atómico",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 5),
            FechaTermino = new DateTime(2026, 2, 27),
            PlazoEjecucion = 40,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        ctx.ConceptosPresupuesto.Add(new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-01",
            Descripcion = "Concepto C-01",
            Unidad = "m2",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Cantidad = 10m,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
        });
        ctx.SaveChanges();

        var generado = new ProgramacionSynchronizationService().GenerateProgram(
            new ProjectDbContextFactory(),
            dbPath,
            proyecto.Id,
            expectedActiveProgramaId: null,
            new DateTime(2026, 1, 5),
            TipoPeriodoPrograma.Semana);

        Assert.AreEqual(BudgetOperationStatus.Committed, generado.Status, generado.Diagnostic);
        programaId = generado.ProgramaObraId!.Value;
        return proyecto.Id;
    }

    private static int CrearCalendario(SOPROContext ctx, int proyectoId, string nombre)
    {
        var calendario = new CalendarioLaboral
        {
            ProyectoId = proyectoId,
            Nombre = nombre,
            Lunes = true,
            Martes = true,
            Miercoles = true,
            Jueves = true,
            Viernes = true,
            Sabado = false,
            Domingo = false,
            HoraInicio = new TimeSpan(8, 0, 0),
            HoraFin = new TimeSpan(18, 0, 0),
            Activo = true,
        };
        ctx.CalendariosLaborales.Add(calendario);
        ctx.SaveChanges();
        return calendario.Id;
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
