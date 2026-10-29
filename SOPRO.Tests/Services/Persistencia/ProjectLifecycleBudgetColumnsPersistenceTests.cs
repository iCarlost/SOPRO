using System.IO;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Persistencia;

/// <summary>
/// Contrato del seed de columnas de presupuesto del ciclo de vida (ADR-0001): el
/// escritor de <c>ColumnaPersonalizada</c> corre en un contexto NUEVO por operación,
/// con transacción y un único SaveChanges, y NO usa el contexto de sesión que termina
/// compartiéndose. Semántica conservada: si ya existe Tipo no se toca nada; si no
/// existe se reemplaza la configuración vieja por las 17 legacy. Solo SQLite temporal.
/// </summary>
[TestClass]
public class ProjectLifecycleBudgetColumnsPersistenceTests
{
    private string _root = null!;
    private string _dbPath = null!;

    [TestInitialize]
    public void Inicializar()
    {
        _root = Path.Combine(Path.GetTempPath(), $"sopro_lifecycle_columnas_{Guid.NewGuid():N}");
        _dbPath = Path.Combine(_root, "proyecto.soproproj");
        Directory.CreateDirectory(_root);
    }

    [TestCleanup]
    public void Limpiar()
    {
        try
        {
            Microsoft.Data.Sqlite.SqliteConnection.ClearAllPools();
            if (Directory.Exists(_root))
                Directory.Delete(_root, recursive: true);
        }
        catch
        {
            // Best-effort de limpieza de temporales.
        }
    }

    [TestMethod]
    public void EnsureDefaultColumns_ContextoDeSesionConConceptoModificado_NoConfirmaElConceptoYSembra17()
    {
        int proyectoId;
        int conceptoId;

        using (var preparacion = TestDbFactory.CreateContextAt(_dbPath))
        {
            var proyecto = AddProject(preparacion, "Proyecto lifecycle");
            proyectoId = proyecto.Id;
            conceptoId = AddConcept(preparacion, proyectoId, cantidad: 5m).Id;
        }

        var lifecycle = CrearLifecycle();

        using (var sesion = new SOPROContext(_dbPath))
        {
            var concepto = sesion.ConceptosPresupuesto.Single(c => c.Id == conceptoId);
            concepto.Cantidad = 999m;
            Assert.AreEqual(EntityState.Modified, sesion.Entry(concepto).State);

            lifecycle.EnsureDefaultColumns(sesion, proyectoId);

            Assert.AreEqual(EntityState.Modified, sesion.Entry(concepto).State,
                "El seed de columnas no debe guardar ni limpiar cambios pendientes del contexto de sesión.");
        }

        using (var verificacion = new SOPROContext(_dbPath))
        {
            var columnas = verificacion.ColumnasPersonalizadas.AsNoTracking()
                .Where(c => c.ProyectoId == proyectoId)
                .ToList();

            Assert.AreEqual(17, columnas.Count);
            Assert.AreEqual(17, columnas.Select(c => c.NombreInterno).Distinct(StringComparer.OrdinalIgnoreCase).Count());
            Assert.AreEqual("Tipo", columnas.Single(c => c.NombreInterno == "Tipo").NombreInterno);
            Assert.AreEqual(5m, verificacion.ConceptosPresupuesto.AsNoTracking()
                .Single(c => c.Id == conceptoId).Cantidad,
                "El Concepto modificado en el contexto de sesión no debe persistirse.");
        }
    }

    [TestMethod]
    public void EnsureDefaultColumns_SegundaLlamada_NoReiniciaAnchosNiConfiguracionDelUsuario()
    {
        int proyectoId;

        using (var preparacion = TestDbFactory.CreateContextAt(_dbPath))
        {
            proyectoId = AddProject(preparacion, "Proyecto lifecycle 2").Id;
        }

        var lifecycle = CrearLifecycle();

        using (var sesion = new SOPROContext(_dbPath))
            lifecycle.EnsureDefaultColumns(sesion, proyectoId);

        using (var personalizacion = new SOPROContext(_dbPath))
        {
            var tipo = personalizacion.ColumnasPersonalizadas
                .Single(c => c.ProyectoId == proyectoId && c.NombreInterno == "Tipo");
            tipo.AnchoColumna = 260;
            tipo.NombreFuente = "Arial";
            personalizacion.SaveChanges();
        }

        using (var segundaSesion = new SOPROContext(_dbPath))
            lifecycle.EnsureDefaultColumns(segundaSesion, proyectoId);

        using var verificacion = new SOPROContext(_dbPath);
        var columnas = verificacion.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .ToList();

        Assert.AreEqual(17, columnas.Count, "La segunda pasada no debe duplicar ni reiniciar columnas.");
        var tipoFinal = columnas.Single(c => c.NombreInterno == "Tipo");
        Assert.AreEqual(260, tipoFinal.AnchoColumna);
        Assert.AreEqual("Arial", tipoFinal.NombreFuente);
    }

