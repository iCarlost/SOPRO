namespace Sopro.Calculation.Calendar;

/// <summary>Kind of exception overriding the weekly working-day pattern.</summary>
public enum CalendarExceptionKind
{
    NonWorking = 0,
    Working = 1
}

/// <summary>One date-specific override in a working calendar.</summary>
public sealed record CalendarException
{
    public DateTime Date { get; init; }
    public CalendarExceptionKind Kind { get; init; }
}

/// <summary>Immutable weekly working calendar and date exceptions.</summary>
public sealed record WorkingCalendar
{
    public bool Monday { get; init; } = true;
    public bool Tuesday { get; init; } = true;
    public bool Wednesday { get; init; } = true;
    public bool Thursday { get; init; } = true;
    public bool Friday { get; init; } = true;
    public bool Saturday { get; init; }
    public bool Sunday { get; init; }
    public IReadOnlyList<CalendarException> Exceptions { get; init; } = Array.Empty<CalendarException>();
}

/// <summary>Deterministic working-day operations independent of persistence and system time.</summary>
public static class WorkingCalendarCalculator
{
    public static bool IsWorkingDay(WorkingCalendar? calendar, DateTime date)
    {
        if (calendar == null)
            return date.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday);

        var exception = calendar.Exceptions.FirstOrDefault(x => x.Date.Date == date.Date);
        if (exception != null)
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

    public static int CountWorkingDays(WorkingCalendar? calendar, DateTime? start, DateTime? end)
    {
        if (!start.HasValue || !end.HasValue || end.Value.Date < start.Value.Date)
            return 0;

        var count = 0;
        for (var current = start.Value.Date; current <= end.Value.Date; current = NextDay(current))
        {
            if (IsWorkingDay(calendar, current))
                count++;
        }

        return count;
    }

    public static DateTime AddWorkingDaysInclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        var current = MoveToWorkingDay(calendar, date.Date, 1);
        if (days <= 0)
            return current;

        return Advance(calendar, current, days);
    }

    public static DateTime AddWorkingDaysExclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        var current = MoveToWorkingDay(calendar, NextDay(date.Date), 1);
        if (days <= 0)
            return current;

        return Advance(calendar, current, days - 1);
    }

    public static DateTime SubtractWorkingDaysInclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        var current = MoveToWorkingDay(calendar, date.Date, -1);
        if (days <= 0)
            return current;

        return Retreat(calendar, current, days);
    }

    public static DateTime SubtractWorkingDaysExclusive(WorkingCalendar? calendar, DateTime date, int days)
    {
        var current = MoveToWorkingDay(calendar, PreviousDay(date.Date), -1);
        if (days <= 0)
            return current;

        return Retreat(calendar, current, days - 1);
    }

    public static DateTime? CalculateFinishDate(WorkingCalendar? calendar, DateTime? start, int duration)
    {
        if (!start.HasValue)
            return null;
        if (duration <= 0)
            return start.Value.Date;

        return AddWorkingDaysInclusive(calendar, start.Value.Date, duration - 1);
    }

    public static DateTime? CalculateStartDate(WorkingCalendar? calendar, DateTime? finish, int duration)
    {
        if (!finish.HasValue)
            return null;
        if (duration <= 0)
            return finish.Value.Date;

        return SubtractWorkingDaysInclusive(calendar, finish.Value.Date, duration - 1);
    }

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
        var current = date.Date;
        while (!IsWorkingDay(calendar, current))
            current = direction > 0 ? NextDay(current) : PreviousDay(current);

        return current;
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
