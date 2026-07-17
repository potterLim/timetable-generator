using System;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanCourseChoiceCandidateItem
{
    public CourseId CourseId { get; }

    public string Code { get; }

    public string Name { get; }

    public CourseCredits Credits { get; }

    public string CreditDisplayText
    {
        get
        {
            return Credits + "학점";
        }
    }

    public string PreferenceSummary { get; }

    public string OfferingSummary { get; }

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

    public PlanCourseChoiceCandidateItem(
        CatalogCourseProjection projection,
        CourseCandidate courseCandidate)
    {
        if (projection == null)
        {
            throw new ArgumentNullException(nameof(projection));
        }

        if (courseCandidate == null)
        {
            throw new ArgumentNullException(nameof(courseCandidate));
        }

        if (projection.Course.Id != courseCandidate.CourseId)
        {
            throw new ArgumentException(
                "Plan course candidates must match their catalog projection.",
                nameof(courseCandidate));
        }

        CourseId = courseCandidate.CourseId;
        Code = projection.Course.Code.Value;
        Name = projection.Course.KoreanName.Value;
        Credits = projection.Course.Credits;
        Accent = projection.Accent;
        PreferenceSummary = createPreferenceSummary(courseCandidate);
        OfferingSummary = createOfferingSummary(courseCandidate);
    }

    private static string createPreferenceSummary(CourseCandidate courseCandidate)
    {
        int preferredCount = 0;
        int acceptableCount = 0;
        int excludedCount = 0;
        foreach (OfferingCandidate offeringCandidate
            in courseCandidate.OfferingCandidates)
        {
            switch (offeringCandidate.Preference)
            {
                case EOfferingPreference.Preferred:
                    ++preferredCount;
                    break;
                case EOfferingPreference.Acceptable:
                    ++acceptableCount;
                    break;
                case EOfferingPreference.Excluded:
                    ++excludedCount;
                    break;
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(courseCandidate),
                        offeringCandidate.Preference,
                        "Unknown offering preference.");
            }
        }

        string summary = "선호 " + preferredCount + " · 가능 " + acceptableCount;
        if (excludedCount > 0)
        {
            summary += " · 제외 " + excludedCount;
        }

        return summary;
    }

    private static string createOfferingSummary(CourseCandidate courseCandidate)
    {
        int eligibleCount = 0;
        foreach (OfferingCandidate offeringCandidate
            in courseCandidate.OfferingCandidates)
        {
            if (offeringCandidate.IsEligible)
            {
                ++eligibleCount;
            }
        }

        if (eligibleCount == 1)
        {
            return "선택 가능한 분반 1개";
        }

        return "선택 가능한 분반 " + eligibleCount + "개";
    }
}
