using System;

namespace TimetableGenerator.Domain.Planning;

public readonly record struct CourseChoiceGroupId
{
    public Guid Value { get; }

    public bool IsValid
    {
        get
        {
            return Value != Guid.Empty;
        }
    }

    public CourseChoiceGroupId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException(
                "Course choice group IDs cannot be empty.",
                nameof(value));
        }

        Value = value;
    }

    public static CourseChoiceGroupId CreateNew()
    {
        return new CourseChoiceGroupId(Guid.NewGuid());
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}
