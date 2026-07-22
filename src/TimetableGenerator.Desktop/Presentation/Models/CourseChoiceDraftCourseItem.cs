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

    private CourseChoiceDraftCourseItem(
        CatalogCourseProjection projection,
        IReadOnlyDictionary<OfferingId, EOfferingPreference> savedPreferences,
        EOfferingPreference defaultPreference)
    {
        if (projection == null)
        {
            throw new ArgumentNullException(nameof(projection));
        }

        if (savedPreferences == null)
        {
            throw new ArgumentNullException(nameof(savedPreferences));
        }

        if (Enum.IsDefined(typeof(EOfferingPreference), defaultPreference) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultPreference));
        }

        Projection = projection;
        Offerings = createOfferings(projection, savedPreferences, defaultPreference);
        if (Offerings.Count == 0)
        {
            throw new ArgumentException(
                "Course choice drafts require at least one offering.",
                nameof(projection));
        }

        foreach (CourseOfferingPreferenceItem offering in Offerings)
        {
            offering.PreferenceChanged += onOfferingPreferenceChanged;
        }
    }

    public static CourseChoiceDraftCourseItem CreateNew(CatalogCourseProjection projection)
    {
        return new CourseChoiceDraftCourseItem(
            projection,
            new Dictionary<OfferingId, EOfferingPreference>(),
            EOfferingPreference.Acceptable);
    }

    public static CourseChoiceDraftCourseItem Restore(
        CatalogCourseProjection projection,
        IEnumerable<OfferingCandidate> savedCandidates)
    {
        if (savedCandidates == null)
        {
            throw new ArgumentNullException(nameof(savedCandidates));
        }

        return new CourseChoiceDraftCourseItem(
            projection,
            createSavedPreferences(savedCandidates),
            EOfferingPreference.Excluded);
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

    public void AllowRemoval()
    {
        setProperty(ref mCanRemove, true, nameof(CanRemove));
    }

    public void PreventRemoval()
    {
        setProperty(ref mCanRemove, false, nameof(CanRemove));
    }

    private static ObservableCollection<CourseOfferingPreferenceItem> createOfferings(
        CatalogCourseProjection projection,
        IReadOnlyDictionary<OfferingId, EOfferingPreference> savedPreferences,
        EOfferingPreference defaultPreference)
    {
        ObservableCollection<CourseOfferingPreferenceItem> offerings = new ObservableCollection<CourseOfferingPreferenceItem>();
        foreach (CatalogOfferingProjection offering in projection.Offerings)
        {
            EOfferingPreference preference = defaultPreference;
            EOfferingPreference savedPreference;
            if (savedPreferences.TryGetValue(offering.Offering.Id, out savedPreference))
            {
                preference = savedPreference;
            }

            offerings.Add(new CourseOfferingPreferenceItem(projection, offering, preference));
        }

        return offerings;
    }

    private static Dictionary<OfferingId, EOfferingPreference> createSavedPreferences(
        IEnumerable<OfferingCandidate> savedCandidates)
    {
        Dictionary<OfferingId, EOfferingPreference> preferences = new Dictionary<OfferingId, EOfferingPreference>();
        foreach (OfferingCandidate candidate in savedCandidates)
        {
            preferences.Add(candidate.OfferingId, candidate.Preference);
        }

        return preferences;
    }

    private void onOfferingPreferenceChanged(object? senderOrNull, EventArgs eventArgs)
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
