using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using Sopro.Calculation.Scheduling;

namespace SOPRO.Tests.Calculation;

[TestClass]
public sealed class ActivityNetworkCalculatorTests
{
    [TestMethod]
    public void BranchingNetwork_CalculatesCriticalPathAndSlack()
    {
        var activities = new[]
        {
            new ActivityNetworkActivityInput(1, 1, 2, new DateTime(2026, 1, 5)),
            new ActivityNetworkActivityInput(2, 2, 4, new DateTime(2026, 1, 5)),
            new ActivityNetworkActivityInput(3, 3, 2, new DateTime(2026, 1, 5))
        };
        var dependencies = new[]
        {
            new ActivityNetworkDependencyInput(2, 3, ActivityDependencyType.FinishToStart)
        };

        var result = ActivityNetworkCalculator.Calculate(new ActivityNetworkInput(
            new DateTime(2026, 1, 5), activities, dependencies));

        Assert.AreEqual(new DateTime(2026, 1, 12), result.ProjectFinishDate);
        AssertActivity(result, 1, "2026-01-05", "2026-01-06", "2026-01-09", "2026-01-12", 4, false);
        AssertActivity(result, 2, "2026-01-05", "2026-01-08", "2026-01-05", "2026-01-08", 0, true);
        AssertActivity(result, 3, "2026-01-09", "2026-01-12", "2026-01-09", "2026-01-12", 0, true);
    }

    [DataTestMethod]
    [DataRow(ActivityDependencyType.FinishToStart, "2026-01-08", "2026-01-09")]
    [DataRow(ActivityDependencyType.StartToStart, "2026-01-07", "2026-01-08")]
    [DataRow(ActivityDependencyType.FinishToFinish, "2026-01-07", "2026-01-08")]
    [DataRow(ActivityDependencyType.StartToFinish, "2026-01-06", "2026-01-07")]
    public void DependencyTypes_ApplyPositiveLag(
        ActivityDependencyType type, string expectedStart, string expectedFinish)
    {
        var source = new ActivityNetworkActivityInput(1, 1, 2, new DateTime(2026, 1, 5));
        var target = new ActivityNetworkActivityInput(2, 2, 2, new DateTime(2026, 1, 5));
        var dependency = new ActivityNetworkDependencyInput(1, 2, type, lagDays: 2);

        var result = ActivityNetworkCalculator.Calculate(new ActivityNetworkInput(
            new DateTime(2026, 1, 5), new[] { source, target }, new[] { dependency }));
        var actual = result.Activities.Single(activity => activity.Id == 2);

        Assert.AreEqual(Date(expectedStart), actual.EarlyStartDate);
        Assert.AreEqual(Date(expectedFinish), actual.EarlyFinishDate);
    }

    [TestMethod]
    public void CyclicNetwork_IsRejectedBeforeProducingDates()
    {
        var activities = new[]
        {
            new ActivityNetworkActivityInput(1, 1, 2),
            new ActivityNetworkActivityInput(2, 2, 2)
        };
        var dependencies = new[]
        {
            new ActivityNetworkDependencyInput(1, 2, ActivityDependencyType.FinishToStart),
            new ActivityNetworkDependencyInput(2, 1, ActivityDependencyType.FinishToStart)
        };

        Assert.ThrowsException<InvalidOperationException>(() =>
            ActivityNetworkCalculator.Calculate(new ActivityNetworkInput(
                new DateTime(2026, 1, 5), activities, dependencies)));
    }

    [TestMethod]
    public void NetworkInput_CopiesActivityAndDependencyCollections()
    {
        var activities = new List<ActivityNetworkActivityInput>
        {
            new(1, 1, 1, new DateTime(2026, 1, 5))
        };
        var dependencies = new List<ActivityNetworkDependencyInput>();
        var input = new ActivityNetworkInput(new DateTime(2026, 1, 5), activities, dependencies);
        activities.Add(new ActivityNetworkActivityInput(2, 2, 1));
        dependencies.Add(new ActivityNetworkDependencyInput(1, 2, ActivityDependencyType.FinishToStart));

        var result = ActivityNetworkCalculator.Calculate(input);

        Assert.AreEqual(1, input.Activities.Count);
        Assert.AreEqual(0, input.Dependencies.Count);
        Assert.AreEqual(1, result.Activities.Count);
    }

    private static void AssertActivity(
        ActivityNetworkResult result,
        int id,
        string earlyStart,
        string earlyFinish,
        string lateStart,
        string lateFinish,
        int slack,
        bool critical)
    {
        var actual = result.Activities.Single(activity => activity.Id == id);
        Assert.AreEqual(Date(earlyStart), actual.EarlyStartDate);
        Assert.AreEqual(Date(earlyFinish), actual.EarlyFinishDate);
        Assert.AreEqual(Date(lateStart), actual.LateStartDate);
        Assert.AreEqual(Date(lateFinish), actual.LateFinishDate);
        Assert.AreEqual(slack, actual.SlackDays);
        Assert.AreEqual(critical, actual.IsCritical);
    }

    private static DateTime Date(string value) =>
        DateTime.ParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture);
}
