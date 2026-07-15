using System.IO;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal static class CatalogFileLayout
{
    public const string INSTITUTION_ID = "handong-global-university";
    public const string INSTITUTION_NAME_KO = "한동대학교";
    public const string INSTITUTION_NAME_EN = "Handong Global University";
    public const string INDEX_FILE_NAME = "index.json";

    public static string GetCatalogId(AcademicTerm term, CatalogRevision revision)
    {
        return INSTITUTION_ID + ":" + term.Id + ":" + revision.FileComponent;
    }

    public static string GetCourseId(CourseCode courseCode)
    {
        return INSTITUTION_ID + ":" + courseCode.Value;
    }

    public static string GetOfferingId(
        AcademicTerm term,
        CourseCode courseCode,
        CourseSectionCode sectionCode)
    {
        return INSTITUTION_ID
            + ":"
            + term.Id
            + ":"
            + courseCode.Value
            + ":"
            + sectionCode.Value;
    }

    public static string GetCatalogRelativePath(AcademicTerm term, CatalogRevision revision)
    {
        return INSTITUTION_ID
            + "/"
            + term.Id
            + "/catalog-"
            + revision.FileComponent
            + ".json";
    }

    public static string GetCatalogPath(
        CatalogOutputRootPath outputRootPath,
        AcademicTerm term,
        CatalogRevision revision)
    {
        return Path.Combine(
            outputRootPath.Value,
            INSTITUTION_ID,
            term.Id,
            "catalog-" + revision.FileComponent + ".json");
    }

    public static string GetIndexPath(CatalogOutputRootPath outputRootPath)
    {
        return Path.Combine(outputRootPath.Value, INDEX_FILE_NAME);
    }
}
