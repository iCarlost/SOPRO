using System;
using System.IO;
using System.Linq;
using System.Reflection;
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
/// Celdas del presupuesto SIN persistencia por fila:
/// <list type="bullet">
/// <item>Observaciones viaja por la ruta de comando (ADR-0001); el miembro
/// <see cref="BudgetTextEditField.Observaciones"/> lo define el ensamblado Application
/// (dependency pendiente): si aún no existe, la edición queda como borrador y
/// NINGUNA ruta la escribe;</item>
/// <item>porcentajes derivados del proyecto (% Indirectos / % Financiamiento /
/// % Utilidad) y CargosAdicionales no editables ni con autoguardado heredado.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoCeldasSinPersistenciaTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    // ══════════════════════════════════════════════════════════════════════════
    // Observaciones: el comando consume la edición (o queda como pendiente del
    // backend), pero NUNCA ensucia el tracker compartido ni reescribe la fila.
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    public void EdicionObservaciones_NuncaEnsuciaElTrackerNiReescribeLaFila()
    {
        var root = CrearRaíz(out var dbPath);
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

            bool eventoDisparado = false;
            grid.CellValueChanged += (_, args) =>
            {
                if (args.RowIndex == fila) eventoDisparado = true;
            };

            var obsCell = BuscarCelda(grid, fila, "Observaciones");
            Assert.IsNotNull(obsCell, "Debe existir la columna Observaciones.");

            const string textoPropuesto = "Nota del usuario en Observaciones";
            obsCell!.Value = textoPropuesto;

            Assert.IsTrue(eventoDisparado,
                "Asignar Observaciones debe disparar CellValueChanged (ruta real de la UI).");

            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Concepto).State,
                "La ruta de Observaciones no muta la entidad compartida antes de confirmar.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "La edición de Observaciones no deja trabajo pendiente en el contexto compartido.");

            var antes = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);

            // Un guardado ajeno posterior (otra operación del usuario) no arrastra
            // un UPDATE de la fila de Observaciones por la ruta heredada.
            escenario.Proyecto.Descripcion = "cambio ajeno de otro caso de uso";
            ctx.SaveChanges();

            var despues = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(antes.Cantidad, despues.Cantidad, "La fila persistida no se reescribe.");
            Assert.AreEqual(antes.ImporteTotal, despues.ImporteTotal,
                "El autoguardado heredado no puede reescribir la fila completa por Observaciones.");
            Assert.AreEqual(escenario.Concepto.Descripcion, despues.Descripcion,
                "El cambio ajeno del proyecto no altera la descripción del concepto.");
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
    public void EdicionObservaciones_MiembroDelComandoDisponible_SeConsumeElComando()
    {
        if (!MiembroObservacionesDisponible())
            Assert.Inconclusive(
                "Dependency pendiente: BudgetTextEditField.Observaciones aún no existe en Application.");

        var root = CrearRaíz(out var dbPath);
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

            var obsCell = BuscarCelda(grid, fila, "Observaciones");
            Assert.IsNotNull(obsCell, "Debe existir la columna Observaciones.");
            obsCell!.Value = "Nota confirmada por comando";

            // Ruta real de la UI: CellValueChanged consume la edición como comando y
            // clasifica el resultado.
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoEdicionTexto,
                "El comando debe confirmarse sin conflicto en un escenario limpio.");

            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Concepto).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El comando confirmado no deja trabajo pendiente en el contexto compartido.");

            // Un guardado ajeno posterior tampoco puede reescribir el campo ya
            // confirmado con el valor del borrador del contexto compartido.
            var confirmada = LeerConceptoEnBase(dbPath, escenario.Concepto.Id).FechaModificacion;
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();

            Assert.AreEqual(confirmada, LeerConceptoEnBase(dbPath, escenario.Concepto.Id).FechaModificacion,
                "El contexto compartido no debe reescribir la fila confirmada.");
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
    public void EdicionObservaciones_FallaElUpdate_NoEscribeNiEnsuciaElTracker()
    {
        if (!MiembroObservacionesDisponible())
            Assert.Inconclusive(
                "Dependency pendiente: BudgetTextEditField.Observaciones aún no existe en Application.");

        var root = CrearRaíz(out var dbPath);
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

            // El trigger se crea DESPUÉS del constructor: la carga de la vista no falla.
            CrearTriggerDeFallo(dbPath);

            var obsCell = BuscarCelda(grid, fila, "Observaciones");
            Assert.IsNotNull(obsCell, "Debe existir la columna Observaciones.");
            obsCell!.Value = "Nota rechazada por la base";

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoEdicionTexto,
                "El intento debe clasificarse como fallo revertido por el comando.");

            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Concepto).State,
                "Un comando fallido no marca la entidad compartida como Modified.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un comando fallido no deja trabajo pendiente en el contexto compartido.");

            EliminarTriggerDeFallo(dbPath);
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
            ctx.SaveChanges();

            var enBase = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(string.Empty, enBase.Notas,
                "Un guardado ajeno no puede confirmar el UPDATE abortado de Observaciones.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Columnas sin persistencia por fila: ni inicio de edición ni autoguardado
    // heredado que arrastre una fila modificada DLL.
    // ══════════════════════════════════════════════════════════════════════════

    [TestMethod]
    [DataRow("PorcentajeIndirectos")]
    [DataRow("PorcentajeFinanciamiento")]
    [DataRow("PorcentajeUtilidad")]
    [DataRow("CargosAdicionales")]
    public void ColumnaSinPersistenciaPorFila_NoEdita_NiCaeEnGuardarCambios(string nombreInterno)
    {
        var root = CrearRaíz(out var dbPath);
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

            var celda = BuscarCelda(grid, fila, nombreInterno);
            Assert.IsNotNull(celda, $"Debe existir la columna {nombreInterno}.");

            // 1) El inicio de edición se cancela (sólo lectura en la UI).
            var cancelArgs = new DataGridViewCellCancelEventArgs(celda!.ColumnIndex, fila);
            InvocarCellBeginEdit(form, cancelArgs);
            Assert.IsTrue(cancelArgs.Cancel,
                $"{nombreInterno} no debe permitir iniciar la edición.");

            // 2) Aunque un valor llegue a la celda por otra vía, el evento NO cae en
            //    el autoguardado genérico: un trabajo pendiente ajeno en el tracker
            //    compartido (esta sería la fila que "regalaría" el guardado completo)
            //    permanece sin escribir.
            string notasPersistidasOriginales = LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Notas;
            escenario.Concepto.Notas = "trabajo pendiente de otra ruta";
            Assert.AreEqual(EntityState.Modified, ctx.Entry(escenario.Concepto).State);

            celda.Value = "99.99%";

            var trasEventoEnBase = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(notasPersistidasOriginales, trasEventoEnBase.Notas,
                $"Editar {nombreInterno} no puede arrastrar al autoguardado la fila del tracker.");
            Assert.AreEqual(10m, trasEventoEnBase.Cantidad,
                "El autoguardado genérico no debe reescribir la fila completa.");

            // 3) El commit explícito del test es quien confirma el trabajo ajeno,
            //    demostrando que el evento de la celda soportada no lo hizo antes.
            ctx.SaveChanges();
            Assert.AreEqual("trabajo pendiente de otra ruta",
                LeerConceptoEnBase(dbPath, escenario.Concepto.Id).Notas,
                "El trabajo pendiente sigue adelante para la ruta que le corresponde.");
        }
        finally
        {
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ══════════════════════════════════════════════════════════════════════════
    // Infraestructura
    // ══════════════════════════════════════════════════════════════════════════

    private static bool MiembroObservacionesDisponible()
        => Enum.IsDefined(typeof(BudgetTextEditField), "Observaciones");

    private static void InvocarCellBeginEdit(FormPresupuesto form, DataGridViewCellCancelEventArgs args)
    {
        var metodo = typeof(FormPresupuesto).GetMethod(
            "DgvPresupuesto_CellBeginEdit",
            BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(metodo, "Debe existir el manejador de inicio de edición.");
        metodo!.Invoke(form, new object?[] { form.GridPrincipal, args });
    }

    private static string CrearRaíz(out string dbPath)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_sinpersist_{Guid.NewGuid():N}");
        dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        Directory.CreateDirectory(root);
        return root;
    }

    private sealed class Escenario
    {
        public Proyecto Proyecto { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto celdas sin persistencia",
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
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();

        return new Escenario { Proyecto = proyecto, Concepto = concepto };
    }

    private static void CrearTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath,
            "CREATE TRIGGER trg_fail_concepto_sinpersist BEFORE UPDATE ON ConceptosPresupuesto " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

    private static void EliminarTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath, "DROP TRIGGER trg_fail_concepto_sinpersist;");

    private static void EjecutarSql(string dbPath, string sql)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(sql);
    }

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
