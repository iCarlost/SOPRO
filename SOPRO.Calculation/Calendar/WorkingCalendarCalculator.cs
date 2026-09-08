using System.Collections.ObjectModel;

namespace Sopro.Calculation.Calendar;

/// <summary>Kind of exception overriding the weekly working-day pattern.</summary>
public enum CalendarExceptionKind
{
    /// <summary>The exception makes the date non-working.</summary>
    NonWorking = 0,
    /// <summary>The exception makes the date working.</summary>
    Working = 1
}

/// <summary>One date-specific override in a working calendar.</summary>
public sealed record CalendarException
{
    /// <summary>Date affected by the exception.</summary>
    public DateTime Date { get; init; }
    /// <summary>Override applied to the date.</summary>
    public CalendarExceptionKind Kind { get; init; }
}

/// <summary>Immutable weekly working calendar and date exceptions.</summary>
public sealed record WorkingCalendar
{
    /// <summary>Whether Monday is working.</summary>
    public bool Monday { get; init; } = true;
    /// <summary>Whether Tuesday is working.</summary>
    public bool Tuesday { get; init; } = true;
    /// <summary>Whether Wednesday is working.</summary>
    public bool Wednesday { get; init; } = true;
    /// <summary>Whether Thursday is working.</summary>
    public bool Thursday { get; init; } = true;
    /// <summary>Whether Friday is working.</summary>
    public bool Friday { get; init; } = true;
    /// <summary>Whether Saturday is working.</summary>
    public bool Saturday { get; init; }
    /// <summary>Whether Sunday is working.</summary>
    public bool Sunday { get; init; }

    /// <summary>Immutable date-specific overrides.</summary>
    public IReadOnlyList<CalendarException> Exceptions
    {
        get => _exceptions;
        init
        {
            var copied = (value ?? throw new ArgumentNullException(nameof(value))).ToList();
            _exceptions = new ReadOnlyCollection<CalendarException>(copied);
            _exceptionsByDate = copied
                .GroupBy(x => x.Date.Date)
                .ToDictionary(x => x.Key, x => x.First());
            _workingExceptionDates = _exceptionsByDate.Values
                .Where(x => x.Kind == CalendarExceptionKind.Working)
                .Select(x => x.Date.Date)
                .OrderBy(x => x)
                .ToArray();
        }
    }

    private IReadOnlyList<CalendarException> _exceptions =
        new ReadOnlyCollection<CalendarException>(Array.Empty<CalendarException>());
    private IReadOnlyDictionary<DateTime, CalendarException> _exceptionsByDate =
        new Dictionary<DateTime, CalendarException>();
    private DateTime[] _workingExceptionDates = Array.Empty<DateTime>();

    internal bool HasWeeklyWorkingDay => Monday || Tuesday || Wednesday || Thursday
        || Friday || Saturday || Sunday;

    internal bool TryGetException(DateTime date, out CalendarException? exception) =>
        _exceptionsByDate.TryGetValue(date.Date, out exception);

    internal DateTime[] WorkingExceptionDates => _workingExceptionDates;
}

/// <summary>Deterministic working-day operations independent of persistence and system time.</summary>
public static class WorkingCalendarCalculator
{
    /// <summary>Determines whether a date is working under the supplied calendar.</summary>
    public static bool IsWorkingDay(WorkingCalendar? calendar, DateTime date)
    {
        EnsureUsable(calendar);
        if (calendar == null)
            return date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

        if (calendar.TryGetException(date, out var exception))
            return exception.Kind == CalendarExceptionKind.Working;

        return date.DayOfWeek switch
        {
            DayOfWeek.Monday => calendar.Monday,
            DayOfWeek.Tuesday => calendar.Tuesday,
            DayOfWeek.Wednesday => calendar.Wednesday,
            DayOfWeek.Thursday => calendar.Thursday,
            DayOfWeek.Friday => calendar.Friday,
            DayOfWeek.Saturday => calendar.Saturday,
            DayOfWeek.Sunday => calendar.Sunday,
            _ => false
        };
    }

    /// <summary>Counts working days between two inclusive endpoints.</summary>
    public static int CountWorkingDays(WorkingCalendar? calendar, DateTime? start, DateTime? end)
    {
        EnsureUsable(calendar);
        if (!start.HasValue || !end.HasValue || end.Value.Date < start.Value.Date)
            return 0;

        var count = 0;
        var current = start.Value.Date;
        while (true)
        {
            if (IsWorkingDay(calendar, current))
                count++;

            if (current == end.Value.Date)
                break;
            current = NextDay(current);
        }

        return count;
    }

    /// <summary>Moves forward by working days, counting the first working date as offset zero.</summary>
    public static DateTime AddWorkingDaysInclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        EnsureUsable(calendar);
        var current = MoveToWorkingDay(calendar, date.Date, 1);
        if (days <= 0)
            return current;

