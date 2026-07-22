namespace TimetableGenerator.Domain.Planning;

public sealed record PersonalScheduleInstructor
{
    public const int MAXIMUM_LENGTH = 80;

    public string Value { get; }

    public PersonalScheduleInstructor(string value)
    {
        Value = PersonalScheduleText.Normalize(value, MAXIMUM_LENGTH, "Personal schedule instructors");
    }

    public override string ToString()
    {
        return Value;
    }
}
