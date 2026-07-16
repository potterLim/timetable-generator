using TimetableGenerator.Desktop.Presentation;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseSearchItem : ObservableObject
{
    private bool mIsAdded;

    public CourseId CourseId { get; }

    public string Code { get; }

    public string Name { get; }

    public string InstructorDisplayText { get; }

    public CreditCount Credits { get; }

    public string CreditDisplayText
    {
        get
        {
            return Credits.Value + "학점";
        }
    }

    public string MeetingDisplayText { get; }

    public string LocationDisplayText { get; }

    public ECourseDepartmentFilter Department { get; }

    public ERequirementFilter Requirement { get; }

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

    public bool IsAdded
    {
        get
        {
            return mIsAdded;
        }
    }

    public string AddButtonAccessibleName
    {
        get
        {
            if (IsAdded)
            {
                return Name + "은 현재 계획에 추가되어 있습니다.";
            }

            return Name + "을 현재 계획에 추가";
        }
    }

    public CourseSearchItem(
        CourseId courseId,
        string code,
        string name,
        string instructorDisplayText,
        CreditCount credits,
        string meetingDisplayText,
        string locationDisplayText,
        ECourseDepartmentFilter department,
        ERequirementFilter requirement,
        ECourseAccent accent,
        EMeetingScheduleStatus scheduleStatus)
    {
        CourseId = courseId;
        Code = code;
        Name = name;
        InstructorDisplayText = instructorDisplayText;
        Credits = credits;
        MeetingDisplayText = meetingDisplayText;
        LocationDisplayText = locationDisplayText;
        Department = department;
        Requirement = requirement;
        Accent = accent;
        ScheduleStatus = scheduleStatus;
    }

    public void MarkAdded()
    {
        setSelectionState(ESelectionState.Selected);
    }

    public void MarkRemoved()
    {
        setSelectionState(ESelectionState.NotSelected);
    }

    public PlanCourseItem CreatePlanCourseItem()
    {
        return new PlanCourseItem(
            CourseId,
            Code,
            Name,
            InstructorDisplayText,
            Credits,
            MeetingDisplayText,
            LocationDisplayText,
            Accent,
            ScheduleStatus);
    }

    private void setSelectionState(ESelectionState selectionState)
    {
        bool isAdded = selectionState == ESelectionState.Selected;
        if (setProperty(ref mIsAdded, isAdded, nameof(IsAdded)))
        {
            raisePropertyChanged(nameof(AddButtonAccessibleName));
        }
    }
}
