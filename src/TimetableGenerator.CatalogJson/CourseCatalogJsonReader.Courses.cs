using System.Collections.Generic;
using System.Text.Json;
using TimetableGenerator.CatalogJson.Internal;
using TimetableGenerator.Domain.Catalogs;

namespace TimetableGenerator.CatalogJson;

public static partial class CourseCatalogJsonReader
{
    private static List<CatalogCourse> parseCourses(
        JsonElement coursesElement,
        InstitutionId institutionId)
    {
        List<CatalogCourse> courses = new List<CatalogCourse>();
        int courseIndex = 0;
        foreach (JsonElement courseElement in coursesElement.EnumerateArray())
        {
            string coursePath = "$.courses[" + courseIndex + "]";
            courses.Add(parseCourse(courseElement, coursePath, institutionId));
            ++courseIndex;
        }

        if (courses.Count == 0)
        {
            throw new CatalogJsonFormatException("$.courses", "at least one course is required.");
        }

        return courses;
    }

    private static CatalogCourse parseCourse(
        JsonElement element,
        string path,
        InstitutionId institutionId)
    {
        StrictJsonObject courseObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "courseId",
                "code",
                "name",
                "credits",
            });
        StrictJsonObject nameObject = StrictJsonObject.Create(
            courseObject.GetElement("name"),
            courseObject.GetPropertyPath("name"),
            new string[]
            {
                "ko",
                "en",
            });

        CourseId courseId = new CourseId(courseObject.GetString("courseId"));
        CourseCode courseCode = new CourseCode(courseObject.GetString("code"));
        string expectedCourseId = CatalogJsonValueParser.BuildCourseId(institutionId, courseCode);
        CatalogJsonValueParser.RequireExactString(
            courseId.Value,
            expectedCourseId,
            courseObject.GetPropertyPath("courseId"));
        KoreanCourseName koreanName = new KoreanCourseName(nameObject.GetString("ko"));
        EnglishCourseName englishName = new EnglishCourseName(nameObject.GetString("en"));
        CourseCredits credits = new CourseCredits(courseObject.GetDecimal("credits"));
        return new CatalogCourse(courseId, courseCode, koreanName, englishName, credits);
    }

    private static Dictionary<CourseId, CourseCode> buildCourseCodesById(
        IEnumerable<CatalogCourse> courses)
    {
        Dictionary<CourseId, CourseCode> courseCodesById = new Dictionary<CourseId, CourseCode>();
        foreach (CatalogCourse course in courses)
        {
            if (courseCodesById.TryAdd(course.Id, course.Code) == false)
            {
                throw new CatalogJsonFormatException("$.courses", "duplicate course IDs are not allowed.");
            }
        }

        return courseCodesById;
    }
}
