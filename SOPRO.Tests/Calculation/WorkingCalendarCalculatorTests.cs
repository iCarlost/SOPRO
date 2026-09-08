using Microsoft.VisualStudio.TestTools.UnitTesting;
using Sopro.Calculation.Calendar;

namespace SOPRO.Tests.Calculation;

[TestClass]
public sealed class WorkingCalendarCalculatorTests
{
    [TestMethod]
    public void DefaultCalendar_UsesWeekdaysAndInclusiveBoundaries()
    {
        Assert.IsTrue(WorkingCalendarCalculator.IsWorkingDay(null, new DateTime(2026, 1, 5)));
        Assert.IsFalse(WorkingCalendarCalculator.IsWorkingDay(null, new DateTime(2026, 1, 10)));
        Assert.AreEqual(5, WorkingCalendarCalculator.CountWorkingDays(
            null, new DateTime(2026, 1, 5), new DateTime(2026, 1, 11)));
        Assert.AreEqual(new DateTime(2026, 1, 9), WorkingCalendarCalculator.CalculateFinishDate(
            null, new DateTime(2026, 1, 5), 5));
        Assert.AreEqual(new DateTime(2026, 1, 5), WorkingCalendarCalculator.CalculateStartDate(
            null, new DateTime(2026, 1, 9), 5));
    }

    [TestMethod]
    public void Offsets_KeepInclusiveAndExclusiveSemantics()
    {
        var monday = new DateTime(2026, 1, 5);

        Assert.AreEqual(new DateTime(2026, 1, 6),
            WorkingCalendarCalculator.AddWorkingDaysInclusive(null, monday, 1));
        Assert.AreEqual(new DateTime(2026, 1, 7),
            WorkingCalendarCalculator.AddWorkingDaysExclusive(null, monday, 2));
        Assert.AreEqual(new DateTime(2026, 1, 2),
            WorkingCalendarCalculator.SubtractWorkingDaysInclusive(null, monday, 1));
        Assert.AreEqual(new DateTime(2026, 1, 1),
            WorkingCalendarCalculator.SubtractWorkingDaysExclusive(null, monday, 2));
    }

    [TestMethod]
    public void Exceptions_OverrideWeeklyPattern()
    {
        var calendar = new WorkingCalendar
        {
            Exceptions = new[]
            {
                new CalendarException { Date = new DateTime(2026, 1, 9), Kind = CalendarExceptionKind.NonWorking },
                new CalendarException { Date = new DateTime(2026, 1, 10), Kind = CalendarExceptionKind.Working }
            }
        };

        Assert.IsFalse(WorkingCalendarCalculator.IsWorkingDay(calendar, new DateTime(2026, 1, 9)));
        Assert.IsTrue(WorkingCalendarCalculator.IsWorkingDay(calendar, new DateTime(2026, 1, 10)));
        Assert.AreEqual(1, WorkingCalendarCalculator.CountWorkingDays(
            calendar, new DateTime(2026, 1, 9), new DateTime(2026, 1, 10)));
    }

    [TestMethod]
    public void CustomWeeklyPattern_IsUsedForEveryDayOfWeek()
    {
        var calendar = new WorkingCalendar
        {
            Monday = false,
            Tuesday = true,
            Wednesday = false,
            Thursday = true,
            Friday = false,
            Saturday = true,
            Sunday = false
        };

        var start = new DateTime(2026, 1, 5); // Monday
        var expected = new[] { false, true, false, true, false, true, false };
        for (var i = 0; i < expected.Length; i++)
            Assert.AreEqual(expected[i], WorkingCalendarCalculator.IsWorkingDay(calendar, start.AddDays(i)));
    }

    [TestMethod]
    public void Exceptions_AreCopiedDefensively()
    {
        var exceptions = new[]
        {
            new CalendarException
            {
                Date = new DateTime(2026, 1, 10),
                Kind = CalendarExceptionKind.Working
            }
        };
        var calendar = new WorkingCalendar { Exceptions = exceptions };

        exceptions[0] = new CalendarException
        {
            Date = new DateTime(2026, 1, 10),
            Kind = CalendarExceptionKind.NonWorking
        };

        Assert.IsTrue(WorkingCalendarCalculator.IsWorkingDay(calendar, new DateTime(2026, 1, 10)));
        Assert.IsFalse(calendar.Exceptions[0].Kind == CalendarExceptionKind.NonWorking);
    }

    [TestMethod]
    public void CalendarWithoutWorkingDays_FailsFast()
    {
        var calendar = new WorkingCalendar
        {
            Monday = false,
            Tuesday = false,
            Wednesday = false,
            Thursday = false,
            Friday = false,
            Saturday = false,
            Sunday = false
        };

        Assert.ThrowsException<ArgumentException>(() =>
            WorkingCalendarCalculator.IsWorkingDay(calendar, new DateTime(2026, 1, 5)));
        Assert.ThrowsException<ArgumentException>(() =>
            WorkingCalendarCalculator.CountWorkingDays(calendar,
                new DateTime(2026, 1, 5), new DateTime(2026, 1, 6)));
    }

