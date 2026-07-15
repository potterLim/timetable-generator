using System;
using System.Collections.Generic;
using System.Threading;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Core.Application.Scheduling;

public sealed class ScheduleGenerator
{
    public ScheduleGenerationResult GenerateSchedules(
        IReadOnlyList<CourseOffering> courseOfferings,
        CancellationToken cancellationToken)
    {
        ScheduleGenerationOptions options = ScheduleGenerationOptions.CreateDefault();
        return GenerateSchedules(courseOfferings, options, cancellationToken);
    }

    public ScheduleGenerationResult GenerateSchedules(
        IReadOnlyList<CourseOffering> courseOfferings,
        ScheduleGenerationOptions options,
        CancellationToken cancellationToken)
    {
        if (courseOfferings == null)
        {
            throw new ArgumentNullException(nameof(courseOfferings));
        }

        if (courseOfferings.Count == 0)
        {
            throw new ArgumentException("At least one course offering is required.", nameof(courseOfferings));
        }

        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        List<CourseOffering> copiedCourseOfferings = copyAndValidateCourseOfferings(courseOfferings);
        IReadOnlyList<CourseChoiceGroup> courseChoiceGroups = buildCourseChoiceGroups(copiedCourseOfferings);
        ScheduleGenerationState state = new ScheduleGenerationState(
            courseChoiceGroups,
            options,
            cancellationToken);

        generateSchedulesRecursive(state, 0);

        return new ScheduleGenerationResult(state.GeneratedSchedules, state.Completion);
    }

    private static List<CourseOffering> copyAndValidateCourseOfferings(
        IReadOnlyList<CourseOffering> courseOfferings)
    {
        List<CourseOffering> copiedCourseOfferings = new List<CourseOffering>(courseOfferings.Count);

        foreach (CourseOffering courseOffering in courseOfferings)
        {
            if (courseOffering == null)
            {
                throw new ArgumentException("Course offering collections cannot contain null values.", nameof(courseOfferings));
            }

            if (courseOffering.ChoiceGroupId.IsValid == false)
            {
                throw new ArgumentException("Course offerings require valid choice group IDs.", nameof(courseOfferings));
            }

            copiedCourseOfferings.Add(courseOffering);
        }

        return copiedCourseOfferings;
    }

    private static IReadOnlyList<CourseChoiceGroup> buildCourseChoiceGroups(
        IReadOnlyList<CourseOffering> courseOfferings)
    {
        Dictionary<CourseChoiceGroupId, List<CourseOffering>> courseOfferingsByChoiceGroupId =
            new Dictionary<CourseChoiceGroupId, List<CourseOffering>>();
        List<CourseChoiceGroupId> choiceGroupIdsInInputOrder = new List<CourseChoiceGroupId>();

        foreach (CourseOffering courseOffering in courseOfferings)
        {
            List<CourseOffering>? groupedCourseOfferingsOrNull;
            bool hasChoiceGroup = courseOfferingsByChoiceGroupId.TryGetValue(
                courseOffering.ChoiceGroupId,
                out groupedCourseOfferingsOrNull);

            if (hasChoiceGroup == false)
            {
                groupedCourseOfferingsOrNull = new List<CourseOffering>();
                courseOfferingsByChoiceGroupId.Add(
                    courseOffering.ChoiceGroupId,
                    groupedCourseOfferingsOrNull);
                choiceGroupIdsInInputOrder.Add(courseOffering.ChoiceGroupId);
            }

            if (groupedCourseOfferingsOrNull == null)
            {
                throw new InvalidOperationException(
                    "A registered course choice group did not contain an offering collection.");
            }

            groupedCourseOfferingsOrNull.Add(courseOffering);
        }

        List<CourseChoiceGroup> courseChoiceGroups = new List<CourseChoiceGroup>(
            choiceGroupIdsInInputOrder.Count);

        foreach (CourseChoiceGroupId choiceGroupId in choiceGroupIdsInInputOrder)
        {
            CourseChoiceGroup courseChoiceGroup = new CourseChoiceGroup(
                choiceGroupId,
                courseOfferingsByChoiceGroupId[choiceGroupId]);
            courseChoiceGroups.Add(courseChoiceGroup);
        }

        return courseChoiceGroups.AsReadOnly();
    }

