namespace TimetableGenerator.Infrastructure.Csv;

public sealed record CourseImportRawValue
{
    public string Value { get; }

    private CourseImportRawValue(string value)
    {
        Value = value;
    }

    internal static CourseImportRawValue create(string? valueOrNull)
    {
        if (valueOrNull == null)
        {
            return new CourseImportRawValue(string.Empty);
        }

        return new CourseImportRawValue(valueOrNull);
    }

    public override string ToString()
    {
        return Value;
    }
}
