using System;
using System.Globalization;

namespace TimetableGenerator.Application.Planning;

public readonly record struct PlanningWorkspaceConcurrencyToken
{
    private const long MISSING_WORKSPACE_GENERATION = 0L;

    public static PlanningWorkspaceConcurrencyToken MissingWorkspace { get; } =
        new PlanningWorkspaceConcurrencyToken(MISSING_WORKSPACE_GENERATION);

    public long Value { get; }

    public bool RepresentsMissingWorkspace
    {
        get
        {
            return Value == MISSING_WORKSPACE_GENERATION;
        }
    }

    public PlanningWorkspaceConcurrencyToken(long value)
    {
        if (value < MISSING_WORKSPACE_GENERATION)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Planning workspace concurrency tokens cannot be negative.");
        }

        Value = value;
    }

    public PlanningWorkspaceConcurrencyToken GetNext()
    {
        if (Value == long.MaxValue)
        {
            throw new InvalidOperationException(
                "The planning workspace concurrency token range is exhausted.");
        }

        return new PlanningWorkspaceConcurrencyToken(Value + 1L);
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
