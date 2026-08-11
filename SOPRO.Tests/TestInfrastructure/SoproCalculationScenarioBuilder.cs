using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;

namespace SOPRO.Tests.TestInfrastructure;

internal sealed class SoproCalculationScenario
{
    public Proyecto Proyecto { get; init; } = null!;
    public Matriz MatrizApu { get; init; } = null!;
    public ConceptoPresupuesto Concepto { get; init; } = null!;
    public Material Cemento { get; init; } = null!;
    public ManoDeObra Oficial { get; init; } = null!;
    public ManoDeObra CaboPorcentajeMo { get; init; } = null!;
    public Herramienta HerramientaMenorPorcentajeMo { get; init; } = null!;
    public Maquinaria Revolvedora { get; init; } = null!;
    public ProgramaObra? Programa { get; set; }
    public PeriodoPrograma? Periodo1 { get; set; }
    public PeriodoPrograma? Periodo2 { get; set; }
}

internal static class SoproCalculationScenarioBuilder
{
    /// <summary>
    /// Crea un escenario mínimo pero completo de SOPRO:
    /// APU con material + MO normal + MO %MO + herramienta %MO + maquinaria.
    /// CD unitario esperado: 100.00
    /// Concepto: cantidad 10.00, CD total esperado 1,000.00
    /// </summary>
    public static SoproCalculationScenario CreateBaseBudgetScenario(SOPROContext context)
    {
        var proyecto = new Proyecto
        {
            Nombre = "Proyecto prueba reconciliación",
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

        var cemento = new Material
        {
            ProyectoId = proyecto.Id,
            Clave = "MAT-CEM",
            Descripcion = "Cemento gris",
            Unidad = "kg",
            PrecioUnitario = 60m,
            Notas = string.Empty
        };

        var oficial = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = "MO-OF",
            Descripcion = "Oficial albañil",
            Unidad = "jor",
            SalarioBase = 30m,
            FactorSalarioReal = 1m,
            SalarioReal = 30m,
            Notas = string.Empty
        };

        var caboPorcentajeMo = new ManoDeObra
        {
            ProyectoId = proyecto.Id,
            Clave = "MO-CABO",
            Descripcion = "Cabo de oficio",
            Unidad = "%MO",
            SalarioBase = 0m,
            FactorSalarioReal = 1m,
            SalarioReal = 0m,
            Notas = string.Empty
        };

        var herramientaMenor = new Herramienta
        {
            ProyectoId = proyecto.Id,
            Clave = "HER-MEN",
            Descripcion = "Herramienta menor",
            Unidad = "%MO",
            PrecioUnitario = 0m,
            Notas = string.Empty
        };

        var revolvedora = new Maquinaria
        {
            ProyectoId = proyecto.Id,
            Clave = "MAQ-REV",
            Descripcion = "Revolvedora",
            CostoHorario = 4m,
            Notas = string.Empty
        };

        context.Materiales.Add(cemento);
        context.ManoDeObra.AddRange(oficial, caboPorcentajeMo);
        context.Herramientas.Add(herramientaMenor);
        context.Maquinaria.Add(revolvedora);
        context.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concreto simple",
            Unidad = "m3",
            Tipo = TipoMatriz.APU,
            Notas = string.Empty
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Material,
            MaterialId = cemento.Id,
            Material = cemento,
            Cantidad = 1m,
            Orden = 1,
            Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = oficial.Id,
            ManoDeObra = oficial,
            Cantidad = 1m,
            Orden = 2,
            Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.ManoDeObra,
            ManoDeObraId = caboPorcentajeMo.Id,
            ManoDeObra = caboPorcentajeMo,
            Cantidad = 0.10m,
            Orden = 3,
            Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Herramienta,
            HerramientaId = herramientaMenor.Id,
            Herramienta = herramientaMenor,
            Cantidad = 0.10m,
            Orden = 4,
            Notas = string.Empty
        });
        matriz.Componentes.Add(new ComponenteMatriz
        {
            MatrizId = matriz.Id,
            TipoComponente = TipoComponenteMatriz.Maquinaria,
            MaquinariaId = revolvedora.Id,
            Maquinaria = revolvedora,
            Cantidad = 1m,
            Rendimiento = 1m,
            Orden = 5,
            Notas = string.Empty
        });

        var totals = MatrixComponentCalculationService.Recalculate(matriz.Componentes.ToList(), proyecto.DecimalesImporte);
        matriz.CostoDirecto = totals.CostoDirectoTotal;
        context.SaveChanges();

        var motor = new MotorCalculoSopro(proyecto);
        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Concreto simple en obra",
            Unidad = "m3",
            Cantidad = 10m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = matriz.CostoDirecto,
            CostoDirectoTotal = motor.Multiplicar(10m, matriz.CostoDirecto),
            PrecioUnitario = matriz.CostoDirecto,
            ImporteTotal = motor.Multiplicar(10m, matriz.CostoDirecto),
            Nivel = 1,
            Orden = 1,
            EsAgrupador = false,
            ColumnasPersonalizadasJSON = string.Empty,
            Notas = string.Empty
        };
        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();

        return new SoproCalculationScenario
        {
            Proyecto = proyecto,
            MatrizApu = matriz,
            Concepto = concepto,
            Cemento = cemento,
            Oficial = oficial,
            CaboPorcentajeMo = caboPorcentajeMo,
            HerramientaMenorPorcentajeMo = herramientaMenor,
            Revolvedora = revolvedora
        };
    }

    public static void AddTwoPeriodProgram(SOPROContext context, SoproCalculationScenario scenario)
    {
        var programa = new ProgramaObra
        {
            ProyectoId = scenario.Proyecto.Id,
            Nombre = "Programa base pruebas",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true,
            GeneradoDesdePresupuesto = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var periodo1 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 1,
            Etiqueta = "P1",
            FechaInicio = new DateTime(2026, 1, 1),
            FechaFin = new DateTime(2026, 1, 7)
        };
        var periodo2 = new PeriodoPrograma
        {
            ProgramaObraId = programa.Id,
            NumeroPeriodo = 2,
            Etiqueta = "P2",
            FechaInicio = new DateTime(2026, 1, 8),
            FechaFin = new DateTime(2026, 1, 14)
        };
        context.PeriodosPrograma.AddRange(periodo1, periodo2);
        context.SaveChanges();

        var actividad = new ActividadProgramada
        {
            ProgramaObraId = programa.Id,
            ConceptoPresupuestoId = scenario.Concepto.Id,
            Clave = scenario.Concepto.Clave,
            Descripcion = scenario.Concepto.Descripcion,
            Unidad = scenario.Concepto.Unidad,
            CantidadTotal = scenario.Concepto.Cantidad,
            CantidadProgramada = scenario.Concepto.Cantidad,
            PrecioUnitario = scenario.Concepto.CostoDirectoUnitario,
            ImporteTotal = scenario.Concepto.CostoDirectoTotal,
            ImporteProgramado = scenario.Concepto.CostoDirectoTotal,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14),
            DuracionDiasHabiles = 10,
            Orden = 1,
            Nivel = 1,
            Notas = string.Empty
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        context.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodo1.Id,
                CantidadProgramada = 4m,
                PorcentajeProgramado = 40m,
                PrecioUnitario = scenario.Concepto.CostoDirectoUnitario,
                ImporteProgramado = 400m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodo2.Id,
                CantidadProgramada = 6m,
                PorcentajeProgramado = 60m,
                PrecioUnitario = scenario.Concepto.CostoDirectoUnitario,
                ImporteProgramado = 600m
            });
        context.SaveChanges();

        scenario.Programa = programa;
        scenario.Periodo1 = periodo1;
        scenario.Periodo2 = periodo2;
    }
}
