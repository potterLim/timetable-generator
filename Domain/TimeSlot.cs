namespace TimetableGenerator
{
    // Parsed time slot used for collision checking and table rendering.
    public class TimeSlot
    {
        public EDay Day { get; private set; }
        public Period Period { get; private set; }

        public CourseId CourseId { get; private set; }
        public string Name { get; private set; }
        public CourseSection Section { get; private set; }

        public ClassroomLocation Classroom { get; private set; }

        public TimeSlot(EDay day, Period period, CourseId courseId, string name, CourseSection section, ClassroomLocation classroom)
        {
            Day = day;
            Period = period;
            CourseId = courseId;
            Name = name;
            Section = section;
            Classroom = classroom;
        }

        public string GetCourseLineText()
        {
            if (Section.IsDefault())
            {
                return Name;
            }

            return $"{Name}({Section.Value})";
        }

        public ScheduleCellContent ToCellContent()
        {
            return new ScheduleCellContent(GetCourseLineText(), Classroom);
        }

        // Collision key for schedule validation (same day and period cannot overlap).
        public ScheduleSlotKey GetCollisionKey()
        {
            return new ScheduleSlotKey(Day, Period);
        }
    }
}
