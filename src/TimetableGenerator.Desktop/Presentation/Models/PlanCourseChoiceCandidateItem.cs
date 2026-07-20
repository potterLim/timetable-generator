using System;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class PlanCourseChoiceCandidateItem : ObservableObject
{
    private readonly CatalogCourseProjection mProjection;

    private readonly CourseCandidate mCourseCandidate;

    private string mSelectedTimeNotProvidedOfferingDisplayText;

    private bool mHasSelectedTimeNotProvidedOffering;

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

    public string SelectedTimeNotProvidedOfferingDisplayText
    {
        get
        {
            return mSelectedTimeNotProvidedOfferingDisplayText;
        }
    }

    public bool HasSelectedTimeNotProvidedOffering
    {
        get
        {
            return mHasSelectedTimeNotProvidedOffering;
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

        mProjection = projection;
        mCourseCandidate = courseCandidate;
        mSelectedTimeNotProvidedOfferingDisplayText = string.Empty;
        CourseId = courseCandidate.CourseId;
        Code = projection.Course.Code.Value;
        Name = projection.Course.KoreanName.Value;
        Credits = projection.Course.Credits;
        Accent = projection.Accent;
        PreferenceSummary = createPreferenceSummary(courseCandidate);
        OfferingSummary = createOfferingSummary(courseCandidate);
    }

    public void SynchronizeSelectedOffering(
        ScheduleRecommendationBookmark? recommendationBookmarkOrNull)
    {
        CatalogOfferingProjection? selectedOfferingOrNull =
            findSelectedOfferingOrNull(recommendationBookmarkOrNull);
        bool hasTimeNotProvidedSelection = selectedOfferingOrNull != null
            && selectedOfferingOrNull.Offering.MeetingSchedule.IsScheduled == false;
        string displayText = string.Empty;
        if (hasTimeNotProvidedSelection && selectedOfferingOrNull != null)
        {
            displayText = selectedOfferingOrNull.Offering.SectionCode.Value
                + "분반: 시간 미정";
        }

        setProperty(
            ref mSelectedTimeNotProvidedOfferingDisplayText,
            displayText,
            nameof(SelectedTimeNotProvidedOfferingDisplayText));
        setProperty(
            ref mHasSelectedTimeNotProvidedOffering,
            hasTimeNotProvidedSelection,
            nameof(HasSelectedTimeNotProvidedOffering));
    }

    private CatalogOfferingProjection? findSelectedOfferingOrNull(
        ScheduleRecommendationBookmark? recommendationBookmarkOrNull)
    {
        if (recommendationBookmarkOrNull == null)
        {
            return null;
        }

        foreach (OfferingCandidate offeringCandidate
            in mCourseCandidate.OfferingCandidates)
        {
            if (recommendationBookmarkOrNull.ContainsOffering(
                offeringCandidate.OfferingId) == false)
            {
                continue;
            }

            foreach (CatalogOfferingProjection offering in mProjection.Offerings)
            {
                if (offering.Offering.Id == offeringCandidate.OfferingId)
                {
                    return offering;
                }
            }

            throw new InvalidOperationException(
                "A selected plan offering is missing from its catalog projection.");
        }

        return null;
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
