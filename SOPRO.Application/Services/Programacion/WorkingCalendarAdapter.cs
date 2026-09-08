using System.Linq;
using Sopro.Calculation.Calendar;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services.Programacion
{
    /// <summary>Bridges legacy calendar entities to the pure working-calendar contract.</summary>
    internal static class WorkingCalendarAdapter
    {
        /// <summary>
        /// Maps a persisted <see cref="CalendarioLaboral"/> to an immutable
        /// <see cref="WorkingCalendar"/>. A null calendar maps to null, meaning
        /// the default Monday-to-Friday working week.
        /// </summary>
        public static WorkingCalendar? ToWorkingCalendar(CalendarioLaboral? calendario)
        {
            if (calendario == null)
                return null;

            return new WorkingCalendar
            {
                Monday = calendario.Lunes,
                Tuesday = calendario.Martes,
                Wednesday = calendario.Miercoles,
                Thursday = calendario.Jueves,
                Friday = calendario.Viernes,
                Saturday = calendario.Sabado,
                Sunday = calendario.Domingo,
                Exceptions = (calendario.Excepciones ?? Enumerable.Empty<ExcepcionCalendario>())
                    .Select(x => new CalendarException
                    {
                        Date = x.Fecha.Date,
                        Kind = x.Tipo == TipoExcepcionCalendario.LaborableEspecial
                            ? CalendarExceptionKind.Working
                            : CalendarExceptionKind.NonWorking
                    })
                    .ToList()
            };
        }
    }
}