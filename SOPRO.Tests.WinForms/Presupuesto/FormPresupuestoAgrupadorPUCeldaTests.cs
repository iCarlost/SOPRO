using System;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Presupuesto;

/// <summary>
/// Prueba temporal del detalle visual del grid del presupuesto: la celda P.U. de
/// un agrupador se exhibe vacía tanto en la carga inicial como tras la recarga
/// y los comandos que aplican filas confirmadas, sin corromper el dato
/// persistido (el PrecioUnitario subyacente del agrupador se conserva).
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoAgrupadorPUCeldaTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void Agrupador_CeldaPU_VaciaEnCargaInicial_TrasRecarga_YTrasRefresco()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_pu_agrupador_{Guid.NewGuid():N}");
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

            form = new FormPresupuesto(ctx, escenario.Proyecto, new SOPRO.Application.Services.ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            Assert.AreNotEqual(-1, filaAgrupador, "El grid debe mostrar al agrupador.");
            int filaConcepto = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.AreNotEqual(-1, filaConcepto, "El grid debe mostrar al concepto.");

            var puAgrupadorCell = BuscarCelda(grid, filaAgrupador, "PrecioUnitario");
            var puConceptoCell = BuscarCelda(grid, filaConcepto, "PrecioUnitario");
            Assert.IsNotNull(puAgrupadorCell);
            Assert.IsNotNull(puConceptoCell);

            var puAgrupadorTag = grid.Rows[filaAgrupador].Tag as ConceptoPresupuesto;
            Assert.IsNotNull(puAgrupadorTag);
            Assert.IsTrue(puAgrupadorTag.EsAgrupador);
            Assert.IsFalse(puAgrupadorTag.PrecioUnitario < 0m, "Dato subyacente valido.");

            // 1) Carga inicial: P.U. del agrupador vacío.
            Assert.AreEqual(string.Empty, puAgrupadorCell.Value?.ToString() ?? string.Empty,
                "En la carga inicial la celda P.U. del agrupador debe verse vacía.");
            Assert.AreNotEqual(string.Empty, puConceptoCell.Value?.ToString() ?? string.Empty,
                "El concepto conserva su P.U. formateado.");

            // 2) Recarga desde base: la celda no debe reaparecer como $0.00.
            form.RecargarPreciosDesdeBase();
            Assert.AreEqual(string.Empty, puAgrupadorCell.Value?.ToString() ?? string.Empty,
                "Tras RecargarPreciosDesdeBase la celda P.U. del agrupador sigue vacía.");
            Assert.AreEqual(escenario.Concepto.PrecioUnitario.ToStringImporte(), puConceptoCell.Value?.ToString(),
                "El concepto conserva su P.U. formateado tras la recarga.");

            var importeAgrupadorCell = BuscarCelda(grid, filaAgrupador, "Importe");
            Assert.IsNotNull(importeAgrupadorCell);
            Assert.AreEqual(1000m.ToStringImporte(), importeAgrupadorCell.Value?.ToString(),
                "El importe acumulado del agrupador se mantiene tras la recarga.");

            // 3) Refresco de precios (AplicarFilasConfirmadas): tampoco debe
            //    reaparecer $0.00 en el agrupador.
            form.RefreshBudgetPrices();

            Assert.AreEqual(string.Empty, puAgrupadorCell.Value?.ToString() ?? string.Empty,
                "Tras RefreshBudgetPrices la celda P.U. del agrupador sigue vacía.");
            Assert.AreEqual(escenario.Concepto.PrecioUnitario.ToStringImporte(), puConceptoCell.Value?.ToString(),
                "El concepto conserva su P.U. formateado tras el refresco.");

            // El Tag del agrupador no se corrompe: conserva EsAgrupador y el
            // total acumulado; el PrecioUnitario subyacente (0) sigue intacto.
            var tagTrasComandos = grid.Rows[filaAgrupador].Tag as ConceptoPresupuesto;
            Assert.IsNotNull(tagTrasComandos);
            Assert.IsTrue(tagTrasComandos.EsAgrupador);
            Assert.AreEqual(0m, tagTrasComandos.PrecioUnitario,
                "El valor numérico subyacente del agrupador no cambia.");
            Assert.AreEqual(1000m, tagTrasComandos.CostoDirectoTotal,
                "El total acumulado del agrupador permanece en la entidad.");

            using var verificacion = new SOPROContext(dbPath);
            var persistidoAgrupador = verificacion.ConceptosPresupuesto
                .AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id);
            Assert.IsTrue(persistidoAgrupador.EsAgrupador,
                "La persistencia del agrupador no se corrompe.");
            Assert.AreEqual(0m, persistidoAgrupador.PrecioUnitario,
                "El PrecioUnitario persistido del agrupador se conserva (0).");
            Assert.AreEqual(1000m, persistidoAgrupador.CostoDirectoTotal);
            Assert.AreEqual(1000m, persistidoAgrupador.ImporteTotal);

            var persistidoConcepto = verificacion.ConceptosPresupuesto
                .AsNoTracking().Single(c => c.Id == escenario.Concepto.Id);
            Assert.AreEqual(100m, persistidoConcepto.PrecioUnitario,
                "El P.U. persistido del concepto no cambia.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Los comandos no dejan trabajo pendiente en el contexto compartido.");
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
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto celda PU agrupador",
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
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 1000m,
            PrecioUnitario = 100m,
            ImporteTotal = 1000m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.AddRange(agrupador, concepto);
        ctx.SaveChanges();

        return new Escenario { Proyecto = proyecto, Agrupador = agrupador, Concepto = concepto };
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
