using System;
using System.Collections.Generic;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal static class CourseCatalogProjector
{
    public static CourseCatalogProjection Project(CourseCatalogDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        Dictionary<CourseId, List<CatalogOfferingProjection>> offeringsByCourseId = createEmptyOfferingGroups(document.Catalog.Courses);
        foreach (CatalogOffering offering in document.Catalog.Offerings)
        {
            CatalogOfferingMetadata metadata = document.FindOfferingMetadataById(offering.Id);
            CatalogOfferingProjection offeringProjection = new CatalogOfferingProjection(offering, metadata);

            List<CatalogOfferingProjection>? courseOfferingsOrNull;
            bool hasCourse = offeringsByCourseId.TryGetValue(offering.CourseId, out courseOfferingsOrNull);
            if (hasCourse == false || courseOfferingsOrNull == null)
            {
                throw new ArgumentException(
                    "Every catalog offering must reference a projected course.",
                    nameof(document));
            }

            courseOfferingsOrNull.Add(offeringProjection);
        }

        List<CatalogCourseProjection> courses = new List<CatalogCourseProjection>();
        foreach (CatalogCourse course in document.Catalog.Courses)
        {
            ECourseAccent accent = CourseAccentAssigner.FindAccent(course.Id);
            courses.Add(new CatalogCourseProjection(course, accent, offeringsByCourseId[course.Id]));
        }

        return new CourseCatalogProjection(document, courses);
    }

    private static Dictionary<CourseId, List<CatalogOfferingProjection>> createEmptyOfferingGroups(IEnumerable<CatalogCourse> courses)
    {
        Dictionary<CourseId, List<CatalogOfferingProjection>> offeringsByCourseId = new Dictionary<CourseId, List<CatalogOfferingProjection>>();
        foreach (CatalogCourse course in courses)
        {
            offeringsByCourseId.Add(course.Id, new List<CatalogOfferingProjection>());
        }

        return offeringsByCourseId;
    }
}
