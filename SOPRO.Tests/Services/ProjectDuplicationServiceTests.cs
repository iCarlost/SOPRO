using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using System.Threading;

namespace SOPRO.Tests.Services;

/// <summary>
/// Contrato de duplicación de proyecto: copia completa con metadata editable
/// aislada, origen intacto (aunque esté abierto en WAL), publicación canónica
/// sin sobrescritura y limpieza total al cancelar o fallar.
/// </summary>
[TestClass]
public class ProjectDuplicationServiceTests
{
    private const string MissingSidecar = "<missing>";

    private static readonly IProjectDbContextFactory Factory = new ProjectDbContextFactory();

    [TestMethod]
    public void Publish_PreservaDatosYRelaciones_SoloCambiaMetadataYUsaExtensionCanonica()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto origen");

            var hashAntes = Sha256(source);
            var escrituraAntes = File.GetLastWriteTimeUtc(source);
            var snapshotOrigen = SnapshotTables(source);

            var workspace = NuevoWorkspace(root);
            var service = new ProjectDuplicationService(workspace, Factory);

            string destino;
            using (var draft = service.PrepareDuplicate(source))
            {
                Assert.AreEqual("Proyecto origen", draft.Proyecto.Nombre);
                Assert.AreEqual("Descripción original", draft.Proyecto.Descripcion);

                var editado = draft.Proyecto;
                editado.Nombre = "Copia";
                editado.Descripcion = "Descripción copia";
                editado.Ubicacion = "Ubicación copia";
                editado.Convocante = "Convocante copia";
                editado.Contratista = "Contratista copia";
                editado.ApoderadoLegal = "Apoderado copia";
                editado.FechaInicio = new DateTime(2026, 2, 1);
                editado.FechaTermino = new DateTime(2026, 2, 28);
                editado.PlazoEjecucion = 28;

                // Intento de cambiar configuración de cálculo desde el llamador:
                // Publish debe ignorarlo y conservar los valores del borrador.
                editado.PorcentajeIVA = 8m;
                editado.DecimalesCantidad = 3;
                editado.DecimalesImporte = 4;
                editado.DecimalesPorcentaje = 5;

                destino = draft.Publish(editado);

                Assert.IsTrue(File.Exists(destino), "El clon debe existir antes de liberar el borrador.");
            }

            Assert.IsTrue(File.Exists(destino), "Dispose después de publicar no debe borrar el clon.");
            Assert.AreEqual(".soproproj", Path.GetExtension(destino));
            Assert.AreEqual(hashAntes, Sha256(source), "El origen no debe cambiar.");
            Assert.AreEqual(escrituraAntes, File.GetLastWriteTimeUtc(source), "La fecha del origen no debe cambiar.");

            var snapshotDestino = SnapshotTables(destino);
            var tablasOrigen = snapshotOrigen.Keys
                .Where(t => t != "Proyectos")
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();
            var tablasDestino = snapshotDestino.Keys
                .Where(t => t != "Proyectos")
                .OrderBy(t => t, StringComparer.Ordinal)
                .ToList();

            CollectionAssert.AreEqual(tablasOrigen, tablasDestino, "Deben conservarse todas las tablas.");
            foreach (var tabla in tablasOrigen)
            {
                CollectionAssert.AreEqual(
                    snapshotOrigen[tabla],
                    snapshotDestino[tabla],
                    $"La tabla '{tabla}' debe conservarse intacta.");
            }

            using var context = new SOPROContext(destino);
            var proyecto = context.Proyectos.Single();

            Assert.AreEqual(1, proyecto.Id, "El Id del proyecto debe conservarse.");
            Assert.AreEqual("Copia", proyecto.Nombre);
            Assert.AreEqual("Descripción copia", proyecto.Descripcion);
            Assert.AreEqual("Ubicación copia", proyecto.Ubicacion);
            Assert.AreEqual("Convocante copia", proyecto.Convocante);
            Assert.AreEqual("Contratista copia", proyecto.Contratista);
            Assert.AreEqual("Apoderado copia", proyecto.ApoderadoLegal);
            Assert.AreEqual(new DateTime(2026, 2, 1), proyecto.FechaInicio);
            Assert.AreEqual(new DateTime(2026, 2, 28), proyecto.FechaTermino);
            Assert.AreEqual(28, proyecto.PlazoEjecucion);
            Assert.AreEqual(16m, proyecto.PorcentajeIVA,
                "El IVA no es editable en la duplicación: debe conservarse el del borrador.");
            Assert.AreEqual(2, proyecto.DecimalesCantidad,
                "Los decimales no son editables en la duplicación: deben conservarse.");
            Assert.AreEqual(2, proyecto.DecimalesImporte,
                "Los decimales no son editables en la duplicación: deben conservarse.");
            Assert.AreEqual(4, proyecto.DecimalesPorcentaje,
                "Los decimales no son editables en la duplicación: deben conservarse.");

