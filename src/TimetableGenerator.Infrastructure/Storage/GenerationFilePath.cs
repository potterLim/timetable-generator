using System;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed record GenerationFilePath
{
    public string Value { get; }

    public GenerationFilePath(string value)
    {
        if (value == null)
        {
            throw new ArgumentNullException(nameof(value));
        }

        Value = value;
    }

    public override string ToString()
    {
        return Value;
    }
}
