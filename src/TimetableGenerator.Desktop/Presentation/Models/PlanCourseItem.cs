using System;

using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanCourseItem
{
    public CourseId CourseId { get; }

    public string Code { get; }

    public string Name { get; }

    public string InstructorDisplayText { get; }

    public CourseCredits Credits { get; }

    public string CreditDisplayText
    {
        get
        {
            return Credits + "학점";
        }
    }

    public string MeetingDisplayText { get; }

    public string LocationDisplayText { get; }

    public ECourseAccent Accent { get; }

    public EMeetingScheduleStatus ScheduleStatus { get; }

    public bool HasConfirmedSchedule
    {
        get
        {
            return ScheduleStatus == EMeetingScheduleStatus.Scheduled;
        }
    }

    public bool IsBlue
    {
        get
        {
            return Accent == ECourseAccent.Blue;
        }
    }

    public bool IsPurple
    {
        get
        {
            return Accent == ECourseAccent.Purple;
        }
    }

    public bool IsGreen
    {
        get
        {
            return Accent == ECourseAccent.Green;
        }
    }

    public string RemoveButtonAccessibleName
    {
        get
        {
            return Name + "을 현재 계획에서 제거";
        }
    }

    public PlanCourseItem(
        CourseId courseId,
        string code,
        string name,
        string instructorDisplayText,
        CourseCredits credits,
        string meetingDisplayText,
        string locationDisplayText,
        ECourseAccent accent,
        EMeetingScheduleStatus scheduleStatus)
    {
        ArgumentNullException.ThrowIfNull(courseId);

        CourseId = courseId;
        Code = code;
        Name = name;
        InstructorDisplayText = instructorDisplayText;
        Credits = credits;
        MeetingDisplayText = meetingDisplayText;
        LocationDisplayText = locationDisplayText;
        Accent = accent;
        ScheduleStatus = scheduleStatus;
    }
}