            // Campos no editables por el diálogo: se conservan tal cual.
            Assert.AreEqual(7.25m, proyecto.PorcentajeIndirectosCentral);
            Assert.AreEqual(4.5m, proyecto.PorcentajeIndirectosCampo);
            Assert.AreEqual(1.75m, proyecto.PorcentajeFinanciamiento);
            Assert.AreEqual(11m, proyecto.PorcentajeUtilidad);
            Assert.AreEqual(2.25m, proyecto.PorcentajeCargosAdicionales);
            Assert.AreEqual(1.7777m, proyecto.FactorSalarioReal);
            Assert.AreEqual("SobreCD", proyecto.ModoCalculoPorcentajes);
            Assert.AreEqual(new DateTime(2025, 12, 1), proyecto.FechaCreacion);

            var material = context.Materiales.Single(m => m.Clave == "MAT-01");
            Assert.AreEqual(proyecto.Id, material.ProyectoId, "La FK del material debe apuntar al proyecto copiado.");
            Assert.AreEqual(OrigenInsumo.Maestro, material.Origen, "El origen del insumo debe conservarse.");
            Assert.AreEqual(777, material.MaterialMaestroId);

            // Fila legacy sin dueño de proyecto: la copia debe conservar el null.
            var materialSinProyecto = context.Materiales.Single(m => m.Clave == "MAT-MAESTRO");
            Assert.IsNull(materialSinProyecto.ProyectoId,
                "La propiedad null de una fila legado del catálogo debe conservarse.");
            Assert.AreEqual(OrigenInsumo.Maestro, materialSinProyecto.Origen);

            var matriz = context.Matrices.Single();
            Assert.AreEqual(proyecto.Id, matriz.ProyectoId);
            Assert.AreEqual(OrigenInsumo.Maestro, matriz.Origen);
            Assert.AreEqual(55, matriz.MatrizMaestraId);

            var componente = context.ComponentesMatriz.Single();
            Assert.AreEqual(matriz.Id, componente.MatrizId);
            Assert.AreEqual(material.Id, componente.MaterialId);
            Assert.AreEqual(2.5m, componente.Cantidad);

            var concepto = context.ConceptosPresupuesto.Single();
            Assert.AreEqual(proyecto.Id, concepto.ProyectoId);
            Assert.AreEqual(matriz.Id, concepto.MatrizId);

