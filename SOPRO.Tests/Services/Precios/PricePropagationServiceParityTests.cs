using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Precios;

// [N5-11] Paridad de PricePropagationService contra la fachada:
// las 6 operaciones del motor (Multiplicar ×3, RedondearImporte ×2, SumarImportes ×1)
// migradas a SoproCalculationEngine se validan replicando el flujo COMPLETO de
// propagación (Propagar → RecalcularConMotor → PropagarAuxiliares recursivo →
// ActualizarConceptos → ActualizarAgrupadores) con primitivas de MotorCalculoSopro
// en un contexto gemelo construido desde los MISMOS valores. Antes de cada fase
// se MODIFICA el precio del insumo propagado (hallazgo del dictamen N5-11), de
// modo que ninguna propagación sea idempotente.

[TestClass]
public class PricePropagationServiceParityTests
{
    [TestMethod]
    public async Task Propagar_BateriaParidadConFachada_SemillaFija()
    {
        var rnd = new Random(20260820);

        for (int iter = 0; iter < 50; iter++)
        {
            using var ctx = TestDbFactory.CreateContext();
            using var ctxRef = TestDbFactory.CreateContext();

            var v = GenerarValores(rnd);
            var esc = await CrearEscenario(ctx, v, iter);
            var escRef = await CrearEscenario(ctxRef, v, iter);

            esc.Material.PrecioUnitario = v.NuevoPu;
            escRef.Material.PrecioUnitario = v.NuevoPu;
            ctx.SaveChanges();
            ctxRef.SaveChanges();
            PricePropagationService.PropagarMaterial(ctx, esc.Material.Id);
            PropagarConFachada(ctxRef, TipoComponenteMatriz.Material, escRef.Material.Id);
            CompararEstado(ctx, ctxRef, esc, escRef, iter, "material");

            esc.ManoDeObra.SalarioReal = v.NuevoSr;
            escRef.ManoDeObra.SalarioReal = v.NuevoSr;
            ctx.SaveChanges();
            ctxRef.SaveChanges();
            PricePropagationService.PropagarManoDeObra(ctx, esc.ManoDeObra.Id);
            PropagarConFachada(ctxRef, TipoComponenteMatriz.ManoDeObra, escRef.ManoDeObra.Id);
            CompararEstado(ctx, ctxRef, esc, escRef, iter, "mano-de-obra");

            esc.Maquinaria.CostoHorario = v.NuevoCh;
            escRef.Maquinaria.CostoHorario = v.NuevoCh;
            ctx.SaveChanges();
            ctxRef.SaveChanges();
            PricePropagationService.PropagarMaquinaria(ctx, esc.Maquinaria.Id);
            PropagarConFachada(ctxRef, TipoComponenteMatriz.Maquinaria, escRef.Maquinaria.Id);
            CompararEstado(ctx, ctxRef, esc, escRef, iter, "maquinaria");

            ctx.ComponentesMatriz.RemoveRange(
                esc.MatrizA.Componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria));
            ctxRef.ComponentesMatriz.RemoveRange(
                escRef.MatrizA.Componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria));
            ctx.SaveChanges();
            ctxRef.SaveChanges();

