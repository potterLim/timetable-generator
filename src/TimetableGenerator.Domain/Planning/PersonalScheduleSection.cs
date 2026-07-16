using System;

namespace TimetableGenerator.Domain.Planning;

public sealed record PersonalScheduleSection
{
    private const int MAXIMUM_LENGTH = 40;

    public string Value { get; }

    public PersonalScheduleSection(string value)
    {
        Value = PersonalScheduleText.Normalize(
            value,
            MAXIMUM_LENGTH,
            "Personal schedule sections");
    }

    public override string ToString()
    {
        return Value;
    }
}
