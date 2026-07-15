using System;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGenerator.Application.Documents;

public sealed class ScheduleDocumentSchedule
{
    public GeneratedSchedule GeneratedSchedule { get; }

    public ScheduleGridViewModel GridViewModel { get; }

    internal ScheduleDocumentSchedule(
        GeneratedSchedule generatedSchedule,
        ScheduleGridViewModel gridViewModel)
    {
        if (generatedSchedule == null)
        {
            throw new ArgumentNullException(nameof(generatedSchedule));
        }

        if (gridViewModel == null)
        {
            throw new ArgumentNullException(nameof(gridViewModel));
        }

        if (generatedSchedule.CourseOfferings.Count != gridViewModel.Summary.SelectedCourseCount)
        {
            throw new ArgumentException(
                "Schedule grid summaries must describe their generated schedule.",
                nameof(gridViewModel));
        }

        GeneratedSchedule = generatedSchedule;
        GridViewModel = gridViewModel;
    }
}
