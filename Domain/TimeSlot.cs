namespace TimetableGenerator
{
    // Parsed time slot used for collision checking and table rendering.
    public sealed class TimeSlot
    {
        public EDay Day { get; }
        public Period Period { get; }

        public CourseId CourseId { get; }
        public string Name { get; }
        public CourseSection Section { get; }

        public ClassroomLocation Classroom { get; }

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
