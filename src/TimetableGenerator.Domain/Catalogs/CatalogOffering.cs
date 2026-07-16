using System;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Domain.Catalogs;

public sealed class CatalogOffering
{
    public OfferingId Id { get; }

    public CourseId CourseId { get; }

    public CourseSectionCode SectionCode { get; }

    public MeetingSchedule MeetingSchedule { get; }

    public CatalogOffering(
        OfferingId id,
        CourseId courseId,
        CourseSectionCode sectionCode,
        MeetingSchedule meetingSchedule)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (sectionCode == null)
        {
            throw new ArgumentNullException(nameof(sectionCode));
        }

        if (meetingSchedule == null)
        {
            throw new ArgumentNullException(nameof(meetingSchedule));
        }

        Id = id;
        CourseId = courseId;
        SectionCode = sectionCode;
        MeetingSchedule = meetingSchedule;
    }
}
