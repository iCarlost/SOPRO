using SOPRO.Application.DTOs.Programacion;
using SOPRO.Core.Entities;
using System.Globalization;

namespace SOPRO.Application.Services;

public static class ProgramacionPeriodHelper
{
    private static readonly CultureInfo LabelCulture = CultureInfo.GetCultureInfo("es-MX");
    public static DateTime AlignStart(DateTime fecha, TipoPeriodoPrograma tipoPeriodo)
    {
        fecha = fecha.Date;
        return tipoPeriodo switch
        {
            TipoPeriodoPrograma.Dia => fecha,
            TipoPeriodoPrograma.Semana => StartOfIsoWeek(fecha),
            TipoPeriodoPrograma.Quincena => fecha.Day <= 15 ? new DateTime(fecha.Year, fecha.Month, 1) : new DateTime(fecha.Year, fecha.Month, 16),
            TipoPeriodoPrograma.Mes => new DateTime(fecha.Year, fecha.Month, 1),
            _ => fecha
        };
    }

    public static DateTime AlignEnd(DateTime fecha, TipoPeriodoPrograma tipoPeriodo)
    {
        fecha = fecha.Date;
        return tipoPeriodo switch
        {
            TipoPeriodoPrograma.Dia => fecha,
            TipoPeriodoPrograma.Semana => StartOfIsoWeek(fecha).AddDays(6),
            TipoPeriodoPrograma.Quincena => fecha.Day <= 15
                ? new DateTime(fecha.Year, fecha.Month, 15)
                : new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)),
            TipoPeriodoPrograma.Mes => new DateTime(fecha.Year, fecha.Month, DateTime.DaysInMonth(fecha.Year, fecha.Month)),
            _ => fecha
        };
    }

    public static DateTime GetPeriodEnd(DateTime start, TipoPeriodoPrograma tipoPeriodo)
        => tipoPeriodo switch
        {
            TipoPeriodoPrograma.Dia => start.Date,
            TipoPeriodoPrograma.Semana => start.Date.AddDays(6),
            TipoPeriodoPrograma.Quincena => start.Day <= 15
                ? new DateTime(start.Year, start.Month, 15)
                : new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
            TipoPeriodoPrograma.Mes => new DateTime(start.Year, start.Month, DateTime.DaysInMonth(start.Year, start.Month)),
            _ => start.Date.AddDays(6)
        };

    public static DateTime GetNextPeriodStart(DateTime currentStart, TipoPeriodoPrograma tipoPeriodo)
        => tipoPeriodo switch
        {
            TipoPeriodoPrograma.Dia => currentStart.Date.AddDays(1),
            TipoPeriodoPrograma.Semana => currentStart.Date.AddDays(7),
            TipoPeriodoPrograma.Quincena => currentStart.Day <= 15
                ? new DateTime(currentStart.Year, currentStart.Month, 16)
                : new DateTime(currentStart.Year, currentStart.Month, 1).AddMonths(1),
            TipoPeriodoPrograma.Mes => new DateTime(currentStart.Year, currentStart.Month, 1).AddMonths(1),
            _ => currentStart.Date.AddDays(7)
        };

    public static string BuildLabel(int numero, DateTime inicio, DateTime fin, TipoPeriodoPrograma tipoPeriodo)
        => tipoPeriodo switch
        {
            TipoPeriodoPrograma.Dia => $"Día {numero:00} - {inicio:dd/MM/yyyy}",
            TipoPeriodoPrograma.Semana => $"Semana {numero:00} - {inicio:dd/MM} a {fin:dd/MM}",
            TipoPeriodoPrograma.Quincena => $"Quincena {numero:00} - {inicio:dd/MM} a {fin:dd/MM}",
            TipoPeriodoPrograma.Mes => inicio.ToString("MMMM yyyy", LabelCulture),
            _ => $"Periodo {numero:00}"
        };

    public static DateTime StartOfIsoWeek(DateTime date)
    {
        var diff = ((int)date.DayOfWeek + 6) % 7;
        return date.AddDays(-diff).Date;
    }
}
