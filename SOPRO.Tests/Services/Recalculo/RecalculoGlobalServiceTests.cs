using System;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Application.Models.Presupuesto;
using SOPRO.Core.Entities;
using SOPRO.Data.Context;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Recalculo;

[TestClass]
public class RecalculoGlobalServiceTests
{
    [TestMethod]
    public void Ejecutar_ConEscenarioBase_ActualizaTodasLasFasesSinCambiarResultados()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        var resultado = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);

        Assert.AreEqual(5, resultado.ComponentesActualizados);
        Assert.AreEqual(1, resultado.MatricesActualizadas);
        Assert.AreEqual(1, resultado.ConceptosActualizados);
        Assert.AreEqual(0, resultado.AgrupadoresTotalesRecalculados);
        Assert.AreEqual(0, resultado.DistribucionesActualizadas);
        Assert.IsFalse(resultado.ProgramaActualizado);

        Assert.AreEqual(100.00m, scenario.MatrizApu.CostoDirecto, "El C.D. de la matriz no debe cambiar con porcentajes 0.");
        Assert.AreEqual(100.00m, scenario.Concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, scenario.Concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public void Ejecutar_ConPorcentajes_ConservaLaCascadaLegacyEnAmbosModos()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        var service = new RecalculoGlobalService();

        scenario.Proyecto.PorcentajeIndirectosCentral = 10m;
        scenario.Proyecto.PorcentajeIndirectosCampo = 5m;
        scenario.Proyecto.PorcentajeFinanciamiento = 2m;
        scenario.Proyecto.PorcentajeUtilidad = 3m;
        scenario.Proyecto.PorcentajeCargosAdicionales = 1m;
        scenario.Proyecto.ModoCalculoPorcentajes = "Acumulables";
        context.SaveChanges();

        var fachada = new MotorCalculoSopro(scenario.Proyecto);
        var esperadoAcumulable = fachada.CalcularPrecioUnitario(100m, new BudgetPercentageInput
        {
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 3m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "Acumulables"
        });

        service.Ejecutar(context, scenario.Proyecto.Id);

        Assert.AreEqual(esperadoAcumulable.PrecioUnitario, scenario.Concepto.PrecioUnitario);
        Assert.AreEqual(fachada.Multiplicar(10m, esperadoAcumulable.PrecioUnitario), scenario.Concepto.ImporteTotal);

        scenario.Proyecto.ModoCalculoPorcentajes = "SobreCD";
        context.SaveChanges();

        var esperadoSobreCd = fachada.CalcularPrecioUnitario(100m, new BudgetPercentageInput
        {
            IndirectosCentral = 10m,
            IndirectosCampo = 5m,
            Financiamiento = 2m,
            Utilidad = 3m,
            CargosAdicionales = 1m,
            ModoCalculoPorcentajes = "SobreCD"
        });

        service.Ejecutar(context, scenario.Proyecto.Id);

        Assert.AreEqual(esperadoSobreCd.PrecioUnitario, scenario.Concepto.PrecioUnitario);
        Assert.AreEqual(fachada.Multiplicar(10m, esperadoSobreCd.PrecioUnitario), scenario.Concepto.ImporteTotal);
    }

    [TestMethod]
    public void Ejecutar_ConProgramaDeDosPeriodos_RedistribuyeLasDistribuciones()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        var resultado = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);

        Assert.AreEqual(5, resultado.ComponentesActualizados);
        Assert.AreEqual(1, resultado.MatricesActualizadas);
        Assert.AreEqual(1, resultado.ConceptosActualizados);
        Assert.AreEqual(0, resultado.AgrupadoresTotalesRecalculados);
        Assert.AreEqual(2, resultado.DistribucionesActualizadas);
        Assert.IsTrue(resultado.ProgramaActualizado);

        // ⚠️ DIVERGENCIA DOCUMENTADA:
        // La Fase 5 recalcula con los porcentajes existentes (40/60 → 400/600), pero la
        // Fase 6 (RecalcularProgramaObra) ELIMINA esas distribuciones y regenera una
        // distribución UNIFORME por días hábiles (50/50 → 500/500). Por eso el estado
        // final NO es 40/60: se congelan los valores uniformes que produce la cascada real.
        var distribuciones = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .ToList();

        Assert.AreEqual(2, distribuciones.Count);
        Assert.AreEqual(1000.00m, distribuciones.Sum(d => d.ImporteProgramado));
        Assert.AreEqual(100.00m, distribuciones.Sum(d => d.PorcentajeProgramado));
        Assert.AreEqual(10.00m, distribuciones.Sum(d => d.CantidadProgramada));

        Assert.AreEqual(500.00m, distribuciones[0].ImporteProgramado, "P1 debe absorber la mitad uniforme.");
        Assert.AreEqual(500.00m, distribuciones[1].ImporteProgramado, "P2 debe absorber la mitad uniforme.");
        Assert.AreEqual(50.0000m, distribuciones[0].PorcentajeProgramado);
        Assert.AreEqual(50.0000m, distribuciones[1].PorcentajeProgramado);
        Assert.AreEqual(5.00m, distribuciones[0].CantidadProgramada);
        Assert.AreEqual(5.00m, distribuciones[1].CantidadProgramada);
    }

    [TestMethod]
    public void Ejecutar_ConProyectoInexistente_LanzaInvalidOperationException()
    {
        using var context = TestDbFactory.CreateContext();

        var ex = Assert.ThrowsException<InvalidOperationException>(
            () => new RecalculoGlobalService().Ejecutar(context, 99999));

        StringAssert.Contains(ex.Message, "99999");
    }

    [TestMethod]
    public void Ejecutar_DespuesDeCambiarDecimales_RecomputaConLaNuevaPrecision()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // Precio discriminante: 60.005 se redondea a 60.01 con 2 decimales (AwayFromZero)
        // pero se conserva como 60.005 con 3 decimales. Esto garantiza que la cascada
        // produce resultados NUMÉRICAMENTE distintos entre una precisión y la otra;
        // si el recálculo ignorara la nueva precisión, estas aserciones fallarían.
        scenario.Cemento.PrecioUnitario = 60.005m;
        context.SaveChanges();

        // Precisión 2 (por defecto del escenario): 60.01 + 30 + 3 + 3 + 4 = 100.01
        var resultado2 = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);
        Assert.AreEqual(1, resultado2.ConceptosActualizados);
        Assert.AreEqual(60.01m, context.ComponentesMatriz
            .Single(c => c.MatrizId == scenario.MatrizApu.Id && c.Orden == 1).Importe);
        Assert.AreEqual(100.01m, scenario.MatrizApu.CostoDirecto);
        Assert.AreEqual(100.01m, scenario.Concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.10m, scenario.Concepto.CostoDirectoTotal);
        Assert.AreEqual(100.01m, scenario.Concepto.PrecioUnitario);
        Assert.AreEqual(1000.10m, scenario.Concepto.ImporteTotal);

        // Precisión 3: 60.005 + 30 + 3 + 3 + 4 = 100.005 → resultados distintos.
        scenario.Proyecto.DecimalesImporte = 3;
        context.SaveChanges();

        var resultado3 = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);
        Assert.AreEqual(1, resultado3.ConceptosActualizados);
        Assert.AreEqual(60.005m, context.ComponentesMatriz
            .Single(c => c.MatrizId == scenario.MatrizApu.Id && c.Orden == 1).Importe);
        Assert.AreEqual(100.005m, scenario.MatrizApu.CostoDirecto);
        Assert.AreEqual(100.005m, scenario.Concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.05m, scenario.Concepto.CostoDirectoTotal);
        Assert.AreEqual(100.005m, scenario.Concepto.PrecioUnitario);
        Assert.AreEqual(1000.05m, scenario.Concepto.ImporteTotal);

        // Discriminante explícito: 2 y 3 decimales producen valores distintos.
        Assert.AreNotEqual(100.01m, scenario.MatrizApu.CostoDirecto);
        Assert.AreNotEqual(1000.10m, scenario.Concepto.CostoDirectoTotal);
    }

    [TestMethod]
    public void Ejecutar_CambiarDecimalesPorcentaje_NoAlteraLaCascadaEconomica()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        // DecimalesPorcentaje=4 (valor por defecto del escenario)
        var r4 = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);
        var valores4 = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .Select(d => new { d.ImporteProgramado, d.CantidadProgramada, d.PorcentajeProgramado })
            .ToList();

        // DecimalesPorcentaje=0 → la cascada ECONÓMICA no debe cambiar.
        scenario.Proyecto.DecimalesPorcentaje = 0;
        context.SaveChanges();

        var r0 = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);
        var valores0 = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .Select(d => new { d.ImporteProgramado, d.CantidadProgramada, d.PorcentajeProgramado })
            .ToList();

        // Congelar: DecimalesPorcentaje NO participa en el cálculo de importes/cantidades.
        Assert.AreEqual(r4.ConceptosActualizados, r0.ConceptosActualizados);
        Assert.AreEqual(100.00m, scenario.MatrizApu.CostoDirecto, "El C.D. de la matriz no depende de DecimalesPorcentaje.");
        Assert.AreEqual(1000.00m, scenario.Concepto.CostoDirectoTotal);
        Assert.AreEqual(valores4.Count, valores0.Count);
        for (var i = 0; i < valores4.Count; i++)
        {
            Assert.AreEqual(valores4[i].ImporteProgramado, valores0[i].ImporteProgramado,
                "El importe por periodo no depende de DecimalesPorcentaje.");
            Assert.AreEqual(valores4[i].CantidadProgramada, valores0[i].CantidadProgramada,
                "La cantidad por periodo no depende de DecimalesPorcentaje.");
        }

        // Divergencia documentada (decision de compatibilidad 18):
        // DecimalesPorcentaje NO participa en la cascada económica.
        // El campo PorcentajeProgramado es el MISMO valor decimal en ambos motores
        // (50.0000m == 50m); la diferencia solo es visible en el formato.
        Assert.AreEqual(valores4[0].PorcentajeProgramado, valores0[0].PorcentajeProgramado);
        Assert.AreEqual(50m, valores0[0].PorcentajeProgramado);

        using (var cultura = new CultureScope(CultureInfo.InvariantCulture))
        {
            var motorCon4 = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 4);
            var motorCon0 = new MotorCalculoSopro(decimalesCantidad: 2, decimalesImporte: 2, decimalesPorcentaje: 0);

            Assert.AreEqual("50.0000", motorCon4.FormatPorcentaje(valores4[0].PorcentajeProgramado),
                "Con 4 decimales la precisión visible es 50.0000.");
            Assert.AreEqual("50", motorCon0.FormatPorcentaje(valores0[0].PorcentajeProgramado),
                "Con 0 decimales la precisión visible es 50.");
        }
    }

    [TestMethod]
    public void Ejecutar_ConValoresCorrompidos_RestauraTodosLosValoresRecomputables()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        SoproCalculationScenarioBuilder.AddTwoPeriodProgram(context, scenario);

        // Corromper los valores que el recálculo debe recomputar en cada fase.
        var componentes = context.ComponentesMatriz
            .Where(c => c.MatrizId == scenario.MatrizApu.Id)
            .OrderBy(c => c.Orden)
            .ToList();
        foreach (var comp in componentes)
            comp.Importe = 999999m;
        scenario.MatrizApu.CostoDirecto = 999999m;

        scenario.Concepto.CostoDirectoUnitario = 999999m;
        scenario.Concepto.CostoDirectoTotal = 999999m;
        scenario.Concepto.PrecioUnitario = 999999m;
        scenario.Concepto.ImporteTotal = 999999m;

        var actividad = context.ActividadesProgramadas
            .Single(a => a.ProgramaObra.ProyectoId == scenario.Proyecto.Id);
        actividad.ImporteTotal = 999999m;
        actividad.ImporteProgramado = 999999m;
        actividad.CantidadProgramada = 999999m;

        var distribuciones = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .ToList();
        foreach (var d in distribuciones)
        {
            d.ImporteProgramado = 999999m;
            d.CantidadProgramada = 999999m;
            d.PorcentajeProgramado = 999999m;
        }
        context.SaveChanges();

        var resultado = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);

        Assert.AreEqual(5, resultado.ComponentesActualizados);
        Assert.AreEqual(1, resultado.MatricesActualizadas);
        Assert.AreEqual(1, resultado.ConceptosActualizados);
        Assert.AreEqual(0, resultado.AgrupadoresTotalesRecalculados);
        Assert.AreEqual(2, resultado.DistribucionesActualizadas);
        Assert.IsTrue(resultado.ProgramaActualizado);

        // Fase 1-2: componentes y matriz restaurados (material 60 + oficial 30 + cabo %MO 3 + herramienta %MO 3 + revolvedora 4 = 100).
        var importesRestaurados = context.ComponentesMatriz
            .Where(c => c.MatrizId == scenario.MatrizApu.Id)
            .OrderBy(c => c.Orden)
            .Select(c => c.Importe)
            .ToList();
        CollectionAssert.AreEqual(new[] { 60.00m, 30.00m, 3.00m, 3.00m, 4.00m }, importesRestaurados);
        Assert.AreEqual(100.00m, scenario.MatrizApu.CostoDirecto);

        // Fase 3: concepto hoja restaurado (CDU 100 × 10 = 1,000; porcentajes 0 → P.U. = CD).
        Assert.AreEqual(100.00m, scenario.Concepto.CostoDirectoUnitario);
        Assert.AreEqual(1000.00m, scenario.Concepto.CostoDirectoTotal);
        Assert.AreEqual(100.00m, scenario.Concepto.PrecioUnitario);
        Assert.AreEqual(1000.00m, scenario.Concepto.ImporteTotal);

        // Fase 5-6: actividad y distribuciones regeneradas.
        // Divergencia fila 17: la Fase 6 elimina las distribuciones y regenera UNIFORME 50/50.
        Assert.AreEqual(1000.00m, actividad.ImporteTotal);
        Assert.AreEqual(1000.00m, actividad.ImporteProgramado);
        Assert.AreEqual(10.00m, actividad.CantidadProgramada);

        var finales = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .ToList();
        Assert.AreEqual(2, finales.Count);
        Assert.AreEqual(500.00m, finales[0].ImporteProgramado);
        Assert.AreEqual(500.00m, finales[1].ImporteProgramado);
        Assert.AreEqual(5.00m, finales[0].CantidadProgramada);
        Assert.AreEqual(5.00m, finales[1].CantidadProgramada);
        Assert.AreEqual(50m, finales[0].PorcentajeProgramado);
        Assert.AreEqual(50m, finales[1].PorcentajeProgramado);
    }

    [TestMethod]
    public void Ejecutar_ConDistribucionNoExacta_ElValorPersistidoDePorcentajeCambiaConLaPrecision()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);
        AddThreePeriodProgram(context, scenario);

        // 3 periodos con días hábiles iguales → 1/3 = 33.3333…% por periodo (distribución NO exacta).
        // La cantidad se redondea primero (DecimalesCantidad=2): 10 × 5/15 = 3.33, y de ahí se
        // deriva el porcentaje: 3.33 / 10 × 100 = 33.3. RedondearPorcentaje(33.3) con 4 decimales
        // → 33.3000; con 0 → 33.
        var r4 = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);
        var con4 = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .Select(d => d.PorcentajeProgramado)
            .ToList();
        var importesCon4 = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .Select(d => d.ImporteProgramado)
            .ToList();

        scenario.Proyecto.DecimalesPorcentaje = 0;
        context.SaveChanges();

        var r0 = new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);
        var con0 = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .OrderBy(d => d.PeriodoProgramaId)
            .Select(d => d.PorcentajeProgramado)
            .ToList();
        var importesCon0 = context.DistribucionesPeriodo
            .Where(d => d.ActividadProgramada.ProgramaObra.ProyectoId == scenario.Proyecto.Id)
            .Select(d => d.ImporteProgramado)
            .ToList();

        Assert.IsTrue(r4.ProgramaActualizado && r0.ProgramaActualizado);
        Assert.AreEqual(3, con4.Count);
        Assert.AreEqual(3, con0.Count);

        // El último periodo absorbe el residuo: 100 − 33.3 − 33.3 = 33.4.
        CollectionAssert.AreEqual(new[] { 33.3000m, 33.3000m, 33.4000m }, con4);
        CollectionAssert.AreEqual(new[] { 33m, 33m, 34m }, con0);

        // Valores numéricamente DISTINTOS: la precisión sí cambia el valor persistido.
        Assert.AreNotEqual(con4[0], con0[0]);
        Assert.AreEqual(100m, con4.Sum());
        Assert.AreEqual(100m, con0.Sum());

        // El importe económico se conserva en AMBAS precisiones (divergencia fila 18):
        // capturado tras la ejecución con 4 decimales y tras la ejecución con 0.
        Assert.AreEqual(1000.00m, importesCon4.Sum());
        Assert.AreEqual(1000.00m, importesCon0.Sum());
        Assert.AreEqual(1000.00m, context.ActividadesProgramadas
            .Single(a => a.ProgramaObra.ProyectoId == scenario.Proyecto.Id).ImporteTotal);
    }

    [TestMethod]
    public void Ejecutar_ConBasicosAnidados_RecalculaEnOrdenTopologico()
    {
        using var context = TestDbFactory.CreateContext();
        var scenario = SoproCalculationScenarioBuilder.CreateBaseBudgetScenario(context);

        // Cadena: cuadrilla (CD 30 = 1 × salario Oficial) ← básico externo (×2 = 60) ← básico anidado (×3 = 180).
        // El básico anidado se crea PRIMERO (Id menor) para que el orden natural de la
        // consulta lo coloque antes que su dependencia: el orden por tipo legacy
        // (Cuadrilla→Básico→APU) lo habría recalculado con el costo obsoleto.
        var cuadrilla = new Matriz
        {
            Clave = "Q-TOP", Descripcion = "Cuadrilla topo", Unidad = "m",
            Tipo = TipoMatriz.Cuadrilla, ProyectoId = scenario.Proyecto.Id
        };
        var basicoAnidado = new Matriz
        {
            Clave = "B2-TOP", Descripcion = "Básico anidado topo", Unidad = "m",
            Tipo = TipoMatriz.Basico, ProyectoId = scenario.Proyecto.Id
        };
        context.Matrices.AddRange(cuadrilla, basicoAnidado);
        context.SaveChanges();

        var basicoExterno = new Matriz
        {
            Clave = "B1-TOP", Descripcion = "Básico externo topo", Unidad = "m",
            Tipo = TipoMatriz.Basico, ProyectoId = scenario.Proyecto.Id
        };
        context.Matrices.Add(basicoExterno);
        context.SaveChanges();

        context.ComponentesMatriz.AddRange(
            new ComponenteMatriz
            {
                MatrizId = cuadrilla.Id, TipoComponente = TipoComponenteMatriz.ManoDeObra,
                ManoDeObraId = scenario.Oficial.Id, Cantidad = 1m, Orden = 1
            },
            new ComponenteMatriz
            {
                MatrizId = basicoExterno.Id, TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = cuadrilla.Id, Cantidad = 2m, Orden = 1
            },
            new ComponenteMatriz
            {
                MatrizId = basicoAnidado.Id, TipoComponente = TipoComponenteMatriz.Auxiliar,
                AuxiliarId = basicoExterno.Id, Cantidad = 3m, Orden = 1
            });

        // Costos obsoletos sembrados: solo el orden topológico garantiza el resultado correcto.
        cuadrilla.CostoDirecto = 999m;
        basicoExterno.CostoDirecto = 999m;
        basicoAnidado.CostoDirecto = 999m;
        context.SaveChanges();

        new RecalculoGlobalService().Ejecutar(context, scenario.Proyecto.Id);

        Assert.AreEqual(30.00m, cuadrilla.CostoDirecto, "La cuadrilla se recalcula desde su MO (1 × 30).");
        Assert.AreEqual(60.00m, basicoExterno.CostoDirecto, "El básico externo consume la cuadrilla ya recalculada (2 × 30).");
        Assert.AreEqual(180.00m, basicoAnidado.CostoDirecto, "El básico anidado consume el básico ya recalculado (3 × 60).");
    }

    private static void AddThreePeriodProgram(SOPROContext context, SoproCalculationScenario scenario)
    {
        var programa = new ProgramaObra
        {
            ProyectoId = scenario.Proyecto.Id,
            Nombre = "Programa 3 periodos",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true,
            GeneradoDesdePresupuesto = true
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var periodos = new[]
        {
            new PeriodoPrograma { ProgramaObraId = programa.Id, NumeroPeriodo = 1, Etiqueta = "P1", FechaInicio = new DateTime(2026, 1, 1), FechaFin = new DateTime(2026, 1, 7) },
            new PeriodoPrograma { ProgramaObraId = programa.Id, NumeroPeriodo = 2, Etiqueta = "P2", FechaInicio = new DateTime(2026, 1, 8), FechaFin = new DateTime(2026, 1, 14) },
            new PeriodoPrograma { ProgramaObraId = programa.Id, NumeroPeriodo = 3, Etiqueta = "P3", FechaInicio = new DateTime(2026, 1, 15), FechaFin = new DateTime(2026, 1, 21) }
        };
        context.PeriodosPrograma.AddRange(periodos);
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
            FechaFinProgramada = new DateTime(2026, 1, 21),
            DuracionDiasHabiles = 15,
            Orden = 1,
            Nivel = 1,
            Notas = string.Empty
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();
    }

    private sealed class CultureScope : IDisposable
    {
        private readonly CultureInfo _original;

        public CultureScope(CultureInfo cultura)
        {
            _original = CultureInfo.CurrentCulture;
            CultureInfo.CurrentCulture = cultura;
        }

        public void Dispose()
        {
            CultureInfo.CurrentCulture = _original;
        }
    }
}
