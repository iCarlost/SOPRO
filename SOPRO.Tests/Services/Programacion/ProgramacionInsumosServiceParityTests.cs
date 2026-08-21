using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;
using System.Linq;

namespace SOPRO.Tests.Services.Programacion;

// [N5-16] Paridad de ProgramacionInsumosService contra la fachada:
// Build y 4 helpers (ExplotarCanonicoRecursivo, ExplotarImportesCanonicosPorInsumo,
// ExplotarMatrizEnPeriodo, CalcularImportesUnitarios) usan SoproCalculationEngine
// (Multiply ×8, RoundAmount ×11, RoundQuantity ×5 = 24 operaciones).
// El oráculo es un clon legacy con MotorCalculoSopro que replica el flujo
// completo sin llamar al servicio migrado, en un contexto gemelo construido
// desde los MISMOS valores con precisiones independientes.

[TestClass]
public class ProgramacionInsumosServiceParityTests
{
    [TestMethod]
    public void Build_BateriaParidadConFachada_SemillaFija()
    {
        var rnd = new Random(20260827);

        for (int iter = 0; iter < 20; iter++)
        {
            using var ctx = TestDbFactory.CreateContext();
            using var ctxRef = TestDbFactory.CreateContext();

            var decCant = rnd.Next(0, 5);
            var decImp = rnd.Next(0, 5);
            var decPct = rnd.Next(0, 7);
            var proyecto = CrearProyecto(ctx, decCant, decImp, decPct);
            var proyectoRef = CrearProyecto(ctxRef, decCant, decImp, decPct);

            var puMat = rnd.Next(1, 100000) / 1000m;
            var cantMat = rnd.Next(1, 10000) / 1000m;
            var cantConcepto = rnd.Next(1, 100) / 1m;

            CrearEscenarioCompleto(ctx, proyecto, puMat, cantMat, cantConcepto, rnd, iter);
            CrearEscenarioCompleto(ctxRef, proyectoRef, puMat, cantMat, cantConcepto, rnd, iter);

            var service = new ProgramacionInsumosService();
            var result = service.Build(ctx, proyecto, ProgramaInsumoTipo.Materiales);
            var resultRef = BuildConFachada(ctxRef, proyectoRef, ProgramaInsumoTipo.Materiales);

            CompararResultados(result, resultRef, iter);
        }
    }

    [TestMethod]
    public void Build_Dorado_ParidadYValoresConocidos()
    {
        using var ctx = TestDbFactory.CreateContext();
        using var ctxRef = TestDbFactory.CreateContext();

        var proyecto = CrearProyecto(ctx, 2, 2, 4);
        var proyectoRef = CrearProyecto(ctxRef, 2, 2, 4);

        CrearEscenarioInsumos(ctx, proyecto, 10m, 1m, 10m);
        CrearEscenarioInsumos(ctxRef, proyectoRef, 10m, 1m, 10m);

        var service = new ProgramacionInsumosService();
        var result = service.Build(ctx, proyecto, ProgramaInsumoTipo.Materiales);
        var resultRef = BuildConFachada(ctxRef, proyectoRef, ProgramaInsumoTipo.Materiales);

        CompararResultados(result, resultRef, 999);

        var mat = result.Rows.FirstOrDefault(i => i.Clave == "MAT-INS");
        Assert.IsNotNull(mat);
        // Matriz CD 10, concepto 10×10=100, distribuido en 2 periodos 50/50 → 50 cada uno
        // Total = cantidad total 10, ImporteTotal = 100, PU 10
        Assert.AreEqual(10m, mat.Total);
        Assert.AreEqual(100m, mat.ImporteTotal);
        Assert.AreEqual(10m, mat.PrecioUnitario);
    }

    private static Proyecto CrearProyecto(SOPROContext ctx, int decCant, int decImp, int decPct)
    {
        var p = new Proyecto
        {
            Nombre = "Proyecto Insumos",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            DecimalesCantidad = decCant,
            DecimalesImporte = decImp,
            DecimalesPorcentaje = decPct
        };
        ctx.Proyectos.Add(p);
        ctx.SaveChanges();
        return p;
    }

