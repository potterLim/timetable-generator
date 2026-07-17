namespace TimetableGenerator.Domain.Planning;

public sealed record PersonalScheduleLocation
{
    public const int MAXIMUM_LENGTH = 120;

    public string Value { get; }

    public PersonalScheduleLocation(string value)
    {
        Value = PersonalScheduleText.Normalize(
            value,
            MAXIMUM_LENGTH,
            "Personal schedule locations");
    }

    public override string ToString()
    {
        return Value;
    }
}
