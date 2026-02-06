namespace TimetableGenerator
{
    // Parsed time slot used for collision checking and table rendering.
    public class TimeSlot
    {
        public EDay Day { get; private set; }
        public int Period { get; private set; }

        public int CourseId { get; private set; }
        public string CourseName { get; private set; }
        public string Section { get; private set; }

        public TimeSlot(EDay day, int period, int courseId, string courseName, string section)
        {
            Day = day;
            Period = period;
            CourseId = courseId;
            CourseName = courseName;
            Section = section;
        }

        public string GetCellText()
        {
            return $"{CourseName}({Section})";
        }

        // Collision key for schedule validation (same day and period cannot overlap).
        public string GetCollisionKey()
        {
            return $"{Day}:{Period}";
        }
    }
}
