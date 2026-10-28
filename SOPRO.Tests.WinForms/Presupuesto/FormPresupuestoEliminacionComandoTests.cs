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
/// Eliminación de filas a través de la ruta de comando (ADR-0001) con el
/// <see cref="FormPresupuesto"/> real sobre un SQLite y un workspace temporales:
/// <list type="bullet">
/// <item>una fila persistida se elimina después de confirmar el comando y el
/// agrupador que queda se sincroniza como <c>Unchanged</c>;</item>
/// <item>un fallo de la base conserva las filas del grid y no ensucia el tracker;</item>
/// <item>un borrador Id=0 se retira solo de la vista, sin escribir.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoEliminacionComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void EliminacionComando_ConceptoPersistido_ConfirmaRemueveFilaYSincronizaElAgrupadorUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_elim_{Guid.NewGuid():N}");
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

            int filaConcepto = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(filaConcepto >= 0, "El concepto debe estar en el grid cargado.");

            Assert.IsTrue(form.EjecutarEliminacionDeFilas(new[] { filaConcepto }));
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoEliminacion,
                "La eliminación debe confirmarse como comando.");

            Assert.AreEqual(-1, BuscarFilaPorConcepto(grid, escenario.Concepto.Id),
                "La fila eliminada desaparece del grid solo después del commit.");

            int filaCapitulo = BuscarFilaPorConcepto(grid, escenario.Capitulo.Id);
            Assert.IsTrue(filaCapitulo >= 0, "El agrupador no formaba parte de la eliminación.");

            var capituloTag = (ConceptoPresupuesto)grid.Rows[filaCapitulo].Tag!;
            Assert.AreEqual(0m, capituloTag.ImporteTotal,
                "El agrupador se sincroniza con lo confirmado por el comando.");
            Assert.AreEqual(0m, capituloTag.CostoDirectoTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(capituloTag).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La eliminación confirmada no deja trabajo pendiente en el contexto compartido.");

            var persistidos = LeerConceptosEnBase(dbPath);
            Assert.AreEqual(1, persistidos.Count, "Solo queda el agrupador en la base.");
            Assert.AreEqual(escenario.Capitulo.Id, persistidos[0].Id);
            Assert.AreEqual(0m, persistidos[0].ImporteTotal);

            // Un guardado ajeno posterior no puede resucitar la fila eliminada.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior a la eliminación";
            ctx.SaveChanges();

            Assert.AreEqual(1, LeerConceptosEnBase(dbPath).Count,
                "El contexto compartido no reinserta la fila eliminada.");
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
    public void EliminacionComando_FalloDeLaBase_ConservaLaFilaYNoEnsuciaElTrackerCompartido()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_elim_fail_{Guid.NewGuid():N}");
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

            int filaConcepto = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(filaConcepto >= 0);

            EjecutarSql(dbPath,
                "CREATE TRIGGER trg_fail_concepto_delete BEFORE DELETE ON ConceptosPresupuesto " +
                "BEGIN SELECT RAISE(ABORT, 'fallo_simulado_delete'); END;");

            Assert.IsTrue(form.EjecutarEliminacionDeFilas(new[] { filaConcepto }));
            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoEliminacion,
                "El rechazo de la base debe clasificarse como fallo revertido.");

            Assert.AreEqual(filaConcepto, BuscarFilaPorConcepto(grid, escenario.Concepto.Id),
                "La fila sigue en el grid tras el fallo.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Concepto).State,
                "La entidad compartida no queda Modified por un comando fallido.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un comando fallido no deja trabajo pendiente en el contexto compartido.");

            EjecutarSql(dbPath, "DROP TRIGGER trg_fail_concepto_delete;");

            Assert.AreEqual(2, LeerConceptosEnBase(dbPath).Count,
                "El DELETE rechazado no puede quedar en la base.");
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
    public void EliminacionComando_BorradorId0_SeRetiraSoloDeLaVistaSinEscribir()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_elim_borrador_{Guid.NewGuid():N}");
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

            int filaVacia = BuscarFilaVacia(grid);
            Assert.IsTrue(filaVacia >= 0, "El grid cargado debe conservar una fila vacía.");

            BuscarCelda(grid, filaVacia, "Descripcion")!.Value = "Borrador no persistido";

            int filasAntes = grid.Rows.Count;
            Assert.IsTrue(form.EjecutarEliminacionDeFilas(new[] { filaVacia }));

            Assert.IsNull(form.UltimoEstadoEliminacion,
                "Un borrador Id=0 no envía comando.");
            Assert.AreEqual(filasAntes - 1, grid.Rows.Count,
                "La fila no persistida se retira de la vista.");
            Assert.AreEqual(2, LeerConceptosEnBase(dbPath).Count,
                "El borrador visual no se elimina ni se inserta en la base.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La eliminación visual no deja trabajo pendiente.");
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

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto comando eliminación",
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
            Descripcion = "Capítulo de prueba",
            EsAgrupador = true,
            Nivel = 0,
            Orden = 0,
            Unidad = string.Empty,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.1",
            Descripcion = "Concepto de prueba",
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

        ctx.ConceptosPresupuesto.AddRange(capitulo, concepto);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            Capitulo = capitulo,
            Concepto = concepto,
        };
    }

    private static void EjecutarSql(string dbPath, string sql)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(sql);
    }

    private static System.Collections.Generic.List<ConceptoPresupuesto> LeerConceptosEnBase(string dbPath)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .OrderBy(c => c.Orden)
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

    private static int BuscarFilaVacia(DataGridView grid)
    {
        for (int i = 0; i < grid.Rows.Count; i++)
        {
            if (grid.Rows[i].Tag is not null) continue;

            var tipo = BuscarCelda(grid, i, "Tipo")?.Value?.ToString();
            var descripcion = BuscarCelda(grid, i, "Descripcion")?.Value?.ToString();
            if (string.IsNullOrWhiteSpace(tipo) && string.IsNullOrWhiteSpace(descripcion))
                return i;
        }

        return -1;
    }

    private static DataGridViewCell? BuscarCelda(DataGridView grid, int rowIndex, string nombreInterno)
    {
        if (rowIndex < 0 || rowIndex >= grid.Rows.Count) return null;

        foreach (DataGridViewColumn col in grid.Columns)
        {
            if (col.Tag is ColumnaPersonalizada definicion
                && string.Equals(definicion.NombreInterno, nombreInterno, StringComparison.OrdinalIgnoreCase))
            {
                return grid.Rows[rowIndex].Cells[col.Index];
            }
        }

        return null;
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
