using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Presupuesto;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Importación desde Excel a través de la ruta de comando (ADR-0001) con el
/// <see cref="FormPresupuesto"/> real sobre un SQLite y un workspace temporales:
/// <list type="bullet">
/// <item>el lote se escribe en una sola operación y el grid se recarga SOLO después
/// del commit, sin el guardado heredado;</item>
/// <item>la recarga recalcula los agrupadores solo para mostrar: no los persiste ni
/// deja entidades compartidas <c>Modified</c>;</item>
/// <item>un conflicto de base no actualiza el grid ni escribe filas.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario. No se abren diálogos.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoImportacionComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void ImportacionComando_LoteOk_RecargaElGridTrasElCommitSinPersistirAgrupadoresNiDejarElTrackerSucio()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_import_{Guid.NewGuid():N}");
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

            var escenario = CrearEscenarioConAgrupadorObsoleto(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            // La carga inicial recalculó el agrupador solo para MOSTRAR: la base
            // conserva su total obsoleto y el tracker compartido queda limpio.
            Assert.AreEqual(999m, LeerImporteAgrupador(dbPath, escenario.Capitulo.Id),
                "CargarPresupuesto no debe persistir los agrupadores recalculados.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La recarga no deja conceptos compartidos Modified.");

            var filas = new List<BudgetExcelImportRowDto>
            {
                new()
                {
                    SourceRowNumber = 40,
                    Clave = "CAP-02",
                    Descripcion = "Capítulo importado",
                    Unidad = string.Empty,
                    Cantidad = null,
                    TipoTexto = "Capitulo"
                },
                new()
                {
                    SourceRowNumber = 41,
                    Clave = "C-200",
                    Descripcion = "Concepto importado",
                    Unidad = "m2",
                    Cantidad = 3.5m,
                    TipoTexto = "Concepto"
                }
            };

            var resultado = form.EjecutarImportacionPresupuesto(filas);

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Committed, resultado!.Status, resultado.Message);
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoImportacion);
            Assert.IsNotNull(resultado.Confirmed);
            Assert.AreEqual(2, resultado.Confirmed!.Rows.Count);
            CollectionAssert.AreEqual(new[] { 40, 41 },
                resultado.Confirmed.Rows.Select(r => r.SourceRowNumber).ToArray());

            // El grid se actualizó con el lote confirmado y con Ids reales.
            var filasConConcepto = grid.Rows.Cast<DataGridViewRow>()
                .Where(r => r.Tag is ConceptoPresupuesto)
                .ToList();
            Assert.AreEqual(4, filasConConcepto.Count,
                "El escenario tenía 2 filas persistidas y se importaron 2.");
            Assert.IsTrue(filasConConcepto.Any(r =>
                r.Tag is ConceptoPresupuesto c && c.Clave == "CAP-02" && c.Id > 0));
            Assert.IsTrue(filasConConcepto.Any(r =>
                r.Tag is ConceptoPresupuesto c && c.Clave == "C-200" && c.Id > 0));

            // Las filas nuevas quedaron persistidas una sola vez y en posiciones contiguas.
            var persistidos = LeerConceptosEnBase(dbPath);
            Assert.AreEqual(4, persistidos.Count);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, persistidos.Select(c => c.Orden).ToArray());

            // La importación persiste inserciones + totales en el MISMO SaveChanges; la
            // recarga posterior solo muestra y no deja trabajo en el contexto compartido.
            Assert.AreEqual(1000m, LeerImporteAgrupador(dbPath, escenario.Capitulo.Id),
                "La importación persiste el agregador recalculado en la misma escritura.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El contexto compartido queda limpio tras la importación confirmada.");

            // Un guardado ajeno posterior no confirma ni duplica nada del lote.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior a la importación";
            ctx.SaveChanges();

            Assert.AreEqual(4, LeerConceptosEnBase(dbPath).Count,
                "El guardado heredado posterior no puede volver a insertar el lote.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void ImportacionComando_ProyectoCambioEnLaBase_ConflictoSinActualizarElGridNiEscribir()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_import_conflicto_{Guid.NewGuid():N}");
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

            var escenario = CrearEscenarioConAgrupadorObsoleto(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            int filasAntes = grid.Rows.Count;

            using (var otroModulo = new SOPROContext(dbPath))
            {
                var proyecto = otroModulo.Proyectos.Single(p => p.Id == escenario.Proyecto.Id);
                proyecto.PorcentajeUtilidad += 7m;
                otroModulo.SaveChanges();
            }

            var resultado = form.EjecutarImportacionPresupuesto(new List<BudgetExcelImportRowDto>
            {
                new()
                {
                    SourceRowNumber = 1,
                    Clave = "C-300",
                    Descripcion = "Concepto que no debe entrar",
                    Unidad = "m2",
                    Cantidad = 1m,
                    TipoTexto = "Concepto"
                }
            });

            Assert.IsNotNull(resultado);
            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado!.Status, resultado.Message);
            Assert.AreEqual(BudgetOperationStatus.Conflict, form.UltimoEstadoImportacion);
            Assert.IsNull(resultado.Confirmed);
            Assert.AreEqual(filasAntes, grid.Rows.Count,
                "Un conflicto conserva la vista; el grid solo cambia tras el commit.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un conflicto no deja trabajo pendiente en el contexto compartido.");
            Assert.IsFalse(LeerConceptosEnBase(dbPath).Any(c => c.Clave == "C-300"),
                "Un conflicto no inserta la fila importada.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed class Escenario
    {
        public Proyecto Proyecto { get; init; } = null!;
        public ConceptoPresupuesto Capitulo { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
    }

    /// <summary>
    /// Proyecto con un capítulo cuyo total persistido (999) NO coincide con la suma
    /// real de su concepto (1,000): así se observa si la carga o la importación
    /// vuelven a escribir agrupadores en la base.
    /// </summary>
    private static Escenario CrearEscenarioConAgrupadorObsoleto(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto comando importación",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 0m,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            PorcentajeIVA = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FechaCreacion = new DateTime(2026, 1, 1),
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.Proyectos.Add(proyecto);
        ctx.SaveChanges();

        var capitulo = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "CAP-01",
            Descripcion = "Capítulo existente",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 999m,
            PrecioUnitario = 0m,
            ImporteTotal = 999m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Concepto existente",
            EsAgrupador = false,
            Nivel = 5,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 10m,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        ctx.ConceptosPresupuesto.AddRange(capitulo, concepto);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            Capitulo = capitulo,
            Concepto = concepto,
        };
    }

    private static decimal LeerImporteAgrupador(string dbPath, int conceptoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Single(c => c.Id == conceptoId)
            .ImporteTotal;
    }

    private static List<ConceptoPresupuesto> LeerConceptosEnBase(string dbPath)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();
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

    private static void EliminarDirectorio(string root)
    {
        for (int intento = 0; intento < 5; intento++)
        {
            try
            {
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
