using Microsoft.EntityFrameworkCore;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using SOPRO.Application.Services;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services;

[TestClass]
public class FinanciamientoCalculationServiceTests
{
    [TestMethod]
    public void Calcular_DebeUsarEgresosConCostoDirectoMasIndirectosYAgregarPeriodoDeDesfase()
    {
        using var context = TestDbFactory.CreateContext();

        var proyecto = new Proyecto
        {
            Nombre = "Proyecto financiamiento",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 1, 31),
            PlazoEjecucion = 31,
            PorcentajeIndirectosCentral = 10m,
            PorcentajeIndirectosCampo = 0m,
            ModoCalculoPorcentajes = "SobreCD",
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var matriz = new Matriz
        {
            ProyectoId = proyecto.Id,
            Clave = "APU-001",
            Descripcion = "Concepto base",
            Unidad = "m2",
            Tipo = TipoMatriz.APU,
            CostoDirecto = 100m
        };
        context.Matrices.Add(matriz);
        context.SaveChanges();

        var concepto = new ConceptoPresupuesto
        {
            ProyectoId = proyecto.Id,
            Clave = "C-001",
            Descripcion = "Pavimento",
            Unidad = "m2",
            Cantidad = 20m,
            MatrizId = matriz.Id,
            CostoDirectoUnitario = 100m,
            CostoDirectoTotal = 2000m,
            PrecioUnitario = 100m,
            ImporteTotal = 2000m
        };
        context.ConceptosPresupuesto.Add(concepto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Programa base",
            FechaInicioPrograma = new DateTime(2026, 1, 1),
            Activo = true
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
            ConceptoPresupuestoId = concepto.Id,
            Clave = concepto.Clave,
            Descripcion = concepto.Descripcion,
            Unidad = concepto.Unidad,
            CantidadTotal = 20m,
            PrecioUnitario = 100m,
            ImporteTotal = 2000m,
            DuracionDiasHabiles = 10,
            FechaInicioProgramada = new DateTime(2026, 1, 1),
            FechaFinProgramada = new DateTime(2026, 1, 14)
        };
        context.ActividadesProgramadas.Add(actividad);
        context.SaveChanges();

        context.DistribucionesPeriodo.AddRange(
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodo1.Id,
                CantidadProgramada = 10m,
                PorcentajeProgramado = 50m,
                PrecioUnitario = 100m,
                ImporteProgramado = 1000m
            },
            new DistribucionPeriodo
            {
                ActividadProgramadaId = actividad.Id,
                PeriodoProgramaId = periodo2.Id,
                CantidadProgramada = 10m,
                PorcentajeProgramado = 50m,
                PrecioUnitario = 100m,
                ImporteProgramado = 1000m
            });

        var config = new ConfiguracionFinanciamiento
        {
            ProyectoId = proyecto.Id,
            TasaTIIE = 12m,
            PuntosAdicionales = 0m,
            PorcentajeAnticipo = 0m,
            DesfaseCobro = 1,
            BaseCalculo = "Acumulable"
        };
        context.ConfiguracionesFinanciamiento.Add(config);
        context.SaveChanges();

        var service = new FinanciamientoCalculationService();

        var porcentaje = service.Calcular(context, config, proyecto);

        var filas = context.FilasFlujoCajaFinanciamiento
            .AsNoTracking()
            .Where(f => f.ConfiguracionFinanciamientoId == config.Id)
            .OrderBy(f => f.NumeroPeriodo)
            .ToList();

        Assert.AreEqual(3, filas.Count, "Debe agregar un período adicional por el desfase de cobro.");

        Assert.AreEqual(1100.00m, filas[0].Egresos, "Egresos debe ser CD + CI, no solo CD.");
        Assert.AreEqual(1100.00m, filas[1].Egresos, "Egresos debe ser CD + CI, no solo CD.");
        Assert.AreEqual(0.00m, filas[2].Egresos);

        Assert.AreEqual(-1100.00m, filas[0].SaldoAcumulado);
        Assert.AreEqual(-1200.00m, filas[1].SaldoAcumulado);
        Assert.AreEqual(-200.00m, filas[2].SaldoAcumulado);

        Assert.AreEqual(5.7534m, config.FinanciamientoNeto);
        Assert.AreEqual(0.26152m, porcentaje);
    }
}
