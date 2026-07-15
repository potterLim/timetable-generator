using System;
using System.Collections.Generic;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Core.Application.Scheduling;

internal sealed class CourseChoiceGroup
{
    public CourseChoiceGroupId ChoiceGroupId { get; }

    private readonly IReadOnlyList<CourseOffering> mCourseOfferings;

    public IReadOnlyList<CourseOffering> CourseOfferings
    {
        get
        {
            return mCourseOfferings;
        }
    }

    public CourseChoiceGroup(
        CourseChoiceGroupId choiceGroupId,
        IEnumerable<CourseOffering> courseOfferings)
    {
        if (choiceGroupId.IsValid == false)
        {
            throw new ArgumentException("Course choice groups require a valid ID.", nameof(choiceGroupId));
        }

        if (courseOfferings == null)
        {
            throw new ArgumentNullException(nameof(courseOfferings));
        }

        List<CourseOffering> copiedCourseOfferings = new List<CourseOffering>();
        foreach (CourseOffering courseOffering in courseOfferings)
        {
            if (courseOffering == null)
            {
                throw new ArgumentException("Course choice groups cannot contain null offerings.", nameof(courseOfferings));
            }

            if (courseOffering.ChoiceGroupId != choiceGroupId)
            {
                throw new ArgumentException("Every offering must belong to the course choice group.", nameof(courseOfferings));
            }

            copiedCourseOfferings.Add(courseOffering);
        }

        if (copiedCourseOfferings.Count == 0)
        {
            throw new ArgumentException("Course choice groups require at least one offering.", nameof(courseOfferings));
        }

        ChoiceGroupId = choiceGroupId;
        mCourseOfferings = copiedCourseOfferings.AsReadOnly();
    }
}
