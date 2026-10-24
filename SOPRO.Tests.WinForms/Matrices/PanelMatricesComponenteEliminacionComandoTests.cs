using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.WinForms.Controls;
using SOPRO.WinForms.Forms;
using SOPRO.WinForms.Helpers;

namespace SOPRO.Tests.WinForms.Matrices;

/// <summary>
/// Ruta de comando (ADR-0001) de ELIMINAR un componente YA persistido desde el
/// botón del panel, con el <see cref="FormPresupuesto"/> real, su
/// <see cref="PanelMatricesEmbebido"/> embebido y un SQLite temporal:
/// <list type="bullet">
/// <item>la eliminación confirma con UNA sola escritura del comando (contexto
/// nuevo): la base pierde la fila, la matriz y sus dos hojas y el agrupador se
/// recalculan, el compartido no emite SaveChanges y queda limpio con el objetivo
/// Detached;</item>
/// <item>un DELETE revertido por trigger conserva la fila, la vista y el
/// tracker, avisa que no se guardó, sin ruta heredada, y el reintento elimina
/// exactamente una vez;</item>
/// <item>un tracker compartido sucio ajeno bloquea el despacho conservando el
/// sucio sin persistirlo y avisa que no se guardó;</item>
/// <item>una eliminación confirmada sincroniza panel y formulario sin segunda
/// escritura</item>.
/// </list>
/// Sólo se usan rutas temporales: nunca datos del usuario.
/// </summary>
[STATestClass]
[DoNotParallelize] // El constructor fija el proyecto global de FormatoHelper.
public class PanelMatricesComponenteEliminacionComandoTests
{
    private const string CampoProyectoGlobalFormato = "_proyectoActual";
    private static readonly Action<string, string, MessageBoxIcon> AvisoPorDefecto =
        (titulo, mensaje, icono) => MessageBox.Show(mensaje, titulo, MessageBoxButtons.OK, icono);

