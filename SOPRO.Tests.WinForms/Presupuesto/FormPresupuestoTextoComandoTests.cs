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
/// Edición de Descripción/Unidad de un concepto persistido a través de la ruta de
/// comando (ADR-0001) con el <see cref="FormPresupuesto"/> real sobre un SQLite y un
/// workspace temporales:
/// <list type="bullet">
/// <item>un UPDATE rechazado por la base no escribe ni deja el tracker compartido
/// sucio, de modo que un guardado ajeno posterior no puede confirmarlo;</item>
/// <item>el reintento del mismo borrador confirma y sincroniza el Tag sin marcarlo
/// <c>Modified</c>;</item>
/// <item>una edición de Unidad confirmada no reescribe la fila desde el contexto
/// compartido en un guardado ajeno posterior.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoTextoComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void EdicionDescripcionComando_FallaElUpdate_NoEscribeNiEnsuciaElTracker_YElReintentoConfirma()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_texto_{Guid.NewGuid():N}");
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

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            CrearTriggerDeFallo(dbPath);

            bool eventoDisparado = false;
            grid.CellValueChanged += (_, args) =>
            {
                if (args.RowIndex == fila) eventoDisparado = true;
            };

            const string textoPropuesto = "Descripción editada por comando";
            var descripcionCell = BuscarCelda(grid, fila, "Descripcion");
            Assert.IsNotNull(descripcionCell, "Debe existir la columna Descripcion.");
            descripcionCell!.Value = textoPropuesto;

            Assert.IsTrue(eventoDisparado,
                "Asignar la descripción debe disparar CellValueChanged (ruta real de la UI).");

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoEdicionTexto,
                "El intento debe clasificarse como fallo revertido por el comando.");

            // El borrador sigue visible, pero nada se dio por guardado.
            Assert.AreEqual(textoPropuesto, descripcionCell.Value?.ToString(),
                "La celda conserva el borrador del usuario tras el fallo.");
            Assert.AreEqual("Concreto simple en obra", escenario.Concepto.Descripcion,
                "La ruta de comando no muta el Tag/entidad compartida antes de confirmar.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Concepto).State,
                "La entidad compartida no queda Modified por un comando fallido.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un comando fallido no deja trabajo pendiente en el contexto compartido.");

            Assert.AreEqual("Concreto simple en obra", LeerDescripcionEnBase(dbPath, escenario.Concepto.Id),
                "El UPDATE rechazado no puede quedar en la base.");

            // Se retira el trigger y se intenta el guardado ajeno que, con la ruta
            // heredada, arrastraba el UPDATE pendiente.
            EliminarTriggerDeFallo(dbPath);
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
            ctx.SaveChanges();

            Assert.AreEqual("Concreto simple en obra", LeerDescripcionEnBase(dbPath, escenario.Concepto.Id),
                "Un guardado ajeno no puede confirmar el UPDATE abortado.");

            // Reintento del MISMO intento del usuario: la celda aún tiene el borrador,
            // así que se invoca el manejador que consume el evento (asignar el mismo
            // valor no vuelve a disparar CellValueChanged).
            Assert.IsTrue(form.ProcesarEdicionTextoConComando(fila, "Descripcion"),
                "El reintento debe consumirse en la ruta de comando.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoEdicionTexto,
                "El reintento sin trigger debe confirmarse.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(textoPropuesto, persistido.Descripcion);
            Assert.AreEqual("m3", persistido.Unidad);
            Assert.AreEqual(10m, persistido.Cantidad);
            Assert.AreEqual(1000m, persistido.ImporteTotal,
                "Una edición de texto no recalcula importes.");

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual(textoPropuesto, tag.Descripcion,
                "El Tag se sincroniza con el campo confirmado.");
            Assert.AreEqual(persistido.FechaModificacion, tag.FechaModificacion,
                "El Tag queda con la marca confirmada en la base.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");

            // Un guardado ajeno posterior al éxito tampoco puede volver a escribir la fila.
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();

            var trasGuardadoAjeno = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(textoPropuesto, trasGuardadoAjeno.Descripcion);
            Assert.AreEqual(persistido.FechaModificacion, trasGuardadoAjeno.FechaModificacion,
                "El contexto compartido no debe reescribir una fila ya confirmada.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State);
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
    public void EdicionUnidadComando_CaminoOk_SincronizaElTagSinMarcarloModified()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_texto_ok_{Guid.NewGuid():N}");
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

            int fila = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(fila >= 0, "El concepto debe estar en el grid cargado.");

            var unidadCell = BuscarCelda(grid, fila, "Unidad");
            Assert.IsNotNull(unidadCell, "Debe existir la columna Unidad.");
            unidadCell!.Value = "ml";

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoEdicionTexto,
                "La edición de unidad debe confirmarse como comando.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("ml", persistido.Unidad);
            Assert.AreEqual("Concreto simple en obra", persistido.Descripcion,
                "Editar la unidad no toca la descripción.");
            Assert.AreEqual(1000m, persistido.ImporteTotal);

            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual("ml", tag.Unidad);
            Assert.AreEqual("Concreto simple en obra", tag.Descripcion);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges());

            var fechaConfirmada = persistido.FechaModificacion;
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();

            var trasGuardadoAjeno = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual("ml", trasGuardadoAjeno.Unidad);
            Assert.AreEqual(fechaConfirmada, trasGuardadoAjeno.FechaModificacion,
                "El contexto compartido no debe reescribir una fila ya confirmada.");
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
            Nombre = "Proyecto comando texto",
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
            Agrupador = agrupador,
            Concepto = concepto,
        };
    }

    private static void CrearTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath,
            "CREATE TRIGGER trg_fail_concepto BEFORE UPDATE ON ConceptosPresupuesto " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

    private static void EliminarTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath, "DROP TRIGGER trg_fail_concepto;");

    private static void EjecutarSql(string dbPath, string sql)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(sql);
    }

    private static string LeerDescripcionEnBase(string dbPath, int conceptoId)
        => LeerConceptoEnBase(dbPath, conceptoId).Descripcion;

    private static ConceptoPresupuesto LeerConceptoEnBase(string dbPath, int conceptoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Single(c => c.Id == conceptoId);
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
