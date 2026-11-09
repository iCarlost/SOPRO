using System;
using System.Threading.Tasks;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.EntityFrameworkCore;
using SOPRO.Application.DTOs.Catalog;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.CostoHorario;

[TestClass]
public sealed class MaquinariaInsumoLinkServiceTests
{
    [TestMethod]
    public async Task SaveMaquinaria_EnlazaMismoMaterialEnAmbosRolesYDesenlazaConservandoEfectivos()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        scenario.Cemento.PrecioUnitario = 12m;
        var operatorSource = new ManoDeObra
        {
            ProyectoId = scenario.Proyecto.Id, Clave = "OP", Descripcion = "Operador", Unidad = "jor",
            SalarioBase = 80m, FactorSalarioReal = 1.75m, SalarioReal = 140m, Notas = string.Empty
        };
        context.ManoDeObra.Add(operatorSource);
        await context.SaveChangesAsync();

        var dto = NewDto(scenario.Proyecto.Id);
        dto.MaterialCombustibleId = scenario.Cemento.Id;
        dto.MaterialAceiteId = scenario.Cemento.Id;
        dto.ManoDeObraOperadorId = operatorSource.Id;
        var saveResult = await CatalogItemService.SaveMaquinariaAsync(context, dto);
        Assert.IsFalse(saveResult.TriggeredRecalculation);
        var machine = await context.Maquinaria.FindAsync(saveResult.EntityId) ?? throw new InvalidOperationException();

        Assert.AreEqual(12m, machine.PrecioCombustible);
        Assert.AreEqual(12m, machine.PrecioAceite);
        Assert.AreEqual(80m, machine.SalarioOperador);
        Assert.AreEqual(1.75m, machine.FactorSalarioReal);
        var fuel = machine.PrecioCombustible;
        var oil = machine.PrecioAceite;
        var wage = machine.SalarioOperador;
        var fsr = machine.FactorSalarioReal;

        dto.MaterialCombustibleId = null;
        dto.MaterialAceiteId = null;
        dto.ManoDeObraOperadorId = null;
        await CatalogItemService.SaveMaquinariaAsync(context, dto, machine);

