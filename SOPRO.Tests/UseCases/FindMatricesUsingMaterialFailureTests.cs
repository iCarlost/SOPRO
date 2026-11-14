using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.UseCases.Materials;
using SOPRO.Data.Context;
using SOPRO.Data.Factories;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.UseCases;

/// <summary>
/// Frontera de error de "Dónde se usa": la creación del contexto (primer y
/// segundo paso de <see cref="FindMatricesUsingMaterial.ExecuteDetailed"/>) se
/// resuelve como <see cref="Result{T}"/> fallido o cancelación, nunca como
/// excepción no controlada. La liberación del contexto por operación también
/// queda dentro de la frontera.
/// </summary>
[TestClass]
public class FindMatricesUsingMaterialFailureTests
{
    private static readonly IProjectDbContextFactory FabricaReal = new ProjectDbContextFactory();

    [TestMethod]
    public async Task Execute_CreacionDeContextoFalla_DevuelveDatabaseSinExcepcion()
    {
        var factory = new ScriptedFactory((_, _) =>
            Task.FromException<SOPROContext>(new InvalidOperationException("fallo de fábrica")));
        var session = SessionSinBase();

        Result<IReadOnlyList<MaterialUsageInMatrix>> result =
            await new FindMatricesUsingMaterial(factory).Execute(
                session, new FindMatricesUsingMaterialRequest(1));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);
        Assert.AreEqual("No se pudo consultar dónde se usa el material.", result.Error.Message);
        StringAssert.Contains(result.Error.Detail, "fallo de fábrica");
    }

    [TestMethod]
    public async Task ExecuteDetailed_PrimeraCreacionDeContextoFalla_DevuelveDatabaseSinExcepcion()
    {
        var factory = new ScriptedFactory((_, _) =>
            Task.FromException<SOPROContext>(new InvalidOperationException("fallo de fábrica")));
        var session = SessionSinBase();

        Result<MaterialUsageDetail> result =
            await new FindMatricesUsingMaterial(factory).ExecuteDetailed(
                session, new FindMatricesUsingMaterialRequest(1));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);
        Assert.AreEqual("No se pudo consultar dónde se usa el material.", result.Error.Message);
        StringAssert.Contains(result.Error.Detail, "fallo de fábrica");
    }

    [TestMethod]
    public async Task ExecuteDetailed_SegundaCreacionDeContextoFalla_DevuelveDatabase()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(context, scenario.Proyecto.Id);
        var factory = new ScriptedFactory(
            (path, _) => Task.FromResult<SOPROContext>(new SOPROContext(path)),
            (_, _) => Task.FromException<SOPROContext>(new InvalidOperationException("fallo de la segunda fábrica")));

        Result<MaterialUsageDetail> result =
            await new FindMatricesUsingMaterial(factory).ExecuteDetailed(
                session, new FindMatricesUsingMaterialRequest(scenario.Cemento.Id));

        Assert.IsFalse(result.IsSuccess,
            "La primera consulta sí resuelve, pero el segundo contexto no debe escapar como excepción.");
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);
        Assert.AreEqual("No se pudo consultar dónde se usa el material.", result.Error.Message);
        StringAssert.Contains(result.Error.Detail, "fallo de la segunda fábrica");
        Assert.AreEqual(2, factory.Creaciones, "ExecuteDetailed abre un contexto por operación.");
    }

    [TestMethod]
    public async Task Execute_TokenYaCancelado_PropagaCancelacion()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new FindMatricesUsingMaterial(FabricaReal).Execute(
                SessionSinBase(), new FindMatricesUsingMaterialRequest(1), cts.Token));
    }

    [TestMethod]
    public async Task Execute_FactoryCancelaLaCreacion_PropagaCancelacion()
    {
        var factory = new ScriptedFactory((_, token) =>
            Task.FromException<SOPROContext>(new OperationCanceledException(token)));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new FindMatricesUsingMaterial(factory).Execute(
                SessionSinBase(), new FindMatricesUsingMaterialRequest(1)));
    }

    [TestMethod]
    public async Task ExecuteDetailed_SegundaCreacionCancelada_PropagaCancelacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(context, scenario.Proyecto.Id);
        var factory = new ScriptedFactory(
            (path, _) => Task.FromResult<SOPROContext>(new SOPROContext(path)),
            (_, token) => Task.FromException<SOPROContext>(new OperationCanceledException(token)));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new FindMatricesUsingMaterial(factory).ExecuteDetailed(
                session, new FindMatricesUsingMaterialRequest(scenario.Cemento.Id)));
    }

    [TestMethod]
    public async Task Execute_FalloAlLiberarElContexto_DevuelveDatabase()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(context, scenario.Proyecto.Id);
        var factory = new ScriptedFactory((path, _) =>
            Task.FromResult<SOPROContext>(new DisposeThrowingContext(
                path, new InvalidOperationException("fallo al liberar"))));

        Result<IReadOnlyList<MaterialUsageInMatrix>> result =
            await new FindMatricesUsingMaterial(factory).Execute(
                session, new FindMatricesUsingMaterialRequest(scenario.Cemento.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);
        StringAssert.Contains(result.Error.Detail, "fallo al liberar");
    }

    [TestMethod]
    public async Task ExecuteDetailed_FalloAlLiberarElSegundoContexto_DevuelveDatabase()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(context, scenario.Proyecto.Id);
        var factory = new ScriptedFactory(
            (path, _) => Task.FromResult<SOPROContext>(new SOPROContext(path)),
            (path, _) => Task.FromResult<SOPROContext>(new DisposeThrowingContext(
                path, new InvalidOperationException("fallo al liberar el segundo"))));

        Result<MaterialUsageDetail> result =
            await new FindMatricesUsingMaterial(factory).ExecuteDetailed(
                session, new FindMatricesUsingMaterialRequest(scenario.Cemento.Id));

        Assert.IsFalse(result.IsSuccess);
        Assert.AreEqual(AppErrorCode.Database, result.Error!.Code);
        StringAssert.Contains(result.Error.Detail, "fallo al liberar el segundo");
    }

    [TestMethod]
    public async Task Execute_LiberacionCancelada_PropagaCancelacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(context, scenario.Proyecto.Id);
        var factory = new ScriptedFactory((path, _) =>
            Task.FromResult<SOPROContext>(new DisposeThrowingContext(
                path, new OperationCanceledException())));

        await Assert.ThrowsExactlyAsync<OperationCanceledException>(() =>
            new FindMatricesUsingMaterial(factory).Execute(
                session, new FindMatricesUsingMaterialRequest(scenario.Cemento.Id)));
    }

    [TestMethod]
    public async Task ExecuteDetailed_DisposaCadaContextoDeLaOperacion()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var session = CrearSession(context, scenario.Proyecto.Id);
        var contextos = new List<CountingContext>();
        var factory = new ScriptedFactory(
            (path, _) => Crear(path),
            (path, _) => Crear(path));

        Result<MaterialUsageDetail> result =
            await new FindMatricesUsingMaterial(factory).ExecuteDetailed(
                session, new FindMatricesUsingMaterialRequest(scenario.Cemento.Id));

        Assert.IsTrue(result.IsSuccess, result.Error?.Message);
        Assert.AreEqual(2, contextos.Count, "Un contexto por operación de I/O.");
        Assert.IsTrue(contextos.All(c => c.Disposals == 1),
            "Cada contexto se libera exactamente una vez, dentro de su operación.");

        Task<SOPROContext> Crear(string path)
        {
            var creado = new CountingContext(path);
            contextos.Add(creado);
            return Task.FromResult<SOPROContext>(creado);
        }
    }

    private static ProjectSessionInfo SessionSinBase()
        => ProjectSessionInfo.Create(new ProjectRef(1, "Proyecto"), TestDbFactory.CreateTempDbPath());

    private static ProjectSessionInfo CrearSession(SOPROContext context, int proyectoId)
    {
        var proyecto = context.Proyectos.Find(proyectoId)
            ?? throw new InvalidOperationException("Proyecto de prueba no encontrado.");
        return ProjectSessionInfo.Create(
            ProjectRef.FromEntity(proyecto),
            context.DatabasePath,
            decimalesImporte: proyecto.DecimalesImporte);
    }

    private sealed class ScriptedFactory : IProjectDbContextFactory
    {
        private readonly Func<string, CancellationToken, Task<SOPROContext>>[] _pasos;
        private int _indice;

        public ScriptedFactory(params Func<string, CancellationToken, Task<SOPROContext>>[] pasos)
            => _pasos = pasos;

        public int Creaciones => _indice;

        public SOPROContext Create(string databasePath)
            => CreateAsync(databasePath).GetAwaiter().GetResult();

        public Task<SOPROContext> CreateAsync(
            string databasePath, CancellationToken cancellationToken = default)
        {
            if (_indice >= _pasos.Length)
                throw new InvalidOperationException("La prueba pidió más contextos de los previstos.");

            return _pasos[_indice++](databasePath, cancellationToken);
        }
    }

    private class CountingContext : SOPROContext
    {
        public CountingContext(string databasePath) : base(databasePath)
        {
        }

        public int Disposals { get; private set; }

        public override async ValueTask DisposeAsync()
        {
            Disposals++;
            await base.DisposeAsync();
        }
    }

    private sealed class DisposeThrowingContext : SOPROContext
    {
        private readonly Exception _error;

        public DisposeThrowingContext(string databasePath, Exception error) : base(databasePath)
            => _error = error;

        public override async ValueTask DisposeAsync()
        {
            await base.DisposeAsync();
            throw _error;
        }
    }
}