            PricePropagationService.PropagarEliminacion(ctx, new List<int> { esc.MatrizA.Id });
            PropagarEliminacionConFachada(ctxRef, new List<int> { escRef.MatrizA.Id });
            CompararEstado(ctx, ctxRef, esc, escRef, iter, "eliminacion");
        }
    }

    [TestMethod]
    public void PropagarMaterial_Dorado_CascadaConAuxiliarConceptoYAgrupador()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto propagacion dorado",
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
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var material = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-DOR",
            Descripcion = string.Empty,
            Unidad = "pza",
            PrecioUnitario = 70m,
            Notas = string.Empty
        };
        var manoDeObra = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = "MO-DOR",
            Descripcion = string.Empty,
            Unidad = "jor",
            SalarioBase = 40m,
            FactorSalarioReal = 1m,
            SalarioReal = 40m,
            Notas = string.Empty
        };
        var maquinaria = new Maquinaria
        {
            ProyectoId = proyecto.Id,
            Clave = "MAQ-DOR",
            Descripcion = string.Empty,
            CostoHorario = 4m,
            Notas = string.Empty
        };
        context.Materiales.Add(material);
        context.ManoDeObra.Add(manoDeObra);
        context.Maquinaria.Add(maquinaria);
        context.SaveChanges();

        var matrizA = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-A",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        matrizA.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Material = material,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        matrizA.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = manoDeObra.Id,
            ManoDeObra = manoDeObra,
            Cantidad = 1m,
            Orden = 2,
            Notas = string.Empty
        });
        matrizA.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            MaquinariaId = maquinaria.Id,
            Maquinaria = maquinaria,
            Cantidad = 1m,
            Rendimiento = 1m,
            Orden = 3,
            Notas = string.Empty
        });
        context.Matrices.Add(matrizA);
        context.SaveChanges();

        var matrizB = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-B",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        matrizB.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizA.Id,
            Auxiliar = matrizA,
            Cantidad = 2m,
            Orden = 1,
            Notas = string.Empty
        });
        context.Matrices.Add(matrizB);
        context.SaveChanges();

        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-AGR",
            Descripcion = "Agrupador",
            Unidad = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        var conceptoA = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-A",
            Descripcion = "Concepto A",
            Unidad = "pza",
            Cantidad = 10m,
            MatrizId = matrizA.Id,
            Nivel = 1,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        var conceptoB = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-B",
            Descripcion = "Concepto B",
            Unidad = "pza",
            Cantidad = 5m,
            MatrizId = matrizB.Id,
            Nivel = 1,
            Orden = 3,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.AddRange(agrupador, conceptoA, conceptoB);
        context.SaveChanges();

        PricePropagationService.PropagarMaterial(context, material.Id);

        // A: material 1×70 + MO 1×40 + maquinaria 1×4 → 114.00; B: auxiliar 2×114 → 228.00
        var a = context.Matrices.Include(m => m.Componentes).Single(m => m.Clave == "APU-A");
        var b = context.Matrices.Include(m => m.Componentes).Single(m => m.Clave == "APU-B");
        Assert.AreEqual(114.00m, a.CostoDirecto);
        Assert.AreEqual(70.00m, a.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Material).Importe);
        Assert.AreEqual(40.00m, a.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra).Importe);
        Assert.AreEqual(4.00m, a.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria).Importe);
        Assert.AreEqual(228.00m, b.CostoDirecto);
        Assert.AreEqual(228.00m, b.Componentes.Single(c => c.TipoComponente == TipoComponenteMatriz.Auxiliar).Importe);

        // Porcentajes 0% → PrecioUnitario == CostoDirectoUnitario (paridad con la pantalla).
        var cA = context.ConceptosPresupuesto.Single(c => c.Clave == "C-A");
        var cB = context.ConceptosPresupuesto.Single(c => c.Clave == "C-B");
        var agr = context.ConceptosPresupuesto.Single(c => c.Clave == "C-AGR");
        Assert.AreEqual(114.00m, cA.CostoDirectoUnitario);
        Assert.AreEqual(1140.00m, cA.CostoDirectoTotal);
        Assert.AreEqual(114.00m, cA.PrecioUnitario);
        Assert.AreEqual(1140.00m, cA.ImporteTotal);
        Assert.AreEqual(228.00m, cB.CostoDirectoUnitario);
        Assert.AreEqual(1140.00m, cB.CostoDirectoTotal);
        Assert.AreEqual(228.00m, cB.PrecioUnitario);
        Assert.AreEqual(1140.00m, cB.ImporteTotal);

        // Agrupador: suma de ImporteTotal de sus hojas → 1140 + 1140 = 2280.00
        Assert.AreEqual(2280.00m, agr.CostoDirectoTotal);
        Assert.AreEqual(2280.00m, agr.ImporteTotal);
    }

    [TestMethod]
    public void PropagarEliminacion_ConProyectoFiltrado_NoRecalculaMatrizDeOtroProyecto()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto1 = CrearProyecto(context, "P1");
        var proyecto2 = CrearProyecto(context, "P2");

        var a1 = CrearMatrizBasica(context, proyecto1, "APU-1");
        var b1 = CrearMatrizAuxiliar(context, proyecto1, "APU-DEP-1", a1);
        CrearConcepto(context, proyecto1, "C-1", a1, 10m, 1);
        var a2 = CrearMatrizBasica(context, proyecto2, "APU-2");
        var b2 = CrearMatrizAuxiliar(context, proyecto2, "APU-DEP-2", a2);
        CrearConcepto(context, proyecto2, "C-2", a2, 10m, 2);

        // Quitar la maquinaria (4) solo de la matriz del proyecto 1.
        context.ComponentesMatriz.RemoveRange(
            a1.Componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Maquinaria));
        context.SaveChanges();

        // La lista incluye ids de ambos proyectos, pero el filtro debe aislar P1.
        PricePropagationService.PropagarEliminacion(
            context, new List<int> { a1.Id, a2.Id }, proyecto1.Id);

        var mA1 = context.Matrices.Include(m => m.Componentes).Single(m => m.Clave == "APU-1");
        var mB1 = context.Matrices.Include(m => m.Componentes).Single(m => m.Clave == "APU-DEP-1");
        Assert.AreEqual(90.00m, mA1.CostoDirecto, "94 - 4 de la maquinaria eliminada.");
        Assert.AreEqual(90.00m, mB1.CostoDirecto);

        var c1 = context.ConceptosPresupuesto.Single(c => c.Clave == "C-1");
        Assert.AreEqual(90.00m, c1.CostoDirectoUnitario);
        Assert.AreEqual(900.00m, c1.CostoDirectoTotal);

        var mA2 = context.Matrices.Include(m => m.Componentes).Single(m => m.Clave == "APU-2");
        var mB2 = context.Matrices.Include(m => m.Componentes).Single(m => m.Clave == "APU-DEP-2");
        Assert.AreEqual(94.00m, mA2.CostoDirecto, "Matriz de otro proyecto no debe recalcularse.");
        Assert.AreEqual(94.00m, mB2.CostoDirecto);

        var c2 = context.ConceptosPresupuesto.Single(c => c.Clave == "C-2");
        Assert.AreEqual(94.00m, c2.CostoDirectoUnitario);
        Assert.AreEqual(940.00m, c2.CostoDirectoTotal);
    }

    // ── Escenario ─────────────────────────────────────────────────────────────

    private sealed record Escenario(
        Matriz MatrizA, Matriz MatrizB, Material Material, ManoDeObra ManoDeObra,
        Maquinaria Maquinaria, ConceptoPresupuesto ConceptoA, ConceptoPresupuesto ConceptoB);

    private sealed record Valores(
        int DecImporte, int Pct, decimal Pu, decimal Sr, decimal Ch,
        decimal Cm, decimal Cmo, decimal Cmaq, decimal Caux, decimal Cc, decimal Ccb,
        decimal NuevoPu, decimal NuevoSr, decimal NuevoCh);

    private static Valores GenerarValores(Random rnd) => new(
        rnd.Next(0, 5),
        rnd.Next(0, 31),
        Valor(rnd), Valor(rnd), Valor(rnd),
        Valor(rnd), Valor(rnd), Valor(rnd), Valor(rnd), Valor(rnd), Valor(rnd),
        Valor(rnd), Valor(rnd), Valor(rnd));

    private static decimal Valor(Random rnd) => rnd.Next(1, 1_000_000) / 1000m;

    private static async Task<Escenario> CrearEscenario(SOPROContext context, Valores v, int iter)
    {
        var proyecto = new Proyecto
        {
            Nombre = $"Proyecto paridad {iter}",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = v.Pct,
            PorcentajeIndirectosCampo = 0m,
            PorcentajeFinanciamiento = 0m,
            PorcentajeUtilidad = 0m,
            PorcentajeCargosAdicionales = 0m,
            DecimalesCantidad = 2,
            DecimalesImporte = v.DecImporte,
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        await context.SaveChangesAsync();

        var material = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = $"MAT-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            PrecioUnitario = v.Pu,
            Notas = string.Empty
        };
        var manoDeObra = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = $"MO-{iter}",
            Descripcion = string.Empty,
            Unidad = "jor",
            SalarioBase = v.Sr,
            FactorSalarioReal = 1m,
            SalarioReal = v.Sr,
            Notas = string.Empty
        };
        var maquinaria = new Maquinaria
        {
            ProyectoId = proyecto.Id,
            Clave = $"MAQ-{iter}",
            Descripcion = string.Empty,
            CostoHorario = v.Ch,
            Notas = string.Empty
        };
        context.Materiales.Add(material);
        context.ManoDeObra.Add(manoDeObra);
        context.Maquinaria.Add(maquinaria);
        await context.SaveChangesAsync();

        var matrizA = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = $"APU-A-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        matrizA.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Material = material,
            Cantidad = v.Cm,
            Orden = 1,
            Notas = string.Empty
        });
        matrizA.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = manoDeObra.Id,
            ManoDeObra = manoDeObra,
            Cantidad = v.Cmo,
            Orden = 2,
            Notas = string.Empty
        });
        matrizA.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            MaquinariaId = maquinaria.Id,
            Maquinaria = maquinaria,
            Cantidad = v.Cmaq,
            Rendimiento = 1m,
            Orden = 3,
            Notas = string.Empty
        });
        context.Matrices.Add(matrizA);
        await context.SaveChangesAsync();

        var totalsA = MatrixComponentCalculationService.Recalculate(matrizA.Componentes.ToList(), proyecto.DecimalesImporte);
        matrizA.CostoDirecto = totalsA.CostoDirectoTotal;
        await context.SaveChangesAsync();

        var matrizB = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = $"APU-B-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        matrizB.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = matrizA.Id,
            Auxiliar = matrizA,
            Cantidad = v.Caux,
            Orden = 1,
            Notas = string.Empty
        });
        context.Matrices.Add(matrizB);
        await context.SaveChangesAsync();

        var totalsB = MatrixComponentCalculationService.Recalculate(matrizB.Componentes.ToList(), proyecto.DecimalesImporte);
        matrizB.CostoDirecto = totalsB.CostoDirectoTotal;
        await context.SaveChangesAsync();

        var agrupador = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = $"C-AGR-{iter}",
            Descripcion = string.Empty,
            Unidad = string.Empty,
            Cantidad = 0m,
            CostoDirectoUnitario = 0m,
            CostoDirectoTotal = 0m,
            PrecioUnitario = 0m,
            ImporteTotal = 0m,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = true,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        var conceptoA = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = $"C-A-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            Cantidad = v.Cc,
            MatrizId = matrizA.Id,
            Nivel = 1,
            Orden = 2,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        var conceptoB = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = $"C-B-{iter}",
            Descripcion = string.Empty,
            Unidad = "pza",
            Cantidad = v.Ccb,
            MatrizId = matrizB.Id,
            Nivel = 1,
            Orden = 3,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.AddRange(agrupador, conceptoA, conceptoB);
        await context.SaveChangesAsync();

        return new Escenario(matrizA, matrizB, material, manoDeObra, maquinaria, conceptoA, conceptoB);
    }

    private static void CompararEstado(
        SOPROContext ctx, SOPROContext ctxRef, Escenario esc, Escenario escRef, int iter, string fase)
    {
        foreach (var clave in new[] { esc.MatrizA.Clave, esc.MatrizB.Clave })
        {
            var m = ctx.Matrices.AsNoTracking().Include(x => x.Componentes).Single(x => x.Clave == clave);
            var mR = ctxRef.Matrices.AsNoTracking().Include(x => x.Componentes).Single(x => x.Clave == clave);
            Assert.AreEqual(mR.CostoDirecto, m.CostoDirecto, $"iter={iter} fase={fase} CostoDirecto {clave}");
            foreach (var c in m.Componentes)
            {
                var cR = mR.Componentes.Single(x => x.TipoComponente == c.TipoComponente);
                Assert.AreEqual(cR.Importe, c.Importe, $"iter={iter} fase={fase} Importe {clave}/{c.TipoComponente}");
            }
        }

        foreach (var clave in new[] { esc.ConceptoA.Clave, esc.ConceptoB.Clave })
        {
            var c = ctx.ConceptosPresupuesto.AsNoTracking().Single(x => x.Clave == clave);
            var cR = ctxRef.ConceptosPresupuesto.AsNoTracking().Single(x => x.Clave == clave);
            Assert.AreEqual(cR.CostoDirectoUnitario, c.CostoDirectoUnitario, $"iter={iter} fase={fase} {clave} CDU");
            Assert.AreEqual(cR.CostoDirectoTotal, c.CostoDirectoTotal, $"iter={iter} fase={fase} {clave} CDT");
            Assert.AreEqual(cR.PrecioUnitario, c.PrecioUnitario, $"iter={iter} fase={fase} {clave} PU");
            Assert.AreEqual(cR.ImporteTotal, c.ImporteTotal, $"iter={iter} fase={fase} {clave} IT");
        }

        var agr = ctx.ConceptosPresupuesto.AsNoTracking().Single(x => x.EsAgrupador);
        var agrR = ctxRef.ConceptosPresupuesto.AsNoTracking().Single(x => x.EsAgrupador);
        Assert.AreEqual(agrR.CostoDirectoTotal, agr.CostoDirectoTotal, $"iter={iter} fase={fase} agrupador CDT");
        Assert.AreEqual(agrR.ImporteTotal, agr.ImporteTotal, $"iter={iter} fase={fase} agrupador IT");
    }

    // ── Referencia compuesta con la fachada (código pre-migración) ────────────

    private static void PropagarConFachada(SOPROContext ctx, TipoComponenteMatriz tipo, int insumoId)
    {
        IQueryable<ComponenteMatriz> query = ctx.ComponentesMatriz
            .Include(c => c.Material)
            .Include(c => c.ManoDeObra)
            .Include(c => c.Maquinaria)
            .Where(c => c.TipoComponente == tipo &&
                (tipo == TipoComponenteMatriz.Material ? c.MaterialId == insumoId :
                 tipo == TipoComponenteMatriz.ManoDeObra ? c.ManoDeObraId == insumoId :
                 c.MaquinariaId == insumoId));

        var componentes = query.ToList();
        if (!componentes.Any()) return;

        var matrizIds = componentes.Select(c => c.MatrizId).Distinct().ToList();
        var matrices = CargarConFachada(ctx, matrizIds);

        var proyectoIds = matrices.Where(m => m.ProyectoId.HasValue)
            .Select(m => m.ProyectoId!.Value).Distinct().ToList();
        var proyectos = ctx.Proyectos.Where(p => proyectoIds.Contains(p.Id)).ToDictionary(p => p.Id);

        foreach (var comp in componentes)
        {
            decimal pu = tipo == TipoComponenteMatriz.Material ? (comp.Material?.PrecioUnitario ?? 0) :
                         tipo == TipoComponenteMatriz.ManoDeObra ? (comp.ManoDeObra?.SalarioReal ?? 0) :
                         (comp.Maquinaria?.CostoHorario ?? 0);

            var mat = matrices.FirstOrDefault(m => m.Id == comp.MatrizId);
            int dec = (mat?.ProyectoId.HasValue == true && proyectos.TryGetValue(mat.ProyectoId!.Value, out var proy))
                ? proy.DecimalesImporte : 2;
            comp.Importe = new MotorCalculoSopro(dec, dec, 4).Multiplicar(comp.Cantidad, pu);
        }

        RecalcularConFachada(ctx, matrices);
        PropagarAuxiliaresConFachada(ctx, matrizIds, matrices);

        var todasIds = matrices.Select(m => m.Id).ToList();
        ActualizarConceptosConFachada(ctx, matrices, todasIds);
        ctx.SaveChanges();
    }

    private static void PropagarEliminacionConFachada(SOPROContext ctx, List<int> matrizIdsAfectadas)
    {
        var matrices = CargarConFachada(ctx, matrizIdsAfectadas);
        RecalcularConFachada(ctx, matrices);
        PropagarAuxiliaresConFachada(ctx, matrizIdsAfectadas, matrices);

        var todasIds = matrices.Select(m => m.Id).ToList();
        ActualizarConceptosConFachada(ctx, matrices, todasIds);
        ctx.SaveChanges();
    }

    private static void RecalcularConFachada(SOPROContext ctx, List<Matriz> matrices)
    {
        var proyectoIds = matrices.Where(m => m.ProyectoId.HasValue)
            .Select(m => m.ProyectoId!.Value).Distinct().ToList();
        var proyectos = ctx.Proyectos.Where(p => proyectoIds.Contains(p.Id)).ToDictionary(p => p.Id);

        foreach (var mat in matrices)
        {
            if (!mat.ProyectoId.HasValue) continue;
            if (!proyectos.TryGetValue(mat.ProyectoId.Value, out var proyecto)) continue;
            var totals = MatrixComponentCalculationService.Recalculate(
                mat.Componentes.ToList(), proyecto.DecimalesImporte);
            mat.CostoDirecto = new MotorCalculoSopro(proyecto)
                .RedondearImporte(totals.CostoDirectoTotal);
        }
    }

    private static void PropagarAuxiliaresConFachada(SOPROContext ctx, List<int> matrizIdsOrigen, List<Matriz> todasMatrices)
    {
        var idsConAuxiliar = ctx.ComponentesMatriz
            .Where(c => c.AuxiliarId.HasValue && matrizIdsOrigen.Contains(c.AuxiliarId.Value))
            .Select(c => c.MatrizId)
            .Distinct()
            .ToList();

        if (!idsConAuxiliar.Any()) return;

        var idsNuevos = idsConAuxiliar.Except(todasMatrices.Select(m => m.Id)).ToList();
        if (!idsNuevos.Any()) return;

        var matricesPadre = CargarConFachada(ctx, idsNuevos);
        RecalcularConFachada(ctx, matricesPadre);
        todasMatrices.AddRange(matricesPadre);
        PropagarAuxiliaresConFachada(ctx, idsNuevos, todasMatrices);
    }

    private static void ActualizarConceptosConFachada(SOPROContext ctx, List<Matriz> matrices, List<int> matrizIds)
    {
        var proyectoIds = matrices.Where(m => m.ProyectoId.HasValue)
            .Select(m => m.ProyectoId!.Value).Distinct().ToList();
        var proyectos = ctx.Proyectos.Where(p => proyectoIds.Contains(p.Id)).ToDictionary(p => p.Id);

        var conceptos = ctx.ConceptosPresupuesto
            .Where(c => c.MatrizId.HasValue && matrizIds.Contains(c.MatrizId.Value)
                && proyectoIds.Contains(c.ProyectoId))
            .ToList();

        foreach (var concepto in conceptos)
        {
            var mat = matrices.First(m => m.Id == concepto.MatrizId);
            if (!mat.ProyectoId.HasValue) continue;
            if (!proyectos.TryGetValue(mat.ProyectoId.Value, out var proyecto)) continue;

            var motor = new MotorCalculoSopro(proyecto);
            concepto.CostoDirectoUnitario = motor.RedondearImporte(mat.CostoDirecto);
            concepto.CostoDirectoTotal = motor.Multiplicar(concepto.Cantidad, concepto.CostoDirectoUnitario);

            concepto.PrecioUnitario = BudgetPricingService.CalculateUnitPrice(proyecto, concepto.CostoDirectoUnitario);
            concepto.ImporteTotal = motor.Multiplicar(concepto.Cantidad, concepto.PrecioUnitario);
        }

        ActualizarAgrupadoresConFachada(ctx, proyectos.Values.ToList());
    }

    private static void ActualizarAgrupadoresConFachada(SOPROContext ctx, List<Proyecto> proyectos)
    {
        foreach (var proyecto in proyectos)
        {
            var todos = ctx.ConceptosPresupuesto
                .Where(c => c.ProyectoId == proyecto.Id)
                .OrderBy(c => c.Orden)
                .ThenBy(c => c.Id)
                .ToList();

            var agrupadores = todos.Where(c => c.EsAgrupador).ToList();
            if (agrupadores.Count == 0) continue;

            var motor = new MotorCalculoSopro(proyecto);

            foreach (var agrupador in agrupadores)
            {
                int idx = todos.FindIndex(c => c.Id == agrupador.Id);
                var importes = new List<decimal>();

                for (int j = idx + 1; j < todos.Count; j++)
                {
                    var fila = todos[j];

                    int nivelFila = fila.EsAgrupador ? fila.Nivel : 5;
                    if (nivelFila <= agrupador.Nivel) break;
                    if (fila.EsAgrupador) continue;

                    importes.Add(fila.ImporteTotal);
                }

                decimal total = motor.SumarImportes(importes);
                agrupador.CostoDirectoTotal = total;
                agrupador.ImporteTotal = total;
            }
        }
    }

    private static List<Matriz> CargarConFachada(SOPROContext ctx, List<int> ids)
    {
        return ctx.Matrices
            .Include(m => m.Componentes).ThenInclude(c => c.Material)
            .Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra)
            .Include(m => m.Componentes).ThenInclude(c => c.Maquinaria)
            .Include(m => m.Componentes).ThenInclude(c => c.Herramienta)
            .Include(m => m.Componentes).ThenInclude(c => c.Auxiliar)
            .Where(m => ids.Contains(m.Id))
            .ToList();
    }

    // ── Helpers del dorado de aislamiento ─────────────────────────────────────

    private static Proyecto CrearProyecto(SOPROContext context, string sufijo)
    {
        var proyecto = new Proyecto
        {
            Nombre = $"Proyecto {sufijo}",
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
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();
        return proyecto;
    }

    private static Matriz CrearMatrizBasica(SOPROContext context, Proyecto proyecto, string clave)
    {
        var material = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = $"{clave}-MAT",
            Descripcion = string.Empty,
            Unidad = "pza",
            PrecioUnitario = 60m,
            Notas = string.Empty
        };
        var manoDeObra = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = $"{clave}-MO",
            Descripcion = string.Empty,
            Unidad = "jor",
            SalarioBase = 30m,
            FactorSalarioReal = 1m,
            SalarioReal = 30m,
            Notas = string.Empty
        };
        var maquinaria = new Maquinaria
        {
            ProyectoId = proyecto.Id,
            Clave = $"{clave}-MAQ",
            Descripcion = string.Empty,
            CostoHorario = 4m,
            Notas = string.Empty
        };
        context.Materiales.Add(material);
        context.ManoDeObra.Add(manoDeObra);
        context.Maquinaria.Add(maquinaria);
        context.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id,
            Material = material,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = manoDeObra.Id,
            ManoDeObra = manoDeObra,
            Cantidad = 1m,
            Orden = 2,
            Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            MaquinariaId = maquinaria.Id,
            Maquinaria = maquinaria,
            Cantidad = 1m,
            Rendimiento = 1m,
            Orden = 3,
            Notas = string.Empty
        });
        context.Matrices.Add(matriz);
        context.SaveChanges();

        var totals = MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totals.CostoDirectoTotal;
        context.SaveChanges();
        return matriz;
    }

    private static Matriz CrearMatrizAuxiliar(SOPROContext context, Proyecto proyecto, string clave, Matriz baseMatriz)
    {
        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = string.Empty,
            Unidad = "pza",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = baseMatriz.Id,
            Auxiliar = baseMatriz,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        context.Matrices.Add(matriz);
        context.SaveChanges();

        var totals = MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totals.CostoDirectoTotal;
        context.SaveChanges();
        return matriz;
    }

    private static void CrearConcepto(SOPROContext context, Proyecto proyecto, string clave, Matriz matriz, decimal cantidad, int orden)
    {
        context.ConceptosPresupuesto.Add(new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = string.Empty,
            Unidad = "pza",
            Cantidad = cantidad,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = matriz.CostoDirecto,
            CostoDirectoTotal = new MotorCalculoSopro(proyecto).Multiplicar(cantidad, matriz.CostoDirecto),
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = new MotorCalculoSopro(proyecto).Multiplicar(cantidad, matriz.CostoDirecto),
            Nivel = 1,
            Orden = orden,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        });
        context.SaveChanges();
    }
}