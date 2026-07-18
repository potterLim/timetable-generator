using System;

using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Models;

internal sealed class CourseScheduleListSource : ScheduleListSource
{
    public CourseId CourseId { get; }

    public OfferingId OfferingId { get; }

    public CourseScheduleListSource(CourseId courseId, OfferingId offeringId)
        : base(EScheduleListEntryKind.Course)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        CourseId = courseId;
        OfferingId = offeringId;
    }

    internal override bool hasSameIdentityAs(ScheduleListSource other)
    {
        CourseScheduleListSource? courseSourceOrNull =
            other as CourseScheduleListSource;
        return courseSourceOrNull != null
            && courseSourceOrNull.CourseId == CourseId
            && courseSourceOrNull.OfferingId == OfferingId;
    }
}
