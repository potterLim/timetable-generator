namespace TimetableGenerator.Domain.Planning;

public sealed class PersonalScheduleDetails
{
    public PersonalScheduleSection? SectionOrNull { get; }

    public PersonalScheduleInstructor? InstructorOrNull { get; }

    public PersonalScheduleLocation? LocationOrNull { get; }

    public PersonalScheduleDetails(PersonalScheduleSection? sectionOrNull, PersonalScheduleInstructor? instructorOrNull, PersonalScheduleLocation? locationOrNull)
    {
        SectionOrNull = sectionOrNull;
        InstructorOrNull = instructorOrNull;
        LocationOrNull = locationOrNull;
    }

    public static PersonalScheduleDetails CreateEmpty()
    {
        return new PersonalScheduleDetails(null, null, null);
    }
}
