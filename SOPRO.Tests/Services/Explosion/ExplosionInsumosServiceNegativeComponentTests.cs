using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation;
using SOPRO.Application.DTOs.Programacion.Insumos;
using SOPRO.Application.Services;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Explosion;

/// <summary>
/// [N7-2] Corrección contable de signos en Explosión/Programación de insumos:
/// los importes negativos (descuentos, Mo de obra negativas, cuadrillas negativas)
/// deben conservarse como filas y participar en la normalización, igual que en la
/// canónica MatrixComponentCalculationService. La omisión de ceros NO es divergencia
/// observable (el paso de distribución descarta importeUnit == 0m en ambas versiones).
/// La acumulación baseMO += imp sin redondeo por paso tampoco: es equivalente a la
/// canónica porque cada imp ya sale con escala DecimalesImporte de engine.Multiply.
/// </summary>
[TestClass]
public class ExplosionInsumosServiceNegativeComponentTests
{
    [TestMethod]
    public void Calculate_MaterialDescuentoNegativo_ConservaFilaYReconcilia()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(ctx);
        var cemento = CrearMaterial(ctx, proyecto, "CEM", 100m);
        var descuento = CrearMaterial(ctx, proyecto, "DESC", -30m);

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id, Clave = "APU-NEG", Descripcion = string.Empty,
            Unidad = "m2", Tipo = TipoMatriz.APU, Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id, Material = cemento, Cantidad = 1m, Orden = 1, Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = descuento.Id, Material = descuento, Cantidad = 1m, Orden = 2, Notas = string.Empty
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        var totals = MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totals.CostoDirectoTotal;

        var concepto = CrearConcepto(ctx, proyecto, matriz, 10m);
        ctx.SaveChanges();

        var result = new ExplosionInsumosService().Calculate(ctx, proyecto.Id, "Todos");

