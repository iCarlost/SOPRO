using System.Collections.ObjectModel;
using Sopro.Calculation.Calendar;

namespace Sopro.Calculation.Scheduling;

/// <summary>Dependency relation between two activities.</summary>
public enum ActivityDependencyType
{
    /// <summary>The successor starts after the predecessor finishes.</summary>
    FinishToStart = 0,
    /// <summary>The successor starts with the predecessor.</summary>
    StartToStart = 1,
    /// <summary>The successor finishes with the predecessor.</summary>
    FinishToFinish = 2,
    /// <summary>The successor finishes with the predecessor start.</summary>
    StartToFinish = 3
}

/// <summary>Immutable scalar activity input for network calculation.</summary>
public sealed class ActivityNetworkActivityInput
{
    /// <summary>Stable activity identifier.</summary>
    public int Id { get; }
    /// <summary>Stable display order used to make evaluation deterministic.</summary>
    public int Order { get; }
    /// <summary>Working-day duration before dependency constraints are applied.</summary>
    public int DurationWorkingDays { get; }
    /// <summary>Original start used for activities without predecessors.</summary>
    public DateTime? InitialStartDate { get; }

    /// <summary>Creates an immutable activity input.</summary>
    public ActivityNetworkActivityInput(
        int id, int order, int durationWorkingDays, DateTime? initialStartDate = null)
    {
        Id = id;
        Order = order;
        DurationWorkingDays = durationWorkingDays;
        InitialStartDate = initialStartDate?.Date;
    }
}

/// <summary>Immutable scalar dependency input.</summary>
public sealed class ActivityNetworkDependencyInput
{
    /// <summary>Predecessor activity identifier.</summary>
    public int SourceActivityId { get; }
    /// <summary>Successor activity identifier.</summary>
    public int TargetActivityId { get; }
    /// <summary>Dependency relation.</summary>
    public ActivityDependencyType Type { get; }
    /// <summary>Working-day lag using the calendar's inclusive/exclusive semantics.</summary>
    public int LagDays { get; }

    /// <summary>Creates an immutable dependency input.</summary>
    public ActivityNetworkDependencyInput(
        int sourceActivityId,
        int targetActivityId,
        ActivityDependencyType type,
        int lagDays = 0)
    {
        SourceActivityId = sourceActivityId;
        TargetActivityId = targetActivityId;
        Type = type;
        LagDays = lagDays;
    }
}

/// <summary>Immutable materialized activity network and calendar.</summary>
public sealed class ActivityNetworkInput
{
    /// <summary>Fallback start used when an activity has no initial start.</summary>
    public DateTime ProgramStartDate { get; }
    /// <summary>Working calendar, or null for the default Monday-to-Friday calendar.</summary>
    public WorkingCalendar? Calendar { get; }
    /// <summary>Activities in deterministic input order.</summary>
    public IReadOnlyList<ActivityNetworkActivityInput> Activities { get; }
    /// <summary>Dependencies between activities.</summary>
    public IReadOnlyList<ActivityNetworkDependencyInput> Dependencies { get; }

    /// <summary>Creates a network and defensively copies its collections.</summary>
    public ActivityNetworkInput(
        DateTime programStartDate,
        IEnumerable<ActivityNetworkActivityInput> activities,
        IEnumerable<ActivityNetworkDependencyInput>? dependencies = null,
        WorkingCalendar? calendar = null)
    {
        ArgumentNullException.ThrowIfNull(activities);

        ProgramStartDate = programStartDate.Date;
        Calendar = calendar;
        Activities = new ReadOnlyCollection<ActivityNetworkActivityInput>(activities.ToList());
        Dependencies = new ReadOnlyCollection<ActivityNetworkDependencyInput>(
            (dependencies ?? Array.Empty<ActivityNetworkDependencyInput>()).ToList());
    }
}

