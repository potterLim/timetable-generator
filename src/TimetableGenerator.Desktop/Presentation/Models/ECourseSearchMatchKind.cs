namespace TimetableGenerator.Desktop.Presentation.Models;

internal enum ECourseSearchMatchKind
{
    ExactCourseCode = 0,
    CourseCodePrefix = 1,
    ExactCourseTitle = 2,
    CourseTitlePrefix = 3,
    CourseTitleContains = 4,
    InstructorContains = 5,
}
