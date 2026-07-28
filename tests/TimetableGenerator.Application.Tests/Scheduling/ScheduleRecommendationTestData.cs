using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Application.Tests.Scheduling;

internal static class ScheduleRecommendationTestData
{
    public static CatalogCourse CreateCourse(string courseCodeValue)
    {
        return new CatalogCourse(
            CreateCourseId(courseCodeValue),
            new CourseCode(courseCodeValue),
            new KoreanCourseName("과목 " + courseCodeValue),
            new EnglishCourseName("Course " + courseCodeValue),
            new CourseCredits(3m));
    }

    public static CatalogOffering CreateScheduledOffering(string courseCodeValue, string sectionCodeValue, IEnumerable<MeetingSlot> meetingSlots)
    {
        return new CatalogOffering(CreateOfferingId(courseCodeValue, sectionCodeValue), CreateCourseId(courseCodeValue), new CourseSectionCode(sectionCodeValue), MeetingSchedule.CreateScheduled(meetingSlots));
    }

    public static CatalogOffering CreateUnscheduledOffering(string courseCodeValue, string sectionCodeValue)
    {
        return new CatalogOffering(CreateOfferingId(courseCodeValue, sectionCodeValue), CreateCourseId(courseCodeValue), new CourseSectionCode(sectionCodeValue), MeetingSchedule.NotProvided);
    }

    public static CourseCatalog CreateCatalog(IEnumerable<CatalogCourse> courses, IEnumerable<CatalogOffering> offerings)
    {
        return new CourseCatalog(
            new CatalogId("handong-global-university:2026-2:r0001"),
            new InstitutionId("handong-global-university"),
            new InstitutionName("한동대학교"),
            AcademicTerm.Parse("2026-2"),
            new CatalogRevision(1),
            courses,
            offerings);
    }

    public static PlanningPlan CreatePlan(CourseCatalog catalog, IEnumerable<CourseChoiceGroup> courseChoiceGroups, IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        return CreatePlan(catalog, courseChoiceGroups, unscheduledSelections, Array.Empty<PersonalSchedule>());
    }

    public static PlanningPlan CreatePlan(CourseCatalog catalog, IEnumerable<CourseChoiceGroup> courseChoiceGroups, IEnumerable<UnscheduledOfferingSelection> unscheduledSelections, IEnumerable<PersonalSchedule> personalSchedules)
    {
        PlanCatalogBinding catalogBinding = new PlanCatalogBinding(
            catalog.Id,
            catalog.InstitutionId,
            catalog.Term,
            catalog.Revision,
            new CatalogArtifactSha256(new string('a', 64)));
        return new PlanningPlan(PlanId.CreateNew(), new PlanName("기본 시간표"), catalogBinding, new PlanningPlanContent(courseChoiceGroups, unscheduledSelections, personalSchedules));
    }

    public static PlanningPlan CreatePlanWithBinding(PlanCatalogBinding catalogBinding, IEnumerable<CourseChoiceGroup> courseChoiceGroups, IEnumerable<UnscheduledOfferingSelection> unscheduledSelections)
    {
        return new PlanningPlan(PlanId.CreateNew(), new PlanName("기본 시간표"), catalogBinding, new PlanningPlanContent(courseChoiceGroups, unscheduledSelections, Array.Empty<PersonalSchedule>()));
    }

    public static CourseCandidate CreateCourseCandidate(string courseCodeValue, EOfferingPreference preference, params string[] sectionCodeValues)
    {
        List<OfferingCandidate> offeringCandidates = new List<OfferingCandidate>();
        foreach (string sectionCodeValue in sectionCodeValues)
        {
            offeringCandidates.Add(new OfferingCandidate(CreateOfferingId(courseCodeValue, sectionCodeValue), preference));
        }

        return new CourseCandidate(CreateCourseId(courseCodeValue), offeringCandidates);
    }

    public static CourseChoiceGroup CreateCourseChoiceGroup(string courseCodeValue, params string[] sectionCodeValues)
    {
        List<OfferingId> offeringIds = new List<OfferingId>();
        foreach (string sectionCodeValue in sectionCodeValues)
        {
            offeringIds.Add(CreateOfferingId(courseCodeValue, sectionCodeValue));
        }

        return CourseChoiceGroup.CreateWithAcceptableOfferings(CourseChoiceGroupId.CreateNew(), CreateCourseId(courseCodeValue), offeringIds);
    }

    public static CourseChoiceGroup CreateCourseChoiceGroupFromCandidates(params CourseCandidate[] courseCandidates)
    {
        return new CourseChoiceGroup(CourseChoiceGroupId.CreateNew(), ECourseChoiceCardinality.ExactlyOne, courseCandidates);
    }

    public static UnscheduledOfferingSelection CreateUnscheduledSelection(string courseCodeValue, string sectionCodeValue)
    {
        return new UnscheduledOfferingSelection(CreateCourseId(courseCodeValue), CreateOfferingId(courseCodeValue, sectionCodeValue));
    }

    public static CourseId CreateCourseId(string courseCodeValue)
    {
        return new CourseId("handong-global-university:" + courseCodeValue);
    }

    public static OfferingId CreateOfferingId(string courseCodeValue, string sectionCodeValue)
    {
        return new OfferingId("handong-global-university:2026-2:" + courseCodeValue + ":" + sectionCodeValue);
    }

    public static MeetingSlot CreateMeetingSlot(EDay day, int periodValue)
    {
        return new MeetingSlot(day, new AcademicPeriod(periodValue));
    }
}
