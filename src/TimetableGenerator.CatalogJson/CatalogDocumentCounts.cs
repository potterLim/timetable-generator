using System;

namespace TimetableGenerator.CatalogJson;

public sealed class CatalogDocumentCounts
{
    public CatalogCourseCount CourseCount { get; }

    public CatalogOfferingCount OfferingCount { get; }

    public CatalogScheduledOfferingCount ScheduledOfferingCount { get; }

    public CatalogMeetingNotProvidedCount MeetingNotProvidedCount { get; }

    public CatalogDocumentCounts(
        CatalogCourseCount courseCount,
        CatalogOfferingCount offeringCount,
        CatalogScheduledOfferingCount scheduledOfferingCount,
        CatalogMeetingNotProvidedCount meetingNotProvidedCount)
    {
        if (courseCount.IsValid == false)
        {
            throw new ArgumentOutOfRangeException(nameof(courseCount));
        }

        if (offeringCount.IsValid == false)
        {
            throw new ArgumentOutOfRangeException(nameof(offeringCount));
        }

        if (scheduledOfferingCount.Value + meetingNotProvidedCount.Value != offeringCount.Value)
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
