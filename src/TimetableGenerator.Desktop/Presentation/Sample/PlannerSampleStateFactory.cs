using System.Collections.Generic;
using System.Collections.ObjectModel;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Sample;

internal static class PlannerSampleStateFactory
{
    private const string COURSE_ID_PREFIX = "handong-global-university:";

    public static PlannerWorkspaceViewModel CreateWorkspace()
    {
        IReadOnlyList<CourseSearchItem> courses = createCourses();
        IReadOnlyList<PlanTabItem> plans = createPlans(courses);
        IReadOnlyList<ScheduleRecommendation> recommendations = createRecommendations();
        return new PlannerWorkspaceViewModel(courses, plans, recommendations);
    }

    private static IReadOnlyList<CourseSearchItem> createCourses()
    {
        List<CourseSearchItem> courses = new List<CourseSearchItem>();
        courses.Add(createCourse(
            "AIE22003",
            "프로그래밍 I",
            "이성훈 교수",
            "월 3교시, 수 4교시",
            "공학관 301",
            ECourseDepartmentFilter.Computing,
            ERequirementFilter.Required,
            ECourseAccent.Blue,
            EMeetingScheduleStatus.Scheduled,
            new CourseCredits(3m)));
        courses.Add(createCourse(
            "AIE23005",
            "인공지능 윤리",
            "최은별 교수",
            "화 2교시, 목 3교시",
            "비전관 407",
            ECourseDepartmentFilter.Computing,
            ERequirementFilter.Elective,
            ECourseAccent.Purple,
            EMeetingScheduleStatus.Scheduled,
            new CourseCredits(3m)));
        courses.Add(createCourse(
            "GCS10004",
            "파이썬 프로그래밍",
            "김영미 교수",
            "월 5교시, 수 6교시",
            "공학관 305",
            ECourseDepartmentFilter.GeneralStudies,
            ERequirementFilter.Required,
            ECourseAccent.Green,
            EMeetingScheduleStatus.Scheduled,
            new CourseCredits(3m)));
        courses.Add(createCourse(
            "AIE22004",
            "인간공학",
            "박지유 교수",
            "월 5교시, 목 3교시",
            "복지관 204",
            ECourseDepartmentFilter.Computing,
            ERequirementFilter.Elective,
            ECourseAccent.Green,
            EMeetingScheduleStatus.Scheduled,
            new CourseCredits(3m)));
        courses.Add(createCourse(
            "AIE21001",
            "글로벌 기업가정신 입문",
            "김한빛 교수",
            "금 1교시",
            "국제어문학관 102",
            ECourseDepartmentFilter.Business,
            ERequirementFilter.Required,
            ECourseAccent.Blue,
            EMeetingScheduleStatus.Scheduled,
            new CourseCredits(3m)));
        courses.Add(createCourse(
            "BFT10005",
            "회계학 원론",
            "박수진 교수",
            "월 3교시, 수 3교시",
            "느헤미야홀 211",
            ECourseDepartmentFilter.Business,
            ERequirementFilter.Required,
            ECourseAccent.Blue,
            EMeetingScheduleStatus.Scheduled,
            new CourseCredits(3m)));
        courses.Add(createCourse(
            "BFT30009",
            "세미나 3",
            "이환동 교수",
            "시간 발표 대기",
            "강의실 미정",
            ECourseDepartmentFilter.Business,
            ERequirementFilter.Elective,
            ECourseAccent.Purple,
            EMeetingScheduleStatus.NotProvided,
            new CourseCredits(3m)));
        return courses.AsReadOnly();
    }

    private static CourseSearchItem createCourse(
        string code,
        string name,
        string instructorDisplayText,
        string meetingDisplayText,
        string locationDisplayText,
        ECourseDepartmentFilter department,
        ERequirementFilter requirement,
        ECourseAccent accent,
        EMeetingScheduleStatus scheduleStatus,
        CourseCredits credits)
    {
        return new CourseSearchItem(
            new CourseId(COURSE_ID_PREFIX + code),
            code,
            name,
            instructorDisplayText,
            credits,
            meetingDisplayText,
            locationDisplayText,
            department,
            requirement,
            accent,
            scheduleStatus);
    }

