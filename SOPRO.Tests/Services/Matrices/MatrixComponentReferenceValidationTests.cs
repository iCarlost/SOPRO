using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Contracts;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models.Matrices;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Matrices;

/// <summary>
/// Issue #58 (defectos relacionados): un componente de matriz debe referenciar
/// exactamente la entidad que exige su TipoComponente. Guardar o recalcular una
/// fila con la referencia ausente/wrong produce una navegación nula; aquí se
/// verifica que el guardado la rechace antes de persistir y que el recálculo
/// global reporte un error tipado con matriz y componente, sin escribir precios.
/// SQLite real con el esquema de producción.
/// </summary>
[TestClass]
public class MatrixComponentReferenceValidationTests
{
    private static readonly TipoComponenteMatriz[] Tipos =
    {
        TipoComponenteMatriz.Material,
        TipoComponenteMatriz.ManoDeObra,
        TipoComponenteMatriz.Maquinaria,
        TipoComponenteMatriz.Herramienta,
        TipoComponenteMatriz.Auxiliar
    };

    // ---------- Guardado: referencia ausente ----------

    [TestMethod]
    public async Task SaveAsync_ComponenteNuevoSinReferenciaPorTipo_RechazaYNoPersiste()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        int matricesAntes = context.Matrices.Count();
        int componentesAntes = context.ComponentesMatriz.Count();

        foreach (var tipo in Tipos)
        {
            var dto = DtoNuevo(scenario.Proyecto.Id, tipo, new MatrixComponentEditDto
            {
                TipoComponente = tipo,
                Cantidad = 1m,
                Orden = 1,
                Notas = string.Empty
            });

            var ex = await Assert.ThrowsExactlyAsync<MatrixComponentReferenceException>(
                () => MatrixApplicationService.SaveAsync(context, dto, null),
                $"El tipo {tipo} sin su referencia debe rechazarse.");

            StringAssert.Contains(ex.Message, NombreReferencia(tipo),
                $"El mensaje debe nombrar la referencia requerida para {tipo}.");
        }