        return Advance(calendar, current, days);
    }

    /// <summary>Moves forward from the first working date after the supplied date.</summary>
    public static DateTime AddWorkingDaysExclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        EnsureUsable(calendar);
        var current = MoveToWorkingDay(calendar, NextDay(date.Date), 1);
        if (days <= 0)
            return current;

        return Advance(calendar, current, days - 1);
    }

    /// <summary>Moves backward by working days, counting the first working date as offset zero.</summary>
    public static DateTime SubtractWorkingDaysInclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        EnsureUsable(calendar);
        var current = MoveToWorkingDay(calendar, date.Date, -1);
        if (days <= 0)
            return current;

        return Retreat(calendar, current, days);
    }

    /// <summary>Moves backward from the last working date before the supplied date.</summary>
    public static DateTime SubtractWorkingDaysExclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        EnsureUsable(calendar);
        var current = MoveToWorkingDay(calendar, PreviousDay(date.Date), -1);
        if (days <= 0)
            return current;

        return Retreat(calendar, current, days - 1);
    }

    /// <summary>Calculates an inclusive finish date from a start date and working-day duration.</summary>
    public static DateTime? CalculateFinishDate(WorkingCalendar? calendar, DateTime? start, int duration)
    {
        EnsureUsable(calendar);
        if (!start.HasValue)
            return null;
        if (duration <= 0)
            return start.Value.Date;

        return AddWorkingDaysInclusive(calendar, start.Value.Date, duration - 1);
    }

    /// <summary>Calculates an inclusive start date from a finish date and working-day duration.</summary>
    public static DateTime? CalculateStartDate(WorkingCalendar? calendar, DateTime? finish, int duration)
    {
        EnsureUsable(calendar);
        if (!finish.HasValue)
            return null;
        if (duration <= 0)
            return finish.Value.Date;

        return SubtractWorkingDaysInclusive(calendar, finish.Value.Date, duration - 1);
    }

    /// <summary>Normalizes extreme dates to an explicit caller-provided fallback date.</summary>
    public static DateTime SanitizeDate(DateTime date, DateTime fallbackDate)
    {
        var normalized = date.Date;
        var fallback = fallbackDate.Date;
        const int marginDays = 180;

        if (normalized < DateTime.MinValue.Date.AddDays(marginDays)
            || normalized > DateTime.MaxValue.Date.AddDays(-marginDays))
            return fallback;

        return normalized;
    }

    private static void EnsureUsable(WorkingCalendar? calendar)
    {
        if (calendar == null)
            return;

        if (calendar.Monday || calendar.Tuesday || calendar.Wednesday || calendar.Thursday
            || calendar.Friday || calendar.Saturday || calendar.Sunday
            || calendar.WorkingExceptionDates.Length > 0)
            return;

        throw new ArgumentException(
            "A working calendar must enable at least one weekday or contain a working exception.",
            nameof(calendar));
    }

    private static DateTime Advance(WorkingCalendar? calendar, DateTime current, int workingDays)
    {
        for (var remaining = workingDays; remaining > 0; remaining--)
        {
            current = MoveToWorkingDay(calendar, NextDay(current), 1);
        }

        return current;
    }

    private static DateTime Retreat(WorkingCalendar? calendar, DateTime current, int workingDays)
    {
        for (var remaining = workingDays; remaining > 0; remaining--)
        {
            current = MoveToWorkingDay(calendar, PreviousDay(current), -1);
        }

        return current;
    }

    private static DateTime MoveToWorkingDay(WorkingCalendar? calendar, DateTime date, int direction)
    {
        if (calendar != null && !calendar.HasWeeklyWorkingDay)
            return FindExceptionWorkingDay(calendar, date.Date, direction);

        var current = date.Date;
        while (!IsWorkingDay(calendar, current))
            current = direction > 0 ? NextDay(current) : PreviousDay(current);

        return current;
    }

    private static DateTime FindExceptionWorkingDay(WorkingCalendar calendar, DateTime date, int direction)
    {
        var dates = calendar.WorkingExceptionDates;
        var index = Array.BinarySearch(dates, date);
        if (index < 0)
        {
            index = ~index;
            if (direction < 0)
                index--;
        }

        if (direction < 0 && index >= dates.Length)
            index = dates.Length - 1;

        if (index < 0 || index >= dates.Length)
            throw new InvalidOperationException(
                "The working calendar has no reachable working exception in the requested direction.");

        return dates[index];
    }

    private static DateTime NextDay(DateTime date)
    {
        if (date >= DateTime.MaxValue.Date)
            throw new InvalidOperationException("Working-day calculation exceeded the maximum supported date.");
        return date.AddDays(1);
    }

    private static DateTime PreviousDay(DateTime date)
    {
        if (date <= DateTime.MinValue.Date)
            throw new InvalidOperationException("Working-day calculation exceeded the minimum supported date.");
        return date.AddDays(-1);
    }
}
