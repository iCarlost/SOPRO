using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;
using SOPRO.Application.DTOs.Matrices;
using SOPRO.Application.Models;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Matrices;

[TestClass]
public class MatrixApplicationServiceParityTests
{
    // ── N5-10: única operación del motor del servicio — RedondearImporte del
    //    CostoDirectoTotal recalculado en la cascada (PropagateCascadeAsync) —
    //    migró a SoproCalculationEngine.RoundAmount.
    //    Paridad de integración: SaveAsync dispara la cascada en dos contextos
    //    SQLite idénticos; la referencia replica SaveAsync+PropagateCascadeAsync
    //    con la fachada (oráculo diferencial) + dorado.

    [TestMethod]
    public async Task SaveAsync_Cascada_ParidadConFachada_BateriaAleatoriaConSemilla()
    {
        var rnd = new Random(20260817);

        for (int iter = 0; iter < 100; iter++)
        {
            using var context = TestDbFactory.CreateContext();
            using var contextRef = TestDbFactory.CreateContext();

            var valores = GenerarValores(rnd);
            var setup = await CrearEscenario(context, valores, iter);
            var setupRef = await CrearEscenario(contextRef, valores, iter);

            var dto = setup.Dto;
            var servicio = await MatrixApplicationService.SaveAsync(context, dto, setup.MatrizA);
            var referencia = await SaveAsyncConFachada(contextRef, dto, setupRef.MatrizA);

            Assert.AreEqual(referencia.IsNew, servicio.IsNew, $"iter={iter} IsNew");
            Assert.AreEqual(referencia.CascadedMatricesUpdated, servicio.CascadedMatricesUpdated, $"iter={iter} CascadedMatricesUpdated");

            var afectada = await context.Matrices.Include(m => m.Componentes)
                .FirstAsync(m => m.Id == setup.MatrizB.Id);
            var afectadaRef = await contextRef.Matrices.Include(m => m.Componentes)
                .FirstAsync(m => m.Id == setupRef.MatrizB.Id);

            Assert.AreEqual(afectadaRef.CostoDirecto, afectada.CostoDirecto, $"iter={iter} CostoDirecto de la matriz afectada");
            Assert.AreEqual(setup.Esperado, afectada.CostoDirecto, $"iter={iter} CostoDirecto contra esperado independiente");

            var compsRef = afectadaRef.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
            var comps = afectada.Componentes.OrderBy(c => c.Orden).ThenBy(c => c.Id).ToList();
            Assert.AreEqual(compsRef.Count, comps.Count, $"iter={iter} cantidad de componentes");
            for (int i = 0; i < compsRef.Count; i++)
            {
                Assert.AreEqual(compsRef[i].Cantidad, comps[i].Cantidad, $"iter={iter} comp[{i}] Cantidad");
                Assert.AreEqual(compsRef[i].Importe, comps[i].Importe, $"iter={iter} comp[{i}] Importe");
            }
        }
    }

    [TestMethod]
    public async Task SaveAsync_Dorado_CascadaConRedondeoDeImporte()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto cascada dorado",
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
        context.Proyectos.Add(proyecto);
        await context.SaveChangesAsync();

        var material = new Material { Clave = "MAT-1", Descripcion = string.Empty, Unidad = "pza", PrecioUnitario = 100m, Notas = string.Empty };
        context.Materiales.Add(material);
        await context.SaveChangesAsync();

        var matrizA = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-A",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizA);
        await context.SaveChangesAsync();