/// <summary>Immutable calculated dates and critical-path values for one activity.</summary>
public sealed class ActivityNetworkActivityResult
{
    /// <summary>Activity identifier.</summary>
    public int Id { get; }
    /// <summary>Calculated early start.</summary>
    public DateTime? EarlyStartDate { get; }
    /// <summary>Calculated early finish.</summary>
    public DateTime? EarlyFinishDate { get; }
    /// <summary>Calculated late start.</summary>
    public DateTime? LateStartDate { get; }
    /// <summary>Calculated late finish.</summary>
    public DateTime? LateFinishDate { get; }
    /// <summary>Working-day duration after dependency constraints are applied.</summary>
    public int DurationWorkingDays { get; }
    /// <summary>Working-day slack.</summary>
    public int SlackDays { get; }
    /// <summary>Whether the activity belongs to the critical path.</summary>
    public bool IsCritical { get; }

    internal ActivityNetworkActivityResult(
        int id,
        DateTime? earlyStartDate,
        DateTime? earlyFinishDate,
        DateTime? lateStartDate,
        DateTime? lateFinishDate,
        int durationWorkingDays,
        int slackDays,
        bool isCritical)
    {
        Id = id;
        EarlyStartDate = earlyStartDate;
        EarlyFinishDate = earlyFinishDate;
        LateStartDate = lateStartDate;
        LateFinishDate = lateFinishDate;
        DurationWorkingDays = durationWorkingDays;
        SlackDays = slackDays;
        IsCritical = isCritical;
    }
}

/// <summary>Immutable result of activity network and critical-path calculation.</summary>
public sealed class ActivityNetworkResult
{
    /// <summary>Calculated activities in the same order as the input.</summary>
    public IReadOnlyList<ActivityNetworkActivityResult> Activities { get; }
    /// <summary>Project finish, equal to the latest early finish.</summary>
    public DateTime? ProjectFinishDate { get; }

    internal ActivityNetworkResult(
        IEnumerable<ActivityNetworkActivityResult> activities, DateTime? projectFinishDate)
    {
        Activities = new ReadOnlyCollection<ActivityNetworkActivityResult>(activities.ToList());
        ProjectFinishDate = projectFinishDate;
    }
}

/// <summary>Pure deterministic activity network and critical-path calculator.</summary>
public static class ActivityNetworkCalculator
{
    /// <summary>Calculates early/late dates, slack and critical-path membership.</summary>
    /// <exception cref="ArgumentException">Thrown for invalid or duplicate network identifiers.</exception>
    /// <exception cref="InvalidOperationException">Thrown when the network contains a cycle.</exception>
    public static ActivityNetworkResult Calculate(ActivityNetworkInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        var activities = input.Activities.ToDictionary(
            activity => activity.Id,
            activity => activity);
        if (activities.Count != input.Activities.Count)
            throw new ArgumentException("Activity identifiers must be unique.", nameof(input));

        var predecessors = activities.Keys.ToDictionary(id => id, _ => new List<ActivityNetworkDependencyInput>());
        var successors = activities.Keys.ToDictionary(id => id, _ => new List<ActivityNetworkDependencyInput>());
        foreach (var dependency in input.Dependencies)
        {
            if (!activities.ContainsKey(dependency.SourceActivityId)
                || !activities.ContainsKey(dependency.TargetActivityId))
                throw new ArgumentException("Dependencies must reference known activities.", nameof(input));
            if (!Enum.IsDefined(dependency.Type))
                throw new ArgumentOutOfRangeException(nameof(input), dependency.Type,
                    "Dependency type is not supported.");

            predecessors[dependency.TargetActivityId].Add(dependency);
            successors[dependency.SourceActivityId].Add(dependency);
        }

        var topologicalOrder = TopologicalOrder(activities, successors);
        var states = activities.ToDictionary(pair => pair.Key, pair => new ActivityState(pair.Value));

        foreach (var id in topologicalOrder)
            CalculateEarlyDates(states[id], predecessors[id], states, input);

        var projectFinish = states.Values
            .Where(state => state.EarlyFinishDate.HasValue)
            .Select(state => state.EarlyFinishDate!.Value)
            .Cast<DateTime?>()
            .Max();

        if (projectFinish.HasValue)
        {
            foreach (var id in topologicalOrder.AsEnumerable().Reverse())
                CalculateLateDates(states[id], successors[id], states, input.Calendar, projectFinish.Value);
        }

        var results = input.Activities.Select(activity =>
        {
            var state = states[activity.Id];
            var slack = state.EarlyStartDate.HasValue && state.LateStartDate.HasValue
                ? Math.Max(0, WorkingCalendarCalculator.CountWorkingDays(
                    input.Calendar, state.EarlyStartDate, state.LateStartDate) - 1)
                : 0;

            return new ActivityNetworkActivityResult(
                activity.Id,
                state.EarlyStartDate,
                state.EarlyFinishDate,
                state.LateStartDate,
                state.LateFinishDate,
                state.DurationWorkingDays,
                slack,
                slack == 0 && state.EarlyStartDate.HasValue && state.LateStartDate.HasValue);
        });

        return new ActivityNetworkResult(results, projectFinish);
    }

