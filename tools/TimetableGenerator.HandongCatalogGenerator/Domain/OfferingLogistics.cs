using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class OfferingLogistics
{
    public MeetingSchedule Schedule { get; }

    public LocationAssignment Location { get; }

    public OfferingLogistics(MeetingSchedule schedule, LocationAssignment location)
    {
        if (schedule == null)
        {
            throw new ArgumentNullException(nameof(schedule));
        }

        if (location == null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        Schedule = schedule;
        Location = location;
    }
}