    private static EGenerationTraversalDecision generateSchedulesRecursive(
        ScheduleGenerationState state,
        int choiceGroupIndex)
    {
        if (state.ShouldStop)
        {
            return EGenerationTraversalDecision.Stop;
        }

        if (state.CancellationToken.IsCancellationRequested)
        {
            state.MarkCanceled();
            return EGenerationTraversalDecision.Stop;
        }

        if (choiceGroupIndex >= state.CourseChoiceGroups.Count)
        {
            return addCompletedSchedule(state);
        }

        CourseChoiceGroup courseChoiceGroup = state.CourseChoiceGroups[choiceGroupIndex];
        foreach (CourseOffering courseOffering in courseChoiceGroup.CourseOfferings)
        {
            if (state.CancellationToken.IsCancellationRequested)
            {
                state.MarkCanceled();
                return EGenerationTraversalDecision.Stop;
            }

            if (canAddCourseOffering(state, courseOffering) == false)
            {
                continue;
            }

            addCourseOffering(state, courseOffering);
            try
            {
                EGenerationTraversalDecision traversalDecision =
                    generateSchedulesRecursive(state, choiceGroupIndex + 1);
                if (traversalDecision == EGenerationTraversalDecision.Stop)
                {
                    return EGenerationTraversalDecision.Stop;
                }
            }
            finally
            {
                removeCourseOffering(state, courseOffering);
            }
        }

        return EGenerationTraversalDecision.Continue;
    }

    private static EGenerationTraversalDecision addCompletedSchedule(
        ScheduleGenerationState state)
    {
        if (state.GeneratedSchedules.Count >= state.Options.MaximumScheduleCount.Value)
        {
            state.MarkMaximumScheduleCountReached();
            return EGenerationTraversalDecision.Stop;
        }

        GeneratedSchedule generatedSchedule = new GeneratedSchedule(state.SelectedCourseOfferings);
        state.GeneratedSchedules.Add(generatedSchedule);
        return EGenerationTraversalDecision.Continue;
    }

    private static bool canAddCourseOffering(
        ScheduleGenerationState state,
        CourseOffering courseOffering)
    {
        foreach (ScheduleSlot scheduleSlot in courseOffering.ScheduleSlots)
        {
            if (state.OccupiedScheduleSlots.Contains(scheduleSlot))
            {
                return false;
            }
        }

        return true;
    }

    private static void addCourseOffering(
        ScheduleGenerationState state,
        CourseOffering courseOffering)
    {
        state.SelectedCourseOfferings.Add(courseOffering);

        foreach (ScheduleSlot scheduleSlot in courseOffering.ScheduleSlots)
        {
            bool hasAddedScheduleSlot = state.OccupiedScheduleSlots.Add(scheduleSlot);
            if (hasAddedScheduleSlot == false)
            {
                throw new InvalidOperationException("A previously validated schedule slot could not be reserved.");
            }
        }
    }

    private static void removeCourseOffering(
        ScheduleGenerationState state,
        CourseOffering courseOffering)
    {
        int selectedCourseOfferingIndex = state.SelectedCourseOfferings.Count - 1;
        CourseOffering selectedCourseOffering = state.SelectedCourseOfferings[selectedCourseOfferingIndex];
        if (ReferenceEquals(selectedCourseOffering, courseOffering) == false)
        {
            throw new InvalidOperationException("The schedule generation rollback order was corrupted.");
        }

        state.SelectedCourseOfferings.RemoveAt(selectedCourseOfferingIndex);

        foreach (ScheduleSlot scheduleSlot in courseOffering.ScheduleSlots)
        {
            bool hasRemovedScheduleSlot = state.OccupiedScheduleSlots.Remove(scheduleSlot);
            if (hasRemovedScheduleSlot == false)
            {
                throw new InvalidOperationException("A reserved schedule slot could not be released.");
            }
        }
    }
}
