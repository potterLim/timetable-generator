using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class OfferingCandidate
{
    public OfferingId OfferingId { get; }

    public EOfferingPreference Preference { get; }

    public bool IsEligible
    {
        get
        {
            return Preference != EOfferingPreference.Excluded;
        }
    }

    public OfferingCandidate(OfferingId offeringId, EOfferingPreference preference)
    {
        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        if (Enum.IsDefined(typeof(EOfferingPreference), preference) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(preference));
        }

        OfferingId = offeringId;
        Preference = preference;
    }
}