    private static IReadOnlyList<PlanTabItem> createPlans(IReadOnlyList<CourseSearchItem> courses)
    {
        ObservableCollection<PlanCourseItem> firstPlanCourses = new ObservableCollection<PlanCourseItem>();
        firstPlanCourses.Add(courses[4].CreatePlanCourseItem());
        firstPlanCourses.Add(courses[0].CreatePlanCourseItem());
        firstPlanCourses.Add(courses[3].CreatePlanCourseItem());
        firstPlanCourses.Add(courses[1].CreatePlanCourseItem());

        ObservableCollection<PlanCourseItem> firstPlanUnconfirmed = new ObservableCollection<PlanCourseItem>();
        firstPlanUnconfirmed.Add(courses[6].CreatePlanCourseItem());

        ObservableCollection<PlanCourseItem> secondPlanCourses = new ObservableCollection<PlanCourseItem>();
        secondPlanCourses.Add(courses[0].CreatePlanCourseItem());
        secondPlanCourses.Add(courses[2].CreatePlanCourseItem());
        secondPlanCourses.Add(courses[1].CreatePlanCourseItem());

        List<PlanTabItem> plans = new List<PlanTabItem>();
        plans.Add(new PlanTabItem(
            PlanId.CreateNew(),
            new PlanName("공강 우선"),
            firstPlanCourses,
            firstPlanUnconfirmed));
        plans.Add(new PlanTabItem(
            PlanId.CreateNew(),
            new PlanName("교수 우선"),
            secondPlanCourses,
            new ObservableCollection<PlanCourseItem>()));
        return plans.AsReadOnly();
    }

    private static IReadOnlyList<ScheduleRecommendation> createRecommendations()
    {
        List<ScheduleRecommendation> recommendations = new List<ScheduleRecommendation>();
        recommendations.Add(new ScheduleRecommendation(createPrimaryScheduleEntries()));
        recommendations.Add(new ScheduleRecommendation(createAlternativeScheduleEntries()));
        return recommendations.AsReadOnly();
    }

    private static IReadOnlyList<ScheduleEntry> createPrimaryScheduleEntries()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createEntry("AIE21001", "글로벌 기업가정신 입문", "김한빛 · 3학점", "국제어문학관 102", EDay.Monday, new AcademicPeriod(1), ECourseAccent.Blue));
        entries.Add(createEntry("AIE21001", "글로벌 기업가정신 입문", "김한빛 · 3학점", "국제어문학관 102", EDay.Friday, new AcademicPeriod(1), ECourseAccent.Blue));
        entries.Add(createEntry("AIE23005", "인공지능 윤리", "최은별 · 3학점", "비전관 407", EDay.Tuesday, new AcademicPeriod(2), ECourseAccent.Purple));
        entries.Add(createEntry("AIE22004", "인간공학", "박지유 · 3학점", "복지관 204", EDay.Thursday, new AcademicPeriod(2), ECourseAccent.Green));
        entries.Add(createEntry("AIE22003", "프로그래밍 I", "이성훈 · 3학점", "공학관 301", EDay.Wednesday, new AcademicPeriod(3), ECourseAccent.Blue));
        entries.Add(createEntry("AIE22003", "프로그래밍 I", "이성훈 · 3학점", "공학관 301", EDay.Wednesday, new AcademicPeriod(4), ECourseAccent.Blue));
        entries.Add(createEntry("AIE22004", "인간공학", "박지유 · 3학점", "복지관 204", EDay.Monday, new AcademicPeriod(5), ECourseAccent.Green));
        return entries.AsReadOnly();
    }

    private static IReadOnlyList<ScheduleEntry> createAlternativeScheduleEntries()
    {
        List<ScheduleEntry> entries = new List<ScheduleEntry>();
        entries.Add(createEntry("AIE21001", "글로벌 기업가정신 입문", "김한빛 · 3학점", "국제어문학관 102", EDay.Tuesday, new AcademicPeriod(1), ECourseAccent.Blue));
        entries.Add(createEntry("AIE23005", "인공지능 윤리", "최은별 · 3학점", "비전관 407", EDay.Thursday, new AcademicPeriod(2), ECourseAccent.Purple));
        entries.Add(createEntry("AIE22004", "인간공학", "박지유 · 3학점", "복지관 204", EDay.Friday, new AcademicPeriod(3), ECourseAccent.Green));
        entries.Add(createEntry("AIE22003", "프로그래밍 I", "이성훈 · 3학점", "공학관 301", EDay.Monday, new AcademicPeriod(4), ECourseAccent.Blue));
        entries.Add(createEntry("AIE22003", "프로그래밍 I", "이성훈 · 3학점", "공학관 301", EDay.Wednesday, new AcademicPeriod(4), ECourseAccent.Blue));
        return entries.AsReadOnly();
    }

    private static ScheduleEntry createEntry(
        string code,
        string name,
        string instructorDisplayText,
        string locationDisplayText,
        EDay day,
        AcademicPeriod period,
        ECourseAccent accent)
    {
        return new ScheduleEntry(
            code,
            name,
            instructorDisplayText,
            locationDisplayText,
            day,
            period,
            accent);
    }
}