        Assert.AreEqual(matricesAntes, context.Matrices.Count(),
            "Ninguna matriz inválida debe persistirse.");
        Assert.AreEqual(componentesAntes, context.ComponentesMatriz.Count(),
            "Ningún componente inválido debe persistirse.");
    }

    [TestMethod]
    public async Task SaveAsync_ComponenteExistenteSinReferenciaPorTipo_RechazaYConservaComponentes()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        int componentesAntes = context.ComponentesMatriz.Count(c => c.MatrizId == scenario.MatrizApu.Id);
        decimal costoAntes = scenario.MatrizApu.CostoDirecto;

        foreach (var tipo in Tipos)
        {
            var dto = DtoNuevo(scenario.Proyecto.Id, tipo, new MatrixComponentEditDto
            {
                TipoComponente = tipo,
                Cantidad = 1m,
                Orden = 1,
                Notas = string.Empty
            });
            dto.Clave = scenario.MatrizApu.Clave;
            dto.Descripcion = scenario.MatrizApu.Descripcion;
            dto.Unidad = scenario.MatrizApu.Unidad;
            dto.Tipo = scenario.MatrizApu.Tipo;

            await Assert.ThrowsExactlyAsync<MatrixComponentReferenceException>(
                () => MatrixApplicationService.SaveAsync(context, dto, scenario.MatrizApu),
                $"La actualización con {tipo} sin su referencia debe rechazarse.");
        }

        context.ChangeTracker.Clear();
        Assert.AreEqual(componentesAntes, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == scenario.MatrizApu.Id),
            "La validación previa no debe eliminar los componentes existentes.");
        Assert.AreEqual(costoAntes, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
    }

    [TestMethod]
    public async Task SaveAsync_ComponenteConReferenciaDeOtroTipo_RechazaYNoPersiste()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var dto = DtoNuevo(scenario.Proyecto.Id, TipoComponenteMatriz.Material, new MatrixComponentEditDto
        {
            TipoComponente = TipoComponenteMatriz.Material,
            ManoDeObraId = scenario.Oficial.Id,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });

        var ex = await Assert.ThrowsExactlyAsync<MatrixComponentReferenceException>(
            () => MatrixApplicationService.SaveAsync(context, dto, null));

        StringAssert.Contains(ex.Message, nameof(ComponenteMatriz.MaterialId));
        StringAssert.Contains(ex.Message, nameof(ComponenteMatriz.ManoDeObraId));

        context.ChangeTracker.Clear();
        Assert.IsFalse(context.Matrices.Any(m => m.Clave == dto.Clave),
            "Una referencia cruzada no debe persistir la matriz.");
    }

    // ---------- Guardado: referencia correcta para los cinco tipos ----------

    [TestMethod]
    public async Task SaveAsync_ReferenciasCorrectasDeLosCincoTipos_GuardaTodosLosComponentes()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var basico = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "BAS-REF",
            Descripcion = "Básico para referencia auxiliar",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        context.Matrices.Add(basico);
        context.SaveChanges();

        var dto = new MatrixEditDto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "APU-CINCO-TIPOS",
            Descripcion = "Matriz con los cinco tipos de componente",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Componentes =
            {
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = scenario.Cemento.Id,
                    Cantidad = 1m, Orden = 1, Notas = string.Empty
                },
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.ManoDeObra,
                    ManoDeObraId = scenario.Oficial.Id,
                    Cantidad = 1m, Orden = 2, Notas = string.Empty
                },
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Maquinaria,
                    MaquinariaId = scenario.Revolvedora.Id,
                    Cantidad = 1m, Orden = 3, Notas = string.Empty
                },
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Herramienta,
                    HerramientaId = scenario.HerramientaMenorPorcentajeMo.Id,
                    Cantidad = 1m, Orden = 4, Notas = string.Empty
                },
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Auxiliar,
                    AuxiliarId = basico.Id,
                    Cantidad = 1m, Orden = 5, Notas = string.Empty
                }
            }
        };

        var result = await MatrixApplicationService.SaveAsync(context, dto, null);

        Assert.IsTrue(result.MatrixId > 0);

        context.ChangeTracker.Clear();
        var guardados = context.ComponentesMatriz.AsNoTracking()
            .Where(c => c.MatrizId == result.MatrixId)
            .OrderBy(c => c.Orden)
            .ToList();

        Assert.AreEqual(5, guardados.Count);
        Assert.AreEqual(scenario.Cemento.Id, guardados.Single(c => c.TipoComponente == TipoComponenteMatriz.Material).MaterialId);
        Assert.AreEqual(scenario.Oficial.Id, guardados.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra).ManoDeObraId);
        Assert.AreEqual(scenario.Revolvedora.Id, guardados.Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).MaquinariaId);
        Assert.AreEqual(scenario.HerramientaMenorPorcentajeMo.Id, guardados.Single(c => c.TipoComponente == TipoComponenteMatriz.Herramienta).HerramientaId);
        Assert.AreEqual(basico.Id, guardados.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar).AuxiliarId);
    }

    // ---------- Recálculo global: error tipado y sin escritura ----------

    [TestMethod]
    public void RecalculateParaVista_FilaMalformada_DevuelveTotalesYNoLanza()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (matriz, componenteMalformado) = AgregarAuxiliarSinReferencia(context, scenario);

        context.ComponentesMatriz.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = scenario.Cemento.Id,
            Cantidad = 1m,
            Orden = 2,
            Notas = string.Empty
        });
        context.SaveChanges();
        context.ChangeTracker.Clear();

        var componentes = context.ComponentesMatriz
            .Include(c => c.Material)
            .Include(c => c.Auxiliar)
            .Where(c => c.MatrizId == matriz.Id)
            .OrderBy(c => c.Orden)
            .ToList();

        var totales = MatrixComponentCalculationService.RecalculateParaVista(componentes, 2);

        Assert.AreEqual(2, componentes.Count,
            "La fila malformada debe seguir en la lista para que el grid la muestre y permita eliminarla.");
        Assert.AreEqual(60.00m, totales.TotalMaterial,
            "Solo el material válido contribuye a los totales de la vista.");
        Assert.AreEqual(60.00m, totales.CostoDirectoTotal);
        Assert.IsTrue(componentes.Any(c => c.Id == componenteMalformado.Id));

        var filas = MatrixComponentPresentationService.BuildRows(componentes, totales.BaseManoObra);
        Assert.AreEqual(2, filas.Count,
            "La fila malformada sigue visible en el grid, no se oculta.");
        Assert.IsFalse(string.IsNullOrWhiteSpace(filas[0].Tipo),
            "La fila malformada conserva una etiqueta de tipo para poder identificarla y eliminarla.");
    }

    [TestMethod]
    public async Task FilaMalformada_RecalculoEstrictoYGuardado_RechazanConErrorTipado()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (matriz, componenteMalformado) = AgregarAuxiliarSinReferencia(context, scenario);

        context.ChangeTracker.Clear();
        var componentes = context.ComponentesMatriz
            .Include(c => c.Auxiliar)
            .Where(c => c.MatrizId == matriz.Id)
            .ToList();

        var estricto = Assert.ThrowsExactly<MatrixComponentReferenceException>(
            () => MatrixComponentCalculationService.Recalculate(componentes, 2, matriz));
        StringAssert.Contains(estricto.Message, matriz.Clave);
        StringAssert.Contains(estricto.Message, componenteMalformado.Id.ToString());

        var dto = new MatrixEditDto
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = matriz.Clave,
            Descripcion = matriz.Descripcion,
            Unidad = matriz.Unidad,
            Tipo = matriz.Tipo,
            CostoDirecto = 0m,
            Componentes =
            {
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Auxiliar,
                    Cantidad = 1m,
                    Orden = 1,
                    Notas = string.Empty
                }
            }
        };

        var guardado = await Assert.ThrowsExactlyAsync<MatrixComponentReferenceException>(
            () => MatrixApplicationService.SaveAsync(context, dto, matriz));
        StringAssert.Contains(guardado.Message, nameof(ComponenteMatriz.AuxiliarId));

        context.ChangeTracker.Clear();
        Assert.AreEqual(1, context.ComponentesMatriz.AsNoTracking()
            .Count(c => c.MatrizId == matriz.Id),
            "El guardado rechazado no debe alterar los componentes existentes.");
    }

    [TestMethod]
    public void RecalculateParaVista_DatosValidos_CoincideConRecalculoEstricto()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var componentes = scenario.MatrizApu.Componentes.ToList();

        var vista = MatrixComponentCalculationService.RecalculateParaVista(componentes, 2);
        var estricto = MatrixComponentCalculationService.Recalculate(componentes, 2);

        Assert.AreEqual(estricto.TotalMaterial, vista.TotalMaterial);
        Assert.AreEqual(estricto.BaseManoObra, vista.BaseManoObra);
        Assert.AreEqual(estricto.TotalManoObra, vista.TotalManoObra);
        Assert.AreEqual(estricto.TotalManoObraResumen, vista.TotalManoObraResumen);
        Assert.AreEqual(estricto.TotalMaquinaria, vista.TotalMaquinaria);
        Assert.AreEqual(estricto.TotalBasicos, vista.TotalBasicos);
        Assert.AreEqual(estricto.TotalHerramientas, vista.TotalHerramientas);
        Assert.AreEqual(estricto.CostoDirectoTotal, vista.CostoDirectoTotal);
    }

    [TestMethod]
    public void RecalculoGlobal_ComponenteAuxiliarSinReferencia_LanzaErrorTipadoConMatrizYComponente()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (matriz, componente) = AgregarAuxiliarSinReferencia(context, scenario);

        var ex = Assert.ThrowsExactly<MatrixComponentReferenceException>(
            () => new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id));

        StringAssert.Contains(ex.Message, matriz.Clave);
        StringAssert.Contains(ex.Message, componente.Id.ToString());
        StringAssert.Contains(ex.Message, nameof(ComponenteMatriz.AuxiliarId));

        context.ChangeTracker.Clear();
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto,
            "El rechazo de la fase 1 no debe escribir precios.");
        Assert.AreEqual(0m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == matriz.Id).CostoDirecto);
        Assert.AreEqual(100.00m, context.ConceptosPresupuesto.AsNoTracking()
            .Single(c => c.Id == scenario.Concepto.Id).CostoDirectoUnitario);
    }

    [TestMethod]
    public void RecalculoGlobal_TryEjecutar_ReportaValidacionYDejaCostosIntactos()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var (matriz, componente) = AgregarAuxiliarSinReferencia(context, scenario);

        decimal importeMaterialAntes = context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.MatrizId == scenario.MatrizApu.Id && c.Orden == 1).Importe;

        var resultado = new RecalculoGlobalService().TryEjecutar(context, scenario.Proyecto.Id);

        Assert.IsFalse(resultado.IsSuccess);
        Assert.AreEqual(AppErrorCode.Validation, resultado.Error!.Code);
        StringAssert.Contains(resultado.Error.Message, matriz.Clave);
        StringAssert.Contains(resultado.Error.Message, componente.Id.ToString());

        context.ChangeTracker.Clear();
        Assert.AreEqual(100.00m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == scenario.MatrizApu.Id).CostoDirecto);
        Assert.AreEqual(0m, context.Matrices.AsNoTracking()
            .Single(m => m.Id == matriz.Id).CostoDirecto);
        Assert.AreEqual(importeMaterialAntes, context.ComponentesMatriz.AsNoTracking()
            .Single(c => c.MatrizId == scenario.MatrizApu.Id && c.Orden == 1).Importe,
            "Los importes por componente tampoco deben recalcularse.");
    }

    // ---------- Infraestructura ----------

    private static (Matriz Matriz, ComponenteMatriz Componente) AgregarAuxiliarSinReferencia(
        SOPROContext context,
        SoproCalculationScenario scenario)
    {
        var matriz = new Matriz
        {
            ProyectoId = scenario.Proyecto.Id,
            Clave = "BAS-MAL",
            Descripcion = "Básico con auxiliar sin referencia",
            Unidad = "m3",
            Tipo = TipoMatriz.Basico,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        var componente = new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = null,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        };
        context.ComponentesMatriz.Add(componente);
        context.SaveChanges();

        return (matriz, componente);
    }

    private static MatrixEditDto DtoNuevo(
        int proyectoId,
        TipoComponenteMatriz tipo,
        MatrixComponentEditDto componente)
        => new()
        {
            ProyectoId = proyectoId,
            Clave = $"APU-INV-{(int)tipo}",
            Descripcion = "Matriz inválida de prueba",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Componentes = new List<MatrixComponentEditDto> { componente }
        };

    private static string NombreReferencia(TipoComponenteMatriz tipo)
        => tipo switch
        {
            TipoComponenteMatriz.Material => nameof(ComponenteMatriz.MaterialId),
            TipoComponenteMatriz.ManoDeObra => nameof(ComponenteMatriz.ManoDeObraId),
            TipoComponenteMatriz.Maquinaria => nameof(ComponenteMatriz.MaquinariaId),
            TipoComponenteMatriz.Herramienta => nameof(ComponenteMatriz.HerramientaId),
            _ => nameof(ComponenteMatriz.AuxiliarId)
        };
}
