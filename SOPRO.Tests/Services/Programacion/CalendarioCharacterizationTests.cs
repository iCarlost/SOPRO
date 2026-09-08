using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using SOPRO.Application.Services.Programacion;
using SOPRO.Core.Entities;
using SOPRO.Tests.TestInfrastructure;

namespace SOPRO.Tests.Services.Programacion;

[TestClass]
public sealed class CalendarioCharacterizationTests
{
    [TestMethod]
    public void CalendarioSinConfiguracion_UsaLunesAViernesYOperacionesInclusivas()
    {
        var cache = new CalendarioCache(null,
            new DateTime(2026, 1, 5), // lunes
            new DateTime(2026, 1, 11));

        Assert.IsTrue(cache.IsWorkingDay(new DateTime(2026, 1, 5)));
        Assert.IsFalse(cache.IsWorkingDay(new DateTime(2026, 1, 10)));
        Assert.AreEqual(5, cache.CountWorkingDays(
            new DateTime(2026, 1, 5), new DateTime(2026, 1, 11)));

        Assert.AreEqual(new DateTime(2026, 1, 5),
            cache.CalculateFinishDate(new DateTime(2026, 1, 3), 1));
        Assert.AreEqual(new DateTime(2026, 1, 9),
            cache.CalculateFinishDate(new DateTime(2026, 1, 5), 5));
        Assert.AreEqual(new DateTime(2026, 1, 5),
            cache.CalculateStartDate(new DateTime(2026, 1, 9), 5));
    }

    [TestMethod]
    public void Desplazamientos_ConservanSemanticaInclusivaYExclusiva()
    {
        var cache = new CalendarioCache(null,
            new DateTime(2026, 1, 5), new DateTime(2026, 1, 16));
        var lunes = new DateTime(2026, 1, 5);

        Assert.AreEqual(new DateTime(2026, 1, 5), cache.AddWorkingDaysInclusive(lunes, 0));
        Assert.AreEqual(new DateTime(2026, 1, 6), cache.AddWorkingDaysInclusive(lunes, 1));
        Assert.AreEqual(new DateTime(2026, 1, 6), cache.AddWorkingDaysExclusive(lunes, 0));
        Assert.AreEqual(new DateTime(2026, 1, 7), cache.AddWorkingDaysExclusive(lunes, 2));

        Assert.AreEqual(new DateTime(2026, 1, 5), cache.SubtractWorkingDaysInclusive(lunes, 0));
        Assert.AreEqual(new DateTime(2026, 1, 2), cache.SubtractWorkingDaysInclusive(lunes, 1));
        Assert.AreEqual(new DateTime(2026, 1, 2), cache.SubtractWorkingDaysExclusive(lunes, 0));
        Assert.AreEqual(new DateTime(2026, 1, 1), cache.SubtractWorkingDaysExclusive(lunes, 2));
    }

    [TestMethod]
    public void Excepciones_AnulanReglaSemanalInclusoEnFinDeSemana()
    {
        var calendario = new CalendarioLaboral
        {
            Lunes = true,
            Martes = true,
            Miercoles = true,
            Jueves = true,
            Viernes = true,
            Sabado = false,
            Domingo = false
        };
        calendario.Excepciones.Add(new ExcepcionCalendario
        {
            Fecha = new DateTime(2026, 1, 10),
            Tipo = TipoExcepcionCalendario.LaborableEspecial
        });
        calendario.Excepciones.Add(new ExcepcionCalendario
        {
            Fecha = new DateTime(2026, 1, 9),
            Tipo = TipoExcepcionCalendario.Inhabil
        });

        var cache = new CalendarioCache(calendario,
            new DateTime(2026, 1, 9), new DateTime(2026, 1, 10));

        Assert.IsFalse(cache.IsWorkingDay(new DateTime(2026, 1, 9)));
        Assert.IsTrue(cache.IsWorkingDay(new DateTime(2026, 1, 10)));
        Assert.AreEqual(1, cache.CountWorkingDays(
            new DateTime(2026, 1, 9), new DateTime(2026, 1, 10)));
    }

