using System;
using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Domain.Planning;

public sealed class CourseChoiceGroup
{
    private readonly IReadOnlyList<CourseCandidate> mCourseCandidates;

    public CourseChoiceGroupId Id { get; }

    public ECourseChoiceCardinality Cardinality { get; }

    public IReadOnlyList<CourseCandidate> CourseCandidates
    {
        get
        {
            return mCourseCandidates;
        }
    }

    public CourseChoiceGroup(CourseChoiceGroupId id, ECourseChoiceCardinality cardinality, IEnumerable<CourseCandidate> courseCandidates)
    {
        if (id.IsValid == false)
        {
            throw new ArgumentException("Course choice groups require a valid ID.", nameof(id));
        }

        if (cardinality != ECourseChoiceCardinality.ExactlyOne)
        {
            throw new ArgumentOutOfRangeException(nameof(cardinality));
        }

        if (courseCandidates == null)
        {
            throw new ArgumentNullException(nameof(courseCandidates));
        }

        List<CourseCandidate> copiedCandidates = new List<CourseCandidate>();
        HashSet<CourseId> courseIds = new HashSet<CourseId>();
        HashSet<OfferingId> offeringIds = new HashSet<OfferingId>();
        foreach (CourseCandidate courseCandidate in courseCandidates)
        {
            if (courseCandidate == null)
            {
                throw new ArgumentException("Course choice groups cannot contain null course candidates.", nameof(courseCandidates));
            }

            if (courseIds.Add(courseCandidate.CourseId) == false)
            {
                throw new ArgumentException("Course choice groups cannot contain duplicate course IDs.", nameof(courseCandidates));
            }

            foreach (OfferingCandidate offeringCandidate in courseCandidate.OfferingCandidates)
            {
                if (offeringIds.Add(offeringCandidate.OfferingId) == false)
                {
                    throw new ArgumentException("Course choice groups cannot contain duplicate offering IDs.", nameof(courseCandidates));
                }
            }

            copiedCandidates.Add(courseCandidate);
        }

        if (copiedCandidates.Count == 0)
        {
            throw new ArgumentException("Course choice groups require at least one course candidate.", nameof(courseCandidates));
        }

        Id = id;
        Cardinality = cardinality;
        mCourseCandidates = copiedCandidates.AsReadOnly();
    }

    public static CourseChoiceGroup CreateWithAcceptableOfferings(CourseChoiceGroupId id, CourseId courseId, IEnumerable<OfferingId> offeringIds)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        if (offeringIds == null)
        {
            throw new ArgumentNullException(nameof(offeringIds));
        }

        List<OfferingCandidate> offeringCandidates = new List<OfferingCandidate>();
        foreach (OfferingId offeringId in offeringIds)
        {
            offeringCandidates.Add(new OfferingCandidate(offeringId, EOfferingPreference.Acceptable));
        }

        CourseCandidate courseCandidate = new CourseCandidate(courseId, offeringCandidates);
        return new CourseChoiceGroup(id, ECourseChoiceCardinality.ExactlyOne, new CourseCandidate[] { courseCandidate });
    }
}