    [TestMethod]
    public void EnsureDefaultColumns_SinColumnaTipo_ReemplazaConfiguracionViejaPorLasLegacy17()
    {
        int proyectoId;

        using (var preparacion = TestDbFactory.CreateContextAt(_dbPath))
        {
            proyectoId = AddProject(preparacion, "Proyecto con huérfanas").Id;

            preparacion.ColumnasPersonalizadas.Add(new ColumnaPersonalizada
            {
                ProyectoId = proyectoId,
                Nombre = "Huérfana",
                NombreInterno = "Col_Huerfana",
                TipoColumna = TipoColumnaPersonalizada.Personal,
                TipoDato = TipoDatoColumna.Texto,
                AnchoColumna = 150,
                Visible = true,
                Orden = 1,
                Formula = string.Empty,
                FormatoNumerico = string.Empty,
                FormatoFecha = string.Empty,
                CondicionTotalizacion = string.Empty
            });
            preparacion.SaveChanges();
        }

        var lifecycle = CrearLifecycle();
        using (var sesion = new SOPROContext(_dbPath))
            lifecycle.EnsureDefaultColumns(sesion, proyectoId);

        using var verificacion = new SOPROContext(_dbPath);
        var columnas = verificacion.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .ToList();

        Assert.AreEqual(17, columnas.Count);
        Assert.IsFalse(columnas.Any(c => c.NombreInterno == "Col_Huerfana"),
            "La configuración vieja sin Tipo se reemplaza, como antes.");
    }

    [TestMethod]
    public void CreateProjectYOpenProject_SembranLas17YLaSegundaAperturaNoReiniciaConfiguracion()
    {
        var lifecycle = CrearLifecycle();
        var workspace = new ProjectWorkspaceService(
            Path.Combine(_root, "sopro"), Path.Combine(_root, "local"));
        string projectPath = workspace.BuildProjectDatabasePath("Proyecto ciclo completo");

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto ciclo completo",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 12, 31),
            PlazoEjecucion = 365
        };

        int proyectoId;
        using (var sesion = lifecycle.CreateProject(proyecto))
        {
            proyectoId = sesion.Project.Id;
        }

        using (var creada = new SOPROContext(projectPath))
            Assert.AreEqual(17, creada.ColumnasPersonalizadas.AsNoTracking().Count(c => c.ProyectoId == proyectoId),
                "Crear proyecto siembra las 17 columnas legacy en la base.");

        using (var personalizacion = new SOPROContext(projectPath))
        {
            var tipo = personalizacion.ColumnasPersonalizadas
                .Single(c => c.ProyectoId == proyectoId && c.NombreInterno == "Tipo");
            tipo.AnchoColumna = 275;
            personalizacion.SaveChanges();
        }

        // Segundo open real: no reinicia anchos ni duplica columnas.
        using (var segundaSesion = lifecycle.OpenProject(projectPath))
        {
            Assert.AreEqual(proyectoId, segundaSesion.Project.Id);
        }

        using var verificacion = new SOPROContext(projectPath);
        var columnas = verificacion.ColumnasPersonalizadas.AsNoTracking()
            .Where(c => c.ProyectoId == proyectoId)
            .ToList();

        Assert.AreEqual(17, columnas.Count);
        Assert.AreEqual(275, columnas.Single(c => c.NombreInterno == "Tipo").AnchoColumna);
    }

    // ═════════════════════════════ Infraestructura ═════════════════════════════

    private ProjectLifecycleService CrearLifecycle()
        => new(
            new ProjectWorkspaceService(Path.Combine(_root, "sopro"), Path.Combine(_root, "local")),
            new ProjectDbContextFactory());

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
            FechaTermino = new DateTime(2026, 12, 31),
            PlazoEjecucion = 365
        };

        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static ConceptoPresupuesto AddConcept(SOPROContext context, int proyectoId, decimal cantidad)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyectoId,
            Clave = "CON-1",
            Descripcion = "Concepto de prueba",
            Unidad = "m2",
            Cantidad = cantidad,
            CostoDirectoUnitario = 10m,
            CostoDirectoTotal = cantidad * 10m,
            PrecioUnitario = 10m,
            ImporteTotal = cantidad * 10m,
            Nivel = 1,
            Orden = 1,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };

        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();
        return concepto;
    }
}