    private static List<int> TopologicalOrder(
        IReadOnlyDictionary<int, ActivityNetworkActivityInput> activities,
        IReadOnlyDictionary<int, List<ActivityNetworkDependencyInput>> successors)
    {
        var indegree = activities.Keys.ToDictionary(id => id, _ => 0);
        foreach (var dependencyList in successors.Values)
        {
            foreach (var dependency in dependencyList)
                indegree[dependency.TargetActivityId]++;
        }

        var ready = new SortedSet<int>(Comparer<int>.Create((left, right) =>
        {
            var byOrder = activities[left].Order.CompareTo(activities[right].Order);
            return byOrder != 0 ? byOrder : left.CompareTo(right);
        }));
        foreach (var pair in indegree.Where(pair => pair.Value == 0))
            ready.Add(pair.Key);

        var result = new List<int>(activities.Count);
        while (ready.Count > 0)
        {
            var id = ready.Min;
            ready.Remove(id);
            result.Add(id);

            foreach (var dependency in successors[id])
            {
                indegree[dependency.TargetActivityId]--;
                if (indegree[dependency.TargetActivityId] == 0)
                    ready.Add(dependency.TargetActivityId);
            }
        }

        if (result.Count != activities.Count)
            throw new InvalidOperationException("Activity network contains a cycle.");

        return result;
    }

    private static void CalculateEarlyDates(
        ActivityState state,
        IReadOnlyList<ActivityNetworkDependencyInput> dependencies,
        IReadOnlyDictionary<int, ActivityState> states,
        ActivityNetworkInput input)
    {
        var minimumStart = (DateTime?)null;
        var minimumFinish = (DateTime?)null;
        foreach (var dependency in dependencies)
        {
            var source = states[dependency.SourceActivityId];
            switch (dependency.Type)
            {
                case ActivityDependencyType.FinishToStart when source.EarlyFinishDate.HasValue:
                    minimumStart = Max(minimumStart, WorkingCalendarCalculator.AddWorkingDaysExclusive(
                        input.Calendar, source.EarlyFinishDate.Value, dependency.LagDays));
                    break;
                case ActivityDependencyType.StartToStart when source.EarlyStartDate.HasValue:
                    minimumStart = Max(minimumStart, WorkingCalendarCalculator.AddWorkingDaysInclusive(
                        input.Calendar, source.EarlyStartDate.Value, dependency.LagDays));
                    break;
                case ActivityDependencyType.FinishToFinish when source.EarlyFinishDate.HasValue:
                    minimumFinish = Max(minimumFinish, WorkingCalendarCalculator.AddWorkingDaysInclusive(
                        input.Calendar, source.EarlyFinishDate.Value, dependency.LagDays));
                    break;
                case ActivityDependencyType.StartToFinish when source.EarlyStartDate.HasValue:
                    minimumFinish = Max(minimumFinish, WorkingCalendarCalculator.AddWorkingDaysInclusive(
                        input.Calendar, source.EarlyStartDate.Value, dependency.LagDays));
                    break;
            }
        }

        var duration = Math.Max(1, state.Input.DurationWorkingDays);
        var hasDependencies = dependencies.Count > 0;
        var baseStart = WorkingCalendarCalculator.SanitizeDate(
            state.Input.InitialStartDate ?? input.ProgramStartDate, input.ProgramStartDate);
        var start = !hasDependencies ? baseStart : minimumStart;
        DateTime? finish = start.HasValue
            ? WorkingCalendarCalculator.CalculateFinishDate(input.Calendar, start, duration)
            : null;

        if (start.HasValue && minimumFinish.HasValue)
        {
            finish = minimumFinish.Value;
            if (finish.Value < start.Value)
                finish = start.Value;
            duration = Math.Max(1, WorkingCalendarCalculator.CountWorkingDays(input.Calendar, start, finish));
        }
        else if (!start.HasValue && minimumFinish.HasValue)
        {
            finish = minimumFinish.Value;
            start = WorkingCalendarCalculator.CalculateStartDate(input.Calendar, finish, duration);
        }

        state.EarlyStartDate = start?.Date;
        state.EarlyFinishDate = finish?.Date;
        state.DurationWorkingDays = duration;
    }

