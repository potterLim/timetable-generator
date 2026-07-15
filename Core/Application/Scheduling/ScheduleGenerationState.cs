using System.Collections.Generic;
using System.Threading;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Core.Application.Scheduling;

internal sealed class ScheduleGenerationState
{
    public IReadOnlyList<CourseChoiceGroup> CourseChoiceGroups { get; }

    public ScheduleGenerationOptions Options { get; }

    public CancellationToken CancellationToken { get; }

    public List<CourseOffering> SelectedCourseOfferings { get; }

    public HashSet<ScheduleSlot> OccupiedScheduleSlots { get; }

    public List<GeneratedSchedule> GeneratedSchedules { get; }

    public EScheduleGenerationCompletion Completion { get; private set; }

    public bool ShouldStop
    {
        get
        {
            return Completion != EScheduleGenerationCompletion.Completed;
        }
    }

    public ScheduleGenerationState(
        IReadOnlyList<CourseChoiceGroup> courseChoiceGroups,
        ScheduleGenerationOptions options,
        CancellationToken cancellationToken)
    {
        CourseChoiceGroups = courseChoiceGroups;
        Options = options;
        CancellationToken = cancellationToken;
        SelectedCourseOfferings = new List<CourseOffering>();
        OccupiedScheduleSlots = new HashSet<ScheduleSlot>();
        GeneratedSchedules = new List<GeneratedSchedule>();
        Completion = EScheduleGenerationCompletion.Completed;
    }

    public void MarkMaximumScheduleCountReached()
    {
        Completion = EScheduleGenerationCompletion.MaximumScheduleCountReached;
    }

    public void MarkCanceled()
    {
        Completion = EScheduleGenerationCompletion.Canceled;
    }
}
