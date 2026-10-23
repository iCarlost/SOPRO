using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Reordenamiento de filas a través de la ruta de comando (ADR-0001) con el
/// <see cref="FormPresupuesto"/> real sobre un SQLite y un workspace temporales:
/// <list type="bullet">
/// <item>el arrastre visual se persiste como un solo comando, sin la ruta heredada de
/// guardado, y los Tags quedan <c>Unchanged</c> con el Orden y los agrupadores
/// confirmados;</item>
/// <item>un conflicto conserva el borrador visual y no escribe ni ensucia el tracker.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[TestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoReordenComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void ReordenComando_ArrastreVisual_ConfirmaOrdenYSincronizaTagsUnchangedSinGuardadoHeredado()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reorden_{Guid.NewGuid():N}");
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

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int filaConceptoB = BuscarFilaPorConcepto(grid, escenario.ConceptoB.Id);
            int filaCapitulo2 = BuscarFilaPorConcepto(grid, escenario.Capitulo2.Id);
            Assert.IsTrue(filaConceptoB > filaCapitulo2, "El escenario carga A y B en capítulos distintos.");

            // Arrastre: el concepto B se mueve antes del capítulo 2.
            var filaMovida = grid.Rows[filaConceptoB];
            grid.Rows.RemoveAt(filaConceptoB);
            grid.Rows.Insert(filaCapitulo2, filaMovida);

            Assert.IsTrue(form.EjecutarReordenPersistido());
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoReorden,
                "El arrastre debe confirmarse como comando.");
            Assert.IsFalse(form.HayReordenSinConfirmar);

            Assert.AreEqual(filaCapitulo2, BuscarFilaPorConcepto(grid, escenario.ConceptoB.Id),
                "El borrador visual se conserva tal como lo dejó el arrastre.");
            Assert.AreEqual(filaCapitulo2 + 1, BuscarFilaPorConcepto(grid, escenario.Capitulo2.Id));

            var tagB = (ConceptoPresupuesto)grid.Rows[BuscarFilaPorConcepto(grid, escenario.ConceptoB.Id)].Tag!;
            var tagCapitulo1 = (ConceptoPresupuesto)grid.Rows[BuscarFilaPorConcepto(grid, escenario.Capitulo1.Id)].Tag!;
            var tagCapitulo2 = (ConceptoPresupuesto)grid.Rows[BuscarFilaPorConcepto(grid, escenario.Capitulo2.Id)].Tag!;

            Assert.AreEqual(2, tagB.Orden, "El Tag sincroniza el Orden confirmado.");
            Assert.AreEqual(0, tagCapitulo1.Orden);
            Assert.AreEqual(3, tagCapitulo2.Orden);

            Assert.AreEqual(1200m, tagCapitulo1.ImporteTotal,
                "El agrupador confirmado se sincroniza con la entidad compartida.");
            Assert.AreEqual(0m, tagCapitulo2.ImporteTotal);

            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagB).State,
                "El Orden sincronizado queda Unchanged para no reescribirse.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagCapitulo1).State);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagCapitulo2).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El reordenamiento confirmado no deja trabajo pendiente en el contexto compartido.");

            // La numeración visible se recalcula con el orden confirmado.
            int filaConceptoADespues = BuscarFilaPorConcepto(grid, escenario.ConceptoA.Id);
            int filaConceptoBDespues = BuscarFilaPorConcepto(grid, escenario.ConceptoB.Id);
            Assert.AreEqual("1", TextoNumeracion(grid, filaConceptoADespues));
            Assert.AreEqual("2", TextoNumeracion(grid, filaConceptoBDespues));

            var persistidos = LeerConceptosEnBase(dbPath);
            CollectionAssert.AreEqual(
                new[] { escenario.Capitulo1.Id, escenario.ConceptoA.Id, escenario.ConceptoB.Id, escenario.Capitulo2.Id },
                persistidos.Select(c => c.Id).ToArray());
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, persistidos.Select(c => c.Orden).ToArray());
            Assert.AreEqual(1200m, persistidos.Single(c => c.Id == escenario.Capitulo1.Id).ImporteTotal);
            Assert.AreEqual(0m, persistidos.Single(c => c.Id == escenario.Capitulo2.Id).ImporteTotal);

            // Un guardado ajeno posterior no puede volver a escribir el orden heredado.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al reordenamiento";
            ctx.SaveChanges();

            var trasGuardadoAjeno = LeerConceptosEnBase(dbPath);
            CollectionAssert.AreEqual(new[] { 0, 1, 2, 3 }, trasGuardadoAjeno.Select(c => c.Orden).ToArray(),
                "La ruta heredada ya no reescribe el Orden.");
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
    public void ReordenComando_ConflictoDeOrden_ConservaElBorradorVisualSinEscribirNiEnsuciarElTracker()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_reorden_conflicto_{Guid.NewGuid():N}");
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

            var escenario = CrearEscenario(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int filaConceptoB = BuscarFilaPorConcepto(grid, escenario.ConceptoB.Id);
            int filaCapitulo2 = BuscarFilaPorConcepto(grid, escenario.Capitulo2.Id);
            Assert.IsTrue(filaConceptoB > filaCapitulo2);

            // La base cambia después de cargar la vista: la base esperada del comando
            // deja de representar lo que el usuario arrastró.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == escenario.ConceptoB.Id);
                concepto.Orden = 4;
                otroModulo.SaveChanges();
            }

            var filaMovida = grid.Rows[filaConceptoB];
            grid.Rows.RemoveAt(filaConceptoB);
            grid.Rows.Insert(filaCapitulo2, filaMovida);

            Assert.IsTrue(form.EjecutarReordenPersistido());
            Assert.AreEqual(BudgetOperationStatus.Conflict, form.UltimoEstadoReorden,
                "El orden obsoleto debe clasificarse como conflicto.");
            Assert.IsTrue(form.HayReordenSinConfirmar,
                "El borrador visual queda marcado como no confirmado.");

            Assert.AreEqual(filaCapitulo2, BuscarFilaPorConcepto(grid, escenario.ConceptoB.Id),
                "El conflicto conserva el borrador visual del usuario.");
            Assert.AreEqual(filaCapitulo2 + 1, BuscarFilaPorConcepto(grid, escenario.Capitulo2.Id));

            var tagB = (ConceptoPresupuesto)grid.Rows[BuscarFilaPorConcepto(grid, escenario.ConceptoB.Id)].Tag!;
            Assert.AreEqual(3, tagB.Orden,
                "La entidad compartida no se toca antes de conocer el resultado.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagB).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un conflicto no deja trabajo pendiente en el contexto compartido.");

            var persistidos = LeerConceptosEnBase(dbPath);
            Assert.AreEqual(4, persistidos.Single(c => c.Id == escenario.ConceptoB.Id).Orden,
                "La base conserva el cambio ajeno, no el borrador rechazado.");
            Assert.AreEqual(1000m, persistidos.Single(c => c.Id == escenario.Capitulo1.Id).ImporteTotal,
                "Un conflicto no recalcula agrupadores.");
            Assert.AreEqual(200m, persistidos.Single(c => c.Id == escenario.Capitulo2.Id).ImporteTotal);
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
        public ConceptoPresupuesto Capitulo1 { get; init; } = null!;
        public ConceptoPresupuesto Capitulo2 { get; init; } = null!;
        public ConceptoPresupuesto ConceptoA { get; init; } = null!;
        public ConceptoPresupuesto ConceptoB { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto comando reordenamiento",
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

        var capitulo1 = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "CAP-01",
            Descripcion = "Capítulo 1",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 0m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var conceptoA = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Concepto A",
            EsAgrupador = false,
            Nivel = 1,
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

        var capitulo2 = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "CAP-02",
            Descripcion = "Capítulo 2",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 2,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 0m,
            ImporteTotal = 200m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var conceptoB = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-002",
            Descripcion = "Concepto B",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 3,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 2m,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 200m,
            PrecioUnitario = 100m,
            ImporteTotal = 200m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        ctx.ConceptosPresupuesto.AddRange(capitulo1, conceptoA, capitulo2, conceptoB);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            Capitulo1 = capitulo1,
            Capitulo2 = capitulo2,
            ConceptoA = conceptoA,
            ConceptoB = conceptoB,
        };
    }

    private static System.Collections.Generic.List<ConceptoPresupuesto> LeerConceptosEnBase(string dbPath)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .OrderBy(c => c.Orden)
            .ThenBy(c => c.Id)
            .ToList();
    }

    private static int BuscarFilaPorConcepto(DataGridView grid, int conceptoId)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is ConceptoPresupuesto concepto && concepto.Id == conceptoId)
                return i;
        }

        return -1;
    }

    private static string? TextoNumeracion(DataGridView grid, int rowIndex)
    {
        if (!grid.Columns.Contains("colNumero")) return null;
        return grid.Rows[rowIndex].Cells["colNumero"].Value?.ToString();
    }

    private static Proyecto? LeerProyectoGlobalFormato()
        => (Proyecto?)CampoProyectoGlobal().GetValue(null);

    private static void RestaurarProyectoGlobalFormato(Proyecto? previo)
        => CampoProyectoGlobal().SetValue(null, previo);

    private static System.Reflection.FieldInfo CampoProyectoGlobal()
        => typeof(FormatoHelper).GetField(CampoProyectoGlobalFormato,
               System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.NonPublic)
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
