using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseChoiceDraftCourseItem : ObservableObject
{
    private bool mCanRemove;

    public event EventHandler? DraftChanged;

    public CatalogCourseProjection Projection { get; }

    public CourseId CourseId
    {
        get
        {
            return Projection.Course.Id;
        }
    }

    public string Code
    {
        get
        {
            return Projection.Course.Code.Value;
        }
    }

    public string Name
    {
        get
        {
            return Projection.Course.KoreanName.Value;
        }
    }

    public string CreditDisplayText
    {
        get
        {
            return Projection.Course.Credits + "학점";
        }
    }

    public ObservableCollection<CourseOfferingPreferenceItem> Offerings { get; }

    public bool CanRemove
    {
        get
        {
            return mCanRemove;
        }
    }

    public bool HasEligibleOffering
    {
        get
        {
            foreach (CourseOfferingPreferenceItem offering in Offerings)
            {
                if (offering.IsExcluded == false)
                {
                    return true;
                }
            }

            return false;
        }
    }

    public string SelectionSummary
    {
        get
        {
            int preferredCount = 0;
            int acceptableCount = 0;
            foreach (CourseOfferingPreferenceItem offering in Offerings)
            {
                if (offering.IsPreferred)
                {
                    ++preferredCount;
                }
                else if (offering.IsAcceptable)
                {
                    ++acceptableCount;
                }
            }

            return "선호 " + preferredCount + " · 가능 " + acceptableCount;
        }
    }

    public string RemoveButtonAccessibleName
    {
        get
        {
            return Name + "을 대안 과목에서 제거";
        }
    }

    public CourseChoiceDraftCourseItem(
        CatalogCourseProjection projection,
        IEnumerable<OfferingCandidate>? savedCandidatesOrNull)
    {
        if (projection == null)
        {
            throw new ArgumentNullException(nameof(projection));
        }

        Projection = projection;
        Offerings = createOfferings(projection, savedCandidatesOrNull);
        if (Offerings.Count == 0)
        {
            throw new ArgumentException(
                "Course choice drafts require at least one scheduled offering.",
                nameof(projection));
        }

        foreach (CourseOfferingPreferenceItem offering in Offerings)
        {
            offering.PreferenceChanged += onOfferingPreferenceChanged;
        }
    }

    public CourseCandidate CreateCandidate()
    {
        List<OfferingCandidate> offeringCandidates = new List<OfferingCandidate>();
        foreach (CourseOfferingPreferenceItem offering in Offerings)
        {
            offeringCandidates.Add(offering.CreateCandidate());
        }

        return new CourseCandidate(CourseId, offeringCandidates);
    }

    public void SetCanRemove(bool canRemove)
    {
        setProperty(ref mCanRemove, canRemove, nameof(CanRemove));
    }

    private static ObservableCollection<CourseOfferingPreferenceItem> createOfferings(
        CatalogCourseProjection projection,
        IEnumerable<OfferingCandidate>? savedCandidatesOrNull)
    {
        Dictionary<OfferingId, EOfferingPreference> savedPreferences =
            createSavedPreferences(savedCandidatesOrNull);
        ObservableCollection<CourseOfferingPreferenceItem> offerings =
            new ObservableCollection<CourseOfferingPreferenceItem>();
        foreach (CatalogOfferingProjection offering in projection.Offerings)
        {
            if (offering.Offering.MeetingSchedule.IsScheduled == false)
            {
                continue;
            }

            EOfferingPreference preference = EOfferingPreference.Excluded;
            EOfferingPreference savedPreference;
            if (savedPreferences.TryGetValue(offering.Offering.Id, out savedPreference))
            {
                preference = savedPreference;
            }

            offerings.Add(new CourseOfferingPreferenceItem(offering, preference));
        }

        return offerings;
    }

    private static Dictionary<OfferingId, EOfferingPreference> createSavedPreferences(
        IEnumerable<OfferingCandidate>? savedCandidatesOrNull)
    {
        Dictionary<OfferingId, EOfferingPreference> preferences =
            new Dictionary<OfferingId, EOfferingPreference>();
        if (savedCandidatesOrNull == null)
        {
            return preferences;
        }

        foreach (OfferingCandidate candidate in savedCandidatesOrNull)
        {
            preferences.Add(candidate.OfferingId, candidate.Preference);
        }

        return preferences;
    }

    private void onOfferingPreferenceChanged(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        raisePropertyChanged(nameof(HasEligibleOffering));
        raisePropertyChanged(nameof(SelectionSummary));
        EventHandler? draftChangedOrNull = DraftChanged;
        if (draftChangedOrNull != null)
        {
            draftChangedOrNull(this, EventArgs.Empty);
        }
    }
}