        var matrizB = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-B",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        matrizB.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizA.Id,
            Cantidad = 2m,
            Orden = 1,
            Notas = string.Empty
        });
        matrizB.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = 3m,
            Orden = 2,
            Notas = string.Empty
        });
        context.Matrices.Add(matrizB);
        await context.SaveChangesAsync();

        var dto = new MatrixEditDto
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-A",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 350m,
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

        var result = await MatrixApplicationService.SaveAsync(context, dto, matrizA);

        Assert.IsFalse(result.IsNew);
        Assert.AreEqual(1, result.CascadedMatricesUpdated);

        // Matriz B: auxiliar cant 2 × CostoDirecto(A)=350 → 700; material cant 3 × 100 → 300;
        // total 1000 → R2 = 1000.00
        var afectada = await context.Matrices.Include(m => m.Componentes)
            .FirstAsync(m => m.Id == matrizB.Id);
        Assert.AreEqual(1000.00m, afectada.CostoDirecto);

        var materialComp = afectada.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
        var auxiliar = afectada.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar);
        Assert.AreEqual(300.00m, materialComp.Importe);
        Assert.AreEqual(700.00m, auxiliar.Importe);
    }

    [TestMethod]
    public async Task SaveAsync_SinDependientes_DebeDevolverCeroCascadas()
    {
        using var context = TestDbFactory.CreateContext();
        var setup = await CrearEscenario(context, GenerarValores(new Random(888)), 0);

        // Quitar la matriz B del contexto para que nadie dependa de A.
        context.Matrices.Remove(setup.MatrizB);
        await context.SaveChangesAsync();

        var result = await MatrixApplicationService.SaveAsync(context, setup.Dto, setup.MatrizA);

        Assert.AreEqual(0, result.CascadedMatricesUpdated);
    }

    // ── Escenario ─────────────────────────────────────────────────────────────

    private sealed record Escenario(Matriz MatrizA, Matriz MatrizB, MatrixEditDto Dto, decimal Esperado);

    private sealed record Valores(int DecImporte, decimal Precio, decimal Original, decimal AuxCant,
        decimal MatCant, decimal Nuevo, decimal DtoCant, decimal DtoImporte);

    private static Valores GenerarValores(Random rnd) => new(
        rnd.Next(0, 5),
        Valor(rnd), Valor(rnd), Valor(rnd), Valor(rnd), Valor(rnd), Valor(rnd), Valor(rnd));

    private static async Task<Escenario> CrearEscenario(SOPROContext context, Valores v, int iter)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto cascada paridad",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = 2,
            DecimalesImporte = v.DecImporte,
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        await context.SaveChangesAsync();

        var material = new Material { Clave = $"MAT-{iter}", Descripcion = string.Empty, Unidad = "pza", PrecioUnitario = v.Precio, Notas = string.Empty };
        context.Materiales.Add(material);
        await context.SaveChangesAsync();

        var matrizA = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = $"APU-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = v.Original,
            Notas = string.Empty
        };
        context.Matrices.Add(matrizA);
        await context.SaveChangesAsync();

        var matrizB = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = $"APU-DEP-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 0m,
            Notas = string.Empty
        };
        matrizB.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizA.Id,
            Cantidad = v.AuxCant,
            Orden = 1,
            Notas = string.Empty
        });
        matrizB.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Cantidad = v.MatCant,
            Orden = 2,
            Notas = string.Empty
        });
        context.Matrices.Add(matrizB);
        await context.SaveChangesAsync();

        var dto = new MatrixEditDto
        {
            ProyectoId = proyecto.Id,
            Clave = $"APU-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            CostoDirecto = v.Nuevo,
            Componentes =
            {
                new MatrixComponentEditDto
                {
                    TipoComponente = TipoComponenteMatriz.Material,
                    MaterialId = material.Id,
                    Cantidad = v.DtoCant,
                    Importe = v.DtoImporte,
                    Orden = 1,
                    Notas = string.Empty
                }
            }
        };

        // Esperado independiente: Recalculate sobre B con el CD NUEVO de A (el del dto,
        // que Apply persiste igualmente) + RoundAmount.
        var aux = matrizB.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar);
        aux.Auxiliar = matrizA;
        aux.Auxiliar.CostoDirecto = v.Nuevo;
        var mat = matrizB.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material);
        mat.Material = material;
        var totals = MatrixComponentCalculationService.Recalculate(matrizB.Componentes.ToList(), proyecto.DecimalesImporte);
        var esperado = new SoproCalculationEngine(
            proyecto.DecimalesCantidad, proyecto.DecimalesImporte, proyecto.DecimalesPorcentaje)
            .RoundAmount(totals.CostoDirectoTotal);

        return new Escenario(matrizA, matrizB, dto, esperado);
    }

    // ── Referencia compuesta con la fachada (código pre-migración) ────────────

    private static async Task<MatrixSaveResult> SaveAsyncConFachada(
        SOPROContext context, MatrixEditDto dto, Matriz? existingMatrix = null)
    {
        var isNew = existingMatrix == null;
        Matriz matrix;

        if (isNew)
        {
            matrix = new Matriz();
            ApplyConFachada(dto, matrix);
            context.Matrices.Add(matrix);
            await context.SaveChangesAsync();
        }
        else
        {
            matrix = existingMatrix!;
            ApplyConFachada(dto, matrix);

            var oldComponents = context.ComponentesMatriz.Where(c => c.MatrizId == matrix.Id);
            context.ComponentesMatriz.RemoveRange(oldComponents);
            await context.SaveChangesAsync();
        }

        var mappedComponents = dto.Componentes
            .Select((component, index) => MapComponentConFachada(matrix.Id, component, index))
            .ToList();

        if (mappedComponents.Count > 0)
        {
            context.ComponentesMatriz.AddRange(mappedComponents);
        }

        await context.SaveChangesAsync();

        var cascaded = isNew
            ? 0
            : await PropagateCascadeAsyncConFachada(context, matrix.Id);

        return new MatrixSaveResult
        {
            MatrixId = matrix.Id,
            IsNew = isNew,
            CascadedMatricesUpdated = cascaded
        };
    }

    private static void ApplyConFachada(MatrixEditDto dto, Matriz matrix)
    {
        matrix.Clave = dto.Clave.Trim().ToUpperInvariant();
        matrix.Descripcion = dto.Descripcion.Trim();
        matrix.Unidad = dto.Unidad.Trim();
        matrix.Tipo = dto.Tipo;
        matrix.CostoDirecto = dto.CostoDirecto;
        matrix.ProyectoId = dto.ProyectoId;
        matrix.Notas ??= string.Empty;
        matrix.FechaModificacion = DateTime.Now;
    }

    private static ComponenteMatriz MapComponentConFachada(int matrixId, MatrixComponentEditDto component, int index)
    {
        return new ComponenteMatriz
        {
            MatrizId = matrixId,
            TipoComponente = component.TipoComponente,
            MaterialId = component.MaterialId,
            ManoDeObraId = component.ManoDeObraId,
            MaquinariaId = component.MaquinariaId,
            AuxiliarId = component.AuxiliarId,
            HerramientaId = component.HerramientaId,
            Cantidad = component.Cantidad,
            Rendimiento = component.TipoComponente == TipoComponenteMatriz.Maquinaria && component.Cantidad > 0
                ? Math.Round(1m / component.Cantidad, 5, MidpointRounding.AwayFromZero) : 0m,
            Importe = component.Importe,
            Orden = component.Orden > 0 ? component.Orden : index + 1,
            Notas = component.Notas ?? string.Empty
        };
    }

    private static async Task<int> PropagateCascadeAsyncConFachada(SOPROContext context, int matrixId)
    {
        var affectedMatrixIds = await context.ComponentesMatriz
            .Where(c => c.AuxiliarId == matrixId)
            .Select(c => c.MatrizId)
            .Distinct()
            .ToListAsync();

        if (affectedMatrixIds.Count == 0)
        {
            return 0;
        }

        foreach (var affectedMatrixId in affectedMatrixIds)
        {
            var affectedMatrix = await context.Matrices
                .Include(m => m.Componentes).ThenInclude(c => c.Material)
                .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
                .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
                .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
                .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
                .FirstOrDefaultAsync(m => m.Id == affectedMatrixId);

            if (affectedMatrix == null)
            {
                continue;
            }

            var proyecto = await context.Proyectos.FindAsync(affectedMatrix.ProyectoId);
            if (proyecto != null)
            {
                var totals = MatrixComponentCalculationService.Recalculate(
                    affectedMatrix.Componentes.ToList(), proyecto.DecimalesImporte);
                affectedMatrix.CostoDirecto = new MotorCalculoSopro(proyecto)
                    .RedondearImporte(totals.CostoDirectoTotal);
            }
            else
            {
                continue;
            }
        }

        await context.SaveChangesAsync();
        return affectedMatrixIds.Count;
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private static decimal Valor(Random rnd)
        => rnd.Next(1, 1_000_000) / 1000m;
}