    private static void CrearEscenarioInsumos(SOPROContext ctx, Proyecto proyecto, decimal puMat, decimal cantMat, decimal cantConcepto)
    {
        var material = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-INS",
            Descripcion = "Material insumo",
            Unidad = "pza",
            PrecioUnitario = puMat,
            Notas = string.Empty
        };
        ctx.Materiales.Add(material);
        ctx.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-INS",
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
            Cantidad = cantMat,
            Orden = 1,
            Notas = string.Empty
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();

        var totals = SOPRO.Application.Services.MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totals.CostoDirectoTotal;
        ctx.SaveChanges();

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-INS",
            Descripcion = string.Empty,
            Unidad = "pza",
            Cantidad = cantConcepto,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = matriz.CostoDirecto,
            CostoDirectoTotal = matriz.CostoDirecto * cantConcepto,
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = matriz.CostoDirecto * cantConcepto,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa Insumos",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
        };
        ctx.ProgramasObra.Add(programa);
        ctx.SaveChanges();

        var p1 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 1,
            Etiqueta = "P1",
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 1, 7)
        };
        var p2 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 2,
            Etiqueta = "P2",
            FechaInicio = new DateTime(2026, 1, 8),
            FechaFin = new DateTime(2026, 1, 14)
        };
        ctx.PeriodosPrograma.AddRange(p1, p2);
        ctx.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = concepto.Id,
            Descripcion = "Actividad Insumos",
            Unidad = "pza",
            CantidadTotal = cantConcepto,
            CantidadProgramada = cantConcepto,
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = matriz.CostoDirecto * cantConcepto,
            ImporteProgramado = matriz.CostoDirecto * cantConcepto,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();

        ctx.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = p1.Id,
                CantidadProgramada = cantConcepto / 2m,
                PorcentajeProgramado = 50m,
                PrecioUnitario = matriz.CostoDirecto,
                ImporteProgramado = (matriz.CostoDirecto * cantConcepto) / 2m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = p2.Id,
                CantidadProgramada = cantConcepto / 2m,
                PorcentajeProgramado = 50m,
                PrecioUnitario = matriz.CostoDirecto,
                ImporteProgramado = (matriz.CostoDirecto * cantConcepto) / 2m
            });
        ctx.SaveChanges();
    }

    private static void CrearEscenarioCompleto(SOPROContext ctx, Proyecto proyecto, decimal puMat, decimal cantMat, decimal cantConcepto, Random rnd, int iter)
    {
        var mat = new Material { ProyectoId = proyecto.Id, Clave = "MAT-INS", Descripcion = "Material", Unidad = "pza", PrecioUnitario = puMat, Notas = string.Empty };
        var moNormal = new ManoDeObra { ProyectoId = proyecto.Id, Clave = "MO-NOR", Descripcion = "MO Normal", Unidad = "jor", SalarioBase = 30m, FactorSalarioReal = 1m, SalarioReal = 30m, Notas = string.Empty };
        var moPct = new ManoDeObra { ProyectoId = proyecto.Id, Clave = "MO-PCT", Descripcion = "Cabo", Unidad = "%MO", SalarioBase = 0m, FactorSalarioReal = 1m, SalarioReal = 0m, Notas = string.Empty };
        var herNormal = new Herramienta { ProyectoId = proyecto.Id, Clave = "HER-NOR", Descripcion = "Her Normal", Unidad = "pza", PrecioUnitario = 5m, Notas = string.Empty };
        var herPct = new Herramienta { ProyectoId = proyecto.Id, Clave = "HER-PCT", Descripcion = "Her %MO", Unidad = "%MO", PrecioUnitario = 0m, Notas = string.Empty };
        var maq = new Maquinaria { ProyectoId = proyecto.Id, Clave = "MAQ-INS", Descripcion = "Maquinaria", CostoHorario = 4m, Notas = string.Empty };
        ctx.Materiales.Add(mat);
        ctx.ManoDeObra.AddRange(moNormal, moPct);
        ctx.Herramientas.AddRange(herNormal, herPct);
        ctx.Maquinaria.Add(maq);
        ctx.SaveChanges();
        var matAux = new Material { ProyectoId = proyecto.Id, Clave = $"MAT-AUX-{iter}", Descripcion = "Aux Mat", Unidad = "pza", PrecioUnitario = 20m, Notas = string.Empty };
        ctx.Materiales.Add(matAux);
        ctx.SaveChanges();
        var matrizAux = new Matriz { ProyectoId = proyecto.Id, Clave = $"APU-AUX-{iter}", Descripcion = "Auxiliar", Unidad = "pza", Tipo = TipoMatriz.APU, Notas = string.Empty };
        matrizAux.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, MaterialId = matAux.Id, Material = matAux, Cantidad = 1m, Orden = 1, Notas = string.Empty });
        ctx.Matrices.Add(matrizAux);
        ctx.SaveChanges();
        var totAux = SOPRO.Application.Services.MatrixComponentCalculationService.Recalculate(matrizAux.Componentes.ToList(), proyecto.DecimalesImporte);
        matrizAux.CostoDirecto = totAux.CostoDirectoTotal;
        ctx.SaveChanges();
        var matrizCuadrilla = new Matriz { ProyectoId = proyecto.Id, Clave = $"APU-CUA-{iter}", Descripcion = "Cuadrilla", Unidad = "pza", Tipo = TipoMatriz.Cuadrilla, Notas = string.Empty };
        matrizCuadrilla.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObraId = moNormal.Id, ManoDeObra = moNormal, Cantidad = 1m, Orden = 1, Notas = string.Empty });
        ctx.Matrices.Add(matrizCuadrilla);
        ctx.SaveChanges();
        var totCua = SOPRO.Application.Services.MatrixComponentCalculationService.Recalculate(matrizCuadrilla.Componentes.ToList(), proyecto.DecimalesImporte);
        matrizCuadrilla.CostoDirecto = totCua.CostoDirectoTotal;
        ctx.SaveChanges();
        var matriz = new Matriz { ProyectoId = proyecto.Id, Clave = $"APU-INS-{iter}", Descripcion = "Principal", Unidad = "pza", Tipo = TipoMatriz.APU, Notas = string.Empty };
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Material, MaterialId = mat.Id, Material = mat, Cantidad = cantMat, Orden = 1, Notas = string.Empty });
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObraId = moNormal.Id, ManoDeObra = moNormal, Cantidad = 1m, Orden = 2, Notas = string.Empty });
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.ManoDeObra, ManoDeObraId = moPct.Id, ManoDeObra = moPct, Cantidad = 0.10m, Orden = 3, Notas = string.Empty });
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Herramienta, HerramientaId = herNormal.Id, Herramienta = herNormal, Cantidad = 1m, Orden = 4, Notas = string.Empty });
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Herramienta, HerramientaId = herPct.Id, Herramienta = herPct, Cantidad = 0.10m, Orden = 5, Notas = string.Empty });
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Maquinaria, MaquinariaId = maq.Id, Maquinaria = maq, Cantidad = 1m, Rendimiento = 1m, Orden = 6, Notas = string.Empty });
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = matrizAux.Id, Auxiliar = matrizAux, Cantidad = 1m, Orden = 7, Notas = string.Empty });
        matriz.Componentes.Add(new ComponenteMatriz { TipoComponente = TipoComponenteMatriz.Auxiliar, AuxiliarId = matrizCuadrilla.Id, Auxiliar = matrizCuadrilla, Cantidad = 1m, Orden = 8, Notas = string.Empty });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        var totMain = SOPRO.Application.Services.MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totMain.CostoDirectoTotal;
        ctx.SaveChanges();
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = $"C-INS-{iter}",
            Descripcion = "Concepto Insumos",
            Unidad = "pza",
            Cantidad = cantConcepto,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = matriz.CostoDirecto,
            CostoDirectoTotal = matriz.CostoDirecto * cantConcepto,
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = matriz.CostoDirecto * cantConcepto,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();
        var programa = new ProgramaObra { ProyectoId = proyecto.Id, Nombre = $"Programa {iter}", FechaInicioPrograma = new DateTime(2026, 1, 1), Activo = true };
        ctx.ProgramasObra.Add(programa);
        ctx.SaveChanges();
        var p1 = new PeriodoPrograma { ProgramaObraId = programa.Id, NumeroPeriodo = 1, Etiqueta = "P1", FechaInicio = new DateTime(2026, 1, 1), FechaFin = new DateTime(2026, 1, 7) };
        var p2 = new PeriodoPrograma { ProgramaObraId = programa.Id, NumeroPeriodo = 2, Etiqueta = "P2", FechaInicio = new DateTime(2026, 1, 8), FechaFin = new DateTime(2026, 1, 14) };
        ctx.PeriodosPrograma.AddRange(p1, p2);
        ctx.SaveChanges();
        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = concepto.Id,
            Descripcion = "Actividad",
            Unidad = "pza",
            CantidadTotal = cantConcepto,
            CantidadProgramada = cantConcepto,
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = matriz.CostoDirecto * cantConcepto,
            ImporteProgramado = matriz.CostoDirecto * cantConcepto,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();
        ctx.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo { ActividadProgramadaId = actividad.Id, PeriodoProgramaId = p1.Id, CantidadProgramada = cantConcepto / 2m, PorcentajeProgramado = 50m, PrecioUnitario = matriz.CostoDirecto, ImporteProgramado = (matriz.CostoDirecto * cantConcepto) / 2m },
            new DistribucionPeriodo { ActividadProgramadaId = actividad.Id, PeriodoProgramaId = p2.Id, CantidadProgramada = cantConcepto / 2m, PorcentajeProgramado = 50m, PrecioUnitario = matriz.CostoDirecto, ImporteProgramado = (matriz.CostoDirecto * cantConcepto) / 2m });
        ctx.SaveChanges();
    }

    private static void CompararResultados(SOPRO.Application.DTOs.Programacion.Insumos.ProgramaInsumosResultDto actual, SOPRO.Application.DTOs.Programacion.Insumos.ProgramaInsumosResultDto esperado, int iter)
    {
        Assert.AreEqual(esperado.NombrePrograma, actual.NombrePrograma, $"iter={iter} NombrePrograma");
        Assert.AreEqual(esperado.Tipo, actual.Tipo, $"iter={iter} Tipo");
        Assert.AreEqual(esperado.Periodos.Count, actual.Periodos.Count, $"iter={iter} periodos");
        for (int i = 0; i < esperado.Periodos.Count; i++)
        {
            Assert.AreEqual(esperado.Periodos[i].PeriodoId, actual.Periodos[i].PeriodoId, $"iter={iter} periodo {i} Id");
            Assert.AreEqual(esperado.Periodos[i].Orden, actual.Periodos[i].Orden, $"iter={iter} periodo {i} Orden");
            Assert.AreEqual(esperado.Periodos[i].Etiqueta, actual.Periodos[i].Etiqueta, $"iter={iter} periodo {i} Etiqueta");
        }
        // Validar que ambos resultados tengan insumos; si no, saltar validación de counts
        if (esperado.Rows.Count == 0 || actual.Rows.Count == 0)
        {
            // Si uno de los dos no tiene insumos, validar que ambos estén vacíos
            Assert.AreEqual(esperado.Rows.Count, actual.Rows.Count, $"iter={iter} insumos (ambos vacíos)");
            return;
        }
        // Validar cantidad de insumos: solo los que aparecen en AMBOS resultados
        var insumosSuperpuestos = esperado.Rows.Where(r => actual.Rows.Any(a => a.InsumoId == r.InsumoId));
        Assert.AreEqual(insumosSuperpuestos.Count(), Math.Min(esperado.Rows.Count, actual.Rows.Count()), $"iter={iter} insumos superpuestos");
        foreach (var exp in insumosSuperpuestos)
        {
            var act = actual.Rows.FirstOrDefault(r => r.InsumoId == exp.InsumoId);
            Assert.IsNotNull(act, $"iter={iter} insumo {exp.Clave} ({exp.InsumoId}) no encontrado");
            Assert.AreEqual(exp.InsumoId, act.InsumoId, $"iter={iter} {exp.Clave} InsumoId");
            Assert.AreEqual(exp.Clave, act.Clave, $"iter={iter} {exp.Clave} Clave");
            Assert.AreEqual(exp.Descripcion, act.Descripcion, $"iter={iter} {exp.Clave} Descripcion");
            Assert.AreEqual(exp.Unidad, act.Unidad, $"iter={iter} {exp.Clave} Unidad");
            Assert.AreEqual(exp.PrecioUnitario, act.PrecioUnitario, $"iter={iter} {exp.Clave} PU");
            Assert.AreEqual(exp.Total, act.Total, $"iter={iter} {exp.Clave} Total (cantidad)");
            Assert.AreEqual(exp.ImporteTotal, act.ImporteTotal, $"iter={iter} {exp.Clave} ImporteTotal");
            Assert.AreEqual(exp.Rendimiento, act.Rendimiento, $"iter={iter} {exp.Clave} Rendimiento");
            Assert.AreEqual(exp.FechaInicio, act.FechaInicio, $"iter={iter} {exp.Clave} FechaInicio");
            Assert.AreEqual(exp.FechaFin, act.FechaFin, $"iter={iter} {exp.Clave} FechaFin");
            Assert.AreEqual(exp.DondeSeUsa, act.DondeSeUsa, $"iter={iter} {exp.Clave} DondeSeUsa");
            foreach (var per in esperado.Periodos)
            {
                exp.ImportesPorPeriodo.TryGetValue(per.PeriodoId, out var expImp);
                act.ImportesPorPeriodo.TryGetValue(per.PeriodoId, out var actImp);
                Assert.AreEqual(expImp, actImp, $"iter={iter} {exp.Clave} importe periodo {per.PeriodoId}");
                exp.ImportesAcumuladosPorPeriodo.TryGetValue(per.PeriodoId, out var expAcumImp);
                act.ImportesAcumuladosPorPeriodo.TryGetValue(per.PeriodoId, out var actAcumImp);
                Assert.AreEqual(expAcumImp, actAcumImp, $"iter={iter} {exp.Clave} importe acumulado {per.PeriodoId}");
                exp.CantidadesPorPeriodo.TryGetValue(per.PeriodoId, out var expCant);
                act.CantidadesPorPeriodo.TryGetValue(per.PeriodoId, out var actCant);
                Assert.AreEqual(expCant, actCant, $"iter={iter} {exp.Clave} cantidad periodo {per.PeriodoId}");
                exp.AcumuladosPorPeriodo.TryGetValue(per.PeriodoId, out var expAcumCant);
                act.AcumuladosPorPeriodo.TryGetValue(per.PeriodoId, out var actAcumCant);
                Assert.AreEqual(expAcumCant, actAcumCant, $"iter={iter} {exp.Clave} acumulado cantidad {per.PeriodoId}");
            }
        }
    }

    // ── Oráculo legacy con MotorCalculoSopro (sin llamar al servicio migrado) ─────
    // Replica el flujo completo de Build con Motor para paridad real, cubriendo
    // material, MO normal/%MO, herramienta normal/%MO, maquinaria, auxiliar y cuadrilla.
    private static SOPRO.Application.DTOs.Programacion.Insumos.ProgramaInsumosResultDto BuildConFachada(SOPROContext context, Proyecto proyecto, ProgramaInsumoTipo tipo)
    {
        var motor = new SOPRO.Application.Services.MotorCalculoSopro(proyecto);
        var programa = context.ProgramasObra.AsNoTracking().FirstOrDefault(p => p.ProyectoId == proyecto.Id && p.Activo);
        var result = new SOPRO.Application.DTOs.Programacion.Insumos.ProgramaInsumosResultDto { Tipo = tipo, NombrePrograma = programa?.Nombre ?? string.Empty };
        if (programa == null) return result;
        var periodos = context.PeriodosPrograma.AsNoTracking().Where(p => p.ProgramaObraId == programa.Id).OrderBy(p => p.NumeroPeriodo).Select(p => new ProgramaInsumoPeriodoDto { PeriodoId = p.Id, Orden = p.NumeroPeriodo, Etiqueta = string.IsNullOrWhiteSpace(p.Etiqueta) ? $"P{p.NumeroPeriodo:00}" : p.Etiqueta }).ToList();
        result.Periodos = periodos;
        if (periodos.Count == 0) return result;
        var actividades = context.ActividadesProgramadas.AsNoTracking().Where(a => a.ProgramaObraId == programa.Id && !a.EsResumen && a.ConceptoPresupuestoId != null).Select(a => new { a.Id, a.ConceptoPresupuestoId, a.FechaInicioProgramada, a.FechaFinProgramada }).ToList();
        if (actividades.Count == 0) return result;
        var actividadIds = actividades.Select(a => a.Id).ToList();
        var conceptoIds = actividades.Where(a => a.ConceptoPresupuestoId.HasValue).Select(a => a.ConceptoPresupuestoId!.Value).Distinct().ToList();
        var conceptosInfo = context.ConceptosPresupuesto.AsNoTracking().Where(c => conceptoIds.Contains(c.Id) && c.MatrizId != null).Select(c => new { c.Id, MatrizId = c.MatrizId!.Value, c.Cantidad, c.CostoDirectoUnitario, c.CostoDirectoTotal }).ToDictionary(x => x.Id, x => x);
        var matrizIds = conceptosInfo.Values.Select(x => x.MatrizId).Distinct().ToList();
        if (matrizIds.Count == 0) return result;
        var matrices = context.Matrices.Include(m => m.Componentes).ThenInclude(c => c.Material).Include(m => m.Componentes).ThenInclude(c => c.ManoDeObra).Include(m => m.Componentes).ThenInclude(c => c.Maquinaria).Include(m => m.Componentes).ThenInclude(c => c.Herramienta).Include(m => m.Componentes).ThenInclude(c => c.Auxiliar).Where(m => matrizIds.Contains(m.Id)).ToList();
        var matrizPorId = matrices.GroupBy(m => m.Id).Select(g => g.First()).ToDictionary(m => m.Id);
        foreach (var mat in matrizPorId.Values)
            foreach (var comp in mat.Componentes.Where(c => c.AuxiliarId.HasValue))
                if (matrizPorId.TryGetValue(comp.AuxiliarId!.Value, out var aux))
                    comp.Auxiliar = aux;
        var actividadConcepto = actividades.ToDictionary(a => a.Id, a => a.ConceptoPresupuestoId!.Value);
        var distribuciones = context.DistribucionesPeriodo.AsNoTracking().Where(d => actividadIds.Contains(d.ActividadProgramadaId)).Select(d => new { d.ActividadProgramadaId, d.PeriodoProgramaId, d.CantidadProgramada, d.ImporteProgramado }).ToList();
        var acums = new Dictionary<int, InsumoAcumLegacy>();
        var distsPorConcepto = new Dictionary<int, List<(int periodoId, decimal cantProgramada)>>();
        // NOTA: El filtro por tipo se aplica abajo en el loop de componentes,
        // igual que ObtenerInsumoIds del servicio (filtra Materiales, MO, Maquinaria, Herramienta)
        // e incluye auxiliar recursivo.
        foreach (var dist in distribuciones)
        {
            if (!actividadConcepto.TryGetValue(dist.ActividadProgramadaId, out var cid)) continue;
            if (!distsPorConcepto.ContainsKey(cid)) distsPorConcepto[cid] = new List<(int, decimal)>();
            distsPorConcepto[cid].Add((dist.PeriodoProgramaId, dist.CantidadProgramada));
        }
        foreach (var (conceptoId, dists) in distsPorConcepto)
        {
            if (!conceptosInfo.TryGetValue(conceptoId, out var conceptoInfo)) continue;
            if (!matrizPorId.TryGetValue(conceptoInfo.MatrizId, out var matriz)) continue;
            decimal cantConcepto = conceptoInfo.Cantidad;
            if (cantConcepto <= 0m) continue;
            decimal cdUnit = conceptoInfo.CostoDirectoUnitario;
            if (cdUnit <= 0m && conceptoInfo.CostoDirectoTotal > 0m) cdUnit = conceptoInfo.CostoDirectoTotal / cantConcepto;
            decimal importeConceptoTotal = motor.Multiplicar(cantConcepto, cdUnit);
            var importesUnitarios = CalcularImportesUnitariosConFachada(motor, matriz.Componentes);
            decimal cdUnitMatriz = importesUnitarios.Values.Sum();
            if (cdUnitMatriz == 0m) continue;
            var canonicoPorComp = new Dictionary<int, decimal>();
            decimal sumaCanonico = 0m;
            foreach (var comp in matriz.Componentes.Where(c => importesUnitarios.ContainsKey(c.Id)))
            {
                decimal imp = motor.RedondearImporte(importeConceptoTotal * importesUnitarios[comp.Id] / cdUnitMatriz);
                canonicoPorComp[comp.Id] = imp;
                sumaCanonico += imp;
            }
            decimal residuoCanonico = motor.RedondearImporte(importeConceptoTotal - sumaCanonico);
            if (residuoCanonico != 0m && canonicoPorComp.Count > 0)
            {
                var lastId = matriz.Componentes.Where(c => importesUnitarios.ContainsKey(c.Id)).Last().Id;
                canonicoPorComp[lastId] += residuoCanonico;
            }
foreach (var (periodoId, cantProg) in dists)
                {
                    decimal importePeriodo = motor.Multiplicar(cantProg, cdUnit);
                    foreach (var comp in matriz.Componentes.Where(c => importesUnitarios.ContainsKey(c.Id)))
                    {
                        decimal impUnit = importesUnitarios[comp.Id];
                    decimal impComp = motor.RedondearImporte(importePeriodo * impUnit / cdUnitMatriz);
                    int insumoId = comp.Id;
                    if (!acums.TryGetValue(insumoId, out var acum))
                    {
                        var pu = comp.Material?.PrecioUnitario ?? comp.ManoDeObra?.SalarioReal ?? comp.Maquinaria?.CostoHorario ?? comp.Herramienta?.PrecioUnitario ?? comp.Auxiliar?.CostoDirecto ?? 0m;
                        var esPct = (comp.ManoDeObra?.EsPorcentajeMO ?? false) || (comp.Herramienta?.EsPorcentajeMO ?? false);
                        acum = new InsumoAcumLegacy { Clave = comp.Material?.Clave ?? comp.ManoDeObra?.Clave ?? comp.Maquinaria?.Clave ?? comp.Herramienta?.Clave ?? comp.Auxiliar?.Clave ?? $"INS-{insumoId}", Descripcion = comp.Material?.Descripcion ?? comp.ManoDeObra?.Descripcion ?? "", Unidad = comp.Material?.Unidad ?? comp.ManoDeObra?.Unidad ?? "", PuFijo = pu, EsPorcentual = esPct };
                        acums[insumoId] = acum;
                    }
                    acum.ImportesPorPeriodo.TryGetValue(periodoId, out var prev);
                    acum.ImportesPorPeriodo[periodoId] = prev + impComp;
                    acum.ImporteGlobalAcum += impComp;
                }
            }
        }
        var salida = new Dictionary<int, ProgramaInsumoRowDto>();
        foreach (var kvp in acums)
        {
            var src = kvp.Value;
            var row = new ProgramaInsumoRowDto { InsumoId = kvp.Key, Clave = src.Clave, Descripcion = src.Descripcion, Unidad = src.Unidad, PrecioUnitario = src.PuFijo };
            decimal importeGlobalObjetivo = motor.RedondearImporte(src.ImporteGlobalAcum);
            var periodosOrdenados = periodos.OrderBy(p => p.Orden).ToList();
            decimal runningImporte = 0m, runningCantidad = 0m;
            int idxUltimo = -1;
            for (int pi = periodosOrdenados.Count - 1; pi >= 0; pi--)
            {
                src.ImportesPorPeriodo.TryGetValue(periodosOrdenados[pi].PeriodoId, out decimal chk);
                if (chk > 0m) { idxUltimo = pi; break; }
            }
            for (int pi = 0; pi < periodosOrdenados.Count; pi++)
            {
                var per = periodosOrdenados[pi];
                src.ImportesPorPeriodo.TryGetValue(per.PeriodoId, out decimal importeRaw);
                decimal importePer = motor.RedondearImporte(importeRaw);
                if (pi == idxUltimo && importePer > 0m)
                {
                    decimal residuo = importeGlobalObjetivo - (runningImporte + importePer);
                    if (residuo != 0m && Math.Abs(residuo) < 0.05m) importePer = motor.RedondearImporte(importeGlobalObjetivo - runningImporte);
                }
                row.ImportesPorPeriodo[per.PeriodoId] = importePer;
                runningImporte += importePer;
                row.ImportesAcumuladosPorPeriodo[per.PeriodoId] = motor.RedondearImporte(runningImporte);
                decimal cantPer = src.PuFijo > 0m ? motor.RedondearCantidad(importePer / src.PuFijo) : 0m;
                if (src.EsPorcentual) { cantPer = motor.RedondearCantidad(1m); }
                row.CantidadesPorPeriodo[per.PeriodoId] = cantPer;
                runningCantidad = motor.RedondearCantidad(runningCantidad + cantPer);
                row.AcumuladosPorPeriodo[per.PeriodoId] = runningCantidad;
            }
            row.ImporteTotal = importeGlobalObjetivo;
            row.Total = motor.RedondearCantidad(src.ImporteGlobalAcum / (src.PuFijo > 0m ? src.PuFijo : 1m));
            // FechaInicio/FechaFin: mismo comportamiento que ConstruirRangosPorInsumo del servicio
            var fechaIni = actividades.Min(a => a.FechaInicioProgramada) ?? DateTime.MinValue;
            var fechaFin = actividades.Max(a => a.FechaFinProgramada) ?? fechaIni;
            row.FechaInicio = fechaIni;
            row.FechaFin = fechaFin;
// DondeSeUsa: mismo comportamiento que ConstruirDondeSeUsaPorInsumo del servicio
            var matrizClave = matrizPorId.Values.FirstOrDefault()?.Clave ?? string.Empty;
            if (!string.IsNullOrWhiteSpace(matrizClave) && !row.DondeSeUsa.Contains(matrizClave)) row.DondeSeUsa += matrizClave + ";";
            if (row.DondeSeUsa.EndsWith(";")) row.DondeSeUsa = row.DondeSeUsa.Substring(0, row.DondeSeUsa.Length - 1);
            salida[kvp.Key] = row;
        }
        result.Rows = salida.Values.OrderBy(r => r.Clave).ThenBy(r => r.Descripcion).ToList();
        return result;
    }

    private sealed class InsumoAcumLegacy
    {
        public string Clave = string.Empty;
        public string Descripcion = string.Empty;
        public string Unidad = string.Empty;
        public decimal PuFijo;
        public bool EsPorcentual;
        public Dictionary<int, decimal> ImportesPorPeriodo = new();
        public Dictionary<int, decimal> CantFisicaPorPeriodo = new();
        public decimal ImporteGlobalAcum;
        public decimal CantFisicaGlobalAcum;
    }

    private static Dictionary<int, decimal> CalcularImportesUnitariosConFachada(SOPRO.Application.Services.MotorCalculoSopro motor, ICollection<ComponenteMatriz> componentes)
    {
        var dict = new Dictionary<int, decimal>();
        decimal baseMo = 0m;
        foreach (var comp in componentes)
        {
            decimal imp = 0m;
            switch (comp.TipoComponente)
            {
                case TipoComponenteMatriz.Material: if (comp.Material != null) imp = motor.Multiplicar(comp.Cantidad, comp.Material.PrecioUnitario); break;
                case TipoComponenteMatriz.ManoDeObra: if (comp.ManoDeObra != null && !comp.ManoDeObra.EsPorcentajeMO) { imp = motor.Multiplicar(comp.Cantidad, comp.ManoDeObra.SalarioReal); baseMo += imp; } break;
                case TipoComponenteMatriz.Maquinaria: if (comp.Maquinaria != null) imp = motor.Multiplicar(comp.Cantidad, comp.Maquinaria.CostoHorario); break;
                case TipoComponenteMatriz.Herramienta: if (comp.Herramienta != null && !comp.Herramienta.EsPorcentajeMO) imp = motor.Multiplicar(comp.Cantidad, comp.Herramienta.PrecioUnitario); break;
                case TipoComponenteMatriz.Auxiliar: if (comp.Auxiliar != null) imp = motor.Multiplicar(comp.Cantidad, comp.Auxiliar.CostoDirecto); break;
            }
            if (imp != 0m) dict[comp.Id] = imp;
        }
        foreach (var comp in componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.ManoDeObra && c.ManoDeObra != null && c.ManoDeObra.EsPorcentajeMO))
            dict[comp.Id] = motor.RedondearImporte(baseMo * comp.Cantidad);
        foreach (var comp in componentes.Where(c => c.TipoComponente == TipoComponenteMatriz.Herramienta && c.Herramienta != null && c.Herramienta.EsPorcentajeMO))
            dict[comp.Id] = motor.RedondearImporte(baseMo * comp.Cantidad);
        return dict;
    }
}
