using System;
using System.Collections.Generic;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSelectionOption
{
    public EnglishInstructionPercentage? ExactEnglishInstructionPercentageOrNull
    {
        get;
    }

    public PlanningCourseSelection Selection { get; }

    public EMeetingScheduleStatus ScheduleStatus { get; }

    public string DisplayName { get; }

    public string AccessibleName { get; }

    public string EnglishInstructionDisplayText
    {
        get
        {
            if (ExactEnglishInstructionPercentageOrNull.HasValue == false)
            {
                return string.Empty;
            }

            return EnglishInstructionPercentageRange.CreateUniform(ExactEnglishInstructionPercentageOrNull.Value).DisplayText;
        }
    }

    public string EnglishInstructionAccessibleText
    {
        get
        {
            if (ExactEnglishInstructionPercentageOrNull.HasValue == false)
            {
                return string.Empty;
            }

            return EnglishInstructionPercentageRange.CreateUniform(ExactEnglishInstructionPercentageOrNull.Value).AccessibleText;
        }
    }

    public bool IsDirectAdd
    {
        get
        {
            return ExactEnglishInstructionPercentageOrNull.HasValue;
        }
    }

    public bool IsTimeNotProvided
    {
        get
        {
            return ScheduleStatus == EMeetingScheduleStatus.NotProvided;
        }
    }

    private CourseSelectionOption(PlanningCourseSelection selection, EMeetingScheduleStatus scheduleStatus, string displayName, EnglishInstructionPercentage? exactEnglishInstructionPercentageOrNull)
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
            throw new ArgumentException("Course selection option names cannot be empty.", nameof(displayName));
        }

        Selection = selection;
        ScheduleStatus = scheduleStatus;
        ExactEnglishInstructionPercentageOrNull = exactEnglishInstructionPercentageOrNull;
        string normalizedDisplayName = displayName.Trim();
        if (exactEnglishInstructionPercentageOrNull.HasValue)
        {
            DisplayName = normalizedDisplayName + " · " + EnglishInstructionDisplayText;
            AccessibleName = normalizedDisplayName + ", " + EnglishInstructionAccessibleText;
        }
        else
        {
            DisplayName = normalizedDisplayName;
            AccessibleName = normalizedDisplayName;
        }
    }

    public static CourseSelectionOption CreateDirectAdd(PlanningCourseSelection selection, EMeetingScheduleStatus scheduleStatus, string displayName, EnglishInstructionPercentage exactEnglishInstructionPercentage)
    {
        validateDirectAddSelection(selection, scheduleStatus);
        return new CourseSelectionOption(selection, scheduleStatus, displayName, exactEnglishInstructionPercentage);
    }

    public static CourseSelectionOption CreatePreferenceEditor(PlanningCourseSelection selection, string displayName)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (selection.Kind != EPlanningCourseSelectionKind.ScheduledAlternatives || selection.GetScheduledOfferingIds().Count <= 1)
        {
            throw new ArgumentException("Preference-editor options require multiple scheduled offerings.", nameof(selection));
        }

        return new CourseSelectionOption(selection, EMeetingScheduleStatus.Scheduled, displayName, null);
    }

    public bool Represents(PlanningCourseSelection selection)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (Selection.CourseId != selection.CourseId || Selection.Kind != selection.Kind)
        {
            return false;
        }

        switch (Selection.Kind)
        {
            case EPlanningCourseSelectionKind.ScheduledAlternatives:
                return containsSameScheduledOfferings(selection);
            case EPlanningCourseSelectionKind.TimeNotProvidedOffering:
                return Selection.GetTimeNotProvidedOfferingId() == selection.GetTimeNotProvidedOfferingId();
            default:
                throw new ArgumentOutOfRangeException(nameof(selection), selection.Kind, "Unknown planning course selection kind.");
        }
    }

    private bool containsSameScheduledOfferings(PlanningCourseSelection selection)
    {
        IReadOnlyList<OfferingId> representedOfferingIds = Selection.GetScheduledOfferingIds();
        IReadOnlyList<OfferingId> candidateOfferingIds = selection.GetScheduledOfferingIds();
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

    private static void validateDirectAddSelection(PlanningCourseSelection selection, EMeetingScheduleStatus scheduleStatus)
    {
        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (Enum.IsDefined(typeof(EMeetingScheduleStatus), scheduleStatus) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduleStatus));
        }

        if (scheduleStatus == EMeetingScheduleStatus.Scheduled)
        {
            if (selection.Kind != EPlanningCourseSelectionKind.ScheduledAlternatives || selection.GetScheduledOfferingIds().Count != 1)
            {
                throw new ArgumentException("Scheduled direct-add options require exactly one offering.", nameof(selection));
            }

            return;
        }

        if (scheduleStatus == EMeetingScheduleStatus.NotProvided && selection.Kind == EPlanningCourseSelectionKind.TimeNotProvidedOffering)
        {
            return;
        }

        throw new ArgumentException("Direct-add option status must match its planning selection.", nameof(selection));
    }
}
