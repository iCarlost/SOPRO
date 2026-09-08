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
}
