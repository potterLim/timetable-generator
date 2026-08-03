using System;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.Calendar;

public sealed class ScheduleCalendarProjectorTests
{
    private static readonly InstitutionName INSTITUTION_NAME = new InstitutionName("한동대학교");

    private static readonly ScheduleInstructorSummary CONFIRMED_INSTRUCTOR = new ScheduleInstructorSummary(InstructorAssignmentMetadata.CreateConfirmed(new InstructorDisplayText("김민수"), new AdditionalInstructorCount(0)));

    private static readonly ScheduleLocationSummary ASSIGNED_LOCATION = new ScheduleLocationSummary(LocationAssignmentMetadata.CreateAssigned(new ClassroomDisplayText("NTH 311")));

    [Fact]
    public void CourseEntriesAtTheSameTimeAreGroupedAcrossDays()
    {
        PlanId planId = new PlanId(new Guid("11111111-1111-1111-1111-111111111111"));
        CourseId courseId = new CourseId("course-1");
        OfferingId offeringId = new OfferingId("offering-1");
        ScheduleRecommendation displayedSchedule = new ScheduleRecommendation(
            new ScheduleEntry[]
            {
                createCourseEntry(
                    courseId,
                    offeringId,
                    EDay.Monday,
                    new AcademicPeriod(3)),
                createCourseEntry(
                    courseId,
                    offeringId,
                    EDay.Thursday,
                    new AcademicPeriod(3)),
            });

        CalendarExportDocument document = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            planId,
            new PlanName("2026-2학기 시간표"),
            INSTITUTION_NAME,
            displayedSchedule,
            getAcademicCalendar());

