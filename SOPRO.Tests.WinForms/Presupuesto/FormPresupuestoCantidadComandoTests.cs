using System;
using System.Globalization;
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
/// Edición de cantidad de un concepto persistido a través de la ruta de comando
/// (ADR-0001) con el <see cref="FormPresupuesto"/> real sobre un SQLite y un
/// workspace temporales:
/// <list type="bullet">
/// <item>un UPDATE rechazado por la base no escribe ni deja el tracker compartido
/// sucio, de modo que un guardado ajeno posterior no puede confirmarlo;</item>
/// <item>un concepto sin matriz se rechaza con el mismo criterio que el comando;</item>
/// <item>al confirmarse, la base, las celdas visibles, los Tags y los agrupadores
/// quedan sincronizados sin una segunda escritura desde el contexto compartido.</item>
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[TestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class FormPresupuestoCantidadComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";

    [TestMethod]
    public void EdicionCantidadComando_FallaElUpdate_NoEscribeNiEnsuciaElTracker_YElReintentoConfirma()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_cantidad_{Guid.NewGuid():N}");
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

            var workspace = new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local"));

            // El trigger se crea DESPUÉS del constructor: la carga de la vista no falla.
            form = new FormPresupuesto(ctx, escenario.Proyecto, workspace);
            var grid = form.GridPrincipal;

            int filaConcepto = BuscarFilaPorConcepto(grid, escenario.Concepto.Id);
            Assert.IsTrue(filaConcepto >= 0, "El concepto debe estar en el grid cargado.");

            CrearTriggerDeFallo(dbPath);

            // Evento real del grid: el usuario escribe 25 en la celda Cantidad.
            bool eventoDisparado = false;
            grid.CellValueChanged += (_, args) =>
            {
                if (args.RowIndex == filaConcepto) eventoDisparado = true;
            };

            var cantidadCell = BuscarCelda(grid, filaConcepto, "Cantidad");
            Assert.IsNotNull(cantidadCell, "Debe existir la columna Cantidad.");
            cantidadCell!.Value = 25m;

            Assert.IsTrue(eventoDisparado,
                "Asignar la cantidad debe disparar CellValueChanged (ruta real de la UI).");

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoEdicionCantidad,
                "El intento debe clasificarse como fallo revertido por el comando.");

            // El borrador sigue visible, pero nada se dio por guardado.
            Assert.AreEqual(25m, Convert.ToDecimal(cantidadCell.Value, CultureInfo.CurrentCulture),
                "La celda conserva el borrador del usuario tras el fallo.");

            Assert.AreEqual(10m, escenario.Concepto.Cantidad,
                "La ruta de comando no muta el Tag/entidad compartida antes de confirmar.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.Concepto).State,
                "La entidad compartida no queda Modified por un comando fallido.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un comando fallido no deja trabajo pendiente en el contexto compartido.");

            Assert.AreEqual(10m, LeerCantidadEnBase(dbPath, escenario.Concepto.Id),
                "El UPDATE rechazado no puede quedar en la base.");

            // Se retira el trigger y se intenta el guardado ajeno que, con la ruta
            // heredada, arrastraba el UPDATE pendiente.
            EliminarTriggerDeFallo(dbPath);
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al fallo";
            ctx.SaveChanges();

            Assert.AreEqual(10m, LeerCantidadEnBase(dbPath, escenario.Concepto.Id),
                "Un guardado ajeno no puede confirmar el UPDATE abortado.");

            // Reintento del MISMO intento del usuario: la celda aún tiene el borrador,
            // así que se invoca el manejador que consume el evento (asignar el mismo
            // valor no vuelve a disparar CellValueChanged).
            Assert.IsTrue(form.ProcesarEdicionCantidadConComando(filaConcepto),
                "El reintento debe consumirse en la ruta de comando.");
            Assert.AreEqual(BudgetOperationStatus.Committed, form.UltimoEstadoEdicionCantidad,
                "El reintento sin trigger debe confirmarse.");

            var esperado = BudgetRowEditFlowService.HandleQuantityCellChange(
                escenario.Proyecto, escenario.Concepto, 25m.ToString(CultureInfo.CurrentCulture));

            var persistido = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(25m, persistido.Cantidad);
            Assert.AreEqual(100m, persistido.CostoDirectoUnitario, "El costo unitario no se edita aquí.");
            Assert.AreEqual(2500m, persistido.CostoDirectoTotal);
            Assert.AreEqual(esperado.PrecioUnitario, persistido.PrecioUnitario);
            Assert.AreEqual(esperado.Importe, persistido.ImporteTotal);

            var agrupadorPersistido = LeerConceptoEnBase(dbPath, escenario.Agrupador.Id);
            Assert.AreEqual(esperado.Importe, agrupadorPersistido.ImporteTotal,
                "El agrupador confirmado debe reflejar la suma de sus hojas.");
            Assert.AreEqual(esperado.Importe, agrupadorPersistido.CostoDirectoTotal);

            // Celdas visibles de la fila editada.
            Assert.AreEqual(esperado.Cantidad,
                Convert.ToDecimal(BuscarCelda(grid, filaConcepto, "Cantidad")!.Value, CultureInfo.CurrentCulture),
                "La cantidad visible queda normalizada a la precisión del proyecto.");
            Assert.AreEqual(esperado.PrecioUnitario.ToStringImporte(),
                BuscarCelda(grid, filaConcepto, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(esperado.Importe.ToStringImporte(),
                BuscarCelda(grid, filaConcepto, "Importe")!.Value?.ToString());
            Assert.AreEqual(esperado.Total.ToStringImporte(),
                BuscarCelda(grid, filaConcepto, "Total")!.Value?.ToString());

            // Tag y total del agrupador visibles, localizados por Id en el grid.
            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            Assert.IsTrue(filaAgrupador >= 0, "El agrupador debe estar en el grid.");
            Assert.AreEqual(esperado.Importe.ToStringImporte(),
                BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString(),
                "El total del agrupador visible se actualiza con la instantánea confirmada.");

            var tagConcepto = (ConceptoPresupuesto)grid.Rows[filaConcepto].Tag!;
            Assert.AreEqual(25m, tagConcepto.Cantidad);
            Assert.AreEqual(esperado.Importe, tagConcepto.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagConcepto).State,
                "El Tag sincronizado queda Unchanged para no reescribirse.");

            var tagAgrupador = (ConceptoPresupuesto)grid.Rows[filaAgrupador].Tag!;
            Assert.AreEqual(esperado.Importe, tagAgrupador.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagAgrupador).State,
                "El agrupador sincronizado queda Unchanged para no reescribirse.");

            // Un guardado ajeno posterior al éxito tampoco puede volver a escribir la fila.
            var fechaConfirmada = LeerConceptoEnBase(dbPath, escenario.Concepto.Id).FechaModificacion;
            escenario.Proyecto.Descripcion = "cambio ajeno posterior al éxito";
            ctx.SaveChanges();

            var trasGuardadoAjeno = LeerConceptoEnBase(dbPath, escenario.Concepto.Id);
            Assert.AreEqual(25m, trasGuardadoAjeno.Cantidad);
            Assert.AreEqual(fechaConfirmada, trasGuardadoAjeno.FechaModificacion,
                "El contexto compartido no debe reescribir una fila ya confirmada.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagConcepto).State);
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
    public void EdicionCantidadComando_ConceptoSinMatriz_SeRechazaSinEscribirYSinEnsucirElTracker()
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_fp_cantidad_sm_{Guid.NewGuid():N}");
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

            var escenario = CrearEscenario(ctx, incluirConceptoSinMatriz: true);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"),
                Path.Combine(root, "local")));
            var grid = form.GridPrincipal;

            int fila = BuscarFilaPorConcepto(grid, escenario.ConceptoSinMatriz!.Id);
            Assert.IsTrue(fila >= 0, "El concepto sin matriz debe estar en el grid.");

            var cantidadCell = BuscarCelda(grid, fila, "Cantidad");
            Assert.IsNotNull(cantidadCell);
            cantidadCell!.Value = 40m;

            Assert.AreEqual(BudgetOperationStatus.Failed, form.UltimoEstadoEdicionCantidad,
                "Sin matriz el comando rechaza la edición (no se recalculan importes).");
            Assert.AreEqual(4m, escenario.ConceptoSinMatriz.Cantidad,
                "Sin matriz el comando no produce recálculo y la entidad no se toca.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(escenario.ConceptoSinMatriz).State);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "El rechazo del comando no deja trabajo pendiente en el contexto compartido.");
            Assert.AreEqual(4m, LeerCantidadEnBase(dbPath, escenario.ConceptoSinMatriz.Id),
                "No se guarda nada cuando falta la matriz.");
            Assert.AreEqual(40m, Convert.ToDecimal(cantidadCell.Value, CultureInfo.CurrentCulture),
                "El borrador del usuario permanece visible sin afirmar que se guardó.");
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
        public ConceptoPresupuesto? ConceptoSinMatriz { get; init; }
    }

    private static Escenario CrearEscenario(SOPROContext ctx, bool incluirConceptoSinMatriz = false)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto comando cantidad",
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

        ConceptoPresupuesto? conceptoSinMatriz = null;
        if (incluirConceptoSinMatriz)
        {
            conceptoSinMatriz = new ConceptoPresupuesto
            {
                ProyectoId = proyecto.Id,
                Clave = "1.2",
                Descripcion = "Concepto sin matriz",
                EsAgrupador = false,
                Nivel = 1,
                Orden = 2,
                Unidad = "pza",
                ColumnasPersonalizadasJSON = string.Empty,
                Notas = string.Empty,
                Cantidad = 4m,
                MatrizId = null,
                CostoDirectoUnitario = 50m,
                CostoDirectoTotal = 200m,
                PrecioUnitario = 50m,
                ImporteTotal = 200m,
                FechaModificacion = new DateTime(2026, 1, 1),
            };
            ctx.ConceptosPresupuesto.Add(conceptoSinMatriz);
            ctx.SaveChanges();
        }

        return new Escenario
        {
            Proyecto = proyecto,
            Matriz = matriz,
            Agrupador = agrupador,
            Concepto = concepto,
            ConceptoSinMatriz = conceptoSinMatriz,
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

    private static decimal LeerCantidadEnBase(string dbPath, int conceptoId)
    {
        using var verificacion = new SOPROContext(dbPath);
        return verificacion.ConceptosPresupuesto
            .AsNoTracking()
            .Single(c => c.Id == conceptoId)
            .Cantidad;
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
