using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningCourseSelection
{
    private readonly IReadOnlyList<OfferingId> mScheduledOfferingIds;

    private readonly OfferingId? mTimeNotProvidedOfferingIdOrNull;

    public CourseId CourseId { get; }

    public EPlanningCourseSelectionKind Kind { get; }

    private PlanningCourseSelection(CourseId courseId, EPlanningCourseSelectionKind kind, IReadOnlyList<OfferingId> scheduledOfferingIds, OfferingId? timeNotProvidedOfferingIdOrNull)
    {
        CourseId = courseId;
        Kind = kind;
        mScheduledOfferingIds = scheduledOfferingIds;
        mTimeNotProvidedOfferingIdOrNull = timeNotProvidedOfferingIdOrNull;
    }

    public static PlanningCourseSelection CreateScheduledAlternatives(CourseId courseId, IEnumerable<OfferingId> offeringIds)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (offeringIds == null)
        {
            throw new ArgumentNullException(nameof(offeringIds));
        }

        List<OfferingId> copiedOfferingIds = new List<OfferingId>();
        HashSet<OfferingId> uniqueOfferingIds = new HashSet<OfferingId>();
        foreach (OfferingId offeringId in offeringIds)
        {
            if (offeringId == null)
            {
                throw new ArgumentException("Scheduled course selections cannot contain null offering IDs.", nameof(offeringIds));
            }

            if (uniqueOfferingIds.Add(offeringId) == false)
            {
                throw new ArgumentException("Scheduled course selections cannot contain duplicate offering IDs.", nameof(offeringIds));
            }

            copiedOfferingIds.Add(offeringId);
        }

        if (copiedOfferingIds.Count == 0)
        {
            throw new ArgumentException("Scheduled course selections require at least one offering ID.", nameof(offeringIds));
        }

        return new PlanningCourseSelection(courseId, EPlanningCourseSelectionKind.ScheduledAlternatives, copiedOfferingIds.AsReadOnly(), null);
    }

    public static PlanningCourseSelection CreateTimeNotProvidedOffering(CourseId courseId, OfferingId offeringId)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        return new PlanningCourseSelection(courseId, EPlanningCourseSelectionKind.TimeNotProvidedOffering, Array.Empty<OfferingId>(), offeringId);
    }

    public IReadOnlyList<OfferingId> GetScheduledOfferingIds()
    {
        if (Kind != EPlanningCourseSelectionKind.ScheduledAlternatives)
        {
            throw new InvalidOperationException("The course selection does not contain scheduled alternatives.");
        }

        return mScheduledOfferingIds;
    }

    public OfferingId GetTimeNotProvidedOfferingId()
    {
        if (Kind != EPlanningCourseSelectionKind.TimeNotProvidedOffering || mTimeNotProvidedOfferingIdOrNull == null)
        {
            throw new InvalidOperationException("The course selection does not identify a time-not-provided offering.");
        }

        return mTimeNotProvidedOfferingIdOrNull;
    }
}
