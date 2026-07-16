using System;
using System.Text.Json;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson.Internal;

internal static class CatalogJsonValueParser
{
    public static InstitutionMetadata ParseInstitution(JsonElement element, string path)
    {
        StrictJsonObject institutionObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "id",
                "name",
            });
        StrictJsonObject nameObject = StrictJsonObject.Create(
            institutionObject.GetElement("name"),
            institutionObject.GetPropertyPath("name"),
            new string[]
            {
                "ko",
                "en",
            });

        InstitutionId institutionId = new InstitutionId(institutionObject.GetString("id"));
        InstitutionName koreanName = new InstitutionName(nameObject.GetString("ko"));
        EnglishInstitutionName englishName = new EnglishInstitutionName(nameObject.GetString("en"));
        return new InstitutionMetadata(institutionId, koreanName, englishName);
    }

    public static AcademicTerm ParseTerm(JsonElement element, string path)
    {
        StrictJsonObject termObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "id",
                "academicYear",
                "semester",
            });

        AcademicYear academicYear = new AcademicYear(termObject.GetInt32("academicYear"));
        AcademicSemester semester = new AcademicSemester(termObject.GetInt32("semester"));
        AcademicTerm term = new AcademicTerm(academicYear, semester);
        string termId = termObject.GetString("id");
        RequireExactString(termId, term.Id, termObject.GetPropertyPath("id"));
        return term;
    }

    public static void RequireSchemaVersion(int schemaVersion, string path)
    {
        if (schemaVersion != CatalogJsonSchema.VERSION)
        {
            throw new CatalogJsonFormatException(
                path,
                "only schemaVersion 1 is supported.");
        }
    }

    public static void RequireExactString(string actualValue, string expectedValue, string path)
    {
        if (string.Equals(actualValue, expectedValue, StringComparison.Ordinal) == false)
        {
            throw new CatalogJsonFormatException(
                path,
                "expected \"" + expectedValue + "\" but found \"" + actualValue + "\".");
        }
    }

    public static string BuildCatalogId(
        InstitutionId institutionId,
        AcademicTerm term,
        CatalogRevision revision)
    {
        return institutionId.Value + ":" + term.Id + ":" + revision.FileComponent;
    }

    public static string BuildCourseId(InstitutionId institutionId, CourseCode courseCode)
    {
        return institutionId.Value + ":" + courseCode.Value;
    }

    public static string BuildOfferingId(
        InstitutionId institutionId,
        AcademicTerm term,
        CourseCode courseCode,
        CourseSectionCode sectionCode)
    {
        return institutionId.Value
            + ":"
            + term.Id
            + ":"
            + courseCode.Value
            + ":"
            + sectionCode.Value;
    }

    public static string BuildCatalogRelativePath(
        InstitutionId institutionId,
        AcademicTerm term,
        CatalogRevision revision)
    {
        return institutionId.Value
            + "/"
            + term.Id
            + "/catalog-"
            + revision.FileComponent
            + ".json";
    }
}
