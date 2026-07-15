using System;
using System.Collections.Generic;

namespace TimetableGenerator.Core.Domain;

public sealed class GeneratedSchedule
{
    private readonly IReadOnlyList<CourseOffering> mCourseOfferings;

    public IReadOnlyList<CourseOffering> CourseOfferings
    {
        get
        {
            return mCourseOfferings;
        }
    }

    public GeneratedSchedule(IEnumerable<CourseOffering> courseOfferings)
    {
        if (courseOfferings == null)
        {
            throw new ArgumentNullException(nameof(courseOfferings));
        }

        List<CourseOffering> copiedCourseOfferings = new List<CourseOffering>();
        HashSet<CourseChoiceGroupId> selectedChoiceGroupIds = new HashSet<CourseChoiceGroupId>();
        HashSet<ScheduleSlot> occupiedScheduleSlots = new HashSet<ScheduleSlot>();

        foreach (CourseOffering courseOffering in courseOfferings)
        {
            if (courseOffering == null)
            {
                throw new ArgumentException("Generated schedules cannot contain null course offerings.", nameof(courseOfferings));
            }

            if (courseOffering.ChoiceGroupId.IsValid == false)
            {
                throw new ArgumentException("Generated schedules cannot contain invalid choice group IDs.", nameof(courseOfferings));
            }

            if (selectedChoiceGroupIds.Add(courseOffering.ChoiceGroupId) == false)
            {
                throw new ArgumentException("Generated schedules can select only one offering per choice group.", nameof(courseOfferings));
            }

            foreach (ScheduleSlot scheduleSlot in courseOffering.ScheduleSlots)
            {
                if (scheduleSlot.IsValid == false)
                {
                    throw new ArgumentException("Generated schedules cannot contain invalid schedule slots.", nameof(courseOfferings));
                }

                if (occupiedScheduleSlots.Add(scheduleSlot) == false)
                {
                    throw new ArgumentException("Generated schedules cannot contain overlapping schedule slots.", nameof(courseOfferings));
                }
            }

            copiedCourseOfferings.Add(courseOffering);
        }

        if (copiedCourseOfferings.Count == 0)
        {
            throw new ArgumentException("Generated schedules require at least one course offering.", nameof(courseOfferings));
        }

        mCourseOfferings = copiedCourseOfferings.AsReadOnly();
    }
}
