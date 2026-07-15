using System;

namespace TimetableGenerator.Infrastructure.Csv;

public sealed class CourseCsvImportOptions
{
    private const int DEFAULT_MAXIMUM_DIAGNOSTIC_COUNT = 100;

    public DiagnosticCountLimit MaximumDiagnosticCount { get; }

    public CourseCsvImportOptions(DiagnosticCountLimit maximumDiagnosticCount)
    {
        if (maximumDiagnosticCount.IsValid == false)
        {
            throw new ArgumentException("A valid maximum diagnostic count is required.", nameof(maximumDiagnosticCount));
        }

        MaximumDiagnosticCount = maximumDiagnosticCount;
    }

    public static CourseCsvImportOptions CreateDefault()
    {
        DiagnosticCountLimit maximumDiagnosticCount = new DiagnosticCountLimit(
            DEFAULT_MAXIMUM_DIAGNOSTIC_COUNT);
        return new CourseCsvImportOptions(maximumDiagnosticCount);
    }
}
