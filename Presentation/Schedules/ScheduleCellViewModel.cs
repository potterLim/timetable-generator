using System;
using CoreClassroomAssignment = TimetableGenerator.Core.Domain.ClassroomAssignment;
using CoreCourseOffering = TimetableGenerator.Core.Domain.CourseOffering;
using CoreScheduleSlot = TimetableGenerator.Core.Domain.ScheduleSlot;

namespace TimetableGenerator.Presentation.Schedules;

public sealed class ScheduleCellViewModel
{
    private readonly CoreCourseOffering mCourseOfferingOrNull;

    public CoreScheduleSlot ScheduleSlot { get; }

    public bool HasCourseOffering
    {
        get
        {
            return mCourseOfferingOrNull != null;
        }
    }

    public string CourseDisplayName { get; }

    public CoreClassroomAssignment ClassroomAssignment { get; }

    public bool HasClassroom
    {
        get
        {
            return HasCourseOffering && ClassroomAssignment.IsAssigned;
        }
    }

    private ScheduleCellViewModel(CoreScheduleSlot scheduleSlot)
    {
        if (scheduleSlot.IsValid == false)
        {
            throw new ArgumentException("Schedule cells require a valid schedule slot.", nameof(scheduleSlot));
        }

        ScheduleSlot = scheduleSlot;
        mCourseOfferingOrNull = null;
        CourseDisplayName = string.Empty;
        ClassroomAssignment = CoreClassroomAssignment.Unassigned;
    }

    private ScheduleCellViewModel(
        CoreScheduleSlot scheduleSlot,
        CoreCourseOffering courseOffering)
    {
        if (scheduleSlot.IsValid == false)
        {
            throw new ArgumentException("Schedule cells require a valid schedule slot.", nameof(scheduleSlot));
        }

        if (courseOffering == null)
        {
            throw new ArgumentNullException(nameof(courseOffering));
        }

        if (containsScheduleSlot(courseOffering, scheduleSlot) == false)
        {
            throw new ArgumentException("The course offering does not occupy the schedule cell.", nameof(courseOffering));
        }

        ScheduleSlot = scheduleSlot;
        mCourseOfferingOrNull = courseOffering;
        CourseDisplayName = buildCourseDisplayName(courseOffering);
        ClassroomAssignment = courseOffering.ClassroomAssignment;
    }

    internal static ScheduleCellViewModel createEmpty(CoreScheduleSlot scheduleSlot)
    {
        return new ScheduleCellViewModel(scheduleSlot);
    }

    internal static ScheduleCellViewModel createScheduled(
        CoreScheduleSlot scheduleSlot,
        CoreCourseOffering courseOffering)
    {
        return new ScheduleCellViewModel(scheduleSlot, courseOffering);
    }

    public CoreCourseOffering GetCourseOffering()
    {
        if (HasCourseOffering == false)
        {
            throw new InvalidOperationException("An empty schedule cell does not contain a course offering.");
        }

        return mCourseOfferingOrNull;
    }

    public string GetClassroomDisplayText()
    {
        if (HasClassroom == false)
        {
            return string.Empty;
        }

        return ClassroomAssignment.GetClassroomLocation().ToDisplayText();
    }

    private static bool containsScheduleSlot(
        CoreCourseOffering courseOffering,
        CoreScheduleSlot scheduleSlot)
    {
        foreach (CoreScheduleSlot occupiedScheduleSlot in courseOffering.ScheduleSlots)
        {
            if (occupiedScheduleSlot == scheduleSlot)
            {
                return true;
            }
        }

        return false;
    }

    private static string buildCourseDisplayName(CoreCourseOffering courseOffering)
    {
        if (courseOffering.SectionCode.IsDefault)
        {
            return courseOffering.Name.Value;
        }

        return courseOffering.Name.Value + " (" + courseOffering.SectionCode.Value + ")";
    }
}
