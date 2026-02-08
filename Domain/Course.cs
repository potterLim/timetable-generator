using System.Collections.Generic;

namespace TimetableGenerator
{
    // Represents one CSV row (one section of a course).
    // Courses with the same CourseId are treated as mutually exclusive options (choose exactly one).
    public sealed class Course
    {
        public CourseId CourseId { get; }
        public CourseSection Section { get; }
        public string Name { get; }

        public ClassroomLocation Classroom { get; }

        // Raw time slot tokens from CSV, e.g. "화요일3교시" (split by '/').
        public IReadOnlyList<string> TimeSlots { get; }

        public int SourceLineNumber { get; }

        public Course(CourseId courseId, CourseSection section, string name, ClassroomLocation classroom, List<string> timeSlots, int sourceLineNumber)
        {
            CourseId = courseId;
            Section = section;
            Name = name;
            Classroom = classroom;

            // Caller-side policy: timeSlots is valid. Make it immutable for consumers.
            TimeSlots = new List<string>(timeSlots).AsReadOnly();

            SourceLineNumber = sourceLineNumber;
        }
    }
}
