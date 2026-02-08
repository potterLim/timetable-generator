namespace TimetableGenerator
{
    // Value object used as a DataTable cell value for schedule rendering.
    // This is used for custom painting (course text + optional classroom text).
    public sealed class ScheduleCellContent
    {
        public string CourseLine { get; }
        public ClassroomLocation Classroom { get; }

        public ScheduleCellContent(string courseLine, ClassroomLocation classroom)
        {
            CourseLine = courseLine;
            Classroom = classroom;
        }

        public bool HasClassroom()
        {
            return Classroom != null;
        }

        public string GetClassroomLine()
        {
            return Classroom == null ? string.Empty : Classroom.ToDisplayText();
        }

        // Fallback string representation (e.g., if custom painting is not applied).
        public override string ToString()
        {
            if (Classroom == null)
            {
                return CourseLine ?? string.Empty;
            }

            return (CourseLine ?? string.Empty) + "\r\n" + Classroom.ToDisplayText();
        }
    }
}
