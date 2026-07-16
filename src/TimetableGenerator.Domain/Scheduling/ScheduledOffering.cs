using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Scheduling;

public sealed class ScheduledOffering
{
    private readonly IReadOnlyList<MeetingSlot> mMeetingSlots;

    public OfferingId OfferingId { get; }

    public CourseId CourseId { get; }

    public CourseSectionCode SectionCode { get; }

    public IReadOnlyList<MeetingSlot> MeetingSlots
    {
        get
        {
            return mMeetingSlots;
        }
    }

    public ScheduledOffering(CatalogOffering catalogOffering)
    {
        if (catalogOffering == null)
        {
            throw new ArgumentNullException(nameof(catalogOffering));
        }

        if (catalogOffering.MeetingSchedule.IsScheduled == false)
        {
            throw new ArgumentException(
                "Scheduled offering projections require a provided meeting schedule.",
                nameof(catalogOffering));
        }

        OfferingId = catalogOffering.Id;
        CourseId = catalogOffering.CourseId;
        SectionCode = catalogOffering.SectionCode;
        mMeetingSlots = catalogOffering.MeetingSchedule.Slots;
    }
}