    private static void CalculateLateDates(
        ActivityState state,
        IReadOnlyList<ActivityNetworkDependencyInput> successors,
        IReadOnlyDictionary<int, ActivityState> states,
        WorkingCalendar? calendar,
        DateTime projectFinish)
    {
        if (successors.Count == 0)
        {
            state.LateFinishDate = projectFinish;
            state.LateStartDate = WorkingCalendarCalculator.CalculateStartDate(
                calendar, projectFinish, state.DurationWorkingDays);
            return;
        }

        var latestStart = (DateTime?)null;
        var latestFinish = (DateTime?)null;
        foreach (var dependency in successors)
        {
            var successor = states[dependency.TargetActivityId];
            switch (dependency.Type)
            {
                case ActivityDependencyType.FinishToStart when successor.LateStartDate.HasValue:
                    latestFinish = Min(latestFinish, WorkingCalendarCalculator.SubtractWorkingDaysExclusive(
                        calendar, successor.LateStartDate.Value, dependency.LagDays));
                    break;
                case ActivityDependencyType.StartToStart when successor.LateStartDate.HasValue:
                    latestStart = Min(latestStart, WorkingCalendarCalculator.SubtractWorkingDaysInclusive(
                        calendar, successor.LateStartDate.Value, dependency.LagDays));
                    break;
                case ActivityDependencyType.FinishToFinish when successor.LateFinishDate.HasValue:
                    latestFinish = Min(latestFinish, WorkingCalendarCalculator.SubtractWorkingDaysInclusive(
                        calendar, successor.LateFinishDate.Value, dependency.LagDays));
                    break;
                case ActivityDependencyType.StartToFinish when successor.LateFinishDate.HasValue:
                    latestStart = Min(latestStart, WorkingCalendarCalculator.SubtractWorkingDaysInclusive(
                        calendar, successor.LateFinishDate.Value, dependency.LagDays));
                    break;
            }
        }

        if (latestStart.HasValue && latestFinish.HasValue)
        {
            state.LateStartDate = latestStart.Value.Date;
            state.LateFinishDate = latestFinish.Value.Date < latestStart.Value.Date
                ? latestStart.Value.Date
                : latestFinish.Value.Date;
        }
        else if (latestStart.HasValue)
        {
            state.LateStartDate = latestStart.Value.Date;
            state.LateFinishDate = WorkingCalendarCalculator.CalculateFinishDate(
                calendar, state.LateStartDate, state.DurationWorkingDays);
        }
        else if (latestFinish.HasValue)
        {
            state.LateFinishDate = latestFinish.Value.Date;
            state.LateStartDate = WorkingCalendarCalculator.CalculateStartDate(
                calendar, state.LateFinishDate, state.DurationWorkingDays);
        }
    }

    private static DateTime? Max(DateTime? left, DateTime? right) =>
        !left.HasValue || (right.HasValue && right.Value.Date > left.Value.Date) ? right?.Date : left.Value.Date;

    private static DateTime? Min(DateTime? left, DateTime? right) =>
        !left.HasValue || (right.HasValue && right.Value.Date < left.Value.Date) ? right?.Date : left.Value.Date;

    private sealed class ActivityState
    {
        public ActivityNetworkActivityInput Input { get; }
        public int DurationWorkingDays { get; set; }
        public DateTime? EarlyStartDate { get; set; }
        public DateTime? EarlyFinishDate { get; set; }
        public DateTime? LateStartDate { get; set; }
        public DateTime? LateFinishDate { get; set; }

        public ActivityState(ActivityNetworkActivityInput input)
        {
            Input = input;
            DurationWorkingDays = Math.Max(1, input.DurationWorkingDays);
        }
    }
}
