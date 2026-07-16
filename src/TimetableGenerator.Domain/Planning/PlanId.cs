using System;

namespace TimetableGenerator.Domain.Planning;

public readonly record struct PlanId
{
    public Guid Value { get; }

    public bool IsValid
    {
        get
        {
            return Value != Guid.Empty;
        }
    }

    public PlanId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Plan IDs cannot be empty.", nameof(value));
        }

        Value = value;
    }

    public static PlanId CreateNew()
    {
        return new PlanId(Guid.NewGuid());
    }

    public override string ToString()
    {
        return Value.ToString("D");
    }
}