        Assert.AreEqual(70m, matriz.CostoDirecto, "CD canónico: 100 − 30.");
        Assert.IsTrue(result.Materiales.ContainsKey(descuento.Id), "la fila del descuento debe conservarse");
        Assert.AreEqual(1000m, result.Materiales[cemento.Id].Cantidad);
        Assert.AreEqual(-300m, result.Materiales[descuento.Id].Cantidad);
        Assert.AreEqual(700m, result.CostoDirectoTotal);
        Assert.AreEqual(result.CostoDirectoPresupuesto, result.CostoDirectoTotal);
    }

    [TestMethod]
    public void Calculate_ManoDeObraNegativaConPorcentajeMO_ConservaAmbasFilas()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(ctx);
        var cemento = CrearMaterial(ctx, proyecto, "CEM", 100m);
        var oficial = CrearManoDeObra(ctx, proyecto, "OFICIAL", "jor", -50m);
        var cabo = CrearManoDeObra(ctx, proyecto, "CABO", "%MO", 0m);

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id, Clave = "APU-MONEG", Descripcion = string.Empty,
            Unidad = "m2", Tipo = TipoMatriz.APU, Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id, Material = cemento, Cantidad = 1m, Orden = 1, Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = oficial.Id, ManoDeObra = oficial, Cantidad = 1m, Orden = 2, Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = cabo.Id, ManoDeObra = cabo, Cantidad = .10m, Orden = 3, Notas = string.Empty
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        var totals = MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totals.CostoDirectoTotal; // 100 − 50 − 5 = 45

        var concepto = CrearConcepto(ctx, proyecto, matriz, 1m);
        ctx.SaveChanges();

        var result = new ExplosionInsumosService().Calculate(ctx, proyecto.Id, "Todos");

        Assert.AreEqual(45m, matriz.CostoDirecto);
        Assert.AreEqual(100m, result.Materiales[cemento.Id].Cantidad);
        Assert.IsTrue(result.ManoObra.ContainsKey(oficial.Id), "la MO negativa debe conservarse");
        Assert.AreEqual(-50m, result.ManoObra[oficial.Id].Cantidad);
        Assert.IsTrue(result.ManoObra.ContainsKey(cabo.Id), "el %MO sobre base negativa debe conservarse");
        Assert.AreEqual(-5m, result.ManoObra[cabo.Id].Cantidad);
        Assert.AreEqual(45m, result.CostoDirectoTotal);
        Assert.AreEqual(result.CostoDirectoPresupuesto, result.CostoDirectoTotal);
    }

    [TestMethod]
    public void Calculate_CuadrillaNegativa_ExploraFilaNegativaYReconcilia()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(ctx);
        var cemento = CrearMaterial(ctx, proyecto, "CEM", 100m);
        var albanil = CrearManoDeObra(ctx, proyecto, "ALBANIL", "jor", -40m);
        var ayudante = CrearManoDeObra(ctx, proyecto, "AYUDANTE", "%MO", 0m);

        var cuadrilla = new Matriz
        {
            ProyectoId = proyecto.Id, Clave = "CUA-NEG", Descripcion = string.Empty,
            Unidad = "jor", Tipo = TipoMatriz.Cuadrilla, Notas = string.Empty
        };
        cuadrilla.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = albanil.Id, ManoDeObra = albanil, Cantidad = 1m, Orden = 1, Notas = string.Empty
        });
        ctx.Matrices.Add(cuadrilla);
        ctx.SaveChanges();
        cuadrilla.CostoDirecto = MatrixComponentCalculationService
            .Recalculate(cuadrilla.Componentes.ToList(), proyecto.DecimalesImporte).CostoDirectoTotal; // −40

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id, Clave = "APU-CUINEG", Descripcion = string.Empty,
            Unidad = "m2", Tipo = TipoMatriz.APU, Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id, Material = cemento, Cantidad = 1m, Orden = 1, Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Auxiliar,
            AuxiliarId = cuadrilla.Id, Auxiliar = cuadrilla, Cantidad = 2m, Orden = 2, Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = ayudante.Id, ManoDeObra = ayudante, Cantidad = .10m, Orden = 3, Notas = string.Empty
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        matriz.CostoDirecto = MatrixComponentCalculationService
            .Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte).CostoDirectoTotal; // 100 − 80 − 8 = 12

        var concepto = CrearConcepto(ctx, proyecto, matriz, 1m);
        ctx.SaveChanges();

        var result = new ExplosionInsumosService().Calculate(ctx, proyecto.Id, "Todos");

        Assert.AreEqual(12m, matriz.CostoDirecto);
        Assert.AreEqual(100m, result.Materiales[cemento.Id].Cantidad);
        Assert.IsTrue(result.ManoObra.ContainsKey(albanil.Id), "la cuadrilla negativa debe explorarse como fila");
        Assert.AreEqual(-80m, result.ManoObra[albanil.Id].Cantidad);
        Assert.IsTrue(result.ManoObra.ContainsKey(ayudante.Id), "el %MO sobre base negativa debe conservarse");
        Assert.AreEqual(-8m, result.ManoObra[ayudante.Id].Cantidad);
        Assert.AreEqual(12m, result.CostoDirectoTotal);
        Assert.AreEqual(result.CostoDirectoPresupuesto, result.CostoDirectoTotal);
    }

    [TestMethod]
    public void Calculate_MaterialConPrecioCero_OmitirCeroNoCambiaResultadosObservables()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(ctx);
        var cemento = CrearMaterial(ctx, proyecto, "CEM", 100m);
        var sinPrecio = CrearMaterial(ctx, proyecto, "CERO", 0m);

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id, Clave = "APU-CERO", Descripcion = string.Empty,
            Unidad = "m2", Tipo = TipoMatriz.APU, Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id, Material = cemento, Cantidad = 1m, Orden = 1, Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = sinPrecio.Id, Material = sinPrecio, Cantidad = 5m, Orden = 2, Notas = string.Empty
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        matriz.CostoDirecto = MatrixComponentCalculationService
            .Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte).CostoDirectoTotal;

        var concepto = CrearConcepto(ctx, proyecto, matriz, 10m);
        ctx.SaveChanges();

        var result = new ExplosionInsumosService().Calculate(ctx, proyecto.Id, "Todos");

        Assert.IsFalse(result.Materiales.ContainsKey(sinPrecio.Id),
            "el importe cero no se materializa en fila (comportamiento congelado: sin efecto observable)");
        Assert.AreEqual(1000m, result.Materiales[cemento.Id].Cantidad);
        Assert.AreEqual(1000m, result.CostoDirectoTotal);
        Assert.AreEqual(result.CostoDirectoPresupuesto, result.CostoDirectoTotal);
    }

    [TestMethod]
    public void Build_InsumoNegativo_ConservaFilaYReconciliaPorPeriodo()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(ctx);
        var material = CrearMaterial(ctx, proyecto, "NEG", -20m);

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id, Clave = "APU-PROGNEG", Descripcion = string.Empty,
            Unidad = "pza", Tipo = TipoMatriz.APU, Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = material.Id, Material = material, Cantidad = 3m, Orden = 1, Notas = string.Empty
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        matriz.CostoDirecto = MatrixComponentCalculationService
            .Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte).CostoDirectoTotal; // −60

        var concepto = CrearConcepto(ctx, proyecto, matriz, 10m); // CD total −600

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id, Nombre = "Programa Neg",
            FechaInicioPrograma = new DateTime(2026, 1, 1), Activo = true
        };
        ctx.ProgramasObra.Add(programa);
        ctx.SaveChanges();

        var p1 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id, NumeroPeriodo = 1, Etiqueta = "P1",
            FechaInicio = new DateTime(2026, 1, 1), FechaFin = new DateTime(2026, 1, 7)
        };
        var p2 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id, NumeroPeriodo = 2, Etiqueta = "P2",
            FechaInicio = new DateTime(2026, 1, 8), FechaFin = new DateTime(2026, 1, 14)
        };
        ctx.PeriodosPrograma.AddRange(p1, p2);
        ctx.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = concepto.Id,
            Descripcion = "Actividad Neg", Unidad = "pza",
            CantidadTotal = 10m, CantidadProgramada = 10m,
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = -600m, ImporteProgramado = -600m,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();

        ctx.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id, PeriodoProgramaId = p1.Id,
                CantidadProgramada = 5m, PorcentajeProgramado = 50m,
                PrecioUnitario = matriz.CostoDirecto, ImporteProgramado = -300m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id, PeriodoProgramaId = p2.Id,
                CantidadProgramada = 5m, PorcentajeProgramado = 50m,
                PrecioUnitario = matriz.CostoDirecto, ImporteProgramado = -300m
            });
        ctx.SaveChanges();

        var result = new ProgramacionInsumosService().Build(ctx, proyecto, ProgramaInsumoTipo.Materiales);

        Assert.AreEqual(1, result.Rows.Count, "la fila del insumo negativo debe existir");
        var row = result.Rows.Single();
        Assert.AreEqual(material.Id, row.InsumoId);
        Assert.AreEqual(-600m, row.ImporteTotal);
        Assert.AreEqual(-300m, row.ImportesPorPeriodo[p1.Id]);
        Assert.AreEqual(-300m, row.ImportesPorPeriodo[p2.Id]);
        Assert.AreEqual(row.ImporteTotal, row.ImportesPorPeriodo.Values.Sum());
    }

    /// <summary>
    /// [N7-2] Discriminante del residuo de reconciliación negativa
    /// (ProgramacionInsumosService.cs:257, `impCheck != 0m`): con componentes
    /// +9 y −2 (CD 7), cantidad 1 y tres periodos 0.3333/0.3333/0.3334, el insumo
    /// negativo queda −0.67/−0.67/−0.67 (Σ −2.01) y la reconciliación debe aplicar
    /// +0.01 al último periodo → −0.67/−0.67/−0.66 con total −2.00. Con el rastreo
    /// viejo (`&gt; 0m`) el último periodo nunca se registra para importes negativos,
    /// el residuo no se absorbe y el total queda en −2.01.
    /// </summary>
    [TestMethod]
    public void Build_ResiduoDeReconciliacionSobreInsumoNegativo_SeAbsorbeEnUltimoPeriodo()
    {
        using var ctx = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto(ctx);
        var positivo = CrearMaterial(ctx, proyecto, "MATPOS", 9m);
        var negativo = CrearMaterial(ctx, proyecto, "MATNEG", -2m);

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id, Clave = "APU-RES", Descripcion = string.Empty,
            Unidad = "m2", Tipo = TipoMatriz.APU, Notas = string.Empty
        };
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = positivo.Id, Material = positivo, Cantidad = 1m, Orden = 1, Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = negativo.Id, Material = negativo, Cantidad = 1m, Orden = 2, Notas = string.Empty
        });
        ctx.Matrices.Add(matriz);
        ctx.SaveChanges();
        matriz.CostoDirecto = MatrixComponentCalculationService
            .Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte).CostoDirectoTotal; // 7

        var concepto = CrearConcepto(ctx, proyecto, matriz, 1m); // CD total 7

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id, Nombre = "Programa Residuo",
            FechaInicioPrograma = new DateTime(2026, 1, 1), Activo = true
        };
        ctx.ProgramasObra.Add(programa);
        ctx.SaveChanges();

        var fechas = new[]
        {
            (1, new DateTime(2026, 1, 1), new DateTime(2026, 1, 7)),
            (2, new DateTime(2026, 1, 8), new DateTime(2026, 1, 14)),
            (3, new DateTime(2026, 1, 15), new DateTime(2026, 1, 21))
        };
        var periodos = new List<PeriodoPrograma>();
        foreach (var (n, ini, fin) in fechas)
        {
            var periodo = new PeriodoPrograma
            {
                ProgramaObraId = programa.Id, NumeroPeriodo = n, Etiqueta = $"P{n}",
                FechaInicio = ini, FechaFin = fin
            };
            ctx.PeriodosPrograma.Add(periodo);
            ctx.SaveChanges();
            periodos.Add(periodo);
        }

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = concepto.Id,
            Descripcion = "Actividad Residuo", Unidad = "m2",
            CantidadTotal = 1m, CantidadProgramada = 1m,
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = 7m, ImporteProgramado = 7m,
            DuracionDiasHabiles = 15,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 21)
        };
        ctx.ActividadesProgramadas.Add(actividad);
        ctx.SaveChanges();

        var cantidades = new[] { 0.3333m, 0.3333m, 0.3334m };
        for (int i = 0; i < periodos.Count; i++)
        {
            ctx.DistribucionesPeriodo.Add(new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodos[i].Id,
                CantidadProgramada = cantidades[i],
                PorcentajeProgramado = cantidades[i] * 100m,
                PrecioUnitario = matriz.CostoDirecto,
                ImporteProgramado = Math.Round(cantidades[i] * 7m, 2, MidpointRounding.AwayFromZero)
            });
        }
        ctx.SaveChanges();

        var result = new ProgramacionInsumosService().Build(ctx, proyecto, ProgramaInsumoTipo.Materiales);

        var filaNegativa = result.Rows.Single(r => r.InsumoId == negativo.Id);
        var filaPositiva = result.Rows.Single(r => r.InsumoId == positivo.Id);

        Assert.AreEqual(-2.00m, filaNegativa.ImporteTotal,
            "el residuo +0.01 debe absorberse: total canónico −2.00, no −2.01");
        CollectionAssert.AreEqual(
            new[] { -0.67m, -0.67m, -0.66m },
            filaNegativa.ImportesPorPeriodo.Values.OrderBy(v => v).ToList(),
            "dos periodos en −0.67 y uno en −0.66 (absorción del residuo)");
        Assert.AreEqual(filaNegativa.ImporteTotal, filaNegativa.ImportesPorPeriodo.Values.Sum());

        Assert.AreEqual(9.00m, filaPositiva.ImporteTotal, "el insumo positivo reconcilia a su canónico 9.00");
    }

    [DataTestMethod]
    [DataRow(0)]
    [DataRow(2)]
    [DataRow(3)]
    [DataRow(4)]
    public void BaseMoeAcumulacionContinua_EsEquivalenteALaCanonicaPorPaso(int decImp)
    {
        // Precisión del dictamen de revisión: baseMO += imp sin redondeo por paso NO es
        // divergencia observable. Cada imp sale de engine.Multiply con escala ≤ decImp;
        // la suma de valores de escala ≤ d también tiene escala ≤ d, por lo que
        // RoundAmount por paso es identidad. Batería con signos mixtos.
        var engine = new SoproCalculationEngine(2, decImp, 4);
        var rnd = new Random(20260902);

        for (var iter = 0; iter < 200; iter++)
        {
            var importes = Enumerable.Range(0, 8)
                .Select(_ => engine.Multiply(
                    rnd.Next(-1000, 1000) / 100m,
                    rnd.Next(-100000, 100000) / 1000m))
                .ToList();

            var continua = importes.Sum();
            var canonica = 0m;
            foreach (var v in importes)
                canonica = engine.RoundAmount(canonica + v);

            Assert.AreEqual(canonica, continua,
                $"iter={iter} decImp={decImp}: la acumulación sin redondeo por paso debe coincidir con la canónica");
        }
    }

    private static Proyecto CrearProyecto(SOPROContext ctx)
    {
        var p = new Proyecto
        {
            Nombre = "Proyecto Negativos",
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
        ctx.Proyectos.Add(p);
        ctx.SaveChanges();
        return p;
    }

    private static Material CrearMaterial(SOPROContext ctx, Proyecto proyecto, string clave, decimal precio)
    {
        var m = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = string.Empty,
            Unidad = "pza",
            PrecioUnitario = precio,
            Notas = string.Empty
        };
        ctx.Materiales.Add(m);
        ctx.SaveChanges();
        return m;
    }

    private static ManoDeObra CrearManoDeObra(SOPROContext ctx, Proyecto proyecto, string clave, string unidad, decimal salarioReal)
    {
        var mo = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = clave,
            Descripcion = string.Empty,
            Unidad = unidad,
            SalarioBase = 0m,
            SalarioReal = salarioReal,
            Notas = string.Empty
        };
        ctx.ManoDeObra.Add(mo);
        ctx.SaveChanges();
        return mo;
    }

    private static ConceptoPresupuesto CrearConcepto(SOPROContext ctx, Proyecto proyecto, Matriz matriz, decimal cantidad)
    {
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-NEG",
            Descripcion = string.Empty,
            Unidad = "pza",
            Cantidad = cantidad,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = matriz.CostoDirecto,
            CostoDirectoTotal = matriz.CostoDirecto * cantidad,
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = matriz.CostoDirecto * cantidad,
            Nivel = 1,
            Orden = 1,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        ctx.ConceptosPresupuesto.Add(concepto);
        ctx.SaveChanges();
        return concepto;
    }
}