        RecurringCalendarEvent calendarEvent = Assert.Single(document.Events);
        Assert.Equal(new EDay[] { EDay.Monday, EDay.Thursday }, calendarEvent.Days);
        Assert.Equal("전자기학", calendarEvent.Content.Summary);
        Assert.Equal("NTH 311", calendarEvent.Content.Location);
        Assert.Equal("과목 코드: ECE20061\n분반: 01\n교수: 김민수", calendarEvent.Content.Description);
        Assert.Equal(64, calendarEvent.Uid.Value.Length);
        foreach (char character in calendarEvent.Uid.Value)
        {
            bool isLowercaseHexadecimal = character >= '0' && character <= '9' || character >= 'a' && character <= 'f';
            Assert.True(isLowercaseHexadecimal);
        }
    }

    [Fact]
    public void CourseTitleUsesNoSpaceSectionSuffixOnlyForAppleCalendar()
    {
        ScheduleRecommendation displayedSchedule = new ScheduleRecommendation(
            new ScheduleEntry[]
            {
                createCourseEntry(
                    new CourseId("course-1"),
                    new OfferingId("offering-1"),
                    EDay.Monday,
                    new AcademicPeriod(3)),
            });
        PlanId planId = PlanId.CreateNew();
        PlanName planName = new PlanName("시간표");
        AcademicTermCalendarMetadata academicCalendar = getAcademicCalendar();

        CalendarExportDocument appleDocument = ScheduleCalendarProjector.ProjectForAppleCalendar(planId, planName, INSTITUTION_NAME, displayedSchedule, academicCalendar);
        CalendarExportDocument googleDocument = ScheduleCalendarProjector.ProjectForGoogleCalendar(planId, planName, INSTITUTION_NAME, displayedSchedule, academicCalendar);
        GoogleCalendarExportPlan googleExportPlan = GoogleCalendarExportPlan.CreateFromDocument(googleDocument);

        RecurringCalendarEvent appleEvent = Assert.Single(appleDocument.Events);
        Assert.Equal("전자기학(01)", appleEvent.Content.Summary);
        GoogleCalendarExportEvent googleEvent = Assert.Single(googleExportPlan.Events);
        Assert.Equal("전자기학", googleEvent.Title);
    }

    [Fact]
    public void SamePeriodOnWednesdayAndARegularDayCreatesSeparateEvents()
    {
        CourseId courseId = new CourseId("course-1");
        OfferingId offeringId = new OfferingId("offering-1");
        ScheduleRecommendation displayedSchedule = new ScheduleRecommendation(
            new ScheduleEntry[]
            {
                createCourseEntry(
                    courseId,
                    offeringId,
                    EDay.Wednesday,
                    new AcademicPeriod(2)),
                createCourseEntry(
                    courseId,
                    offeringId,
                    EDay.Friday,
                    new AcademicPeriod(2)),
            });

        CalendarExportDocument document = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            PlanId.CreateNew(),
            new PlanName("시간표"),
            INSTITUTION_NAME,
            displayedSchedule,
            getAcademicCalendar());

        Assert.Collection(
            document.Events,
            calendarEvent =>
            {
                Assert.Equal(new EDay[] { EDay.Wednesday }, calendarEvent.Days);
                Assert.Equal(new DailyTimeRange(new ScheduleTime(10, 0), new ScheduleTime(11, 15)), calendarEvent.TimeRange);
            },
            calendarEvent =>
            {
                Assert.Equal(new EDay[] { EDay.Friday }, calendarEvent.Days);
                Assert.Equal(new DailyTimeRange(new ScheduleTime(10, 30), new ScheduleTime(11, 45)), calendarEvent.TimeRange);
            });
        Assert.NotEqual(document.Events[0].Uid, document.Events[1].Uid);
    }

    [Fact]
    public void SameOfferingAtDifferentTimesCreatesSeparateEvents()
    {
        CourseId courseId = new CourseId("course-1");
        OfferingId offeringId = new OfferingId("offering-1");
        ScheduleRecommendation displayedSchedule = new ScheduleRecommendation(
            new ScheduleEntry[]
            {
                createCourseEntry(
                    courseId,
                    offeringId,
                    EDay.Monday,
                    new AcademicPeriod(3)),
                createCourseEntry(
                    courseId,
                    offeringId,
                    EDay.Thursday,
                    new AcademicPeriod(4)),
            });

        CalendarExportDocument document = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            PlanId.CreateNew(),
            new PlanName("시간표"),
            INSTITUTION_NAME,
            displayedSchedule,
            getAcademicCalendar());

        Assert.Equal(2, document.Events.Count);
        Assert.NotEqual(document.Events[0].Uid, document.Events[1].Uid);
    }

    [Fact]
    public void PersonalScheduleMapsOptionalDetailsWithoutCourseMetadata()
    {
        PersonalScheduleId scheduleId = new PersonalScheduleId(new Guid("22222222-2222-2222-2222-222222222222"));
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(12, 0), new ScheduleTime(13, 0));
        WeeklyTimeRange mondayRange = new WeeklyTimeRange(EDay.Monday, timeRange);
        WeeklyTimeRange fridayRange = new WeeklyTimeRange(EDay.Friday, timeRange);
        PersonalSchedule personalSchedule = new PersonalSchedule(
            scheduleId,
            new PersonalScheduleTitle("랩 미팅"),
            new WeeklyTimeRange[] { mondayRange, fridayRange },
            new PersonalScheduleDetails(new PersonalScheduleSection("A"), new PersonalScheduleInstructor("박교수"), new PersonalScheduleLocation("OH 401")));
        ScheduleRecommendation displayedSchedule = new ScheduleRecommendation(
            new ScheduleEntry[]
            {
                new PersonalScheduleEntry(personalSchedule, mondayRange),
                new PersonalScheduleEntry(personalSchedule, fridayRange),
            });

        CalendarExportDocument document = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            PlanId.CreateNew(),
            new PlanName("연구 일정"),
            INSTITUTION_NAME,
            displayedSchedule,
            getAcademicCalendar());

        RecurringCalendarEvent calendarEvent = Assert.Single(document.Events);
        Assert.Equal("랩 미팅", calendarEvent.Content.Summary);
        Assert.Equal("OH 401", calendarEvent.Content.Location);
        Assert.Equal("분반: A\n담당: 박교수", calendarEvent.Content.Description);
        Assert.Equal(new EDay[] { EDay.Monday, EDay.Friday }, calendarEvent.Days);
    }

    [Fact]
    public void StableUidDependsOnPlanItemAndTimeInsteadOfInputOrder()
    {
        PlanId planId = new PlanId(new Guid("33333333-3333-3333-3333-333333333333"));
        CourseId courseId = new CourseId("course-1");
        OfferingId offeringId = new OfferingId("offering-1");
        CourseScheduleEntry mondayEntry = createCourseEntry(courseId, offeringId, EDay.Monday, new AcademicPeriod(3));
        CourseScheduleEntry thursdayEntry = createCourseEntry(courseId, offeringId, EDay.Thursday, new AcademicPeriod(3));

        CalendarExportDocument firstDocument = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            planId,
            new PlanName("이름 변경 전"),
            INSTITUTION_NAME,
            new ScheduleRecommendation(
                new ScheduleEntry[] { mondayEntry, thursdayEntry }),
            getAcademicCalendar());
        CalendarExportDocument secondDocument = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            planId,
            new PlanName("이름 변경 후"),
            INSTITUTION_NAME,
            new ScheduleRecommendation(
                new ScheduleEntry[] { thursdayEntry, mondayEntry }),
            getAcademicCalendar());

        Assert.Equal(Assert.Single(firstDocument.Events).Uid, Assert.Single(secondDocument.Events).Uid);
    }

    [Fact]
    public void StableUidChangesWhenTheMeetingTimeChanges()
    {
        PlanId planId = new PlanId(new Guid("44444444-4444-4444-4444-444444444444"));
        CourseId courseId = new CourseId("course-1");
        OfferingId offeringId = new OfferingId("offering-1");

        CalendarExportDocument thirdPeriodDocument = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            planId,
            new PlanName("시간표"),
            INSTITUTION_NAME,
            new ScheduleRecommendation(
                new ScheduleEntry[]
                {
                    createCourseEntry(
                        courseId,
                        offeringId,
                        EDay.Monday,
                        new AcademicPeriod(3)),
                }),
            getAcademicCalendar());
        CalendarExportDocument fourthPeriodDocument = ScheduleCalendarProjector.ProjectForGoogleCalendar(
            planId,
            new PlanName("시간표"),
            INSTITUTION_NAME,
            new ScheduleRecommendation(
                new ScheduleEntry[]
                {
                    createCourseEntry(
                        courseId,
                        offeringId,
                        EDay.Monday,
                        new AcademicPeriod(4)),
                }),
            getAcademicCalendar());

        Assert.NotEqual(Assert.Single(thirdPeriodDocument.Events).Uid, Assert.Single(fourthPeriodDocument.Events).Uid);
    }

    [Fact]
    public void DuplicateWeekdayForTheSameSourceAndTimeIsRejected()
    {
        CourseId courseId = new CourseId("course-1");
        OfferingId offeringId = new OfferingId("offering-1");
        CourseScheduleEntry entry = createCourseEntry(courseId, offeringId, EDay.Monday, new AcademicPeriod(3));
        ScheduleRecommendation displayedSchedule = new ScheduleRecommendation(new ScheduleEntry[] { entry, entry });

        Assert.Throws<ArgumentException>(
            () => ScheduleCalendarProjector.ProjectForGoogleCalendar(
                PlanId.CreateNew(),
                new PlanName("시간표"),
                INSTITUTION_NAME,
                displayedSchedule,
                getAcademicCalendar()));
    }

    private static CourseScheduleEntry createCourseEntry(CourseId courseId, OfferingId offeringId, EDay day, AcademicPeriod period)
    {
        return new CourseScheduleEntry(
            courseId,
            offeringId,
            new ScheduleCourseDetails(
                new CourseCode("ECE20061"),
                new KoreanCourseName("전자기학"),
                new CourseCredits(3m),
                CONFIRMED_INSTRUCTOR,
                ASSIGNED_LOCATION),
            new CourseSectionCode("01"),
            new MeetingSlot(day, period),
            ECourseAccent.Green);
    }

    private static AcademicTermCalendarMetadata getAcademicCalendar()
    {
        return AcademicTermCalendarMetadataRegistry.findByTerm(AcademicTerm.Parse("2026-2"), new CalendarTimeZoneId("Asia/Seoul"));
    }
}
