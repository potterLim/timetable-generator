using System;
using System.Collections.Generic;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
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

    private PlanCourseItem(
        CatalogCourseProjection course,
        string instructorDisplayText,
        string meetingDisplayText,
        string locationDisplayText,
        EMeetingScheduleStatus scheduleStatus)
    {
        CourseId = course.Course.Id;
        Code = course.Course.Code.Value;
        Name = course.Course.KoreanName.Value;
        InstructorDisplayText = instructorDisplayText;
        Credits = course.Course.Credits;
        MeetingDisplayText = meetingDisplayText;
        LocationDisplayText = locationDisplayText;
        Accent = course.Accent;
        ScheduleStatus = scheduleStatus;
    }

    public static PlanCourseItem CreateScheduled(
        CatalogCourseProjection course,
        ScheduledCourseChoice choice)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (choice == null)
        {
            throw new ArgumentNullException(nameof(choice));
        }

        if (choice.CourseId != course.Course.Id)
        {
            throw new ArgumentException(
                "Scheduled plan choices must match their projected course.",
                nameof(choice));
        }

        List<CatalogOfferingProjection> offerings = findOfferings(
            course,
            choice.OfferingIds,
            EMeetingScheduleStatus.Scheduled);
        string instructorSummary = createOfferingValueSummary(
            offerings,
            EOfferingSummaryKind.Instructor);
        string locationSummary = createOfferingValueSummary(
            offerings,
            EOfferingSummaryKind.Location);
        string meetingSummary;
        if (offerings.Count == 1)
        {
            meetingSummary = offerings[0].ScheduleSummary;
        }
        else
        {
            meetingSummary = offerings.Count + "개 분반에서 충돌 없는 시간표를 자동 추천";
        }

        return new PlanCourseItem(
            course,
            instructorSummary,
            meetingSummary,
            locationSummary,
            EMeetingScheduleStatus.Scheduled);
    }

    public static PlanCourseItem CreateTimeNotProvided(
        CatalogCourseProjection course,
        UnscheduledOfferingSelection selection)
    {
        if (course == null)
        {
            throw new ArgumentNullException(nameof(course));
        }

        if (selection == null)
        {
            throw new ArgumentNullException(nameof(selection));
        }

        if (selection.CourseId != course.Course.Id)
        {
            throw new ArgumentException(
                "Time-not-provided selections must match their projected course.",
                nameof(selection));
        }

        CatalogOfferingProjection offering = findOffering(
            course,
            selection.OfferingId,
            EMeetingScheduleStatus.NotProvided);
        string meetingSummary = offering.Offering.SectionCode.Value
            + "분반 · "
            + offering.ScheduleSummary;
        return new PlanCourseItem(
            course,
            offering.InstructorSummary,
            meetingSummary,
            offering.LocationSummary,
            EMeetingScheduleStatus.NotProvided);
    }

    private static List<CatalogOfferingProjection> findOfferings(
        CatalogCourseProjection course,
        IEnumerable<OfferingId> offeringIds,
        EMeetingScheduleStatus expectedStatus)
    {
        List<CatalogOfferingProjection> offerings =
            new List<CatalogOfferingProjection>();
        foreach (OfferingId offeringId in offeringIds)
        {
            offerings.Add(findOffering(course, offeringId, expectedStatus));
        }

        if (offerings.Count == 0)
        {
            throw new ArgumentException(
                "Scheduled plan choices require at least one projected offering.",
                nameof(offeringIds));
        }

        return offerings;
    }

    private static CatalogOfferingProjection findOffering(
        CatalogCourseProjection course,
        OfferingId offeringId,
        EMeetingScheduleStatus expectedStatus)
    {
        foreach (CatalogOfferingProjection offering in course.Offerings)
        {
            if (offering.Offering.Id == offeringId)
            {
                if (offering.Offering.MeetingSchedule.Status != expectedStatus)
                {
                    throw new ArgumentException(
                        "The selected offering has an unexpected schedule status.",
                        nameof(offeringId));
                }

                return offering;
            }
        }

        throw new ArgumentException(
            "The selected offering does not belong to the projected course.",
            nameof(offeringId));
    }

    private static string createOfferingValueSummary(
        IReadOnlyList<CatalogOfferingProjection> offerings,
        EOfferingSummaryKind summaryKind)
    {
        string firstValue = findOfferingValue(offerings[0], summaryKind);
        for (int index = 1; index < offerings.Count; ++index)
        {
            string candidateValue = findOfferingValue(offerings[index], summaryKind);
            if (string.Equals(firstValue, candidateValue, StringComparison.Ordinal) == false)
            {
                return "분반별 " + findOfferingValueKindDisplayName(summaryKind);
            }
        }

        return firstValue;
    }

    private static string findOfferingValue(
        CatalogOfferingProjection offering,
        EOfferingSummaryKind summaryKind)
    {
        switch (summaryKind)
        {
            case EOfferingSummaryKind.Instructor:
                return offering.InstructorSummary;
            case EOfferingSummaryKind.Location:
                return offering.LocationSummary;
            default:
                throw new ArgumentOutOfRangeException(nameof(summaryKind));
        }
    }

    private static string findOfferingValueKindDisplayName(EOfferingSummaryKind summaryKind)
    {
        switch (summaryKind)
        {
            case EOfferingSummaryKind.Instructor:
                return "담당교원";
            case EOfferingSummaryKind.Location:
                return "강의실";
            default:
                throw new ArgumentOutOfRangeException(nameof(summaryKind));
        }
    }
}
