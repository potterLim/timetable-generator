using System.Collections.Generic;

namespace TimetableGenerator
{
    public class Course
    {
        public int CourseId { get; set; }
        public string Section { get; set; }
        public string Name { get; set; }
        public List<string> TimeSlots { get; set; }
    }

    public class TimeSlot
    {
        public string Day { get; set; }
        public string Period { get; set; }
        public string CourseName { get; set; }
    }
}
