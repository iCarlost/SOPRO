using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Matrices;

/// <summary>
/// Issue #121: el guardado de matriz debe escribir en un contexto por operación
/// y una sola transacción (matriz + componentes + cascada + commit). Un fallo
/// posterior a las primeras escrituras revierte todo; el contexto de la sesión
/// no se guarda ni se limpia (su trabajo pendiente y sus instancias siguen
/// rastreadas y stale). SQLite real con el esquema de producción: los fallos se
/// inyectan con triggers.
/// </summary>
[TestClass]
public class MatrixApplicationServiceTransactionTests
{
    private const string NombreProyectoOriginal = "Proyecto transacción";
    private const string NombreProyectoSentinel = "SENTINELA sin guardar";
    private const string DescripcionMatrizOriginal = "A original";
    private const string DescripcionMatrizEditada = "A editada";
    private const decimal CostoDirectoOriginal = 100m;
    private const decimal CostoDirectoEditado = 350m;

    [TestMethod]
    public async Task SaveAsync_Exito_GuardaEnContextoDeOperacionYNoTocaElTrackerDelLlamador()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        var result = await MatrixApplicationService.SaveAsync(context, escenario.Dto, escenario.MatrizA);

        Assert.AreEqual(escenario.MatrizA.Id, result.MatrixId);
        Assert.IsFalse(result.IsNew);
        Assert.AreEqual(1, result.CascadedMatricesUpdated);

        // El contexto de la sesión queda intacto y stale: no se guardó ni se limpió.
        Assert.AreEqual(EntityState.Modified, context.Entry(escenario.Proyecto).State);
        Assert.IsTrue(context.ChangeTracker.HasChanges(),
            "El trabajo pendiente ajeno debe seguir rastreado.");
        Assert.AreEqual(DescripcionMatrizOriginal, escenario.MatrizA.Descripcion,
            "El servicio no debe mutar la entidad rastreada por el llamador.");
        Assert.AreEqual(CostoDirectoOriginal, escenario.MatrizA.CostoDirecto,
            "El contexto de la sesión conserva el valor stale.");

        var estado = await LeerEstadoPersistidoAsync(dbPath, escenario.MatrizA.Id, escenario.MatrizB.Id);

        Assert.AreEqual(DescripcionMatrizEditada, estado.MatrizADescripcion);
        Assert.AreEqual(CostoDirectoEditado, estado.MatrizACostoDirecto);
        Assert.AreEqual(1, estado.ComponentesA.Count, "El reemplazo de componentes debe persistirse.");
        Assert.AreEqual(5m, estado.ComponentesA[0].Cantidad);
        Assert.AreEqual(500m, estado.ComponentesA[0].Importe);

        // Cascada: auxiliar 2 × 350 = 700.
        Assert.AreEqual(700m, estado.MatrizBCostoDirecto);