        Assert.IsNull(machine.MaterialCombustibleId);
        Assert.IsNull(machine.MaterialAceiteId);
        Assert.IsNull(machine.ManoDeObraOperadorId);
        Assert.AreEqual(fuel, machine.PrecioCombustible);
        Assert.AreEqual(oil, machine.PrecioAceite);
        Assert.AreEqual(wage, machine.SalarioOperador);
        Assert.AreEqual(fsr, machine.FactorSalarioReal);
    }

    [TestMethod]
    public async Task SaveMaquinaria_RechazaVinculosEnCostoManualYPropiedadCruzada()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var dto = NewDto(scenario.Proyecto.Id);
        dto.EsCostoCalculado = false;
        dto.MaterialCombustibleId = scenario.Cemento.Id;
        AssertInvalidOperation(() => CatalogItemService.SaveMaquinariaAsync(context, dto).GetAwaiter().GetResult());

        dto.EsCostoCalculado = true;
        dto.MaterialCombustibleId = scenario.Cemento.Id;
        dto.ProyectoId = scenario.Proyecto.Id + 100;
        AssertInvalidOperation(() => CatalogItemService.SaveMaquinariaAsync(context, dto).GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task SaveMaterial_ActualizaMaquinariaVinculadaYMatrizYPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var machine = scenario.Revolvedora;
        machine.CantidadCombustible = 2m;
        machine.CantidadAceite = 1m;
        machine.MaterialCombustibleId = scenario.Cemento.Id;
        machine.MaterialAceiteId = scenario.Cemento.Id;
        machine.PrecioCombustible = scenario.Cemento.PrecioUnitario;
        machine.CalcularCostoHorario();
        await context.SaveChangesAsync();
        var budgetBefore = scenario.Concepto.CostoDirectoTotal;

        await CatalogItemService.SaveMaterialAsync(context, new MaterialEditDto
        {
            Clave = scenario.Cemento.Clave, Descripcion = scenario.Cemento.Descripcion,
            Unidad = scenario.Cemento.Unidad, PrecioUnitario = 75m,
            ProyectoId = scenario.Proyecto.Id, Notas = scenario.Cemento.Notas
        }, scenario.Cemento);

        Assert.AreEqual(75m, machine.PrecioCombustible);
        Assert.AreEqual(75m, machine.PrecioAceite);
        Assert.AreNotEqual(budgetBefore, scenario.Concepto.CostoDirectoTotal);
        Assert.AreEqual(scenario.MatrizApu.CostoDirecto, scenario.Concepto.CostoDirectoUnitario);
    }

    [TestMethod]
    public async Task SaveMaquinaria_ValidaPropiedadMaestraYProyectoIncluyendoUpdateConDtoSinProyecto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var globalMaterial = MakeMaterial("MAT-GLOBAL", null);
        var globalLabor = MakeLabor("MO-GLOBAL", null);
        var projectLabor = MakeLabor("MO-PROJECT", scenario.Proyecto.Id);
        var otherProject = new Proyecto { Nombre = "Otro proyecto", Descripcion = string.Empty, Ubicacion = string.Empty,
            Convocante = string.Empty, Contratista = string.Empty, ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1), FechaTermino = new DateTime(2026, 1, 31), PlazoEjecucion = 31 };
        context.Proyectos.Add(otherProject);
        await context.SaveChangesAsync();
        context.Materiales.AddRange(globalMaterial, MakeMaterial("MAT-PROJECT", scenario.Proyecto.Id), MakeMaterial("MAT-OTHER", otherProject.Id));
        context.ManoDeObra.AddRange(globalLabor, projectLabor, MakeLabor("MO-OTHER", otherProject.Id));
        await context.SaveChangesAsync();

        var masterDto = NewDto(null);
        masterDto.GuardarEnMaestro = true;
        masterDto.MaterialCombustibleId = globalMaterial.Id;
        masterDto.ManoDeObraOperadorId = globalLabor.Id;
        var master = await CatalogItemService.SaveMaquinariaAsync(context, masterDto);
        Assert.IsNotNull(await context.Maquinaria.SingleOrDefaultAsync(m => m.Id == master.EntityId && m.ProyectoId == null));

        masterDto.MaterialCombustibleId = scenario.Cemento.Id;
        AssertInvalidOperation(() => CatalogItemService.SaveMaquinariaAsync(context, masterDto).GetAwaiter().GetResult());
        masterDto.MaterialCombustibleId = null;
        masterDto.ManoDeObraOperadorId = projectLabor.Id;
        AssertInvalidOperation(() => CatalogItemService.SaveMaquinariaAsync(context, masterDto).GetAwaiter().GetResult());

        var projectDto = NewDto(scenario.Proyecto.Id);
        projectDto.MaterialCombustibleId = globalMaterial.Id;
        AssertInvalidOperation(() => CatalogItemService.SaveMaquinariaAsync(context, projectDto).GetAwaiter().GetResult());
        projectDto.MaterialCombustibleId = context.Materiales.Single(m => m.Clave == "MAT-OTHER").Id;
        AssertInvalidOperation(() => CatalogItemService.SaveMaquinariaAsync(context, projectDto).GetAwaiter().GetResult());

        projectDto.MaterialCombustibleId = scenario.Cemento.Id;
        var projectMachine = await CatalogItemService.SaveMaquinariaAsync(context, projectDto);
        projectDto.ProyectoId = null; // ApplyOwnership permite omitirlo en una actualización.
        projectDto.MaterialCombustibleId = globalMaterial.Id;
        var trackedProjectMachine = await context.Maquinaria.SingleAsync(m => m.Id == projectMachine.EntityId);
        AssertInvalidOperation(() => CatalogItemService.SaveMaquinariaAsync(context, projectDto, trackedProjectMachine).GetAwaiter().GetResult());
    }

    [TestMethod]
    public async Task SaveManoDeObra_ActualizaVinculoIndividualYPropagaHastaPresupuesto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var machine = scenario.Revolvedora;
        machine.ManoDeObraOperadorId = scenario.Oficial.Id;
        machine.SalarioOperador = scenario.Oficial.SalarioBase;
        machine.FactorSalarioReal = scenario.Oficial.FactorSalarioReal;
        machine.CalcularCostoHorario();
        await context.SaveChangesAsync();
        var budgetBefore = scenario.Concepto.CostoDirectoTotal;

        await CatalogItemService.SaveManoDeObraAsync(context, new ManoDeObraEditDto
        {
            Clave = scenario.Oficial.Clave, Descripcion = scenario.Oficial.Descripcion,
            Unidad = scenario.Oficial.Unidad, SalarioBase = 90m, FactorSalarioReal = 2.25m,
            SalarioReal = 202.5m, ProyectoId = scenario.Proyecto.Id, Notas = scenario.Oficial.Notas
        }, scenario.Oficial);

        Assert.AreEqual(90m, machine.SalarioOperador);
        Assert.AreEqual(2.25m, machine.FactorSalarioReal);
        Assert.AreNotEqual(budgetBefore, scenario.Concepto.CostoDirectoTotal);
        Assert.AreEqual(scenario.MatrizApu.CostoDirecto, scenario.Concepto.CostoDirectoUnitario);
    }

    [TestMethod]
    public async Task SaveMaterial_ConCicloRevierteFuenteMaquinariaMatrizYPresupuesto()
    {
        var path = TestDbFactory.CreateTempDbPath();
        using (var context = TestDbFactory.CreateContextAt(path))
        {
            var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
            var machine = scenario.Revolvedora;
            machine.CantidadCombustible = 2m;
            machine.MaterialCombustibleId = scenario.Cemento.Id;
            machine.PrecioCombustible = scenario.Cemento.PrecioUnitario;
            machine.CalcularCostoHorario();
            var cycle = new Matriz { ProyectoId = scenario.Proyecto.Id, Clave = "CYCLE", Descripcion = "", Unidad = "m", Tipo = TipoMatriz.Basico };
            context.Matrices.Add(cycle);
            await context.SaveChangesAsync();
            context.ComponentesMatriz.AddRange(
                new ComponenteMatriz { MatrizId = scenario.MatrizApu.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = cycle.Id, Cantidad = 1m },
                new ComponenteMatriz { MatrizId = cycle.Id, TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = scenario.MatrizApu.Id, Cantidad = 1m });
            await context.SaveChangesAsync();
            var oldPrice = scenario.Cemento.PrecioUnitario;
            var oldEffective = machine.PrecioCombustible;
            var oldHourlyCost = machine.CostoHorario;
            var oldMatrix = scenario.MatrizApu.CostoDirecto;
            var oldBudget = scenario.Concepto.CostoDirectoTotal;

            await Assert.ThrowsExactlyAsync<InvalidOperationException>(() => CatalogItemService.SaveMaterialAsync(context, new MaterialEditDto
            {
                Clave = scenario.Cemento.Clave, Descripcion = scenario.Cemento.Descripcion, Unidad = scenario.Cemento.Unidad,
                PrecioUnitario = 999m, ProyectoId = scenario.Proyecto.Id, Notas = scenario.Cemento.Notas
            }, scenario.Cemento));

            using var fresh = new SOPROContext(path);
            Assert.AreEqual(oldPrice, fresh.Materiales.Single(m => m.Id == scenario.Cemento.Id).PrecioUnitario);
            Assert.AreEqual(oldEffective, fresh.Maquinaria.Single(m => m.Id == machine.Id).PrecioCombustible);
            Assert.AreEqual(oldHourlyCost, fresh.Maquinaria.Single(m => m.Id == machine.Id).CostoHorario);
            Assert.AreEqual(oldMatrix, fresh.Matrices.Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
            Assert.AreEqual(oldBudget, fresh.ConceptosPresupuesto.Single(c => c.Id == scenario.Concepto.Id).CostoDirectoTotal);
        }
    }

    private static MaquinariaEditDto NewDto(int? projectId) => new()
    {
        Clave = "MAQ-LINK", Descripcion = "Equipo vinculado", ProyectoId = projectId,
        EsCostoCalculado = true, CostoHorario = 0m, PotenciaNominal = 100m,
        TipoCombustible = TipoCombustible.Diesel
    };

    private static Material MakeMaterial(string key, int? projectId) => new()
    {
        ProyectoId = projectId, Clave = key, Descripcion = key, Unidad = "u", PrecioUnitario = 10m, Notas = string.Empty
    };

    private static ManoDeObra MakeLabor(string key, int? projectId) => new()
    {
        ProyectoId = projectId, Clave = key, Descripcion = key, Unidad = "jor", SalarioBase = 20m,
        FactorSalarioReal = 1.5m, SalarioReal = 30m, Notas = string.Empty
    };

    private static void AssertInvalidOperation(Action action)
    {
        try { action(); }
        catch (InvalidOperationException) { return; }
        Assert.Fail("Se esperaba InvalidOperationException.");
    }
}
