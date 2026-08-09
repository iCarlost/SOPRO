using System;
using System.Collections.Generic;
using SOPRO.Core.Entities;

namespace SOPRO.Application.Services.Programacion
{
    /// <summary>
    /// Caché de días hábiles para un rango de fechas dado.
    /// Precalcula en O(rango) y responde en O(1) a:
    ///   - IsWorkingDay(date)
    ///   - CountWorkingDays(start, end)
    ///   - AddWorkingDays(date, n)
    ///   - SubtractWorkingDays(date, n)
    ///
    /// Reemplaza los loops día a día en ProgramacionCalculationService y
    /// ProgramacionDistributionService, que son el principal cuello de botella.
    /// </summary>
    internal sealed class CalendarioCache
    {
        private readonly DateTime _inicio;
        private readonly DateTime _fin;
        // _habil[i] = true si _inicio + i días es hábil
        private readonly bool[] _habil;
        // _acum[i] = cantidad de días hábiles desde _inicio hasta _inicio + i días (inclusive)
        private readonly int[] _acum;
        // Días hábiles ordenados para AddWorkingDays/SubtractWorkingDays
        private readonly DateTime[] _dias;

        private static readonly CalendarioCache _fallback =
            new CalendarioCache(null, new DateTime(2000, 1, 1), new DateTime(2099, 12, 31));

        public static CalendarioCache Fallback => _fallback;

        /// <summary>Días de expansión del rango para cubrir desfases (AddWorkingDays/SubtractWorkingDays).</summary>
        internal const int MargenDias = 180;

        /// <summary>
        /// Normaliza una fecha al día actual si su expansión con MargenDias no es representable
        /// (p. ej. DateTime.MinValue/MaxValue materializado desde datos históricos corruptos).
        /// </summary>
        internal static DateTime SanitizarFecha(DateTime date)
        {
            var d = date.Date;
            if (d < DateTime.MinValue.Date.AddDays(MargenDias) || d > DateTime.MaxValue.Date.AddDays(-MargenDias))
                return DateTime.Today.Date;
            return d;
        }

        public CalendarioCache(CalendarioLaboral? calendario, DateTime rangoInicio, DateTime rangoFin)
        {
            // Ampliar el rango para cubrir AddWorkingDays con desfases grandes.
            // Fechas default/corruptas (MinValue/MaxValue) se normalizan para no desbordar.
            var inicioBase = SanitizarFecha(rangoInicio);
            var finBase    = SanitizarFecha(rangoFin);
            if (finBase < inicioBase)
                finBase = inicioBase.AddDays(MargenDias);

            _inicio = inicioBase.AddDays(-MargenDias);
            _fin    = finBase.AddDays(MargenDias);

            int n = (int)(_fin - _inicio).TotalDays + 1;
            _habil = new bool[n];
            _acum  = new int[n];

            var diasList = new List<DateTime>(n / 2);
            int acum = 0;

            for (int i = 0; i < n; i++)
            {
                var fecha = _inicio.AddDays(i);
                bool esHabil = EsHabilRaw(calendario, fecha);
                _habil[i] = esHabil;
                if (esHabil)
                {
                    acum++;
                    diasList.Add(fecha);
                }
                _acum[i] = acum;
            }

            _dias = diasList.ToArray();
        }

        public bool IsWorkingDay(DateTime date)
        {
            int idx = Index(date);
            return idx >= 0 && idx < _habil.Length && _habil[idx];
        }

        public int CountWorkingDays(DateTime? start, DateTime? end)
        {
            if (!start.HasValue || !end.HasValue) return 0;
            var s = start.Value.Date;
            var e = end.Value.Date;
            if (e < s) return 0;

            int is_ = Index(s);
            int ie  = Index(e);
            is_ = Math.Max(0, Math.Min(is_, _acum.Length - 1));
            ie  = Math.Max(0, Math.Min(ie,  _acum.Length - 1));

            // Días hábiles entre s y e inclusive
            int antes = is_ > 0 ? _acum[is_ - 1] : 0;
            return _acum[ie] - antes;
        }

        /// <summary>
        /// Avanza n días hábiles desde date (inclusive si date es hábil y n==1).
        /// Equivale a AddWorkingDaysInclusive del servicio original.
        /// </summary>
        public DateTime AddWorkingDaysInclusive(DateTime date, int days)
        {
            // Semántica del original:
            //   days <= 0 → primer día hábil >= date (sin avanzar)
            //   days > 0  → avanza 'days' días hábiles (el día de origen NO cuenta)
            int startHabilIdx = FindFirstHabilFrom(date);
            if (startHabilIdx < 0) return date;

            if (days <= 0) return _dias[startHabilIdx];

            int targetIdx = startHabilIdx + days;
            if (targetIdx >= _dias.Length) return _dias[_dias.Length - 1];
            return _dias[targetIdx];
        }

        /// <summary>
        /// Avanza n días hábiles desde el día hábil posterior a date.
        /// Equivale a AddWorkingDaysExclusive del servicio original.
        /// </summary>
        public DateTime AddWorkingDaysExclusive(DateTime date, int days)
        {
            // Primer día hábil DESPUÉS de date
            int startHabilIdx = FindFirstHabilAfter(date);
            if (startHabilIdx < 0) return date;

            if (days <= 0) return _dias[startHabilIdx];

            int targetIdx = startHabilIdx + days - 1;
            if (targetIdx >= _dias.Length) return _dias[_dias.Length - 1];
            return _dias[targetIdx];
        }

