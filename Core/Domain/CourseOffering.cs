using System;
using System.Collections.Generic;

namespace TimetableGenerator.Core.Domain;

public sealed class CourseOffering
{
    public CourseChoiceGroupId ChoiceGroupId { get; }

    public CourseName Name { get; }

    public CourseSectionCode SectionCode { get; }

    public ClassroomAssignment ClassroomAssignment { get; }

    private readonly IReadOnlyList<ScheduleSlot> mScheduleSlots;

    public IReadOnlyList<ScheduleSlot> ScheduleSlots
    {
        get
        {
            return mScheduleSlots;
        }
    }

    public CourseOffering(
        CourseChoiceGroupId choiceGroupId,
        CourseName name,
        CourseSectionCode sectionCode,
        IEnumerable<ScheduleSlot> scheduleSlots)
        : this(
            choiceGroupId,
            name,
            sectionCode,
            ClassroomAssignment.Unassigned,
            scheduleSlots)
    {
    }

    public CourseOffering(
        CourseChoiceGroupId choiceGroupId,
        CourseName name,
        CourseSectionCode sectionCode,
        ClassroomAssignment classroomAssignment,
        IEnumerable<ScheduleSlot> scheduleSlots)
    {
        if (choiceGroupId.IsValid == false)
        {
            throw new ArgumentException("Course offerings require a valid choice group ID.", nameof(choiceGroupId));
        }

        if (name == null)
        {
            throw new ArgumentNullException(nameof(name));
        }

        if (sectionCode == null)
        {
            throw new ArgumentNullException(nameof(sectionCode));
        }

        if (scheduleSlots == null)
        {
            throw new ArgumentNullException(nameof(scheduleSlots));
        }

        List<ScheduleSlot> copiedScheduleSlots = new List<ScheduleSlot>();
        HashSet<ScheduleSlot> uniqueScheduleSlots = new HashSet<ScheduleSlot>();

        foreach (ScheduleSlot scheduleSlot in scheduleSlots)
        {
            if (scheduleSlot.IsValid == false)
            {
                throw new ArgumentException("Course offerings cannot contain invalid schedule slots.", nameof(scheduleSlots));
            }

            if (uniqueScheduleSlots.Add(scheduleSlot) == false)
            {
                throw new ArgumentException("Course offerings cannot contain duplicate schedule slots.", nameof(scheduleSlots));
            }

            copiedScheduleSlots.Add(scheduleSlot);
        }

        if (copiedScheduleSlots.Count == 0)
        {
            throw new ArgumentException("Course offerings require at least one schedule slot.", nameof(scheduleSlots));
        }

        ChoiceGroupId = choiceGroupId;
        Name = name;
        SectionCode = sectionCode;
        ClassroomAssignment = classroomAssignment;
        mScheduleSlots = copiedScheduleSlots.AsReadOnly();
    }
}
