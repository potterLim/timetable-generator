using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogDocumentCounts
{
    public int CourseCount { get; }

    public int OfferingCount { get; }

    public int ScheduledOfferingCount { get; }

    public int MeetingNotProvidedCount { get; }

    public CatalogDocumentCounts(
        int courseCount,
        int offeringCount,
        int scheduledOfferingCount,
        int meetingNotProvidedCount)
    {
        if (courseCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(courseCount));
        }

        if (offeringCount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(offeringCount));
        }

        if (scheduledOfferingCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(scheduledOfferingCount));
        }

        if (meetingNotProvidedCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(meetingNotProvidedCount));
        }

        if (scheduledOfferingCount + meetingNotProvidedCount != offeringCount)
        {
            throw new ArgumentException(
                "Scheduled and time-not-provided counts must partition all offerings.");
        }

        CourseCount = courseCount;
        OfferingCount = offeringCount;
        ScheduledOfferingCount = scheduledOfferingCount;
        MeetingNotProvidedCount = meetingNotProvidedCount;
    }
}
