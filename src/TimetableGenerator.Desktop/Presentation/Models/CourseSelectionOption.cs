using System;
using System.Collections.Generic;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSelectionOption
{
    public PlanningCourseSelection Selection { get; }

    public EMeetingScheduleStatus ScheduleStatus { get; }

    public string DisplayName { get; }

    public bool IsTimeNotProvided
    {
        get
        {
            return ScheduleStatus == EMeetingScheduleStatus.NotProvided;
        }
    }

    public CourseSelectionOption(
        PlanningCourseSelection selection,
        EMeetingScheduleStatus scheduleStatus,
        string displayName)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (Enum.IsDefined(typeof(EMeetingScheduleStatus), scheduleStatus) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleStatus));
        }

        if (string.IsNullOrWhiteSpace(displayName))
        {
            throw new ArgumentException(
                "Course selection option names cannot be empty.",
                nameof(displayName));
        }

        Selection = selection;
        ScheduleStatus = scheduleStatus;
        DisplayName = displayName.Trim();
    }

    public bool Represents(PlanningCourseSelection selection)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (Selection.CourseId != selection.CourseId
            || Selection.Kind != selection.Kind)
        {
            return false;
        }

        switch (Selection.Kind)
        {
            case EPlanningCourseSelectionKind.ScheduledAlternatives:
                return containsSameScheduledOfferings(selection);
            case EPlanningCourseSelectionKind.TimeNotProvidedOffering:
                return Selection.GetTimeNotProvidedOfferingId()
                    == selection.GetTimeNotProvidedOfferingId();
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(selection),
                    selection.Kind,
                    "Unknown planning course selection kind.");
        }
    }

    private bool containsSameScheduledOfferings(PlanningCourseSelection selection)
    {
        IReadOnlyList<OfferingId> representedOfferingIds =
            Selection.GetScheduledOfferingIds();
        IReadOnlyList<OfferingId> candidateOfferingIds =
            selection.GetScheduledOfferingIds();
        if (representedOfferingIds.Count != candidateOfferingIds.Count)
        {
            return false;
        }

        foreach (OfferingId representedOfferingId in representedOfferingIds)
        {
            bool containsOffering = false;
            foreach (OfferingId candidateOfferingId in candidateOfferingIds)
            {
                if (representedOfferingId == candidateOfferingId)
                {
                    containsOffering = true;
                    break;
                }
            }

            if (containsOffering == false)
            {
                return false;
            }
        }

        return true;
    }
}