    [TestMethod]
    public void FiniteExceptionCalendar_FindsReachableDatesWithoutScanningTheWholeRange()
    {
        var calendar = CalendarWithOnlyWorkingExceptions(new DateTime(2026, 1, 10), new DateTime(2026, 1, 20));

        Assert.AreEqual(new DateTime(2026, 1, 10),
            WorkingCalendarCalculator.AddWorkingDaysInclusive(calendar, new DateTime(2026, 1, 1), 0));
        Assert.AreEqual(new DateTime(2026, 1, 20),
            WorkingCalendarCalculator.AddWorkingDaysExclusive(calendar, new DateTime(2026, 1, 10), 1));
        Assert.AreEqual(new DateTime(2026, 1, 20),
            WorkingCalendarCalculator.SubtractWorkingDaysInclusive(calendar, new DateTime(2026, 12, 1), 0));
        Assert.AreEqual(new DateTime(2026, 1, 10),
            WorkingCalendarCalculator.SubtractWorkingDaysExclusive(calendar, new DateTime(2026, 1, 20), 1));
    }

    [TestMethod]
    public void FiniteExceptionCalendar_RejectsMissingDirectionAndExhaustedOffsets()
    {
        var calendar = CalendarWithOnlyWorkingExceptions(new DateTime(2026, 1, 10), new DateTime(2026, 1, 20));
        var onlyPastException = CalendarWithOnlyWorkingExceptions(new DateTime(2025, 1, 1));

        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkingCalendarCalculator.AddWorkingDaysInclusive(onlyPastException, new DateTime(2026, 1, 1), 0));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkingCalendarCalculator.SubtractWorkingDaysInclusive(calendar, new DateTime(2025, 1, 1), 0));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkingCalendarCalculator.AddWorkingDaysInclusive(calendar, new DateTime(2026, 1, 10), 2));
        Assert.ThrowsException<InvalidOperationException>(() =>
            WorkingCalendarCalculator.SubtractWorkingDaysInclusive(calendar, new DateTime(2026, 1, 20), 2));
    }

    [TestMethod]
    public void FiniteExceptionCalendar_PreservesExactBackwardMatchAndNormalizesTime()
    {
        var calendar = CalendarWithOnlyWorkingExceptions(
            new DateTime(2026, 1, 10, 12, 30, 0), new DateTime(2026, 1, 20));

        Assert.AreEqual(new DateTime(2026, 1, 20),
            WorkingCalendarCalculator.SubtractWorkingDaysInclusive(calendar, new DateTime(2026, 1, 20), 0));
        Assert.AreEqual(new DateTime(2026, 1, 20),
            WorkingCalendarCalculator.CalculateStartDate(calendar, new DateTime(2026, 1, 20), 1));
        Assert.AreEqual(new DateTime(2026, 1, 10),
            WorkingCalendarCalculator.AddWorkingDaysInclusive(calendar, new DateTime(2026, 1, 10), 0));
    }

    [TestMethod]
    public void CountWorkingDays_HandlesMaximumDateWithoutIncrementingPastIt()
    {
        Assert.AreEqual(1, WorkingCalendarCalculator.CountWorkingDays(
            null, DateTime.MaxValue.Date, DateTime.MaxValue.Date));
    }

    [TestMethod]
    public void CustomCalendar_MatchesDayByDayOracleAcrossRange()
    {
        var calendar = new WorkingCalendar
        {
            Monday = false,
            Tuesday = true,
            Wednesday = true,
            Thursday = false,
            Friday = true,
            Saturday = true,
            Sunday = false,
            Exceptions = new[]
            {
                new CalendarException { Date = new DateTime(2026, 2, 3), Kind = CalendarExceptionKind.NonWorking },
                new CalendarException { Date = new DateTime(2026, 2, 8), Kind = CalendarExceptionKind.Working }
            }
        };
        var start = new DateTime(2026, 2, 1);
        var end = new DateTime(2026, 3, 31);
        var expected = 0;

        for (var date = start; date <= end; date = date.AddDays(1))
        {
            var actual = WorkingCalendarCalculator.IsWorkingDay(calendar, date);
            var oracle = IsWorkingDayOracle(calendar, date);
            Assert.AreEqual(oracle, actual, date.ToString("yyyy-MM-dd"));
            if (oracle)
                expected++;
        }

        Assert.AreEqual(expected, WorkingCalendarCalculator.CountWorkingDays(calendar, start, end));
    }

    [TestMethod]
    public void Sanitization_UsesExplicitFallbackForBoundaryDates()
    {
        var fallback = new DateTime(2026, 1, 5);

        Assert.AreEqual(fallback,
            WorkingCalendarCalculator.SanitizeDate(DateTime.MinValue, fallback));
        Assert.AreEqual(fallback,
            WorkingCalendarCalculator.SanitizeDate(DateTime.MaxValue, fallback));
        Assert.AreEqual(0, WorkingCalendarCalculator.CountWorkingDays(
            null, new DateTime(2026, 1, 10), new DateTime(2026, 1, 9)));
    }

    private static bool IsWorkingDayOracle(WorkingCalendar calendar, DateTime date)
    {
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

    private static WorkingCalendar CalendarWithOnlyWorkingExceptions(params DateTime[] dates) => new()
    {
        Monday = false,
        Tuesday = false,
        Wednesday = false,
        Thursday = false,
        Friday = false,
        Saturday = false,
        Sunday = false,
        Exceptions = dates.Select(date => new CalendarException
        {
            Date = date,
            Kind = CalendarExceptionKind.Working
        }).ToArray()
    };
}
