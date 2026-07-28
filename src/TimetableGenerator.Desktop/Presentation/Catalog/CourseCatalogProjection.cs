using System;
using System.Collections.Generic;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal sealed class CourseCatalogProjection
{
    private readonly IReadOnlyList<CatalogCourseProjection> mCourses;

    private readonly IReadOnlyList<OfferingUnitName> mOfferingUnitNames;

    private readonly IReadOnlyList<CatalogRequirementGroup> mRequirementGroups;

    private readonly IReadOnlyDictionary<CourseId, CatalogCourseProjection> mCoursesById;

    private readonly IReadOnlyDictionary<OfferingId, CatalogOfferingProjection> mOfferingsById;

    public CourseCatalogDocument Document { get; }

    public IReadOnlyList<CatalogCourseProjection> Courses
    {
        get
        {
            return mCourses;
        }
    }

    public IReadOnlyList<OfferingUnitName> OfferingUnitNames
    {
        get
        {
            return mOfferingUnitNames;
        }
    }

    public IReadOnlyList<CatalogRequirementGroup> RequirementGroups
    {
        get
        {
            return mRequirementGroups;
        }
    }

    public CourseCatalogProjection(CourseCatalogDocument document, IEnumerable<CatalogCourseProjection> courses)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (courses == null)
        {
            throw new ArgumentNullException(nameof(courses));
        }

        Dictionary<CourseId, CatalogCourse> sourceCoursesById = createSourceCoursesById(document.Catalog.Courses);
        Dictionary<OfferingId, CatalogOffering> sourceOfferingsById = createSourceOfferingsById(document.Catalog.Offerings);
        Dictionary<CourseId, CatalogCourseProjection> coursesById = new Dictionary<CourseId, CatalogCourseProjection>();
        Dictionary<OfferingId, CatalogOfferingProjection> offeringsById = new Dictionary<OfferingId, CatalogOfferingProjection>();
        List<CatalogCourseProjection> copiedCourses = copyAndValidateCourses(
            courses,
            sourceCoursesById,
            sourceOfferingsById,
            coursesById,
            offeringsById);

        if (coursesById.Count != sourceCoursesById.Count)
        {
            throw new ArgumentException("Catalog projections must preserve every source course.", nameof(courses));
        }

        if (offeringsById.Count != sourceOfferingsById.Count)
        {
            throw new ArgumentException("Catalog projections must preserve every source offering.", nameof(courses));
        }

        Document = document;
        mCourses = copiedCourses.AsReadOnly();
        mCoursesById = coursesById;
        mOfferingsById = offeringsById;
        mOfferingUnitNames = collectOfferingUnitNames(copiedCourses);
        mRequirementGroups = createRequirementGroups(copiedCourses);
    }

    public bool HasCourse(CourseId courseId)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        return mCoursesById.ContainsKey(courseId);
    }

    public bool HasOffering(OfferingId offeringId)
    {
        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        return mOfferingsById.ContainsKey(offeringId);
    }

    public CatalogCourseProjection FindCourseById(CourseId courseId)
    {
        if (courseId == null)
        {
            throw new ArgumentNullException(nameof(courseId));
        }

        CatalogCourseProjection? courseOrNull;
        bool hasCourse = mCoursesById.TryGetValue(courseId, out courseOrNull);
        if (hasCourse == false || courseOrNull == null)
        {
            throw new KeyNotFoundException("No projected course exists for " + courseId + ".");
        }

        return courseOrNull;
    }

    public CatalogOfferingProjection FindOfferingById(OfferingId offeringId)
    {
        if (offeringId == null)
        {
            throw new ArgumentNullException(nameof(offeringId));
        }

        CatalogOfferingProjection? offeringOrNull;
        bool hasOffering = mOfferingsById.TryGetValue(offeringId, out offeringOrNull);
        if (hasOffering == false || offeringOrNull == null)
        {
            throw new KeyNotFoundException("No projected offering exists for " + offeringId + ".");
        }

        return offeringOrNull;
    }

    private static Dictionary<CourseId, CatalogCourse> createSourceCoursesById(IEnumerable<CatalogCourse> sourceCourses)
    {
        Dictionary<CourseId, CatalogCourse> sourceCoursesById = new Dictionary<CourseId, CatalogCourse>();
        foreach (CatalogCourse sourceCourse in sourceCourses)
        {
            sourceCoursesById.Add(sourceCourse.Id, sourceCourse);
        }

        return sourceCoursesById;
    }

    private static Dictionary<OfferingId, CatalogOffering> createSourceOfferingsById(IEnumerable<CatalogOffering> sourceOfferings)
    {
        Dictionary<OfferingId, CatalogOffering> sourceOfferingsById = new Dictionary<OfferingId, CatalogOffering>();
        foreach (CatalogOffering sourceOffering in sourceOfferings)
        {
            sourceOfferingsById.Add(sourceOffering.Id, sourceOffering);
        }

        return sourceOfferingsById;
    }

    private static List<CatalogCourseProjection> copyAndValidateCourses(
        IEnumerable<CatalogCourseProjection> courses,
        IReadOnlyDictionary<CourseId, CatalogCourse> sourceCoursesById,
        IReadOnlyDictionary<OfferingId, CatalogOffering> sourceOfferingsById,
        IDictionary<CourseId, CatalogCourseProjection> coursesById,
        IDictionary<OfferingId, CatalogOfferingProjection> offeringsById)
    {
        List<CatalogCourseProjection> copiedCourses = new List<CatalogCourseProjection>();
        foreach (CatalogCourseProjection course in courses)
        {
            validateSourceCourse(course, sourceCoursesById, coursesById);
            foreach (CatalogOfferingProjection offering in course.Offerings)
            {
                validateSourceOffering(offering, sourceOfferingsById, offeringsById);
            }

            copiedCourses.Add(course);
        }

        return copiedCourses;
    }

    private static void validateSourceCourse(CatalogCourseProjection course, IReadOnlyDictionary<CourseId, CatalogCourse> sourceCoursesById, IDictionary<CourseId, CatalogCourseProjection> coursesById)
    {
        if (course == null)
        {
            throw new ArgumentException("Catalog projections cannot contain null courses.", nameof(course));
        }

        CatalogCourse? sourceCourseOrNull;
        bool hasSourceCourse = sourceCoursesById.TryGetValue(course.Course.Id, out sourceCourseOrNull);
        if (hasSourceCourse == false
            || sourceCourseOrNull == null
            || ReferenceEquals(sourceCourseOrNull, course.Course) == false)
        {
            throw new ArgumentException("Projected courses must come from the source catalog document.", nameof(course));
        }

        if (coursesById.TryAdd(course.Course.Id, course) == false)
        {
            throw new ArgumentException("Catalog projections cannot contain duplicate courses.", nameof(course));
        }
    }

    private static void validateSourceOffering(CatalogOfferingProjection offering, IReadOnlyDictionary<OfferingId, CatalogOffering> sourceOfferingsById, IDictionary<OfferingId, CatalogOfferingProjection> offeringsById)
    {
        CatalogOffering? sourceOfferingOrNull;
        bool hasSourceOffering = sourceOfferingsById.TryGetValue(offering.Offering.Id, out sourceOfferingOrNull);
        if (hasSourceOffering == false
            || sourceOfferingOrNull == null
            || ReferenceEquals(sourceOfferingOrNull, offering.Offering) == false)
        {
            throw new ArgumentException("Projected offerings must come from the source catalog document.", nameof(offering));
        }

        if (offeringsById.TryAdd(offering.Offering.Id, offering) == false)
        {
            throw new ArgumentException("Catalog projections cannot contain duplicate offerings.", nameof(offering));
        }
    }

    private static IReadOnlyList<OfferingUnitName> collectOfferingUnitNames(IEnumerable<CatalogCourseProjection> courses)
    {
        SortedDictionary<string, OfferingUnitName> offeringUnitNamesByValue = new SortedDictionary<string, OfferingUnitName>(StringComparer.Ordinal);
        foreach (CatalogCourseProjection course in courses)
        {
            foreach (OfferingUnitName offeringUnitName in course.OfferingUnitNames)
            {
                offeringUnitNamesByValue.TryAdd(offeringUnitName.Value, offeringUnitName);
            }
        }

        return new List<OfferingUnitName>(offeringUnitNamesByValue.Values).AsReadOnly();
    }

    private static IReadOnlyList<CatalogRequirementGroup> createRequirementGroups(IReadOnlyList<CatalogCourseProjection> courses)
    {
        SortedDictionary<ERequirementType, List<CatalogCourseProjection>> coursesByRequirement = new SortedDictionary<ERequirementType, List<CatalogCourseProjection>>();
        foreach (CatalogCourseProjection course in courses)
        {
            foreach (ERequirementType requirementType in course.RequirementTypes)
            {
                List<CatalogCourseProjection>? matchingCoursesOrNull;
                bool hasGroup = coursesByRequirement.TryGetValue(requirementType, out matchingCoursesOrNull);
                if (hasGroup == false || matchingCoursesOrNull == null)
                {
                    matchingCoursesOrNull = new List<CatalogCourseProjection>();
                    coursesByRequirement.Add(requirementType, matchingCoursesOrNull);
                }

                matchingCoursesOrNull.Add(course);
            }
        }

        List<CatalogRequirementGroup> requirementGroups = new List<CatalogRequirementGroup>();
        foreach (KeyValuePair<ERequirementType, List<CatalogCourseProjection>> pair in coursesByRequirement)
        {
            requirementGroups.Add(new CatalogRequirementGroup(pair.Key, pair.Value));
        }

        return requirementGroups.AsReadOnly();
    }
}