            var programa = context.ProgramasObra.Single();
            Assert.AreEqual(proyecto.Id, programa.ProyectoId);
            Assert.AreEqual(1, context.ActividadesProgramadas.Count(a => a.ProgramaObraId == programa.Id));
        }
        finally
        {
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_FuenteAbiertaEnWal_FallaCerradoSinModificarOrigen()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto WAL");

            using var sourceLock = WorkspaceLock.Acquire(source);
            using var writer = AbrirEscritorWal(source);
            Ejecutar(writer, @"
                INSERT INTO Materiales
                    (Clave, Descripcion, Unidad, PrecioUnitario, Origen, MaterialMaestroId,
                     ProyectoId, Notas, FechaCreacion, FechaModificacion)
                VALUES
                    ('WAL-01', 'Confirmado en WAL', 'm3', 12.5, 1, 888,
                     (SELECT Id FROM Proyectos LIMIT 1), 'wal', '2026-01-02 00:00:00', '2026-01-02 00:00:00');");

            var walPath = source + "-wal";
            Assert.IsTrue(File.Exists(walPath), "El escenario requiere WAL pendiente.");
            Assert.IsTrue(File.Exists(source + "-shm"), "El escenario requiere el sidecar -shm compartido.");

            var sidecarsAntes = SnapshotSidecars(source);
            Assert.AreNotEqual(MissingSidecar, sidecarsAntes["-shm"],
                "El escenario requiere un -shm existente antes de duplicar.");

            var workspace = NuevoWorkspace(root);
            var service = new ProjectDuplicationService(workspace, Factory);
            var ex = Assert.ThrowsExactly<InvalidOperationException>(
                () => service.PrepareDuplicate(source));

            StringAssert.Contains(ex.Message, "exclusivamente",
                "La duplicación debe indicar que la fuente abierta se rechazó por seguridad.");
            Assert.IsFalse(Directory.Exists(workspace.ProjectsFolder)
                && Directory.GetFiles(workspace.ProjectsFolder).Length > 0,
                "Un fallo cerrado no debe dejar borradores ni artefactos.");
            AssertSidecarsSinCambios(sidecarsAntes, source, "El rechazo de una fuente WAL abierta");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_LiberaHandleInactivoDelPoolTrasCerrarContexto()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto con handle pooled");

            using (var context = Factory.Create(source))
            {
                context.Database.OpenConnection();
                context.Database.CloseConnection();
            }

            // No se llama a ClearAllPools: simula una sesión de proyecto cerrada
            // cuyo handle SQLite quedó retenido por el pool normal de la app.
            var service = new ProjectDuplicationService(NuevoWorkspace(root), Factory);
            using var draft = service.PrepareDuplicate(source);

            Assert.AreEqual("Proyecto con handle pooled", draft.Proyecto.Nombre);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_DespuesDeCerrarSesion_LiberaElPoolConLaRutaDeApertura()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto abierto y cerrado");

            var workspace = NuevoWorkspace(root);
            var lifecycle = new ProjectLifecycleService(workspace, Factory);
            var rutaDeApertura = OperatingSystem.IsWindows()
                ? source.ToUpperInvariant()
                : source;
            var session = lifecycle.OpenProject(rutaDeApertura);
            lifecycle.CloseProjectSession(session);

            var service = new ProjectDuplicationService(workspace, Factory);
            using var draft = service.PrepareDuplicate(source);

            Assert.AreEqual("Proyecto abierto y cerrado", draft.Proyecto.Nombre);
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_FuenteWalSinShm_NoCreaShmNiModificaElOrigenEIncluyeWal()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var original = Path.Combine(root, "fuente", "OrigenOriginal.db");
            var source = Path.Combine(root, "fuente", "OrigenSinShm.db");
            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
            CrearBaseActual(original, "Proyecto WAL sin shm");

            using (var writer = AbrirEscritorWal(original))
            {
                Ejecutar(writer, @"
                    INSERT INTO Materiales
                        (Clave, Descripcion, Unidad, PrecioUnitario, Origen, MaterialMaestroId,
                         ProyectoId, Notas, FechaCreacion, FechaModificacion)
                    VALUES
                        ('WAL-SIN-SHM', 'Confirmado en WAL', 'm3', 12.5, 1, 889,
                         (SELECT Id FROM Proyectos LIMIT 1), 'wal', '2026-01-02 00:00:00', '2026-01-02 00:00:00');");

                Assert.IsTrue(File.Exists(original + "-wal"), "El escenario requiere WAL pendiente.");

                // Instantánea realista de un origen WAL cuyos dueños ya no conservan
                // el -shm: se copian db + wal y se omite el sidecar compartido.
                File.Copy(original, source);
                File.Copy(original + "-wal", source + "-wal");
            }

            Assert.IsTrue(File.Exists(source + "-wal"), "La fuente debe conservar el WAL.");
            Assert.IsFalse(File.Exists(source + "-shm"), "La fuente debe carecer de -shm.");
            SqliteConnection.ClearAllPools();

            var sidecarsAntes = SnapshotSidecars(source);
            Assert.AreEqual(MissingSidecar, sidecarsAntes["-shm"],
                "El escenario requiere un -shm ausente antes de duplicar.");

            var service = new ProjectDuplicationService(NuevoWorkspace(root), Factory);

            string destino;
            using (var draft = service.PrepareDuplicate(source))
            {
                var editado = draft.Proyecto;
                editado.Nombre = "Copia WAL sin shm";
                destino = draft.Publish(editado);
            }

            using (var context = new SOPROContext(destino))
            {
                Assert.IsTrue(
                    context.Materiales.Any(m => m.Clave == "WAL-SIN-SHM"),
                    "El backup debe incluir los datos confirmados que aún viven en el WAL.");
            }

            Assert.IsFalse(File.Exists(source + "-shm"),
                "Duplicar una fuente WAL sin -shm no debe crear el sidecar en el origen.");
            AssertSidecarsSinCambios(sidecarsAntes, source, "La duplicación WAL sin -shm");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_WalSinShmConHandleExclusivoAjeno_FallaCerradoSinArtefactos()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var original = Path.Combine(root, "fuente", "OrigenOriginal.db");
            var source = Path.Combine(root, "fuente", "OrigenSinShm.db");
            Directory.CreateDirectory(Path.GetDirectoryName(original)!);
            CrearBaseActual(original, "Proyecto WAL sin shm bloqueado");

            using (var writer = AbrirEscritorWal(original))
            {
                Ejecutar(writer, @"
                    INSERT INTO Materiales
                        (Clave, Descripcion, Unidad, PrecioUnitario, Origen, MaterialMaestroId,
                         ProyectoId, Notas, FechaCreacion, FechaModificacion)
                    VALUES
                        ('WAL-BLOQUEADO', 'Confirmado en WAL', 'm3', 12.5, 1, 890,
                         (SELECT Id FROM Proyectos LIMIT 1), 'wal', '2026-01-02 00:00:00', '2026-01-02 00:00:00');");

                File.Copy(original, source);
                File.Copy(original + "-wal", source + "-wal");
            }

            SqliteConnection.ClearAllPools();
            Assert.IsFalse(File.Exists(source + "-shm"));
            var hashMain = Sha256(source);
            var hashWal = Sha256Compartido(source + "-wal");
            var sidecarsAntes = SnapshotSidecars(source);

            // Un handle ajeno con sharing de lectura/escritura impide que la
            // duplicación obtenga los handles exclusivos necesarios: debe fallar
            // cerrado sin abrir el original con SQLite ni crear -shm.
            using (var foreign = new FileStream(
                source, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            {
                var workspace = NuevoWorkspace(root);
                var service = new ProjectDuplicationService(workspace, Factory);

                var ex = Assert.ThrowsExactly<InvalidOperationException>(
                    () => service.PrepareDuplicate(source));

                StringAssert.Contains(ex.Message, "exclusivamente",
                    "El fallo debe explicar que otro handle impide asegurar la instantánea.");
                Assert.IsFalse(Directory.Exists(workspace.ProjectsFolder)
                    && Directory.GetFiles(workspace.ProjectsFolder).Length > 0,
                    "Un fallo cerrado no debe dejar temporales.");
            }

            Assert.IsFalse(File.Exists(source + "-shm"),
                "La ruta de fallo cerrado no debe crear el -shm ausente.");
            Assert.AreEqual(hashMain, Sha256(source), "El archivo principal no debe cambiar.");
            Assert.AreEqual(hashWal, Sha256Compartido(source + "-wal"), "El WAL no debe cambiar.");
            AssertSidecarsSinCambios(sidecarsAntes, source, "El fallo cerrado sin -shm");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void Dispose_FalloDeCandadoYArchivo_ExponeAmbosYCompletaLimpieza()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto con candado roto");
            var sidecarsAntes = SnapshotSidecars(source);

            var workspace = NuevoWorkspace(root);
            var operations = new FailingDeleteOperations();
            var throwingLock = new ThrowingDisposeLock(throwOnce: true);
            var service = new ProjectDuplicationService(
                workspace,
                Factory,
                operations,
                _ => throwingLock);

            var draft = service.PrepareDuplicate(source);
            var draftFile = Directory
                .GetFiles(workspace.ProjectsFolder, ".sopro-duplicado-*.tmp")
                .Single();

            operations.FailPath = draftFile;

            // El candado falla al liberarse y el archivo no se puede borrar:
            // la excepción debe conservar AMBOS datos, sin que el candado
            // enmascare la ruta residual.
            var ex = Assert.ThrowsExactly<ProjectDraftCleanupException>(
                () => draft.Dispose());

            CollectionAssert.Contains(
                ex.ResidualPaths.ToList(),
                draftFile,
                "La excepción debe conservar la ruta residual exacta.");
            Assert.IsNotNull(ex.LockException,
                "La excepción debe conservar el error al liberar el candado.");
            StringAssert.Contains(ex.Message, draftFile);
            Assert.IsTrue(File.Exists(draftFile));

            // Al desaparecer la causa de borrado, el reintento completa la limpieza.
            operations.FailPath = null;
            draft.Dispose();

            Assert.IsFalse(File.Exists(draftFile), "El reintento debe eliminar el temporal.");
            AssertSidecarsSinCambios(sidecarsAntes, source, "El fallo de limpieza con candado");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_FalloConCandadoYCopia_ConservaRutasYErrores()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto con versión futura");

            using (var context = new SOPROContext(source))
            {
                context.Database.ExecuteSqlRaw(
                    "DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
                context.Database.ExecuteSqlRaw(
                    "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) VALUES ({0}, datetime('now'));",
                    "SchemaVersion_2999.01.1");
            }
            SqliteConnection.ClearAllPools();

            var workspace = NuevoWorkspace(root);
            var operations = new FailingDeleteOperations { FailSuffix = string.Empty };
            var throwingLock = new ThrowingDisposeLock(throwOnce: false);
            var service = new ProjectDuplicationService(
                workspace,
                Factory,
                operations,
                _ => throwingLock);

            var ex = Assert.ThrowsExactly<ProjectDraftCleanupException>(
                () => service.PrepareDuplicate(source));

            Assert.IsNotNull(ex.LockException,
                "La excepción debe conservar el fallo del candado durante la preparación.");
            Assert.IsTrue(
                ex.ResidualPaths.Any(path => path.Contains(".sopro-duplicado-", StringComparison.Ordinal)),
                "La excepción debe exponer las rutas residuales exactas del temporal.");
            Assert.IsTrue(
                operations.DeleteAttempts.Any(path => path.Contains(".sopro-duplicado-", StringComparison.Ordinal)),
                "La limpieza de archivos debe intentarse aunque el candado falle al liberarse.");
            Assert.IsNotNull(ex.InnerException,
                "La excepción debe conservar el fallo original de la preparación.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void Publish_FalloAlBorrarSidecarTemporal_NoPublicaNiDeclaraExito()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto con sidecar residual");
            var sidecarsAntes = SnapshotSidecars(source);

            var workspace = NuevoWorkspace(root);
            var operations = new FailingDeleteOperations();
            var service = new ProjectDuplicationService(workspace, Factory, operations);

            var draft = service.PrepareDuplicate(source);
            var draftFile = Directory
                .GetFiles(workspace.ProjectsFolder, ".sopro-duplicado-*.tmp")
                .Single();

            draft.Proyecto.Nombre = "Copia con sidecar residual";
            var destination = workspace.BuildProjectDatabasePath(draft.Proyecto.Nombre);

            // El seam recrea un -wal al intentar limpiar y además impide borrarlo:
            // simula un sidecar que sobrevive hasta la frontera de publicación.
            operations.InjectSidecarOnClear = true;
            operations.FailSuffix = "-wal";

            var ex = Assert.ThrowsExactly<ProjectDraftCleanupException>(
                () => draft.Publish(draft.Proyecto));

            Assert.IsFalse(File.Exists(destination),
                "No debe existir un proyecto publicado si quedan sidecars temporales.");
            CollectionAssert.Contains(
                ex.ResidualPaths.ToList(),
                draftFile + "-wal",
                "La publicación fallida debe exponer el sidecar residual exacto.");
            Assert.IsTrue(File.Exists(draftFile + "-wal"),
                "El sidecar residual debe seguir localizable para reintentar la limpieza.");

            operations.InjectSidecarOnClear = false;
            operations.FailSuffix = null;
            draft.Dispose();

            Assert.IsFalse(File.Exists(draftFile), "El reintento debe limpiar el temporal.");
            Assert.IsFalse(File.Exists(draftFile + "-wal"), "El reintento debe limpiar el sidecar.");
            AssertSidecarsSinCambios(sidecarsAntes, source, "La publicación fallida");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void Dispose_FalloDeBorrado_ExponeRutaResidualYNoLaOlvida()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto con borrado forzado");
            var sidecarsAntes = SnapshotSidecars(source);

            var workspace = NuevoWorkspace(root);
            var operations = new FailingDeleteOperations();
            var service = new ProjectDuplicationService(workspace, Factory, operations);

            var draft = service.PrepareDuplicate(source);
            var draftFile = Directory
                .GetFiles(workspace.ProjectsFolder, ".sopro-duplicado-*.tmp")
                .Single();

            operations.FailPath = draftFile;

            var ex = Assert.ThrowsExactly<ProjectDraftCleanupException>(
                () => draft.Dispose());

            CollectionAssert.Contains(
                ex.ResidualPaths.ToList(),
                draftFile,
                "La excepción debe exponer la ruta residual exacta.");
            StringAssert.Contains(ex.Message, draftFile,
                "El mensaje debe permitir localizar el archivo residual.");
            Assert.IsTrue(File.Exists(draftFile),
                "La ruta residual no debe olvidarse mientras el archivo siga existiendo.");

            // Al desaparecer la causa, un segundo Dispose debe reintentar y
            // completar la limpieza sin perder el borrador.
            operations.FailPath = null;
            draft.Dispose();

            Assert.IsFalse(File.Exists(draftFile),
                "El reintento de limpieza debe eliminar el temporal residual.");
            AssertSidecarsSinCambios(sidecarsAntes, source, "El fallo de limpieza");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_FuenteLegada_MigraSoloElBorradorYPublicaCanonico()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.sopro");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto legado");

            using (var context = new SOPROContext(source))
            {
                context.Database.ExecuteSqlRaw(
                    "DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(source);

            var service = new ProjectDuplicationService(NuevoWorkspace(root), Factory);

            string destino;
            using (var draft = service.PrepareDuplicate(source))
            {
                Assert.IsNotNull(draft.Proyecto, "La copia temporal debe quedar migrada y legible.");

                var editado = draft.Proyecto;
                editado.Nombre = "Copia legada";
                destino = draft.Publish(editado);
            }

            Assert.AreEqual(".soproproj", Path.GetExtension(destino),
                "Toda publicación debe usar la extensión canónica.");
            Assert.AreEqual(hashAntes, Sha256(source),
                "El origen legado no debe migrarse ni modificarse.");

            using (var origen = Factory.CreateReadOnly(source))
            {
                Assert.IsNull(SchemaManager.GetVersionRegistrada(origen),
                    "El origen debe conservar su versión ausente.");
            }

            using (var copia = Factory.CreateReadOnly(destino))
            {
                Assert.AreEqual(SchemaManager.VersionActual, SchemaManager.GetVersionRegistrada(copia),
                    "Solo el borrador publicado debe quedar en la versión actual.");
            }
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void Dispose_SinPublicar_NoDejaDestinoNiTemporales()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto cancelado");

            var hashAntes = Sha256(source);
            var workspace = NuevoWorkspace(root);
            var service = new ProjectDuplicationService(workspace, Factory);

            var draft = service.PrepareDuplicate(source);
            Assert.IsTrue(Directory.Exists(workspace.ProjectsFolder),
                "Publicar/duplicar usa la carpeta de proyectos del workspace.");
            Assert.IsTrue(
                Directory.GetFiles(workspace.ProjectsFolder).Length > 0,
                "El borrador debe existir como archivo temporal mientras está activo.");

            draft.Dispose();

            Assert.AreEqual(0, Directory.GetFiles(workspace.ProjectsFolder).Length,
                "Cancelar no debe dejar temporales, sidecars ni candados.");
            Assert.AreEqual(hashAntes, Sha256(source), "Cancelar no debe tocar el origen.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [DataTestMethod]
    [DataRow("2999.01.1", DisplayName = "Versión futura")]
    [DataRow("legacy-sin-formato", DisplayName = "Versión no reconocida")]
    public void PrepareDuplicate_VersionInvalida_SeRechazaSinArtefactos(string versionRegistrada)
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto inválido");

            using (var context = new SOPROContext(source))
            {
                context.Database.ExecuteSqlRaw(
                    "DELETE FROM __MigracionesCustom WHERE Clave LIKE 'SchemaVersion_%';");
                context.Database.ExecuteSqlRaw(
                    "INSERT INTO __MigracionesCustom (Clave, FechaAplicacion) VALUES ({0}, datetime('now'));",
                    $"SchemaVersion_{versionRegistrada}");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(source);
            var workspace = NuevoWorkspace(root);
            var service = new ProjectDuplicationService(workspace, Factory);

            Assert.ThrowsExactly<InvalidOperationException>(
                () => service.PrepareDuplicate(source));

            Assert.AreEqual(hashAntes, Sha256(source), "Un rechazo no debe tocar el origen.");
            Assert.IsFalse(
                Directory.Exists(workspace.ProjectsFolder)
                && Directory.GetFiles(workspace.ProjectsFolder).Length > 0,
                "Un rechazo no debe crear temporales.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void PrepareDuplicate_FalloAlMigrarElBorrador_LimpiaTemporalesYNoTocaElOrigen()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto con migración rota");

            // Versión antigua legible + tabla de control sin la columna que usa
            // RegistrarVersion: el backup funciona y la migración del borrador falla.
            using (var context = new SOPROContext(source))
            {
                context.Database.ExecuteSqlRaw("DROP TABLE __MigracionesCustom;");
                context.Database.ExecuteSqlRaw("CREATE TABLE __MigracionesCustom (Clave TEXT PRIMARY KEY NOT NULL);");
                context.Database.ExecuteSqlRaw(
                    "INSERT INTO __MigracionesCustom (Clave) VALUES ('SchemaVersion_2020.01.1');");
            }
            SqliteConnection.ClearAllPools();

            var hashAntes = Sha256(source);
            var workspace = NuevoWorkspace(root);
            var service = new ProjectDuplicationService(workspace, Factory);

            Assert.ThrowsExactly<SqliteException>(
                () => service.PrepareDuplicate(source));

            Assert.AreEqual(hashAntes, Sha256(source), "El origen no debe cambiar aunque la copia falle.");
            Assert.IsFalse(
                Directory.Exists(workspace.ProjectsFolder)
                && Directory.GetFiles(workspace.ProjectsFolder).Length > 0,
                "Un fallo de migración no debe dejar temporales.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    [TestMethod]
    public void Publish_ColisionDeNombreSaneado_ConservaElProyectoExistente()
    {
        var root = NuevaCarpetaTemporal();
        try
        {
            var source = Path.Combine(root, "fuente", "Origen.db");
            Directory.CreateDirectory(Path.GetDirectoryName(source)!);
            CrearBaseActual(source, "Proyecto origen");

            var workspace = NuevoWorkspace(root);
            Directory.CreateDirectory(workspace.ProjectsFolder);

            var incumbente = Path.Combine(workspace.ProjectsFolder, "Obra_Norte.soproproj");
            File.Copy(source, incumbente);
            var hashIncumbente = Sha256(incumbente);

            var service = new ProjectDuplicationService(workspace, Factory);

            using var draft = service.PrepareDuplicate(source);
            draft.Proyecto.Nombre = "Obra/Norte";

            Assert.ThrowsExactly<InvalidOperationException>(() => draft.Publish(draft.Proyecto));

            Assert.AreEqual(hashIncumbente, Sha256(incumbente),
                "El proyecto existente no debe sobrescribirse.");
            Assert.AreEqual(1, Directory.GetFiles(workspace.ProjectsFolder).Length,
                "La colisión no debe dejar temporales ni candados.");
        }
        finally
        {
            SqliteConnection.ClearAllPools();
            BorrarCarpeta(root);
        }
    }

    // ── Helpers ────────────────────────────────────────────────────────────────

    private static ProjectWorkspaceService NuevoWorkspace(string root)
        => new(
            Path.Combine(root, "workspace"),
            Path.Combine(root, "local"));

    private static string NuevaCarpetaTemporal()
    {
        var path = Path.Combine(
            Path.GetTempPath(),
            "sopro_duplicacion_tests",
            Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void CrearBaseActual(string dbPath, string nombreProyecto)
    {
        using var context = new SOPROContext(dbPath);
        context.Database.EnsureCreated();

        var proyecto = new Proyecto
        {
            Nombre = nombreProyecto,
            Descripcion = "Descripción original",
            Ubicacion = "Ubicación original",
            Convocante = "Convocante original",
            Contratista = "Contratista original",
            ApoderadoLegal = "Apoderado original",
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 7.25m,
            PorcentajeIndirectosCampo = 4.5m,
            PorcentajeFinanciamiento = 1.75m,
            PorcentajeUtilidad = 11m,
            PorcentajeCargosAdicionales = 2.25m,
            PorcentajeIVA = 16m,
            ModoCalculoPorcentajes = "SobreCD",
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4,
            FactorSalarioReal = 1.7777m,
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var material = new Material
        {
            Clave = "MAT-01",
            Descripcion = "Material de prueba",
            Unidad = "m3",
            PrecioUnitario = 12.5m,
            Origen = OrigenInsumo.Maestro,
            MaterialMaestroId = 777,
            ProyectoId = proyecto.Id,
            Notas = "material",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };

        var manoDeObra = new ManoDeObra
        {
            Clave = "MO-01",
            Descripcion = "Mano de obra de prueba",
            Unidad = "jor",
            SalarioBase = 500m,
            FactorSalarioReal = 1.5m,
            SalarioReal = 750m,
            Origen = OrigenInsumo.Proyecto,
            ProyectoId = proyecto.Id,
            Notas = "mo",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };

        var maquinaria = new Maquinaria
        {
            Clave = "MAQ-01",
            Descripcion = "Maquinaria de prueba",
            ProyectoId = proyecto.Id,
            Origen = OrigenInsumo.Proyecto,
            CostoHorario = 250m,
            Notas = "maq",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };

        var herramienta = new Herramienta
        {
            Clave = "HER-01",
            Descripcion = "Herramienta de prueba",
            Unidad = "hr",
            PrecioUnitario = 10m,
            ProyectoId = proyecto.Id,
            Origen = OrigenInsumo.Proyecto,
            Notas = "her",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };

        context.Materiales.Add(material);
        context.Materiales.Add(new Material
        {
            Clave = "MAT-MAESTRO",
            Descripcion = "Material de catálogo sin proyecto",
            Unidad = "pza",
            PrecioUnitario = 5m,
            Origen = OrigenInsumo.Maestro,
            MaterialMaestroId = 1,
            ProyectoId = null,
            Notas = "maestro",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        });
        context.ManoDeObra.Add(manoDeObra);
        context.Maquinaria.Add(maquinaria);
        context.Herramientas.Add(herramienta);
        context.SaveChanges();

        var matriz = new Matriz
        {
            Clave = "APU-01",
            Descripcion = "Matriz de prueba",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 31.25m,
            Origen = OrigenInsumo.Maestro,
            MatrizMaestraId = 55,
            ProyectoId = proyecto.Id,
            Notas = "matriz",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 2.5m,
            Rendimiento = 0m,
            Importe = 31.25m,
            Orden = 1,
            Notas = "componente"
        });

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-01",
            Descripcion = "Concepto de prueba",
            Unidad = "m3",
            Nivel = 1,
            EsAgrupador = false,
            Orden = 1,
            Cantidad = 3m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 12.5m,
            CostoDirectoTotal = 37.5m,
            PrecioUnitario = 12.5m,
            ImporteTotal = 37.5m,
            ColumnasPersonalizadasJSON = "{}",
            Notas = "concepto",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };
        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa de prueba",
            Descripcion = "Programa",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        context.ActividadesProgramadas.Add(new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = concepto.Id,
            Clave = "A-01",
            Descripcion = "Actividad de prueba",
            Unidad = "m3",
            Nivel = 1,
            Orden = 1,
            CantidadTotal = 3m,
            PrecioUnitario = 12.5m,
            ImporteTotal = 37.5m,
            Notas = "actividad",
            FechaCreacion = new DateTime(2025, 12, 1),
            FechaModificacion = new DateTime(2025, 12, 2)
        });

        context.SaveChanges();
        SchemaManager.EnsureCurrentSchema(context);
        context.Dispose();
        SqliteConnection.ClearAllPools();
    }

    private static Dictionary<string, List<string>> SnapshotTables(string dbPath)
    {
        var resultado = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        var connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = dbPath,
            Mode = SqliteOpenMode.ReadOnly,
            Pooling = false
        }.ToString();

        using var connection = new SqliteConnection(connectionString);
        connection.Open();

        var tablas = new List<string>();
        using (var command = connection.CreateCommand())
        {
            command.CommandText =
                "SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%' ORDER BY name";
            using var reader = command.ExecuteReader();
            while (reader.Read())
                tablas.Add(reader.GetString(0));
        }

        foreach (var tabla in tablas)
        {
            var filas = new List<string>();

            using var command = connection.CreateCommand();
            command.CommandText = $"SELECT * FROM [{tabla.Replace("]", "]]")}]";
            using var reader = command.ExecuteReader();

            while (reader.Read())
            {
                var valores = new string[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    valores[i] = reader.IsDBNull(i)
                        ? "<null>"
                        : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? string.Empty;
                }

                filas.Add(string.Join('\u001f', valores));
            }

            filas.Sort(StringComparer.Ordinal);
            resultado[tabla] = filas;
        }

        return resultado;
    }

    private static SqliteConnection AbrirEscritorWal(string dbPath)
    {
        var connection = new SqliteConnection(
            new SqliteConnectionStringBuilder { DataSource = dbPath, Pooling = false }.ToString());
        connection.Open();

        Ejecutar(connection, "PRAGMA journal_mode=WAL;");
        Ejecutar(connection, "PRAGMA wal_autocheckpoint=0;");

        return connection;
    }

    private static void Ejecutar(SqliteConnection connection, string sql)
    {
        using var command = connection.CreateCommand();
        command.CommandText = sql;
        command.ExecuteNonQuery();
    }

    private static Dictionary<string, string> SnapshotSidecars(string databasePath)
    {
        var snapshot = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var suffix in new[] { "", "-wal", "-shm", "-journal" })
        {
            var path = databasePath + suffix;
            var info = new FileInfo(path);

            snapshot[suffix] = info.Exists
                ? info.Length.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + info.LastWriteTimeUtc.Ticks.ToString(CultureInfo.InvariantCulture)
                    + "|"
                    + Sha256Compartido(path)
                : MissingSidecar;
        }

        return snapshot;
    }

    private static void AssertSidecarsSinCambios(
        Dictionary<string, string> antes,
        string sourcePath,
        string contexto)
    {
        var despues = SnapshotSidecars(sourcePath);
        foreach (var suffix in antes.Keys)
        {
            Assert.AreEqual(
                antes[suffix],
                despues[suffix],
                $"{contexto} no debe modificar el sidecar '{(string.IsNullOrEmpty(suffix) ? "principal" : suffix)}' del origen "
                + "(existencia, longitud, fecha y contenido).");
        }
    }

    private static string Sha256(string path)
    {
        using var stream = File.OpenRead(path);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string Sha256Compartido(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static void BorrarCarpeta(string path)
    {
        SqliteConnection.ClearAllPools();

        try
        {
            if (Directory.Exists(path))
                Directory.Delete(path, recursive: true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private sealed class ThrowingDisposeLock : IWorkspaceLock
    {
        private readonly bool _throwOnce;
        private int _disposed;

        public ThrowingDisposeLock(bool throwOnce)
        {
            _throwOnce = throwOnce;
        }

        public string LockFilePath => "<test-lock>";

        public void Dispose()
        {
            if (_throwOnce && Interlocked.Exchange(ref _disposed, 1) != 0)
                return;

            throw new IOException("Fallo de liberación de candado forzado por la prueba.");
        }
    }

    private sealed class FailingDeleteOperations : IProjectDraftFileOperations
    {
        public string? FailPath { get; set; }

        public string? FailSuffix { get; set; }

        public bool InjectSidecarOnClear { get; set; }

        public List<string> DeleteAttempts { get; } = new();

        public bool FileExists(string path) => File.Exists(path);

        public void DeleteFile(string path)
        {
            DeleteAttempts.Add(path);

            if (FailPath is not null
                && string.Equals(
                    Path.GetFullPath(path),
                    Path.GetFullPath(FailPath),
                    StringComparison.OrdinalIgnoreCase))
            {
                throw new IOException("Fallo de borrado forzado por la prueba.");
            }

            if (FailSuffix is not null
                && path.EndsWith(FailSuffix, StringComparison.Ordinal))
            {
                throw new IOException("Fallo de borrado de sidecar forzado por la prueba.");
            }

            File.Delete(path);
        }

        public void ClearSqlitePool(string databasePath)
        {
            SqliteConnection.ClearAllPools();

            if (InjectSidecarOnClear
                && databasePath.Contains(".sopro-duplicado-", StringComparison.Ordinal)
                && databasePath.EndsWith(".tmp", StringComparison.Ordinal))
            {
                File.WriteAllText(databasePath + "-wal", "sidecar temporal de prueba");
            }
        }

        public void Pause(int milliseconds)
        {
            // Sin espera: el fallo es determinista y no necesita backoff real.
        }
    }
}