        /// <summary>
        /// Retrocede n días hábiles desde date (inclusive si date es hábil y n==1).
        /// Equivale a SubtractWorkingDaysInclusive del servicio original.
        /// </summary>
        public DateTime SubtractWorkingDaysInclusive(DateTime date, int days)
        {
            // Semántica del original:
            //   days <= 0 → último día hábil <= date (sin retroceder)
            //   days > 0  → retrocede 'days' días hábiles (el día de origen NO cuenta)
            int startHabilIdx = FindLastHabilUpTo(date);
            if (startHabilIdx < 0) return date;

            if (days <= 0) return _dias[startHabilIdx];

            int targetIdx = startHabilIdx - days;
            if (targetIdx < 0) return _dias[0];
            return _dias[targetIdx];
        }

        /// <summary>
        /// Retrocede n días hábiles desde el día hábil anterior a date.
        /// Equivale a SubtractWorkingDaysExclusive del servicio original.
        /// </summary>
        public DateTime SubtractWorkingDaysExclusive(DateTime date, int days)
        {
            int startHabilIdx = FindLastHabilBefore(date);
            if (startHabilIdx < 0) return date;

            if (days <= 0) return _dias[startHabilIdx];

            int targetIdx = startHabilIdx - days + 1;
            if (targetIdx < 0) return _dias[0];
            return _dias[targetIdx];
        }

        /// <summary>
        /// Calcula la fecha fin a partir de inicio y duración en días hábiles.
        /// Equivale a CalculateFinishDate del servicio original.
        /// </summary>
        public DateTime? CalculateFinishDate(DateTime? start, int duracion)
        {
            // Semántica del original: duracion=1 → primer día hábil desde start (el inicio cuenta)
            // Equivale a AddWorkingDaysInclusive(start, duracion - 1)
            if (!start.HasValue) return null;
            if (duracion <= 0) return start.Value.Date;
            return AddWorkingDaysInclusive(start.Value.Date, duracion - 1);
        }

        /// <summary>
        /// Calcula la fecha inicio a partir de fin y duración en días hábiles.
        /// Equivale a CalculateStartDate del servicio original.
        /// </summary>
        public DateTime? CalculateStartDate(DateTime? finish, int duracion)
        {
            // Semántica del original: duracion=1 → último día hábil <= finish (el fin cuenta)
            // Equivale a SubtractWorkingDaysInclusive(finish, duracion - 1)
            if (!finish.HasValue) return null;
            if (duracion <= 0) return finish.Value.Date;
            return SubtractWorkingDaysInclusive(finish.Value.Date, duracion - 1);
        }

        // ── Helpers privados ──────────────────────────────────────────────────

        private int Index(DateTime date) => (int)(date.Date - _inicio).TotalDays;

        private int FindFirstHabilFrom(DateTime date)
        {
            int idx = Index(date);
            idx = Math.Max(0, Math.Min(idx, _habil.Length - 1));
            while (idx < _habil.Length && !_habil[idx]) idx++;
            if (idx >= _habil.Length) return -1;
            // Retornar índice en _dias
            return _acum[idx] - 1;
        }

        private int FindFirstHabilAfter(DateTime date)
        {
            int idx = Index(date) + 1;
            idx = Math.Max(0, Math.Min(idx, _habil.Length - 1));
            while (idx < _habil.Length && !_habil[idx]) idx++;
            if (idx >= _habil.Length) return -1;
            return _acum[idx] - 1;
        }

        private int FindLastHabilUpTo(DateTime date)
        {
            int idx = Index(date);
            idx = Math.Max(0, Math.Min(idx, _habil.Length - 1));
            while (idx >= 0 && !_habil[idx]) idx--;
            if (idx < 0) return -1;
            return _acum[idx] - 1;
        }

        private int FindLastHabilBefore(DateTime date)
        {
            int idx = Index(date) - 1;
            idx = Math.Max(0, Math.Min(idx, _habil.Length - 1));
            while (idx >= 0 && !_habil[idx]) idx--;
            if (idx < 0) return -1;
            return _acum[idx] - 1;
        }

        private static bool EsHabilRaw(CalendarioLaboral? cal, DateTime date)
        {
            if (cal == null)
                return date.DayOfWeek != DayOfWeek.Saturday
                    && date.DayOfWeek != DayOfWeek.Sunday;

            if (cal.Excepciones != null)
            {
                foreach (var ex in cal.Excepciones)
                {
                    if (ex.Fecha.Date == date.Date)
                        return ex.Tipo == TipoExcepcionCalendario.LaborableEspecial;
                }
            }

            return date.DayOfWeek switch
            {
                DayOfWeek.Monday    => cal.Lunes,
                DayOfWeek.Tuesday   => cal.Martes,
                DayOfWeek.Wednesday => cal.Miercoles,
                DayOfWeek.Thursday  => cal.Jueves,
                DayOfWeek.Friday    => cal.Viernes,
                DayOfWeek.Saturday  => cal.Sabado,
                DayOfWeek.Sunday    => cal.Domingo,
                _ => false
            };
        }
    }
}
