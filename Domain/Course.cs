using System.Collections.Generic;

namespace TimetableGenerator
{
    // Represents one CSV row (one section of a course).
    // Courses with the same CourseId are treated as mutually exclusive options (choose exactly one).
    public class Course
    {
        public CourseId CourseId { get; private set; }
        public CourseSection Section { get; private set; }
        public string Name { get; private set; }

        public ClassroomLocation Classroom { get; private set; }

        // Raw time slot tokens from CSV, e.g. "화요일3교시" (split by '/').
        public List<string> TimeSlots { get; private set; }

        public int SourceLineNumber { get; private set; }

        public Course(CourseId courseId, CourseSection section, string name, ClassroomLocation classroom, List<string> timeSlots, int sourceLineNumber)
        {
            CourseId = courseId;
            Section = section;
            Name = name;
            Classroom = classroom;
            TimeSlots = timeSlots;
            SourceLineNumber = sourceLineNumber;
        }
    }
}
