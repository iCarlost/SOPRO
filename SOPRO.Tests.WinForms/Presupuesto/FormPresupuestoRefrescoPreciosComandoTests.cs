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
/// Refresco de precios del <see cref="FormPresupuesto"/> real sobre SQLite y
/// workspace temporales: la ruta confirmada sincroniza Tags y celdas sin un
/// guardado heredado; con trabajo pendiente en el contexto compartido o una base
/// que ya cambió, la vista no escribe ni afirma guardado.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoRefrescoPreciosComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void RefrescoPreciosComando_PrecioDeMatrizCambiado_ConfirmaYSincronizaTagsYCeldas()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_precios_{Guid.NewGuid():N}");
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

            var puCell = BuscarCelda(grid, filaConcepto, "PrecioUnitario");
            Assert.IsNotNull(puCell);
            grid.CurrentCell = puCell;

            // El editor de matrices cambia el precio y NO propaga a los conceptos.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == escenario.Matriz.Id).CostoDirecto = 250m;
                otroModulo.SaveChanges();
            }

            form.RefreshBudgetPrices();

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoRefrescoPrecios,
                form.UltimoDiagnosticoRefrescoPrecios);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyecto = verificacion.Proyectos.AsNoTracking().Single(p => p.Id == escenario.Proyecto.Id);
                decimal cdUnit = BudgetPricingService.RoundImporte(proyecto, 250m);
                decimal pu = BudgetPricingService.CalculateUnitPrice(proyecto, cdUnit);
                decimal importe = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, 10m, pu);

                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == escenario.Concepto.Id);

                Assert.AreEqual(cdUnit, persistido.CostoDirectoUnitario);
                Assert.AreEqual(pu, persistido.PrecioUnitario);
                Assert.AreEqual(importe, persistido.ImporteTotal);

                var agrupador = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id);
                Assert.AreEqual(importe, agrupador.ImporteTotal);

                var tag = (ConceptoPresupuesto)grid.Rows[filaConcepto].Tag!;
                Assert.AreEqual(pu, tag.PrecioUnitario);
                Assert.AreEqual(importe, tag.ImporteTotal);
                Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                    "La entidad compartida sincronizada queda Unchanged, sin guardado heredado.");

                Assert.AreEqual(pu.ToStringImporte(), puCell!.Value?.ToString());
                Assert.AreEqual(importe.ToStringImporte(),
                    BuscarCelda(grid, filaConcepto, "Importe")!.Value?.ToString());
                Assert.AreSame(puCell, grid.CurrentCell, "La selección actual se conserva.");

                int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
                Assert.IsTrue(filaAgrupador >= 0);
                Assert.AreEqual(importe.ToStringImporte(),
                    BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString(),
                    "La celda del agrupador se sincroniza con la instantánea confirmada.");
                Assert.AreEqual(importe, ((ConceptoPresupuesto)grid.Rows[filaAgrupador].Tag!).ImporteTotal);
            }
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
    public void RefrescoPreciosComando_PorcentajesDelProyecto_RecalculaPuEImporteYConfirma()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_precios_pct_{Guid.NewGuid():N}");
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

            // FormPorcentajes guarda el proyecto y después dispara su evento: mismo
            // estado que encuentra el manejador del presupuesto.
            escenario.Proyecto.PorcentajeUtilidad = 10m;
            ctx.SaveChanges();

            form.RefreshBudgetPrices();

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoRefrescoPrecios,
                form.UltimoDiagnosticoRefrescoPrecios);

            using (var verificacion = new SOPROContext(dbPath))
            {
                var proyecto = verificacion.Proyectos.AsNoTracking().Single(p => p.Id == escenario.Proyecto.Id);
                decimal pu = BudgetPricingService.CalculateUnitPrice(proyecto, 100m);
                decimal importe = BudgetPricingService.MultiplyUsingDisplayPrecision(proyecto, 10m, pu);

                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == escenario.Concepto.Id);
                Assert.AreEqual(110m, pu, "El P.U. aplica la utilidad del proyecto.");
                Assert.AreEqual(pu, persistido.PrecioUnitario);
                Assert.AreEqual(importe, persistido.ImporteTotal);

                int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
                Assert.AreEqual(pu.ToStringImporte(),
                    BuscarCelda(grid, fila, "PrecioUnitario")!.Value?.ToString());
                Assert.AreEqual(importe.ToStringImporte(),
                    BuscarCelda(grid, fila, "Importe")!.Value?.ToString());
            }
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
    public void RefrescoPreciosComando_TrabajoPendienteAjeno_SeNiegaYNoEscribeNiGuardaNada()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_precios_dirty_{Guid.NewGuid():N}");
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
            var puCell = BuscarCelda(grid, filaConcepto, "PrecioUnitario");
            Assert.IsNotNull(puCell);
            string puVisiblePrevio = puCell!.Value?.ToString() ?? string.Empty;

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == escenario.Matriz.Id).CostoDirecto = 250m;
                otroModulo.SaveChanges();
            }

            // Un cambio ajeno pendiente en el contexto compartido (p. ej. otra celda
            // en edición): el refresco no puede enviar el comando ni guardar.
            escenario.Proyecto.Descripcion = "pendiente ajeno";
            bool pendienteAntes = ctx.ChangeTracker.HasChanges();

            form.RefreshBudgetPrices();

            Assert.IsTrue(pendienteAntes);
            Assert.IsNull(form.UltimoEstadoRefrescoPrecios,
                "No se envió ningún comando: la vista no puede afirmar guardado.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El trabajo pendiente ajeno permanece intacto.");
            Assert.AreEqual(EntityState.Modified, ctx.Entry(escenario.Proyecto).State,
                "El pendiente ajeno no se descarta.");
            Assert.AreEqual(puVisiblePrevio, puCell.Value?.ToString(),
                "La celda conserva lo que el usuario veía.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == escenario.Concepto.Id);
                Assert.AreEqual(100m, persistido.PrecioUnitario);
                Assert.AreEqual(1000m, persistido.ImporteTotal);
            }

            // Un guardado ajeno posterior no puede arrastrar ningún UPDATE del refresco.
            ctx.SaveChanges();
            using (var verificacion = new SOPROContext(dbPath))
            {
                var persistido = verificacion.ConceptosPresupuesto
                    .AsNoTracking().Single(c => c.Id == escenario.Concepto.Id);
                Assert.AreEqual(100m, persistido.PrecioUnitario);
                Assert.AreEqual(1000m, persistido.ImporteTotal);
            }
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
    public void RefrescoPreciosComando_BaseYaCambiada_ConflictoNoAfirmaGuardadoNiPisaLaBase()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_precios_conf_{Guid.NewGuid():N}");
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
            var puCell = BuscarCelda(grid, filaConcepto, "PrecioUnitario");
            string puVisiblePrevio = puCell!.Value?.ToString() ?? string.Empty;

            // Otra ruta ya propagó y guardó el precio: la base no coincide con lo
            // que la vista cargó, así que el refresco debe rechazarse.
            using (var otroModulo = new SOPROContext(dbPath))
            {
                var concepto = otroModulo.ConceptosPresupuesto.Single(c => c.Id == escenario.Concepto.Id);
                concepto.PrecioUnitario = 175m;
                concepto.ImporteTotal = 1750m;
                otroModulo.SaveChanges();
            }

            form.RefreshBudgetPrices();

            Assert.AreEqual(BudgetOperationStatus.Conflict, form.UltimoEstadoRefrescoPrecios);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un conflicto no deja trabajo pendiente en el contexto compartido.");
            Assert.AreEqual(puVisiblePrevio, puCell.Value?.ToString(),
                "La vista conserva lo que mostraba y no afirma guardado.");

            using var verificacion = new SOPROContext(dbPath);
            var persistido = verificacion.ConceptosPresupuesto
                .AsNoTracking().Single(c => c.Id == escenario.Concepto.Id);
            Assert.AreEqual(175m, persistido.PrecioUnitario,
                "El comando no pisa la actualización ya propagada.");
            Assert.AreEqual(1750m, persistido.ImporteTotal);
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
    public void RecargaDesdeBase_SoloReconcilia_NoEmiteElComandoNiEscribePrecios()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_recarga_{Guid.NewGuid():N}");
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
            var puCell = BuscarCelda(grid, filaConcepto, "PrecioUnitario");
            Assert.IsNotNull(puCell);

            using (var otroModulo = new SOPROContext(dbPath))
            {
                otroModulo.Matrices.Single(m => m.Id == escenario.Matriz.Id).CostoDirecto = 250m;
                otroModulo.SaveChanges();
            }

            form.RecargarPreciosDesdeBase();

            Assert.IsNull(form.UltimoEstadoRefrescoPrecios,
                "La recarga de reconciliación no emite el comando de refresco.");
            Assert.AreEqual(100m.ToStringImporte(), puCell!.Value?.ToString(),
                "La vista conserva lo que la base tiene para el concepto.");

            using var verificacion = new SOPROContext(dbPath);
            var persistido = verificacion.ConceptosPresupuesto
                .AsNoTracking().Single(c => c.Id == escenario.Concepto.Id);
            Assert.AreEqual(100m, persistido.PrecioUnitario,
                "La reconciliación no propaga por sí sola un cambio de precio de matriz.");
            Assert.AreEqual(1000m, persistido.ImporteTotal);
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
        public Matriz Matriz { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto refresco de precios",
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

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto simple",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m,
            Notas = string.Empty,
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();

        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1",
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

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "1.1",
            Descripcion = "Concreto simple en obra",
            EsAgrupador = false,
            Nivel = 1,
            Orden = 1,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = 10m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.AddRange(agrupador, concepto);
        ctx.SaveChanges();

        return new Escenario
        {
            Proyecto = proyecto,
            Matriz = matriz,
            Agrupador = agrupador,
            Concepto = concepto,
        };
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
