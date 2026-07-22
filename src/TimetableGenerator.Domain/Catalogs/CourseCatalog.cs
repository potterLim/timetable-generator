using System;
using System.Collections.Generic;

namespace TimetableGenerator.Domain.Catalogs;

public sealed class CourseCatalog
{
    private readonly IReadOnlyList<CatalogCourse> mCourses;

    private readonly IReadOnlyList<CatalogOffering> mOfferings;

    public CatalogId Id { get; }

    public InstitutionId InstitutionId { get; }

    public InstitutionName InstitutionName { get; }

    public AcademicTerm Term { get; }

    public CatalogRevision Revision { get; }

    public IReadOnlyList<CatalogCourse> Courses
    {
        get
        {
            return mCourses;
        }
    }

    public IReadOnlyList<CatalogOffering> Offerings
    {
        get
        {
            return mOfferings;
        }
    }

    public CourseCatalog(
        CatalogId id,
        InstitutionId institutionId,
        InstitutionName institutionName,
        AcademicTerm term,
        CatalogRevision revision,
        IEnumerable<CatalogCourse> courses,
        IEnumerable<CatalogOffering> offerings)
    {
        if (id == null)
        {
            throw new ArgumentNullException(nameof(id));
        }

        if (institutionId == null)
        {
            throw new ArgumentNullException(nameof(institutionId));
        }

        if (institutionName == null)
        {
            throw new ArgumentNullException(nameof(institutionName));
        }

        if (term.IsValid == false)
        {
            throw new ArgumentException("Course catalogs require a valid term.", nameof(term));
        }

        if (revision.IsValid == false)
        {
            throw new ArgumentException("Course catalogs require a valid revision.", nameof(revision));
        }

        if (courses == null)
        {
            throw new ArgumentNullException(nameof(courses));
        }

        if (offerings == null)
        {
            throw new ArgumentNullException(nameof(offerings));
        }

        IReadOnlyList<CatalogCourse> copiedCourses = copyAndValidateCourses(courses);
        IReadOnlyList<CatalogOffering> copiedOfferings = copyAndValidateOfferings(offerings, copiedCourses);

        Id = id;
        InstitutionId = institutionId;
        InstitutionName = institutionName;
        Term = term;
        Revision = revision;
        mCourses = copiedCourses;
        mOfferings = copiedOfferings;
    }

    private static IReadOnlyList<CatalogCourse> copyAndValidateCourses(
        IEnumerable<CatalogCourse> courses)
    {
        List<CatalogCourse> copiedCourses = new List<CatalogCourse>();
        HashSet<CourseId> courseIds = new HashSet<CourseId>();
        HashSet<CourseCode> courseCodes = new HashSet<CourseCode>();
        foreach (CatalogCourse course in courses)
        {
            if (course == null)
            {
                throw new ArgumentException("Course catalogs cannot contain null courses.", nameof(courses));
            }

            if (courseIds.Add(course.Id) == false)
            {
                throw new ArgumentException(
                    "Course catalogs cannot contain duplicate course IDs.",
                    nameof(courses));
            }

            if (courseCodes.Add(course.Code) == false)
            {
                throw new ArgumentException(
                    "Course catalogs cannot contain duplicate course codes.",
                    nameof(courses));
            }

            copiedCourses.Add(course);
        }

        if (copiedCourses.Count == 0)
        {
            throw new ArgumentException("Course catalogs require at least one course.", nameof(courses));
        }

        return copiedCourses.AsReadOnly();
    }

    private static IReadOnlyList<CatalogOffering> copyAndValidateOfferings(
        IEnumerable<CatalogOffering> offerings,
        IReadOnlyList<CatalogCourse> courses)
    {
        HashSet<CourseId> knownCourseIds = new HashSet<CourseId>();
        foreach (CatalogCourse course in courses)
        {
            knownCourseIds.Add(course.Id);
        }

        List<CatalogOffering> copiedOfferings = new List<CatalogOffering>();
        HashSet<OfferingId> offeringIds = new HashSet<OfferingId>();
        foreach (CatalogOffering offering in offerings)
        {
            if (offering == null)
            {
                throw new ArgumentException("Course catalogs cannot contain null offerings.", nameof(offerings));
            }

            if (offeringIds.Add(offering.Id) == false)
            {
                throw new ArgumentException(
                    "Course catalogs cannot contain duplicate offering IDs.",
                    nameof(offerings));
            }

            if (knownCourseIds.Contains(offering.CourseId) == false)
            {
                throw new ArgumentException(
                    "Every catalog offering must reference a catalog course.",
                    nameof(offerings));
            }

            copiedOfferings.Add(offering);
        }

        if (copiedOfferings.Count == 0)
        {
            throw new ArgumentException("Course catalogs require at least one offering.", nameof(offerings));
        }

        return copiedOfferings.AsReadOnly();
    }
}