    [TestMethod]
    public void ReglasDeDiasHabiles_ParidadConOraculoDiaADia()
    {
        var calendario = new CalendarioLaboral { Sabado = true };
        calendario.Excepciones.Add(new ExcepcionCalendario
        {
            Fecha = new DateTime(2026, 2, 3),
            Tipo = TipoExcepcionCalendario.Inhabil
        });
        calendario.Excepciones.Add(new ExcepcionCalendario
        {
            Fecha = new DateTime(2026, 2, 8),
            Tipo = TipoExcepcionCalendario.LaborableEspecial
        });

        var inicio = new DateTime(2026, 2, 1);
        var fin = new DateTime(2026, 2, 14);
        var cache = new CalendarioCache(calendario, inicio, fin);
        var esperado = Enumerable.Range(0, (fin - inicio).Days + 1)
            .Count(offset => EsHabilOraculo(calendario, inicio.AddDays(offset)));

        Assert.AreEqual(esperado, cache.CountWorkingDays(inicio, fin));
        for (var fecha = inicio; fecha <= fin; fecha = fecha.AddDays(1))
            Assert.AreEqual(EsHabilOraculo(calendario, fecha), cache.IsWorkingDay(fecha), fecha.ToString("yyyy-MM-dd"));
    }

    [TestMethod]
    public void EntradasVaciasOInvertidas_DevuelvenResultadosEstables()
    {
        var cache = new CalendarioCache(null,
            new DateTime(2026, 1, 1), new DateTime(2026, 1, 10));

        Assert.AreEqual(0, cache.CountWorkingDays(null, DateTime.Today));
        Assert.AreEqual(0, cache.CountWorkingDays(
            new DateTime(2026, 1, 10), new DateTime(2026, 1, 1)));
        Assert.IsNull(cache.CalculateFinishDate(null, 5));
        Assert.IsNull(cache.CalculateStartDate(null, 5));
        Assert.AreEqual(DateTime.Today.Date,
            CalendarioCache.SanitizarFecha(DateTime.MinValue));
        Assert.AreEqual(DateTime.Today.Date,
            CalendarioCache.SanitizarFecha(DateTime.MaxValue));
    }

    [DataTestMethod]
    [DataRow(TipoDependenciaActividad.FS, "2026-01-08", "2026-01-09")]
    [DataRow(TipoDependenciaActividad.SS, "2026-01-07", "2026-01-08")]
    [DataRow(TipoDependenciaActividad.FF, "2026-01-07", "2026-01-08")]
    [DataRow(TipoDependenciaActividad.SF, "2026-01-06", "2026-01-07")]
    public void RedDeActividades_ConservaSemanticaDeDependencia(
        TipoDependenciaActividad tipo, string inicioEsperado, string finEsperado)
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = new Proyecto
        {
            Nombre = "Calendario caracterizacion",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 12, 31),
            PlazoEjecucion = 365,
            DecimalesCantidad = 2,
            DecimalesImporte = 2,
            DecimalesPorcentaje = 4
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            Nombre = "Red de actividades",
            FechaInicioPrograma = new DateTime(2026, 1, 5)
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var origen = CrearActividad(programa.Id, "Origen", new DateTime(2026, 1, 5), 2, 1);
        var destino = CrearActividad(programa.Id, "Destino", new DateTime(2026, 1, 5), 2, 2);
        context.ActividadesProgramadas.AddRange(origen, destino);
        context.SaveChanges();
        context.DependenciasActividad.Add(new DependenciaActividad
        {
            ActividadOrigenId = origen.Id,
            ActividadDestinoId = destino.Id,
            TipoDependencia = tipo,
            DesfaseDias = 2
        });
        context.SaveChanges();

