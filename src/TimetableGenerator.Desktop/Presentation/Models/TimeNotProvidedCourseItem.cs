using System;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class TimeNotProvidedCourseItem
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

    public string InstructorCreditDisplayText
    {
        get
        {
            return InstructorDisplayText + " · " + CreditDisplayText;
        }
    }

    public string MeetingDisplayText { get; }

    public string LocationDisplayText { get; }

    public string ScheduleLocationDisplayText
    {
        get
        {
            return MeetingDisplayText + " · " + LocationDisplayText;
        }
    }

    public ECourseAccent Accent { get; }

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
            return Name + "을 현재 시간표에서 제거";
        }
    }

    public TimeNotProvidedCourseItem(
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

        CatalogOfferingProjection offering = findTimeNotProvidedOffering(course, selection);
        CourseId = course.Course.Id;
        Code = course.Course.Code.Value;
        Name = course.Course.KoreanName.Value;
        InstructorDisplayText = offering.InstructorSummary;
        Credits = course.Course.Credits;
        MeetingDisplayText = offering.Offering.SectionCode.Value
            + "분반 · "
            + offering.ScheduleSummary;
        LocationDisplayText = offering.LocationSummary;
        Accent = course.Accent;
    }

    private static CatalogOfferingProjection findTimeNotProvidedOffering(
        CatalogCourseProjection course,
        UnscheduledOfferingSelection selection)
    {
        foreach (CatalogOfferingProjection offering in course.Offerings)
        {
            if (offering.Offering.Id == selection.OfferingId)
            {
                if (offering.Offering.MeetingSchedule.Status != EMeetingScheduleStatus.NotProvided)
                {
                    throw new ArgumentException(
                        "The selected offering must have no provided meeting time.",
                        nameof(selection));
                }

                return offering;
            }
        }

        throw new ArgumentException(
            "The selected offering does not belong to its projected course.",
            nameof(selection));
    }
}
