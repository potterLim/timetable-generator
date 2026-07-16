using System;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class UnscheduledOfferingSelection
{
    public CourseId CourseId { get; }

    public OfferingId OfferingId { get; }

    public UnscheduledOfferingSelection(CourseId courseId, OfferingId offeringId)
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
}