        new SOPRO.Application.Services.ProgramacionCalculationService()
            .RecalculateProgram(context, programa.Id);

        var actual = context.ActividadesProgramadas.Find(destino.Id)!;
        Assert.AreEqual(Fecha(inicioEsperado), actual.FechaInicioProgramada!.Value.Date);
        Assert.AreEqual(Fecha(finEsperado), actual.FechaFinProgramada!.Value.Date);
    }

    [TestMethod]
    public void RedRamificada_ConservaRutaCriticaYHolguras()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = CrearProyecto("Red ramificada");
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            FechaInicioPrograma = new DateTime(2026, 1, 5)
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var independiente = CrearActividad(programa.Id, "Independiente", new DateTime(2026, 1, 5), 2, 1);
        var tramoCritico = CrearActividad(programa.Id, "Tramo critico", new DateTime(2026, 1, 5), 4, 2);
        var sucesora = CrearActividad(programa.Id, "Sucesora", new DateTime(2026, 1, 5), 2, 3);
        context.ActividadesProgramadas.AddRange(independiente, tramoCritico, sucesora);
        context.SaveChanges();
        context.DependenciasActividad.Add(new DependenciaActividad
        {
            ActividadOrigenId = tramoCritico.Id,
            ActividadDestinoId = sucesora.Id,
            TipoDependencia = TipoDependenciaActividad.FS
        });
        context.SaveChanges();

        new SOPRO.Application.Services.ProgramacionCalculationService()
            .RecalculateProgram(context, programa.Id);

        var actividades = context.ActividadesProgramadas.ToDictionary(a => a.Descripcion);
        AssertActividad(actividades["Independiente"], "2026-01-05", "2026-01-06", "2026-01-09", "2026-01-12", 4, false);
        AssertActividad(actividades["Tramo critico"], "2026-01-05", "2026-01-08", "2026-01-05", "2026-01-08", 0, true);
        AssertActividad(actividades["Sucesora"], "2026-01-09", "2026-01-12", "2026-01-09", "2026-01-12", 0, true);
    }

    [TestMethod]
    [Timeout(5000)]
    public void RedConCiclo_TerminaSinBucleInfinito()
    {
        using var context = TestDbFactory.CreateContext();
        var proyecto = new Proyecto
        {
            Nombre = "Ciclo de calendario",
            Descripcion = string.Empty,
            Ubicacion = string.Empty,
            Convocante = string.Empty,
            Contratista = string.Empty,
            ApoderadoLegal = string.Empty,
            FechaInicio = new DateTime(2026, 1, 1),
            FechaTermino = new DateTime(2026, 12, 31),
            PlazoEjecucion = 365
        };
        context.Proyectos.Add(proyecto);
        context.SaveChanges();

        var programa = new ProgramaObra
        {
            ProyectoId = proyecto.Id,
            FechaInicioPrograma = new DateTime(2026, 1, 5)
        };
        context.ProgramasObra.Add(programa);
        context.SaveChanges();

        var primera = CrearActividad(programa.Id, "Primera", new DateTime(2026, 1, 5), 2, 1);
        var segunda = CrearActividad(programa.Id, "Segunda", new DateTime(2026, 1, 5), 2, 2);
        context.ActividadesProgramadas.AddRange(primera, segunda);
        context.SaveChanges();
        context.DependenciasActividad.AddRange(
            new DependenciaActividad { ActividadOrigenId = primera.Id, ActividadDestinoId = segunda.Id },
            new DependenciaActividad { ActividadOrigenId = segunda.Id, ActividadDestinoId = primera.Id });
        context.SaveChanges();

        new SOPRO.Application.Services.ProgramacionCalculationService()
            .RecalculateProgram(context, programa.Id);

        var primeraFinal = context.ActividadesProgramadas.Find(primera.Id)!;
        var segundaFinal = context.ActividadesProgramadas.Find(segunda.Id)!;
        // Contrato legacy caracterizado: el límite de iteraciones evita el bucle,
        // pero no diagnostica el ciclo; N7-16b deberá reemplazarlo por rechazo formal.
        Assert.AreEqual(Fecha("2026-03-10"), primeraFinal.FechaInicioProgramada!.Value.Date);
        Assert.AreEqual(Fecha("2026-03-11"), primeraFinal.FechaFinProgramada!.Value.Date);
        Assert.AreEqual(Fecha("2026-03-12"), segundaFinal.FechaInicioProgramada!.Value.Date);
        Assert.AreEqual(Fecha("2026-03-13"), segundaFinal.FechaFinProgramada!.Value.Date);
        Assert.IsNull(primeraFinal.FechaInicioTardia);
        Assert.IsNull(primeraFinal.FechaFinTardia);
        Assert.IsNull(segundaFinal.FechaInicioTardia);
        Assert.IsNull(segundaFinal.FechaFinTardia);
        Assert.AreEqual(0, primeraFinal.HolguraDias);
        Assert.AreEqual(0, segundaFinal.HolguraDias);
        Assert.IsFalse(primeraFinal.RutaCritica);
        Assert.IsFalse(segundaFinal.RutaCritica);
    }

    private static Proyecto CrearProyecto(string nombre) => new()
    {
        Nombre = nombre,
        Descripcion = string.Empty,
        Ubicacion = string.Empty,
        Convocante = string.Empty,
        Contratista = string.Empty,
        ApoderadoLegal = string.Empty,
        FechaInicio = new DateTime(2026, 1, 1),
        FechaTermino = new DateTime(2026, 12, 31),
        PlazoEjecucion = 365,
        DecimalesCantidad = 2,
        DecimalesImporte = 2,
        DecimalesPorcentaje = 4
    };

    private static ActividadProgramada CrearActividad(
        int programaId, string descripcion, DateTime inicio, int duracion, int orden) => new()
    {
        ProgramaObraId = programaId,
        Descripcion = descripcion,
        FechaInicioProgramada = inicio,
        DuracionDiasHabiles = duracion,
        Orden = orden,
        MetodoDistribucion = MetodoDistribucionActividad.Uniforme
    };

    private static void AssertActividad(
        ActividadProgramada actividad,
        string inicio, string fin, string inicioTardio, string finTardio,
        int holgura, bool rutaCritica)
    {
        Assert.AreEqual(Fecha(inicio), actividad.FechaInicioProgramada!.Value.Date);
        Assert.AreEqual(Fecha(fin), actividad.FechaFinProgramada!.Value.Date);
        Assert.AreEqual(Fecha(inicioTardio), actividad.FechaInicioTardia!.Value.Date);
        Assert.AreEqual(Fecha(finTardio), actividad.FechaFinTardia!.Value.Date);
        Assert.AreEqual(holgura, actividad.HolguraDias);
        Assert.AreEqual(rutaCritica, actividad.RutaCritica);
    }

    private static DateTime Fecha(string value) =>
        DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture).Date;

    private static bool EsHabilOraculo(CalendarioLaboral calendario, DateTime fecha)
    {
        var excepcion = calendario.Excepciones.FirstOrDefault(x => x.Fecha.Date == fecha.Date);
        if (excepcion != null)
            return excepcion.Tipo == TipoExcepcionCalendario.LaborableEspecial;

        return fecha.DayOfWeek switch
        {
            DayOfWeek.Monday => calendario.Lunes,
            DayOfWeek.Tuesday => calendario.Martes,
            DayOfWeek.Wednesday => calendario.Miercoles,
            DayOfWeek.Thursday => calendario.Jueves,
            DayOfWeek.Friday => calendario.Viernes,
            DayOfWeek.Saturday => calendario.Sabado,
            DayOfWeek.Sunday => calendario.Domingo,
            _ => false
        };
    }
}
