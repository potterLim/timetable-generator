using System;
using System.Globalization;

namespace TimetableGenerator.Infrastructure.Persistence;

public readonly record struct WorkspaceGeneration
{
    private const long FIRST_GENERATION = 1;

    public long Value { get; }

    public bool IsValid
    {
        get
        {
            return Value >= FIRST_GENERATION;
        }
    }

    public string FileComponent
    {
        get
        {
            return "g" + Value.ToString("D20", CultureInfo.InvariantCulture);
        }
    }

    public WorkspaceGeneration(long value)
    {
        if (value < FIRST_GENERATION)
        {
            throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Workspace generations must be positive.");
        }

        Value = value;
    }

    public WorkspaceGeneration GetNext()
    {
        if (Value == long.MaxValue)
        {
            throw new InvalidOperationException("The planning workspace generation range is exhausted.");
        }

        return new WorkspaceGeneration(Value + 1);
    }

    public static bool TryParseFileComponent(string value, out WorkspaceGeneration generation)
    {
        generation = default(WorkspaceGeneration);
        if (value == null || value.Length != 21 || value[0] != 'g')
        {
            return false;
        }

        long parsedValue;
        bool isParsed = long.TryParse(
            value.AsSpan(1),
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out parsedValue);
        if (isParsed == false || parsedValue < FIRST_GENERATION)
        {
            return false;
        }

        generation = new WorkspaceGeneration(parsedValue);
        return true;
    }

    public override string ToString()
    {
        return Value.ToString(CultureInfo.InvariantCulture);
    }
}
