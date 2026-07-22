using System;

namespace TimetableGenerator.Domain.Planning;

public readonly record struct PersonalScheduleId
{
    public Guid Value { get; }

    public bool IsValid
    {
        get
        {
            return Value != Guid.Empty;
        }
    }

    public PersonalScheduleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Personal schedule IDs cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static PersonalScheduleId CreateNew()
    {
        return new PersonalScheduleId(Guid.NewGuid());
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}
