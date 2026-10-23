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
/// Cambio de tipo a través de la ruta de comando (ADR-0001) con el
/// <see cref="FormPresupuesto"/> real sobre un SQLite y un workspace temporales:
/// <list type="bullet">
/// <item>un UPDATE rechazado por la base no escribe ni ensucia el tracker compartido,
/// de modo que un guardado ajeno posterior no puede confirmarlo;</item>
/// <item>el reintento del mismo borrador de Tipo confirma y sincroniza el Tag;</item>
/// <item>una fila nueva se crea en el contexto del comando, se adjunta al contexto
/// compartido como <c>Unchanged</c> y no se duplica por un guardado posterior.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoTipoComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void CambioTipoComando_FilaExistente_FallaElUpdate_NoEscribeNiEnsuciaElTracker_YElReintentoConfirma()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_tipo_{Guid.NewGuid():N}");
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

            var tipoCell = BuscarCelda(grid, fila, "Tipo");
            Assert.IsNotNull(tipoCell, "Debe existir la columna Tipo.");
            tipoCell!.Value = "Subcapitulo";

            Assert.IsTrue(eventoDisparado,
                "Asignar el tipo debe disparar CellValueChanged (ruta real de la UI).");

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoCambioTipo,
                "El intento debe clasificarse como fallo revertido por el comando.");

            // El borrador de Tipo sigue visible, pero nada se dio por guardado.
            Assert.AreEqual("Subcapitulo", tipoCell.Value?.ToString(),
                "La celda conserva el borrador del usuario tras el fallo.");
            Assert.IsFalse(escenario.Concepto.EsAgrupador,
                "La ruta de comando no muta el Tag/entidad compartida antes de confirmar.");
            Assert.AreEqual(5, escenario.Concepto.Nivel);
            Assert.AreEqual("m3", escenario.Concepto.Unidad);
            Assert.AreEqual(10m, escenario.Concepto.Cantidad);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Concepto).State,
                "La entidad compartida no queda Modified por un comando fallido.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un comando fallido no deja trabajo pendiente en el contexto compartido.");

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.IsFalse(persistido.EsAgrupador, "El UPDATE rechazado no puede quedar en la base.");
            Assert.AreEqual(5, persistido.Nivel);
            Assert.AreEqual("m3", persistido.Unidad);
            Assert.AreEqual(10m, persistido.Cantidad);

            // Se retira el trigger y se intenta el guardado ajeno que, con la ruta
            // heredada, arrastraba el UPDATE pendiente.
            EliminarTriggerDeFallo(dbPath);
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
            ctx.SaveChanges();

            Assert.IsFalse(LeerConceptoEnBase(dbPath, escenario.Concepto.Id).EsAgrupador,
                "Un guardado ajeno no puede confirmar el UPDATE abortado.");

            // Reintento del MISMO borrador: la celda ya contiene el tipo, así que se
            // invoca el manejador que consume el evento.
            Assert.IsTrue(form.ProcesarCambioTipoConComando(fila),
                "El reintento debe consumirse en la ruta de comando.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoCambioTipo,
                "El reintento sin trigger debe confirmarse.");

            var confirmado = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.IsTrue(confirmado.EsAgrupador);
            Assert.AreEqual(1, confirmado.Nivel, "Subcapítulo se normaliza al nivel 1.");
            Assert.AreEqual(string.Empty, confirmado.Unidad);
            Assert.AreEqual(0m, confirmado.Cantidad);
            Assert.AreEqual(0m, confirmado.PrecioUnitario);
            Assert.AreEqual(0m, confirmado.ImporteTotal,
                "El agrupador sin hojas queda en cero tras el recálculo.");

            // Tag y celdas visibles sincronizados con lo confirmado.
            var tag = (ConceptoPresupuesto)grid.Rows[fila].Tag!;
            Assert.AreEqual(escenario.Concepto.Id, tag.Id);
            Assert.IsTrue(tag.EsAgrupador);
            Assert.AreEqual(1, tag.Nivel);
            Assert.AreEqual(string.Empty, tag.Unidad);
            Assert.AreEqual(0m, tag.Cantidad);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");
            Assert.AreEqual(string.Empty, BuscarCelda(grid, fila, "Unidad")!.Value?.ToString());
            Assert.AreEqual(string.Empty, BuscarCelda(grid, fila, "Cantidad")!.Value?.ToString());
            Assert.AreEqual(string.Empty, BuscarCelda(grid, fila, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(string.Empty, BuscarCelda(grid, fila, "Importe")!.Value?.ToString());
            Assert.AreEqual("Subcapitulo", tipoCell.Value?.ToString(),
                "La celda Tipo conserva el valor confirmado.");

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El éxito del comando no deja trabajo pendiente en el contexto compartido.");

            // Un guardado ajeno posterior al éxito no puede volver a escribir la fila.
            var fechaConfirmada = confirmado.FechaModificacion;
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();

            var trasGuardadoAjeno = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(fechaConfirmada, trasGuardadoAjeno.FechaModificacion,
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
    public void CambioTipoComando_FilaNueva_CreaUnaSolaVezYAdjuntaElTagComoUnchanged()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_tipo_nuevo_{Guid.NewGuid():N}");
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

            int fila = BuscarFilaVacia(grid);
            Assert.IsTrue(fila >= 0, "El grid cargado debe conservar una fila vacía para el alta.");

            BuscarCelda(grid, fila, "Clave")!.Value = "C-100";
            BuscarCelda(grid, fila, "Descripcion")!.Value = "Concepto nuevo de la UI";
            BuscarCelda(grid, fila, "Unidad")!.Value = "pza";

            var tipoCell = BuscarCelda(grid, fila, "Tipo");
            Assert.IsNotNull(tipoCell);
            tipoCell!.Value = "Concepto";

            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoCambioTipo,
                "El alta de la fila debe confirmarse como comando.");

            var tag = grid.Rows[fila].Tag as ConceptoPresupuesto;
            Assert.IsNotNull(tag, "La fila nueva debe quedar asociada al concepto confirmado.");
            Assert.IsTrue(tag!.Id > 0, "El Id se genera en el contexto de escritura.");

            var persistido = LeerConceptoEnBase(dbPath, tag.Id);
            Assert.AreEqual("C-100", persistido.Clave);
            Assert.AreEqual("Concepto nuevo de la UI", persistido.Descripcion);
            Assert.AreEqual("pza", persistido.Unidad);
            Assert.IsFalse(persistido.EsAgrupador);
            Assert.AreEqual(escenario.Concepto.Orden + 1, persistido.Orden,
                "Se conserva la semántica histórica max(Orden) + 1.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tag).State,
                "El Tag adjuntado queda Unchanged para no insertarse otra vez.");

            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El alta no deja trabajo pendiente en el contexto compartido.");

            // Un guardado ajeno posterior no duplica ni reescribe la fila confirmada.
            var fechaConfirmada = persistido.FechaModificacion;
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al alta";
            ctx.SaveChanges();

            var creados = LeerConceptosPorClave(dbPath, "C-100");
            Assert.AreEqual(1, creados.Count, "La fila nueva no se inserta dos veces.");
            Assert.AreEqual(fechaConfirmada, creados[0].FechaModificacion,
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
        public Matriz Matriz { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
        public ConceptoPresupuesto Concepto { get; init; } = null!;
    }

    private static Escenario CrearEscenario(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto comando tipo",
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
            Nivel = 5,
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

    private static ConceptoPresupuesto LeerConceptoEnBase(string dbPath, int conceptoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Single(c => c.Id == conceptoId);
    }

    private static System.Collections.Generic.List<ConceptoPresupuesto> LeerConceptosPorClave(
        string dbPath,
        string clave)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Where(c => c.Clave == clave)
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