    [TestMethod]
    public void EliminarComponentePersistido_UnaEscritura_TargetDetachedYTrackerLimpio()
    {
        var root = CrearRaiz("elim_ok");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoEliminacionComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));

            var compMaterial = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaMaterial = BuscarFilaComponente(dgvComp, compMaterial.Id);
            Assert.IsTrue(filaMaterial >= 0);
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(), "Antes de eliminar el tracker compartido debe estar limpio.");

            bool eventoEmitido = false;
            BudgetMatrixComponentDeletionSnapshot? snapshotRecibida = null;
            panel.ComponenteMatrizEliminadoConfirmado += (_, s) => { eventoEmitido = true; snapshotRecibida = s; };

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            bool consumida = panel.ProcesarEliminacionComponentePersistida(filaMaterial);
            Assert.IsTrue(consumida, "La fila persistida debe consumirse en la ruta de comando.");
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEliminacionComponente,
                panel.UltimaRazonEliminacionSinDespachar);
            Assert.AreEqual(0, guardasCompartidos,
                "El compartido jamás emite SaveChanges: la escritura la hizo el comando en un contexto nuevo.");
            Assert.IsTrue(eventoEmitido, "FormPresupuesto debe recibir la instantánea confirmada.");
            Assert.IsNotNull(snapshotRecibida);

            // La base perdió la fila y la matriz quedó con el componente restante.
            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsFalse(verificacion.ComponentesMatriz.AsNoTracking()
                    .Any(c => c.Id == compMaterial.Id), "El DELETE del comando debe cerrar la transacción.");
                Assert.AreEqual(1, verificacion.ComponentesMatriz.AsNoTracking()
                    .Count(c => c.MatrizId == escenario.MatrizApu.Id));
                Assert.AreEqual(30m, verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);

                var hoja1Db = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja1.Id);
                var hoja2Db = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Hoja2.Id);
                Assert.AreEqual(30m, hoja1Db.CostoDirectoUnitario);
                Assert.AreEqual(300m, hoja1Db.ImporteTotal);
                Assert.AreEqual(30m, hoja2Db.CostoDirectoUnitario, "TODAS las hojas con la matriz se recalculan.");
                Assert.AreEqual(150m, hoja2Db.ImporteTotal);

                var agrupadorDb = verificacion.ConceptosPresupuesto.AsNoTracking().Single(c => c.Id == escenario.Agrupador.Id);
                Assert.AreEqual(450m, agrupadorDb.ImporteTotal);
                Assert.AreEqual(450m, agrupadorDb.CostoDirectoTotal);
            }

            // El compartido queda limpio: sin trabajo pendiente, sin Deleted, y
            // ninguna entidad del componente eliminado tracked.
            var sucios = ctx.ChangeTracker.Entries()
                .Where(e => e.State != EntityState.Unchanged && e.State != EntityState.Detached)
                .Select(e => $"{e.Entity.GetType().Name}#{(e.Entity as Matriz)?.Id}{(e.Entity as ComponenteMatriz)?.Id}#{e.State}")
                .ToList();
            Assert.IsFalse(sucios.Any(),
                $"La ruta de comando no deja trabajo pendiente en el compartido. Sucios: {string.Join("; ", sucios)}");
            Assert.IsFalse(ctx.ChangeTracker.Entries<ComponenteMatriz>().Any(e => e.Entity.Id == compMaterial.Id),
                "La entidad compartida del componente eliminado debe quedar Detached.");

            // Vista del panel: fila removida, totals desde lo confirmado.
            Assert.AreEqual(-1, BuscarFilaComponente(dgvComp, compMaterial.Id),
                "La fila del componente eliminado desaparece del grid.");
            StringAssert.Contains(panel.LblDirInterno.Text, 30m.ToStringImporte(),
                "El costo directo del panel refleja la instantánea confirmada.");

            // Hojas y agrupador visibles en el grid del presupuesto, aplicadas por Id.
            int filaHoja = BuscarFilaPorConcepto(grid, escenario.Hoja1.Id);
            int filaHoja2 = BuscarFilaPorConcepto(grid, escenario.Hoja2.Id);
            int filaAgrupador = BuscarFilaPorConcepto(grid, escenario.Agrupador.Id);
            Assert.AreEqual(30m.ToStringImporte(), BuscarCelda(grid, filaHoja, "PrecioUnitario")!.Value?.ToString());
            Assert.AreEqual(300m.ToStringImporte(), BuscarCelda(grid, filaHoja, "Importe")!.Value?.ToString());
            Assert.AreEqual(150m.ToStringImporte(), BuscarCelda(grid, filaHoja2, "Importe")!.Value?.ToString());
            Assert.AreEqual(450m.ToStringImporte(), BuscarCelda(grid, filaAgrupador, "Importe")!.Value?.ToString());

            var tagHoja1 = (ConceptoPresupuesto)grid.Rows[filaHoja].Tag!;
            var tagAgrupador = (ConceptoPresupuesto)grid.Rows[filaAgrupador].Tag!;
            Assert.AreEqual(300m, tagHoja1.ImporteTotal);
            Assert.AreEqual(450m, tagAgrupador.ImporteTotal);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagHoja1).State);
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(tagAgrupador).State);

            Assert.AreEqual(0, avisos.Count, "La ruta confirmada no muestra avisos.");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoEliminacionComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EliminarComponentePersistido_RollbackPorTrigger_ConservaFilaYElReintentoEliminaUnaVez()
    {
        var root = CrearRaiz("elim_rollback");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoEliminacionComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));
            var compMaterial = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaMaterial = BuscarFilaComponente(dgvComp, compMaterial.Id);

            bool eventoEmitido = false;
            panel.ComponenteMatrizEliminadoConfirmado += (_, __) => eventoEmitido = true;

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;

            CrearTriggerDeFallo(dbPath);

            bool consumida = panel.ProcesarEliminacionComponentePersistida(filaMaterial);
            Assert.IsTrue(consumida);
            Assert.AreEqual(BudgetOperationStatus.Failed, panel.UltimoEstadoEliminacionComponente,
                "El intento debe clasificarse como fallo revertido por el comando. " +
                panel.UltimaRazonEliminacionSinDespachar);
            Assert.IsFalse(eventoEmitido, "Un fallo no emite instantánea.");
            Assert.AreEqual(0, guardasCompartidos, "Sin guardado heredado ni desde el comando.");

            // Fila y vista intactas; tracker limpio; nada escrito.
            Assert.IsTrue(filaMaterial >= 0 && filaMaterial < dgvComp.Rows.Count
                && Convert.ToInt32(dgvComp.Rows[filaMaterial].Cells["ColId"].Value) == compMaterial.Id,
                "La fila del componente se conserva tras el rollback.");
            StringAssert.Contains(panel.LblDirInterno.Text, 150m.ToStringImporte(),
                "El costo directo mostrado se conserva tras el rollback.");
            Assert.AreEqual(2, dgvComp.Rows.Cast<DataGridViewRow>()
                .Count(r => r.Cells["ColId"].Value != null),
                "Ambos componentes siguen pintados.");
            Assert.IsFalse(ctx.ChangeTracker.HasChanges(),
                "Un fallo revertido no deja trabajo pendiente en el compartido.");
            Assert.AreEqual(EntityState.Unchanged, ctx.Entry(compMaterial).State);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsTrue(verificacion.ComponentesMatriz.AsNoTracking().Any(c => c.Id == compMaterial.Id),
                    "El DELETE revertido no puede perder la fila.");
                Assert.AreEqual(150m, verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
                Assert.AreEqual(1500m, verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
            }

            Assert.AreEqual(1, avisos.Count, "El usuario debe ver que no se eliminó.");
            StringAssert.Contains(avisos[0], "no se confirmó",
                "El aviso del comando fallido comunica que no se guardó nada.");

            EliminarTriggerDeFallo(dbPath);

            // Reintento del mismo componente: UN solo commit, sin duplicar.
            guardasCompartidos = 0;
            bool consumidaReintento = panel.ProcesarEliminacionComponentePersistida(filaMaterial);
            Assert.IsTrue(consumidaReintento);
            Assert.AreEqual(BudgetOperationStatus.Committed, panel.UltimoEstadoEliminacionComponente,
                panel.UltimaRazonEliminacionSinDespachar);
            Assert.AreEqual(0, guardasCompartidos);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsFalse(verificacion.ComponentesMatriz.AsNoTracking().Any(c => c.Id == compMaterial.Id),
                    "El reintento elimina el componente exactamente una vez.");
                Assert.AreEqual(30m, verificacion.Matrices.AsNoTracking()
                    .Single(m => m.Id == escenario.MatrizApu.Id).CostoDirecto);
                Assert.AreEqual(300m, verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal);
            }

            Assert.AreEqual(-1, BuscarFilaComponente(dgvComp, compMaterial.Id));
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoEliminacionComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    [TestMethod]
    public void EliminarComponentePersistido_TrackerSucioAjeno_BloqueaSinPersistirYAvisaQueNoGuardo()
    {
        var root = CrearRaiz("elim_sucio");
        var dbPath = Path.Combine(root, "proyecto-temporal.soproproj");
        var avisos = new List<string>();
        PanelMatricesEmbebido.MostrarAvisoEliminacionComponente = (_, m, _) => avisos.Add(m);

        SOPROContext? ctx = null;
        FormPresupuesto? form = null;
        var proyectoGlobalPrevio = LeerProyectoGlobalFormato();
        try
        {
            ctx = new SOPROContext(dbPath);
            ctx.Database.EnsureDeleted();
            ctx.Database.EnsureCreated();

            var escenario = CrearEscenarioDosHojas(ctx);

            form = new FormPresupuesto(ctx, escenario.Proyecto, new ProjectWorkspaceService(
                Path.Combine(root, "sopro"), Path.Combine(root, "local")));
            var grid = form.GridPrincipal;
            var panel = form.PanelMatricesInterno!;
            var dgvComp = panel.DgvComponentesInterno;

            panel.NotificarFilaCambiada(BuscarFilaPorConcepto(grid, escenario.Hoja1.Id));
            var compMaterial = escenario.MatrizApu.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
            int filaMaterial = BuscarFilaComponente(dgvComp, compMaterial.Id);

            bool eventoEmitido = false;
            panel.ComponenteMatrizEliminadoConfirmado += (_, __) => eventoEmitido = true;

            // Trabajo pendiente ajeno en el contexto compartido.
            escenario.Agrupador.Notas = "ajena pendiente";
            ctx.ChangeTracker.DetectChanges();
            Assert.IsTrue(ctx.ChangeTracker.HasChanges());

            int guardasCompartidos = 0;
            ctx.SavedChanges += (_, __) => guardasCompartidos++;
            Assert.IsTrue(ctx.ChangeTracker.HasChanges());

            bool consumida = panel.ProcesarEliminacionComponentePersistida(filaMaterial);
            Assert.IsTrue(consumida, "La fila persistida no cae a la ruta heredada.");
            Assert.IsNull(panel.UltimoEstadoEliminacionComponente,
                "El comando no llegó a enviarse por el trabajo ajeno pendiente.");
            Assert.IsFalse(eventoEmitido);
            Assert.AreEqual(0, guardasCompartidos, "El bloqueo no persiste el sucio ajeno.");
            Assert.IsTrue(ctx.ChangeTracker.HasChanges(),
                "El sucio ajeno se conserva sin tocar ni persistir.");
            Assert.AreEqual("ajena pendiente", escenario.Agrupador.Notas,
                "El trabajo pendiente ajeno se preserva intacto.");
            Assert.AreEqual(EntityState.Modified, ctx.Entry(escenario.Agrupador).State);

            // La fila y el componente visibles se conservan.
            Assert.IsTrue(filaMaterial >= 0 && filaMaterial < dgvComp.Rows.Count
                && Convert.ToInt32(dgvComp.Rows[filaMaterial].Cells["ColId"].Value) == compMaterial.Id,
                "La fila del componente se conserva tras el bloqueo.");
            Assert.IsFalse(ctx.ChangeTracker.Entries<ComponenteMatriz>()
                .Any(e => e.Entity.Id == compMaterial.Id && e.State == EntityState.Deleted),
                "La ruta bloqueada nunca marca el componente como Deleted.");
            Assert.AreEqual("Contexto compartido sucio", panel.UltimaRazonEliminacionSinDespachar,
                "El bloqueo se clasifica antes de construir el comando.");

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.IsTrue(verificacion.ComponentesMatriz.AsNoTracking().Any(c => c.Id == compMaterial.Id),
                    "Nada se escribió en la base.");
                Assert.AreEqual(1500m, verificacion.ConceptosPresupuesto.AsNoTracking()
                    .Single(c => c.Id == escenario.Hoja1.Id).ImporteTotal,
                    "La hoja conserva su estado previo.");
            }

            Assert.AreEqual(1, avisos.Count, "Debe avisarse al usuario que no se guardó.");
            StringAssert.Contains(avisos[0], "no se eliminó");
        }
        finally
        {
            PanelMatricesEmbebido.MostrarAvisoEliminacionComponente = AvisoPorDefecto;
            form?.Dispose();
            ctx?.Dispose();
            RestaurarProyectoGlobalFormato(proyectoGlobalPrevio);
            EliminarDirectorio(root);
        }
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private sealed class EscenarioPanel
    {
        public Proyecto Proyecto { get; init; } = null!;
        public Matriz MatrizApu { get; init; } = null!;
        public ConceptoPresupuesto Hoja1 { get; init; } = null!;
        public ConceptoPresupuesto Hoja2 { get; init; } = null!;
        public ConceptoPresupuesto Agrupador { get; init; } = null!;
    }

    private static EscenarioPanel CrearEscenarioDosHojas(SOPROContext ctx)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto panel eliminación",
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

        var material = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-CEM",
            Descripcion = "Cemento gris",
            Unidad = "kg",
            PrecioUnitario = 60m,
            Notas = string.Empty,
        };
        var oficial = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = "MO-OF",
            Descripcion = "Oficial albañil",
            Unidad = "jor",
            SalarioBase = 30m,
            FactorSalarioReal = 1m,
            SalarioReal = 30m,
            Notas = string.Empty,
        };
        ctx.Materiales.Add(material);
        ctx.ManoDeObra.Add(oficial);
        ctx.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto simple",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty,
        };
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Material = material,
            Cantidad = 2m,
            Importe = 120m,
            Orden = 1,
            Notas = string.Empty,
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = oficial.Id,
            ManoDeObra = oficial,
            Cantidad = 1m,
            Importe = 30m,
            Orden = 2,
            Notas = string.Empty,
        });
        matriz.CostoDirecto = 150m;
        ctx.SaveChanges();

        ConceptoPresupuesto CrearHoja(string clave, decimal cantidad, decimal costo, int orden) => new()
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = $"Hoja {clave}",
            EsAgrupador = false,
            Nivel = 1,
            Orden = orden,
            Unidad = "m3",
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
            Cantidad = cantidad,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = costo,
            CostoDirectoTotal = cantidad * costo,
            PrecioUnitario = costo,
            ImporteTotal = cantidad * costo,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

        var hoja1 = CrearHoja("C-001", 10m, 150m, 1);
        var hoja2 = CrearHoja("C-002", 5m, 150m, 2);
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
            CostoDirectoTotal = 2250m,
            PrecioUnitario = 0m,
            ImporteTotal = 2250m,
            FechaModificacion = new DateTime(2026, 1, 1),
        };
        ctx.ConceptosPresupuesto.AddRange(hoja1, hoja2, agrupador);
        ctx.SaveChanges();

        return new EscenarioPanel
        {
            Proyecto = proyecto,
            MatrizApu = matriz,
            Hoja1 = hoja1,
            Hoja2 = hoja2,
            Agrupador = agrupador,
        };
    }

    private static void CrearTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath,
            "CREATE TRIGGER trg_fail_componente_eliminacion BEFORE DELETE ON ComponentesMatriz " +
            "BEGIN SELECT RAISE(ABORT, 'fallo_simulado'); END;");

    private static void EliminarTriggerDeFallo(string dbPath)
        => EjecutarSql(dbPath, "DROP TRIGGER trg_fail_componente_eliminacion;");

    private static void EjecutarSql(string dbPath, string sql)
    {
        using var admin = new SOPROContext(dbPath);
        admin.Database.ExecuteSqlRaw(sql);
    }

    private static int BuscarFilaComponente(DataGridView dgv, int componenteId)
    {
        for (int i = 0; i < dgv.Rows.Count; i++)
        {
            var cell = dgv.Rows[i].Cells["ColId"];
            if (cell?.Value != null && Convert.ToInt32(cell.Value) == componenteId)
                return i;
        }

        return -1;
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

    private static string CrearRaiz(string nombre)
    {
        var root = Path.Combine(Path.GetTempPath(), $"sopro_elim_{nombre}_{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        return root;
    }

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
