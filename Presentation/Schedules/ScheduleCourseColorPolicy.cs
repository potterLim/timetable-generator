using System;
using System.Diagnostics;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Presentation.Schedules;

internal static class ScheduleCourseColorPolicy
{
    private const int COLOR_COUNT = 3;

    internal static EScheduleCourseColor findColor(CourseChoiceGroupId choiceGroupId)
    {
        int colorIndex = (choiceGroupId.Value - 1) % COLOR_COUNT;
        switch (colorIndex)
        {
            case 0:
                return EScheduleCourseColor.Blue;
            case 1:
                return EScheduleCourseColor.Green;
            case 2:
                return EScheduleCourseColor.Purple;
            default:
                Debug.Fail("Unexpected schedule course color index: " + colorIndex);
                throw new InvalidOperationException("The schedule course color index is invalid.");
        }
    }
}