        Assert.AreEqual(NombreProyectoOriginal, estado.NombreProyecto,
            "La edición pendiente de la sesión no debe persistirse.");
    }

    [TestMethod]
    public async Task SaveAsync_FallaAlInsertarComponentes_RevierteMatrizYConservaElTracker()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        // Los componentes viejos se eliminan en un SaveChanges previo; el trigger
        // hace fallar la inserción del lote nuevo, después del borrado.
        context.Database.ExecuteSqlRaw(
            $@"CREATE TRIGGER fail_componente_insert
               BEFORE INSERT ON ComponentesMatriz
               WHEN NEW.MatrizId = {escenario.MatrizA.Id}
               BEGIN
                   SELECT RAISE(ABORT, 'fallo inyectado al insertar componentes');
               END;");

        await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => MatrixApplicationService.SaveAsync(context, escenario.Dto, escenario.MatrizA));

        await AssertEstadoOriginalAsync(dbPath, escenario, context);
    }

    [TestMethod]
    public async Task SaveAsync_FallaEnCascada_RevierteCambiosDeMatrizYComponentesYConservaElTracker()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        // La matriz padre y sus componentes ya se guardaron cuando la cascada
        // intenta actualizar la matriz dependiente: el fallo debe revertirlos.
        context.Database.ExecuteSqlRaw(
            $@"CREATE TRIGGER fail_cascada_dependiente
               BEFORE UPDATE ON Matrices
               WHEN NEW.Id = {escenario.MatrizB.Id}
               BEGIN
                   SELECT RAISE(ABORT, 'fallo inyectado en la cascada');
               END;");

        await Assert.ThrowsExactlyAsync<DbUpdateException>(
            () => MatrixApplicationService.SaveAsync(context, escenario.Dto, escenario.MatrizA));

        await AssertEstadoOriginalAsync(dbPath, escenario, context);
    }

    [TestMethod]
    public async Task SaveAsync_ContextoDeOperacion_NoBloqueaConCambiosPendientesDelLlamador()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        var result = await MatrixApplicationService.SaveAsync(
            new SOPRO.Data.Factories.ProjectDbContextFactory(),
            dbPath,
            escenario.Dto,
            escenario.MatrizA);

        Assert.AreEqual(escenario.MatrizA.Id, result.MatrixId);
        Assert.IsTrue(context.ChangeTracker.HasChanges(),
            "La sobrecarga con fábrica tampoco debe tocar el tracker del llamador.");

        var estado = await LeerEstadoPersistidoAsync(dbPath, escenario.MatrizA.Id, escenario.MatrizB.Id);
        Assert.AreEqual(DescripcionMatrizEditada, estado.MatrizADescripcion);
        Assert.AreEqual(NombreProyectoOriginal, estado.NombreProyecto);
    }

    // ── Guarda de ediciones pendientes en la sesión (#121) ────────────────────

    [TestMethod]
    public async Task SaveAsync_DependienteConCostoPendienteEnSesion_RechazaSinEscribirNiTocarElTracker()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        escenario.MatrizB.CostoDirecto = 999m;
        context.ChangeTracker.DetectChanges();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(context, escenario.Dto, escenario.MatrizA));

        StringAssert.Contains(ex.Message, "cambios sin guardar");
        StringAssert.Contains(ex.Message, escenario.MatrizB.Id.ToString());

        // La guarda corre antes de cualquier escritura: el estado persistido queda intacto.
        await AssertEstadoOriginalAsync(dbPath, escenario, context);
        Assert.AreEqual(EntityState.Modified, context.Entry(escenario.MatrizB).State,
            "El pendiente de la dependiente debe seguir rastreado.");
        Assert.AreEqual(999m, escenario.MatrizB.CostoDirecto,
            "La guarda no debe mutar la copia de la sesión.");
    }

    [TestMethod]
    public async Task SaveAsync_ComponenteDeDependientePendienteEnSesion_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        var componenteDependiente = escenario.MatrizB.Componentes.Single();
        componenteDependiente.Cantidad = 3m;
        context.ChangeTracker.DetectChanges();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(
                new SOPRO.Data.Factories.ProjectDbContextFactory(),
                dbPath,
                escenario.Dto,
                escenario.MatrizA,
                context));

        StringAssert.Contains(ex.Message, "componentes");
        StringAssert.Contains(ex.Message, escenario.MatrizB.Id.ToString());

        await AssertEstadoOriginalAsync(dbPath, escenario, context);
        Assert.AreEqual(EntityState.Modified, context.Entry(componenteDependiente).State);
        Assert.AreEqual(3m, componenteDependiente.Cantidad);
    }

    [TestMethod]
    public async Task SaveAsync_ComponenteDeDependienteConImporteDerivadoPendiente_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        // Importe es derivado y la cascada lo reescribe en el contexto de la
        // operación; un pendiente de sesión podría sobrescribirlo después. Debe
        // rechazarse igual que cualquier otra edición de un componente dependiente.
        var componenteDependiente = escenario.MatrizB.Componentes.Single();
        componenteDependiente.Importe = 12_345m;
        context.ChangeTracker.DetectChanges();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(
                new SOPRO.Data.Factories.ProjectDbContextFactory(),
                dbPath,
                escenario.Dto,
                escenario.MatrizA,
                context));

        StringAssert.Contains(ex.Message, "componentes");
        StringAssert.Contains(ex.Message, escenario.MatrizB.Id.ToString());

        await AssertEstadoOriginalAsync(dbPath, escenario, context);
        Assert.AreEqual(EntityState.Modified, context.Entry(componenteDependiente).State);
        Assert.AreEqual(12_345m, componenteDependiente.Importe);
    }

    [TestMethod]
    public async Task SaveAsync_ComponenteAgregadoEnLaMatrizObjetivoPendiente_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        // Alta ajena a la copia de trabajo del editor: la reconciliación posterior
        // descarta todo componente rastreado de la matriz objetivo, así que debe
        // rechazarse antes de escribir en vez de perderlo en silencio.
        var componenteAgregado = new ComponenteMatriz
        {
            MatrizId = escenario.MatrizA.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = escenario.Material.Id,
            Cantidad = 4m,
            Importe = 400m,
            Orden = 2,
            Notas = string.Empty
        };
        context.ComponentesMatriz.Add(componenteAgregado);
        context.ChangeTracker.DetectChanges();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(
                new SOPRO.Data.Factories.ProjectDbContextFactory(),
                dbPath,
                escenario.Dto,
                escenario.MatrizA,
                context));

        StringAssert.Contains(ex.Message, "agregados, modificados o eliminados");
        StringAssert.Contains(ex.Message, escenario.MatrizA.Id.ToString());

        await AssertEstadoOriginalAsync(dbPath, escenario, context);
        Assert.AreEqual(EntityState.Added, context.Entry(componenteAgregado).State,
            "El alta pendiente debe seguir rastreada y sin persistir.");
        Assert.IsTrue(context.ChangeTracker.Entries<ComponenteMatriz>()
            .Any(entry => entry.State == EntityState.Added));
    }

    [TestMethod]
    public async Task SaveAsync_ComponenteEliminadoEnLaMatrizObjetivoPendiente_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);
        MarcarSentinel(context, escenario.Proyecto);

        var componenteObjetivo = context.ComponentesMatriz.Single(c => c.MatrizId == escenario.MatrizA.Id);
        context.ComponentesMatriz.Remove(componenteObjetivo);
        context.ChangeTracker.DetectChanges();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(
                new SOPRO.Data.Factories.ProjectDbContextFactory(),
                dbPath,
                escenario.Dto,
                escenario.MatrizA,
                context));

        StringAssert.Contains(ex.Message, "agregados, modificados o eliminados");
        StringAssert.Contains(ex.Message, escenario.MatrizA.Id.ToString());

        await AssertEstadoOriginalAsync(dbPath, escenario, context);
        Assert.AreEqual(EntityState.Deleted, context.Entry(componenteObjetivo).State,
            "La baja pendiente debe seguir rastreada y sin persistir.");
    }

    [TestMethod]
    public async Task SaveAsync_MatrizObjetivoConCambioPendienteEnConflicto_RechazaSinEscribir()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        escenario.MatrizA.Descripcion = "conflicto ajeno";
        context.ChangeTracker.DetectChanges();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(
                new SOPRO.Data.Factories.ProjectDbContextFactory(),
                dbPath,
                escenario.Dto,
                escenario.MatrizA,
                context));

        StringAssert.Contains(ex.Message, escenario.MatrizA.Id.ToString());

        var estado = await LeerEstadoPersistidoAsync(dbPath, escenario.MatrizA.Id, escenario.MatrizB.Id);
        Assert.AreEqual(DescripcionMatrizOriginal, estado.MatrizADescripcion);
        Assert.AreEqual(CostoDirectoOriginal, estado.MatrizACostoDirecto);
        Assert.AreEqual(1, estado.ComponentesA.Count);
        Assert.AreEqual(0m, estado.MatrizBCostoDirecto);
        Assert.AreEqual(EntityState.Modified, context.Entry(escenario.MatrizA).State);
    }

    [TestMethod]
    public async Task SaveAsync_ComponenteModificadoDeLaMatrizObjetivoPendienteEnSesion_RechazaSinEscribirYConservaElPendiente()
    {
        using var context = TestDbFactory.CreateContext();
        var dbPath = context.DatabasePath;
        var escenario = CrearEscenario(context);

        // El editor usa copias AsNoTracking: un ComponenteMatriz rastreado y
        // modificado en la matriz objetivo es trabajo ajeno. El reemplazo por
        // operación lo dejaría obsoleto y un guardado posterior podría
        // sobrescribir los valores recién confirmados.
        MarcarSentinel(context, escenario.Proyecto);
        var componenteObjetivo = context.ComponentesMatriz.Single(c => c.MatrizId == escenario.MatrizA.Id);
        componenteObjetivo.Cantidad = 9m;
        context.ChangeTracker.DetectChanges();

        var ex = await Assert.ThrowsExactlyAsync<InvalidOperationException>(
            () => MatrixApplicationService.SaveAsync(
                new SOPRO.Data.Factories.ProjectDbContextFactory(),
                dbPath,
                escenario.Dto,
                escenario.MatrizA,
                context));

        StringAssert.Contains(ex.Message, "agregados, modificados o eliminados");
        StringAssert.Contains(ex.Message, escenario.MatrizA.Id.ToString());

        await AssertEstadoOriginalAsync(dbPath, escenario, context);
        Assert.AreEqual(EntityState.Modified, context.Entry(componenteObjetivo).State,
            "El pendiente ajeno debe seguir rastreado y sin persistir.");
        Assert.AreEqual(9m, componenteObjetivo.Cantidad,
            "La guarda no debe mutar la copia de la sesión.");
    }

    // ── Escenario ─────────────────────────────────────────────────────────────

    private sealed record Escenario(Proyecto Proyecto, Material Material, Matriz MatrizA, Matriz MatrizB, MatrixEditDto Dto);

    private static Escenario CrearEscenario(SOPROContext context)
    {
        var proyecto = new Proyecto
        {
            Nombre = NombreProyectoOriginal,
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };

        var material = new Material
        {
            Clave = "MAT-TX",
            Descripcion = string.Empty,
            Unidad = "pza",
            PrecioUnitario = 100m,
            Notas = string.Empty
        };

        context.Proyectos.Add(proyecto);
        context.Materiales.Add(material);
        context.SaveChanges();

        var matrizA = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-A",
            Descripcion = DescripcionMatrizOriginal,
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = CostoDirectoOriginal,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizA);
        context.SaveChanges();

        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matrizA.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 1m,
            Importe = CostoDirectoOriginal,
            Orden = 1,
            Notas = string.Empty
        });

        var matrizB = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-B",
            Descripcion = "Dependiente",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        matrizB.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizA.Id,
            Cantidad = 2m,
            Importe = 0m,
            Orden = 1,
            Notas = string.Empty
        });
        context.Matrices.Add(matrizB);
        context.SaveChanges();

        var dto = new MatrixEditDto
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-A",
            Descripcion = DescripcionMatrizEditada,
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = CostoDirectoEditado,
            Componentes =
            {
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = material.Id,
                    Cantidad = 5m,
                    Importe = 500m,
                    Orden = 1,
                    Notas = string.Empty
                }
            }
        };

        return new Escenario(proyecto, material, matrizA, matrizB, dto);
    }

    /// <summary>
    /// Edición pendiente ajena a la operación: debe seguir rastreada y sin
    /// persistir tanto en éxito como en fallo.
    /// </summary>
    private static void MarcarSentinel(SOPROContext context, Proyecto proyecto)
    {
        proyecto.Nombre = NombreProyectoSentinel;
        context.ChangeTracker.DetectChanges();
        Assert.IsTrue(context.ChangeTracker.HasChanges());
    }

    // ── Verificación desde un contexto separado ───────────────────────────────

    private sealed record EstadoPersistido(
        string MatrizADescripcion,
        decimal MatrizACostoDirecto,
        IReadOnlyList<ComponenteMatriz> ComponentesA,
        decimal MatrizBCostoDirecto,
        IReadOnlyList<ComponenteMatriz> ComponentesB,
        string NombreProyecto);

    private static async Task<EstadoPersistido> LeerEstadoPersistidoAsync(string dbPath, int matrizAId, int matrizBId)
    {
        await using var verification = new SOPROContext(dbPath);

        var matrizA = await verification.Matrices.AsNoTracking().SingleAsync(m => m.Id == matrizAId);
        var matrizB = await verification.Matrices.AsNoTracking().SingleAsync(m => m.Id == matrizBId);
        var nombreProyecto = await verification.Proyectos.AsNoTracking()
            .Where(p => p.Id == matrizA.ProyectoId)
            .Select(p => p.Nombre)
            .SingleAsync();

        var componentesA = await verification.ComponentesMatriz.AsNoTracking()
            .Where(c => c.MatrizId == matrizAId)
            .OrderBy(c => c.Id)
            .ToListAsync();
        var componentesB = await verification.ComponentesMatriz.AsNoTracking()
            .Where(c => c.MatrizId == matrizBId)
            .OrderBy(c => c.Id)
            .ToListAsync();

        return new EstadoPersistido(
            matrizA.Descripcion,
            matrizA.CostoDirecto,
            componentesA,
            matrizB.CostoDirecto,
            componentesB,
            nombreProyecto);
    }

    private static async Task AssertEstadoOriginalAsync(string dbPath, Escenario escenario, SOPROContext context)
    {
        var estado = await LeerEstadoPersistidoAsync(dbPath, escenario.MatrizA.Id, escenario.MatrizB.Id);

        Assert.AreEqual(DescripcionMatrizOriginal, estado.MatrizADescripcion,
            "La edición de la matriz padre debe revertirse.");
        Assert.AreEqual(CostoDirectoOriginal, estado.MatrizACostoDirecto,
            "El costo de la matriz padre debe revertirse.");
        Assert.AreEqual(1, estado.ComponentesA.Count,
            "Debe conservarse el componente original, no el lote nuevo.");
        Assert.AreEqual(1m, estado.ComponentesA[0].Cantidad);
        Assert.AreEqual(CostoDirectoOriginal, estado.ComponentesA[0].Importe);

        Assert.AreEqual(0m, estado.MatrizBCostoDirecto,
            "La matriz dependiente no debe quedar con el costo cascado.");
        Assert.AreEqual(1, estado.ComponentesB.Count);
        Assert.AreEqual(0m, estado.ComponentesB[0].Importe,
            "Los importes de la matriz dependiente no deben quedar recalculados.");

        Assert.AreEqual(NombreProyectoOriginal, estado.NombreProyecto,
            "El trabajo pendiente de la sesión no debe persistirse.");

        Assert.AreEqual(EntityState.Modified, context.Entry(escenario.Proyecto).State,
            "El sentinel debe seguir rastreado como Modified.");
        Assert.IsTrue(context.ChangeTracker.HasChanges(),
            "El servicio no debe limpiar el tracker del llamador.");
        Assert.AreEqual(NombreProyectoSentinel, escenario.Proyecto.Nombre);
    }
}
