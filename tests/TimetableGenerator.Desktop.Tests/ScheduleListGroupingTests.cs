using System;
using System.Collections.Generic;
using System.Linq;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class ScheduleListGroupingTests
{
    private static readonly ScheduleInstructorSummary CONFIRMED_INSTRUCTOR = new ScheduleInstructorSummary(
        InstructorAssignmentMetadata.CreateConfirmed(
            new InstructorDisplayText("김교수"),
            new AdditionalInstructorCount(0)));

    private static readonly ScheduleLocationSummary ASSIGNED_LOCATION = new ScheduleLocationSummary(LocationAssignmentMetadata.CreateAssigned(new ClassroomDisplayText("NTH 311")));

    private static readonly ScheduleInstructorSummary UNCONFIRMED_INSTRUCTOR = new ScheduleInstructorSummary(InstructorAssignmentMetadata.Unconfirmed);

    private static readonly ScheduleLocationSummary UNASSIGNED_LOCATION = new ScheduleLocationSummary(LocationAssignmentMetadata.NotProvided);

    [Fact]
    public void CourseMeetingsWithMatchingMetadataShareOneOccurrence()
    {
        CourseId courseId = new CourseId("course-electromagnetics");
        OfferingId offeringId = new OfferingId("offering-electromagnetics-01");
        CourseScheduleEntry mondayEntry = createCourseEntry(
            courseId,
            offeringId,
            new KoreanCourseName("전자기학"),
            new CourseSectionCode("01"),
            EDay.Monday,
            new AcademicPeriod(2),
            CONFIRMED_INSTRUCTOR,
            ASSIGNED_LOCATION);
        CourseScheduleEntry fridayEntry = createCourseEntry(
            courseId,
            offeringId,
            new KoreanCourseName("전자기학"),
            new CourseSectionCode("01"),
            EDay.Friday,
            new AcademicPeriod(2),
            CONFIRMED_INSTRUCTOR,
            ASSIGNED_LOCATION);

        ScheduleListGroup group = Assert.Single(ScheduleListProjector.Project(
            new ScheduleEntry[] { fridayEntry, mondayEntry }));
        ScheduleListOccurrence occurrence = Assert.Single(group.Occurrences);
        CourseScheduleListSource source = Assert.IsType<CourseScheduleListSource>(Assert.Single(group.Sources));

        Assert.Equal("전자기학", group.Title);
        Assert.Equal("전자기학(01)", group.TitleDisplayText);
        Assert.Equal(new EDay[] { EDay.Monday, EDay.Friday }, occurrence.Days);
        Assert.Equal("월·금: 10:30–11:45", occurrence.ScheduleDisplayText);
        Assert.Equal("NTH 311 · 김교수", occurrence.MetadataDisplayText);
        Assert.DoesNotContain("(01)", occurrence.MetadataDisplayText);
        Assert.Equal(courseId, source.CourseId);
        Assert.Equal(offeringId, source.OfferingId);
        Assert.Single(occurrence.Sources);
        Assert.Contains("분반 01", group.AccessibleName);
        Assert.Contains("장소 NTH 311", group.AccessibleName);
        Assert.Contains("담당 김교수", group.AccessibleName);
    }

    [Fact]
    public void SamePeriodOnWednesdayAndARegularDayKeepsSeparateOccurrences()
    {
        CourseId courseId = new CourseId("course-electromagnetics");
        OfferingId offeringId = new OfferingId("offering-electromagnetics-01");
        CourseScheduleEntry wednesdayEntry = createCourseEntry(
            courseId,
            offeringId,
            new KoreanCourseName("전자기학"),
            new CourseSectionCode("01"),
            EDay.Wednesday,
            new AcademicPeriod(2),
            CONFIRMED_INSTRUCTOR,
            ASSIGNED_LOCATION);
        CourseScheduleEntry fridayEntry = createCourseEntry(
            courseId,
            offeringId,
            new KoreanCourseName("전자기학"),
            new CourseSectionCode("01"),
            EDay.Friday,
            new AcademicPeriod(2),
            CONFIRMED_INSTRUCTOR,
            ASSIGNED_LOCATION);

        ScheduleListGroup group = Assert.Single(ScheduleListProjector.Project(
            new ScheduleEntry[] { fridayEntry, wednesdayEntry }));

        Assert.True(group.HasMultipleOccurrences);
        Assert.Collection(
            group.Occurrences,
            occurrence => Assert.Equal(
                "수: 10:00–11:15",
                occurrence.ScheduleDisplayText),
            occurrence => Assert.Equal(
                "금: 10:30–11:45",
                occurrence.ScheduleDisplayText));
    }

    [Fact]
    public void NormalizedTitleGroupsCourseAndPersonalScheduleWithoutLosingSources()
    {
        CourseId courseId = new CourseId("course-project-lab");
        OfferingId offeringId = new OfferingId("offering-project-lab-01");
        CourseScheduleEntry courseEntry = createCourseEntry(
            courseId,
            offeringId,
            new KoreanCourseName("Project　 Lab"),
            new CourseSectionCode("01"),
            EDay.Monday,
            new AcademicPeriod(2),
            CONFIRMED_INSTRUCTOR,
            ASSIGNED_LOCATION);
        PersonalScheduleId scheduleId = new PersonalScheduleId(new Guid("c142907f-09c8-44e4-a192-1f7401605e04"));
        PersonalScheduleEntry personalEntry = createPersonalEntry(
            scheduleId,
            new PersonalScheduleTitle("project lab"),
            EDay.Wednesday,
            AcademicPeriodTimeTable.GetTimeRange(courseEntry.Slot),
            new PersonalScheduleDetails(
                new PersonalScheduleSection("01"),
                new PersonalScheduleInstructor("김교수"),
                new PersonalScheduleLocation("NTH 311")));

        ScheduleListGroup group = Assert.Single(ScheduleListProjector.Project(
            new ScheduleEntry[] { personalEntry, courseEntry }));
        ScheduleListOccurrence occurrence = Assert.Single(group.Occurrences);
        CourseScheduleListSource courseSource = Assert.IsType<CourseScheduleListSource>(
            group.Sources.Single(
                source => source.Kind == EScheduleListEntryKind.Course));
        PersonalScheduleListSource personalSource =
            Assert.IsType<PersonalScheduleListSource>(
                group.Sources.Single(
                    source => source.Kind
                        == EScheduleListEntryKind.PersonalSchedule));

        Assert.Equal("Project Lab", group.Title);
        Assert.Equal("Project Lab(01)", group.TitleDisplayText);
        Assert.Equal(new EDay[] { EDay.Monday, EDay.Wednesday }, occurrence.Days);
        Assert.Equal("NTH 311 · 김교수", occurrence.MetadataDisplayText);
        Assert.Equal(courseId, courseSource.CourseId);
        Assert.Equal(offeringId, courseSource.OfferingId);
        Assert.Equal(scheduleId, personalSource.ScheduleId);
        Assert.Equal(2, occurrence.Sources.Count);
        Assert.StartsWith("과목 및 개인 일정", group.AccessibleName);
    }

    [Fact]
    public void SameTitleWithDifferentTimesOrMetadataKeepsSeparateOccurrences()
    {
        CourseScheduleEntry firstSection = createCourseEntry(
            new CourseId("course-design-a"),
            new OfferingId("offering-design-01"),
            new KoreanCourseName("제품 디자인"),
            new CourseSectionCode("01"),
            EDay.Tuesday,
            new AcademicPeriod(2),
            CONFIRMED_INSTRUCTOR,
            ASSIGNED_LOCATION);
        CourseScheduleEntry secondSection = createCourseEntry(
            new CourseId("course-design-b"),
            new OfferingId("offering-design-02"),
            new KoreanCourseName("제품 디자인"),
            new CourseSectionCode("02"),
            EDay.Thursday,
            new AcademicPeriod(3),
            CONFIRMED_INSTRUCTOR,
            new ScheduleLocationSummary(
                LocationAssignmentMetadata.CreateAssigned(
                    new ClassroomDisplayText("OH 401"))));

        ScheduleListGroup group = Assert.Single(ScheduleListProjector.Project(
            new ScheduleEntry[] { secondSection, firstSection }));

        Assert.Equal("제품 디자인", group.TitleDisplayText);
        Assert.Equal(2, group.Occurrences.Count);
        Assert.Equal("화: 10:30–11:45", group.Occurrences[0].ScheduleDisplayText);
        Assert.Equal("(01) · NTH 311 · 김교수", group.Occurrences[0].MetadataDisplayText);
        Assert.Equal("목: 12:00–13:15", group.Occurrences[1].ScheduleDisplayText);
        Assert.Equal("(02) · OH 401 · 김교수", group.Occurrences[1].MetadataDisplayText);
        Assert.Equal(2, group.Sources.Count);
    }

    [Fact]
    public void SeparatelyCreatedMatchingPersonalSchedulesKeepEveryIdentity()
    {
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(12, 0), new ScheduleTime(13, 0));
        PersonalScheduleId mondayScheduleId = new PersonalScheduleId(new Guid("ec29ea12-3130-4395-9764-13244700da45"));
        PersonalScheduleId thursdayScheduleId = new PersonalScheduleId(new Guid("4d4cf0ba-314f-4115-b032-97a57b2c1e37"));
        PersonalScheduleEntry mondayEntry = createPersonalEntry(
            mondayScheduleId,
            new PersonalScheduleTitle("랩 미팅"),
            EDay.Monday,
            timeRange,
            new PersonalScheduleDetails(
                new PersonalScheduleSection("A"),
                null,
                null));
        PersonalScheduleEntry thursdayEntry = createPersonalEntry(
            thursdayScheduleId,
            new PersonalScheduleTitle("랩 미팅"),
            EDay.Thursday,
            timeRange,
            new PersonalScheduleDetails(
                new PersonalScheduleSection("A"),
                null,
                null));

        ScheduleListGroup group = Assert.Single(ScheduleListProjector.Project(
            new ScheduleEntry[] { thursdayEntry, mondayEntry }));
        ScheduleListOccurrence occurrence = Assert.Single(group.Occurrences);
        IReadOnlyList<PersonalScheduleId> retainedScheduleIds = occurrence.Sources
            .Cast<PersonalScheduleListSource>()
            .Select(source => source.ScheduleId)
            .ToList();

        Assert.Equal("랩 미팅(A)", group.TitleDisplayText);
        Assert.Equal("월·목: 12:00–13:00", occurrence.ScheduleDisplayText);
        Assert.True(occurrence.HasSection);
        Assert.False(occurrence.HasMetadata);
        Assert.Equal(2, retainedScheduleIds.Count);
        Assert.Contains(mondayScheduleId, retainedScheduleIds);
        Assert.Contains(thursdayScheduleId, retainedScheduleIds);
    }

    [Fact]
    public void OnePersonalScheduleListsEveryMatchingSelectedDayInOneOccurrence()
    {
        DailyTimeRange timeRange = new DailyTimeRange(new ScheduleTime(18, 0), new ScheduleTime(19, 0));
        WeeklyTimeRange wednesdayRange = new WeeklyTimeRange(EDay.Wednesday, timeRange);
        WeeklyTimeRange sundayRange = new WeeklyTimeRange(EDay.Sunday, timeRange);
        PersonalScheduleId scheduleId = new PersonalScheduleId(new Guid("7e8a4f55-78eb-4e03-bf61-9db52985e845"));
        PersonalSchedule schedule = new PersonalSchedule(
            scheduleId,
            new PersonalScheduleTitle("저녁 모임"),
            new WeeklyTimeRange[] { sundayRange, wednesdayRange },
            PersonalScheduleDetails.CreateEmpty());

        ScheduleListGroup group = Assert.Single(ScheduleListProjector.Project(
            new ScheduleEntry[]
            {
                new PersonalScheduleEntry(schedule, sundayRange),
                new PersonalScheduleEntry(schedule, wednesdayRange),
            }));
        ScheduleListOccurrence occurrence = Assert.Single(group.Occurrences);
        PersonalScheduleListSource source = Assert.IsType<PersonalScheduleListSource>(Assert.Single(occurrence.Sources));

        Assert.Equal("저녁 모임", group.TitleDisplayText);
        Assert.Equal(
            new EDay[] { EDay.Wednesday, EDay.Sunday },
            occurrence.Days);
        Assert.Equal("수·일: 18:00–19:00", occurrence.ScheduleDisplayText);
        Assert.Equal(scheduleId, source.ScheduleId);
        Assert.Contains("수요일, 일요일 18:00–19:00", group.AccessibleName);
    }

    [Fact]
    public void GroupsAreSortedByEarliestDayThenTimeAndOmitUnavailableMetadata()
    {
        CourseScheduleEntry tuesdayMorning = createCourseEntry(
            new CourseId("course-tuesday"),
            new OfferingId("offering-tuesday"),
            new KoreanCourseName("화요일 수업"),
            new CourseSectionCode("01"),
            EDay.Tuesday,
            new AcademicPeriod(1),
            UNCONFIRMED_INSTRUCTOR,
            UNASSIGNED_LOCATION);
        CourseScheduleEntry mondayAfternoon = createCourseEntry(
            new CourseId("course-monday-afternoon"),
            new OfferingId("offering-monday-afternoon"),
            new KoreanCourseName("월요일 오후"),
            new CourseSectionCode("01"),
            EDay.Monday,
            new AcademicPeriod(4),
            UNCONFIRMED_INSTRUCTOR,
            UNASSIGNED_LOCATION);
        CourseScheduleEntry mondayMorning = createCourseEntry(
            new CourseId("course-monday-morning"),
            new OfferingId("offering-monday-morning"),
            new KoreanCourseName("월요일 오전"),
            new CourseSectionCode("01"),
            EDay.Monday,
            new AcademicPeriod(1),
            UNCONFIRMED_INSTRUCTOR,
            UNASSIGNED_LOCATION);

        IReadOnlyList<ScheduleListGroup> groups = ScheduleListProjector.Project(
            new ScheduleEntry[]
            {
                tuesdayMorning,
                mondayAfternoon,
                mondayMorning,
            });

        Assert.Equal(
            new string[]
            {
                "월요일 오전(01)",
                "월요일 오후(01)",
                "화요일 수업(01)",
            },
            groups.Select(group => group.TitleDisplayText));
        ScheduleListOccurrence firstOccurrence = groups[0].Occurrences[0];
        Assert.False(firstOccurrence.HasLocation);
        Assert.False(firstOccurrence.HasResponsiblePerson);
        Assert.False(firstOccurrence.HasMetadata);
        Assert.DoesNotContain("장소", groups[0].AccessibleName);
        Assert.DoesNotContain("담당", groups[0].AccessibleName);
    }

    private static CourseScheduleEntry createCourseEntry(
        CourseId courseId,
        OfferingId offeringId,
        KoreanCourseName name,
        CourseSectionCode sectionCode,
        EDay day,
        AcademicPeriod period,
        ScheduleInstructorSummary instructorSummary,
        ScheduleLocationSummary locationSummary)
    {
        return new CourseScheduleEntry(
            courseId,
            offeringId,
            new ScheduleCourseDetails(
                new CourseCode("TST00100"),
                name,
                new CourseCredits(3m),
                instructorSummary,
                locationSummary),
            sectionCode,
            new MeetingSlot(day, period),
            ECourseAccent.Blue);
    }

    private static PersonalScheduleEntry createPersonalEntry(
        PersonalScheduleId scheduleId,
        PersonalScheduleTitle title,
        EDay day,
        DailyTimeRange timeRange,
        PersonalScheduleDetails details)
    {
        WeeklyTimeRange weeklyTimeRange = new WeeklyTimeRange(day, timeRange);
        PersonalSchedule schedule = new PersonalSchedule(
            scheduleId,
            title,
            new WeeklyTimeRange[] { weeklyTimeRange },
            details);
        return new PersonalScheduleEntry(schedule, weeklyTimeRange);
    }
}
