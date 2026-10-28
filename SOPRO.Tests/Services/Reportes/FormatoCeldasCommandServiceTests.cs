using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Application.Models.Reporting.ReportColumns;
using SOPRO.Application.Services.Reporting;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Reportes;

/// <summary>
/// Contrato del comando ÚNICO de escritura del formato por celda (ADR-0001):
/// contexto nuevo por operación sobre la ruta real, base esperada EXACTA por
/// dirección, lote todo-o-nada, operaciones secuenciales sobre la misma clave,
/// borrar+recrear sin violar el índice único, un solo SaveChanges, aislamiento
/// por proyecto/tipo/vista y clasificación de resultado incierto sin reintento.
/// No usa bases reales del usuario.
/// </summary>
[TestClass]
public class FormatoCeldasCommandServiceTests
{
    private const string Tipo = "Presupuesto";

    // ─────────────────────────── Éxito parcial y borrado ───────────────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_ParcialYCrearYBorrar_PersisteSoloLoPropuesto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            var existente = new ReportStyleOverlay(Negrita: true, ColorFondo: "#111111");

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto parcial").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal", existente));
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:4", "ImporteTotal",
                    new ReportStyleOverlay(Cursiva: true)));
                seed.SaveChanges();
            }

            var comandos = new List<CambioFormatoCeldaCommand>
            {
                // Parcial sobre fila existente: sólo cambia el fondo, conserva Negrita.
                new(string.Empty, "C:1", "ImporteTotal", existente,
                    new ReportStyleOverlay(ColorFondo: "#222222")),
                // Crear desde cero.
                new(string.Empty, "C:2", "ImporteTotal", null,
                    new ReportStyleOverlay(Cursiva: true)),
                // Overlay vacío sobre una celda inexistente: no crea nada.
                new(string.Empty, "C:3", "ImporteTotal", null, new ReportStyleOverlay()),
                // Overlay null sobre una fila existente: la borra.
                new(string.Empty, "C:4", "ImporteTotal",
                    new ReportStyleOverlay(Cursiva: true), null),
            };

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
            Assert.IsNotNull(resultado.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var filas = verificacion.FormatosCeldaReporte.AsNoTracking()
                .Where(f => f.ProyectoId == proyectoId && f.TipoReporte == Tipo)
                .ToList();

            Assert.AreEqual(2, filas.Count, "Sólo C:1 y C:2 deben existir.");
            var fila1 = filas.Single(f => f.ClaveFila == "C:1");
            Assert.AreEqual("#222222", fila1.ColorFondo);
            Assert.IsTrue(fila1.Negrita, "La propiedad no informada del overlay parcial se conserva.");
            Assert.IsTrue(filas.Single(f => f.ClaveFila == "C:2").Cursiva);
            Assert.IsFalse(filas.Any(f => f.ClaveFila == "C:3" || f.ClaveFila == "C:4"));

            var snapshot = resultado.Confirmed!;
            Assert.AreEqual(2, snapshot.Overlays.Count);
            Assert.IsFalse(snapshot.Overlays.ContainsKey(new ReportCellAddress(string.Empty, "C:4", "ImporteTotal")));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────────────── Base esperada: todo o nada ──────────────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_UnaBaseDistinta_AbortaTodoElLoteSinEscribir()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            var baseVieja = new ReportStyleOverlay(ColorFondo: "#111111");
            var baseActual = new ReportStyleOverlay(ColorFondo: "#999999");

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto conflicto").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal", baseActual));
                seed.SaveChanges();
            }

            var comandos = new List<CambioFormatoCeldaCommand>
            {
                // Este cambio SÍ es válido y sería el primero en aplicarse.
                new(string.Empty, "C:2", "ImporteTotal", null,
                    new ReportStyleOverlay(ColorFondo: "#00FF00")),
                // Este otro tiene una base vieja: debe invalidar TODO el lote.
                new(string.Empty, "C:1", "ImporteTotal", baseVieja,
                    new ReportStyleOverlay(ColorFondo: "#FF0000")),
            };

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado.Status);
            Assert.IsNull(resultado.Confirmed);
            Assert.IsNotNull(resultado.Message);

            using var verificacion = new SOPROContext(dbPath);
            var filas = verificacion.FormatosCeldaReporte.AsNoTracking()
                .Where(f => f.ProyectoId == proyectoId)
                .ToList();

            Assert.AreEqual(1, filas.Count, "Ni C:2 (válido) ni la actualización de C:1 deben persistirse.");
            Assert.AreEqual("C:1", filas[0].ClaveFila);
            Assert.AreEqual("#999999", filas[0].ColorFondo, "La fila en conflicto queda intacta.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_EsperadoNullPeroFilaExiste_DevuelveConflicto()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto esperado null").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal",
                    new ReportStyleOverlay(Negrita: true)));
                seed.SaveChanges();
            }

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand(string.Empty, "C:1", "ImporteTotal", null,
                        new ReportStyleOverlay(ColorFondo: "#123456")),
                });

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado.Status);
            Assert.IsNull(resultado.Confirmed);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────────────── Aislamiento por proyecto/tipo/vista ─────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_SoloTocaElProyectoTipoYVistaPedidos()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoA;
            int proyectoB;
            var original = new ReportStyleOverlay(ColorFondo: "#010101");
            var pedida = new ReportStyleOverlay(ColorFondo: "#020202");

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoA = AddProject(seed, "Proyecto A").Id;
                proyectoB = AddProject(seed, "Proyecto B").Id;

                seed.FormatosCeldaReporte.Add(Row(proyectoA, Tipo, "V1", "C:1", "ImporteTotal", pedida));
                seed.FormatosCeldaReporte.Add(Row(proyectoA, Tipo, "V2", "C:1", "ImporteTotal", original));
                seed.FormatosCeldaReporte.Add(Row(proyectoA, "APU", "V1", "C:1", "ImporteTotal", original));
                seed.FormatosCeldaReporte.Add(Row(proyectoB, Tipo, "V1", "C:1", "ImporteTotal", original));
                seed.SaveChanges();
            }

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoA, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand("V1", "C:1", "ImporteTotal", pedida,
                        new ReportStyleOverlay(ColorFondo: "#0A0B0C")),
                });

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

            using var verificacion = new SOPROContext(dbPath);
            var filas = verificacion.FormatosCeldaReporte.AsNoTracking().ToList();

            Assert.AreEqual("#0A0B0C", filas.Single(f =>
                f.ProyectoId == proyectoA && f.TipoReporte == Tipo && f.ClaveVista == "V1").ColorFondo);
            Assert.AreEqual("#010101", filas.Single(f =>
                f.ProyectoId == proyectoA && f.TipoReporte == Tipo && f.ClaveVista == "V2").ColorFondo);
            Assert.AreEqual("#010101", filas.Single(f =>
                f.ProyectoId == proyectoA && f.TipoReporte == "APU").ColorFondo);
            Assert.AreEqual("#010101", filas.Single(f =>
                f.ProyectoId == proyectoB && f.TipoReporte == Tipo).ColorFondo);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────── Operaciones secuenciales y borrar+recrear ───────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_MismaDireccionSecuencial_CoalesceSinViolarIndice()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            var original = new ReportStyleOverlay(ColorFondo: "#111111");
            var paso1 = new ReportStyleOverlay(ColorFondo: "#222222");
            var paso2 = new ReportStyleOverlay(ColorFondo: "#333333");

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto secuencial").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal", original));
                seed.SaveChanges();
            }

            var comandos = new List<CambioFormatoCeldaCommand>
            {
                // Actualizar.
                new(string.Empty, "C:1", "ImporteTotal", original, paso1),
                // Volver a actualizar sobre el resultado simulado de la anterior.
                new(string.Empty, "C:1", "ImporteTotal", paso1, paso2),
                // BORRAR y RECREAR la MISMA clave única dentro del mismo lote.
                new(string.Empty, "C:1", "ImporteTotal", paso2, null),
                new(string.Empty, "C:1", "ImporteTotal", null, new ReportStyleOverlay(Negrita: true)),
            };

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

            using var verificacion = new SOPROContext(dbPath);
            var filas = verificacion.FormatosCeldaReporte.AsNoTracking()
                .Where(f => f.ProyectoId == proyectoId)
                .ToList();

            Assert.AreEqual(1, filas.Count, "La clave única debe terminar en UNA sola fila.");
            Assert.IsTrue(filas[0].Negrita);
            Assert.IsNull(filas[0].ColorFondo, "La recreación parte de cero, sin arrastrar el fondo anterior.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────────────── Un solo SaveChanges por lote ────────────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_LoteDe25_UsaUnSoloSaveChangesYUnaTransaccion()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto save único").Id;
            }

            var comandos = Enumerable.Range(1, 25)
                .Select(i => new CambioFormatoCeldaCommand(
                    string.Empty,
                    ClaveFilaReporte.Concepto(i),
                    "ImporteTotal",
                    null,
                    new ReportStyleOverlay(ColorFondo: "#0A0B0C")))
                .ToList();

            var factory = new CountingDbContextFactory();
            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                factory, dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
            Assert.IsNotNull(factory.Last);
            Assert.AreEqual(1, factory.Last!.SaveChangesCount, "El lote debe confirmar con UN solo SaveChanges.");

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(25, verificacion.FormatosCeldaReporte.AsNoTracking()
                .Count(f => f.ProyectoId == proyectoId));
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────────────── Fallo y rollback total ──────────────────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_TriggerQueAborta_SegundoCambio_RevierteTodoElLote()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto trigger").Id;
                seed.Database.ExecuteSqlRaw(
                    "CREATE TRIGGER trg_fail_formato BEFORE INSERT ON FormatosCeldaReporte " +
                    "WHEN NEW.ClaveFila = 'C:2' " +
                    "BEGIN SELECT RAISE(ABORT, 'prueba de rollback'); END;");
            }

            var comandos = new List<CambioFormatoCeldaCommand>
            {
                new(string.Empty, "C:1", "ImporteTotal", null, new ReportStyleOverlay(ColorFondo: "#111111")),
                new(string.Empty, "C:2", "ImporteTotal", null, new ReportStyleOverlay(ColorFondo: "#222222")),
                new(string.Empty, "C:3", "ImporteTotal", null, new ReportStyleOverlay(ColorFondo: "#333333")),
            };

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.Failed, resultado.Status);
            Assert.IsNull(resultado.Confirmed);
            Assert.IsNotNull(resultado.Diagnostic);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(0, verificacion.FormatosCeldaReporte.AsNoTracking()
                .Count(f => f.ProyectoId == proyectoId),
                "El rollback debe deshacer también los INSERT ya ejecutados del lote.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────────── Contexto compartido intacto ─────────────────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_NoTocaElChangeTrackerDelContextoDeLaVista()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            int conceptoId;

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto contexto").Id;
                var concepto = AddConcept(seed, proyectoId, "C-EDIT");
                seed.SaveChanges();
                conceptoId = concepto.Id;
            }

            using var compartido = new CountingContext(dbPath);
            var conceptoCompartido = compartido.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
            conceptoCompartido.Descripcion = "Cambio local sin guardar";
            Assert.AreEqual(EntityState.Modified, compartido.Entry(conceptoCompartido).State);

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand(string.Empty, "C:1", "ImporteTotal", null,
                        new ReportStyleOverlay(Negrita: true)),
                });

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);
            Assert.AreEqual(EntityState.Modified, compartido.Entry(conceptoCompartido).State,
                "El concepto pendiente sigue Modified: el comando no hizo Clear ni SaveChanges sobre el contexto de la vista.");
            Assert.AreEqual(0, compartido.SaveChangesCount, "El comando no guarda el trabajo pendiente de la vista.");
            Assert.AreEqual(0, compartido.FormatosCeldaReporte.Local.Count,
                "El contexto compartido no recibe las entidades del comando.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────────── Resultado incierto: sin reintento ───────────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_CommitConDisposeQueFalla_DevuelveOutcomeUnknownYNoDuplica()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto incierto").Id;
            }

            var comandos = new[]
            {
                new CambioFormatoCeldaCommand(string.Empty, "C:1", "ImporteTotal", null,
                    new ReportStyleOverlay(ColorFondo: "#ABCDEF")),
            };

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new DisposeThrowingFactory(dbPath), dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.OutcomeUnknown, resultado.Status);
            Assert.IsNull(resultado.Confirmed);
            Assert.IsNotNull(resultado.Diagnostic);

            using (var verificacion = new SOPROContext(dbPath))
            {
                Assert.AreEqual(1, verificacion.FormatosCeldaReporte.AsNoTracking()
                    .Count(f => f.ProyectoId == proyectoId),
                    "El commit sí ocurrió antes de que fallara el cierre del contexto.");
            }

            // Un reintento a ciegas con la MISMA base esperada detecta el cambio y no
            // duplica ni pisa: la vista debe reconciliar antes de reintentar.
            var reintento = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.Conflict, reintento.Status);
            Assert.IsNull(reintento.Confirmed);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────── Reemplazo EXACTO del overlay (Undo/Redo de formato) ──────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_ReplaceQuitaLasPropiedadesQueLaEdicionAnadio()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            var direccion = new ReportCellAddress(string.Empty, "C:1", "ImporteTotal");
            // Estado persistido tras la edición: la celda preexistente (fondo) ganó Negrita.
            var editado = new ReportStyleOverlay(Negrita: true, ColorFondo: "#111111");
            // Estado anterior COMPLETO que debe restaurar el undo.
            var anterior = new ReportStyleOverlay(ColorFondo: "#111111");

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto replace").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal", editado));
                seed.SaveChanges();
            }

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand(
                        string.Empty, "C:1", "ImporteTotal", editado, anterior, OverlayApplyMode.Replace),
                });

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

            using var verificacion = new SOPROContext(dbPath);
            var fila = verificacion.FormatosCeldaReporte.AsNoTracking()
                .Single(f => f.ProyectoId == proyectoId && f.TipoReporte == Tipo);

            Assert.AreEqual("#111111", fila.ColorFondo, "La propiedad conservada por el undo sigue ahí.");
            Assert.IsNull(fila.Negrita,
                "El undo debe QUITAR la propiedad añadida por la edición, no fusionarla.");
            Assert.AreEqual(anterior, resultado.Confirmed!.Overlays[direccion],
                "El snapshot confirmado coincide con el estado exacto persistido.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_ReplaceConPropuestoNull_BorraLaFila()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            var existente = new ReportStyleOverlay(Negrita: true, ColorFondo: "#222222");

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto replace null").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal", existente));
                seed.SaveChanges();
            }

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand(
                        string.Empty, "C:1", "ImporteTotal", existente, null, OverlayApplyMode.Replace),
                });

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

            using var verificacion = new SOPROContext(dbPath);
            Assert.AreEqual(0, verificacion.FormatosCeldaReporte.AsNoTracking()
                .Count(f => f.ProyectoId == proyectoId && f.TipoReporte == Tipo),
                "Replace con null borra el override completo.");
            Assert.AreEqual(0, resultado.Confirmed!.Overlays.Count);
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_ReplaceConBaseDistinta_NoBorraNiEscribe()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            var persistido = new ReportStyleOverlay(Negrita: true);

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto replace conflicto").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal", persistido));
                seed.SaveChanges();
            }

            // Base esperada obsoleta (la vista cree que no hay override): el borrado
            // por Replace NO debe ejecutarse.
            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand(
                        string.Empty, "C:1", "ImporteTotal", null, null, OverlayApplyMode.Replace),
                });

            Assert.AreEqual(BudgetOperationStatus.Conflict, resultado.Status);
            Assert.IsNull(resultado.Confirmed);

            using var verificacion = new SOPROContext(dbPath);
            var fila = verificacion.FormatosCeldaReporte.AsNoTracking()
                .Single(f => f.ProyectoId == proyectoId && f.TipoReporte == Tipo);
            Assert.IsTrue(fila.Negrita, "Con conflicto la fila queda intacta.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_MergeEsElModoPorDefecto()
    {
        var comando = new CambioFormatoCeldaCommand(
            string.Empty, "C:1", "ImporteTotal", null, new ReportStyleOverlay(Negrita: true));

        Assert.AreEqual(OverlayApplyMode.Merge, comando.ApplyMode,
            "La edición normal sigue fusionando el overlay parcial por defecto.");
    }

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_ReplaceYMergeSecuenciales_SeEncadenanSobreElEstadoSimulado()
    {
        var dbPath = TestDbFactory.CreateTempDbPath();
        try
        {
            int proyectoId;
            var inicial = new ReportStyleOverlay(Negrita: true, ColorFondo: "#111111");

            using (var seed = TestDbFactory.CreateContextAt(dbPath))
            {
                proyectoId = AddProject(seed, "Proyecto secuencial replace").Id;
                seed.FormatosCeldaReporte.Add(Row(proyectoId, Tipo, string.Empty, "C:1", "ImporteTotal", inicial));
                seed.SaveChanges();
            }

            var comandos = new List<CambioFormatoCeldaCommand>
            {
                // 1) Replace: el estado exacto deja fuera la Negrita persistida.
                new(string.Empty, "C:1", "ImporteTotal", inicial,
                    new ReportStyleOverlay(ColorFondo: "#222222"), OverlayApplyMode.Replace),
                // 2) Merge sobre el resultado simulado de la anterior: conserva el fondo.
                new(string.Empty, "C:1", "ImporteTotal",
                    new ReportStyleOverlay(ColorFondo: "#222222"),
                    new ReportStyleOverlay(Cursiva: true)),
            };

            var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), dbPath, proyectoId, Tipo, comandos);

            Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status, resultado.Message);

            using var verificacion = new SOPROContext(dbPath);
            var fila = verificacion.FormatosCeldaReporte.AsNoTracking()
                .Single(f => f.ProyectoId == proyectoId && f.TipoReporte == Tipo);

            Assert.AreEqual("#222222", fila.ColorFondo, "El Merge posterior ve el resultado del Replace.");
            Assert.IsTrue(fila.Cursiva);
            Assert.IsNull(fila.Negrita, "El Replace secuencial quitó la propiedad no informada.");
            Assert.AreEqual(1, verificacion.FormatosCeldaReporte.AsNoTracking()
                .Count(f => f.ProyectoId == proyectoId), "La clave única sigue en UNA sola fila.");
        }
        finally
        {
            DeleteQuietly(dbPath);
        }
    }

    // ───────────────────────── Validación de comandos ──────────────────────────

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_ComandoMalformado_LanzaSinAbrirLaBase()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(() =>
            FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), "ignorada.db", 0, Tipo, Array.Empty<CambioFormatoCeldaCommand>()));

        Assert.ThrowsExactly<ArgumentException>(() =>
            FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), "ignorada.db", 1, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand(string.Empty, " ", "ImporteTotal", null, null),
                }));

        Assert.ThrowsExactly<ArgumentException>(() =>
            FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
                new ProjectDbContextFactory(), "ignorada.db", 1, Tipo,
                new[]
                {
                    new CambioFormatoCeldaCommand(string.Empty, "C:1", " ", null, null),
                }));
    }

    [TestMethod]
    public void ExecuteAplicarCeldasCommand_LoteVacio_EsCommittedSinAbrirLaBase()
    {
        var resultado = FormatoCeldasCommandService.ExecuteAplicarCeldasCommand(
            new ThrowingFactory(), "ignorada.db", 1, Tipo, Array.Empty<CambioFormatoCeldaCommand>());

        Assert.AreEqual(BudgetOperationStatus.Committed, resultado.Status);
        Assert.IsNotNull(resultado.Confirmed);
        Assert.AreEqual(0, resultado.Confirmed!.Overlays.Count);
    }

    // ───────────────────────────── Infraestructura ─────────────────────────────

    private static Proyecto AddProject(SOPROContext context, string nombre)
    {
        var proyecto = new Proyecto
        {
            Nombre = nombre,
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
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static ConceptoPresupuesto AddConcept(SOPROContext context, int proyectoId, string clave)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = clave,
            Descripcion = "Concepto de prueba",
            Unidad = "pza",
            Cantidad = 1m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty,
        };

        context.ConceptosPresupuesto.Add(concepto);
        return concepto;
    }

    private static FormatoCeldaReporte Row(
        int proyectoId,
        string tipoReporte,
        string vista,
        string claveFila,
        string columna,
        ReportStyleOverlay overlay)
        => new()
        {
            ProyectoId = proyectoId,
            TipoReporte = tipoReporte,
            ClaveVista = vista,
            ClaveFila = claveFila,
            NombreColumna = columna,
            Fuente = overlay.Fuente,
            TamanoFuente = overlay.TamanoFuente,
            Negrita = overlay.Negrita,
            Cursiva = overlay.Cursiva,
            ColorFuente = overlay.ColorFuente,
            ColorFondo = overlay.ColorFondo,
            AlineacionHorizontal = overlay.AlineacionHorizontal?.ToString(),
            AlineacionVertical = overlay.AlineacionVertical?.ToString(),
            Wrap = overlay.Wrap,
            FormatoNumerico = overlay.FormatoNumerico,
            FechaModificacion = new DateTime(2026, 1, 1),
        };

    private sealed class CountingDbContextFactory : IProjectDbContextFactory
    {
        public CountingContext? Last { get; private set; }

        public SOPROContext Create(string databasePath)
        {
            Last = new CountingContext(databasePath);
            return Last;
        }

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private class CountingContext : SOPROContext
    {
        public CountingContext(string databasePath)
            : base(databasePath)
        {
        }

        public int SaveChangesCount { get; private set; }

        public override int SaveChanges()
        {
            SaveChangesCount++;
            return base.SaveChanges();
        }
    }

    private sealed class ThrowingFactory : IProjectDbContextFactory
    {
        public SOPROContext Create(string databasePath)
            => throw new InvalidOperationException("apertura_simulada");

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => throw new InvalidOperationException("apertura_simulada");
    }

    private sealed class DisposeThrowingFactory : IProjectDbContextFactory
    {
        private readonly string _dbPath;

        public DisposeThrowingFactory(string dbPath) => _dbPath = dbPath;

        public SOPROContext Create(string databasePath) => new DisposeThrowingContext(_dbPath);

        public Task<SOPROContext> CreateAsync(string databasePath, CancellationToken cancellationToken = default)
            => Task.FromResult(Create(databasePath));
    }

    private sealed class DisposeThrowingContext : SOPROContext
    {
        public DisposeThrowingContext(string dbPath)
            : base(dbPath)
        {
        }

        public override void Dispose()
        {
            base.Dispose();
            throw new InvalidOperationException("dispose_simulado");
        }
    }

    private static void DeleteQuietly(string dbPath)
    {
        Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
        for (var intento = 0; intento < 3; intento++)
        {
            try
            {
                if (File.Exists(dbPath)) File.Delete(dbPath);
                return;
            }
            catch (IOException)
            {
                Thread.Sleep(150);
            }
        }
    }
}
