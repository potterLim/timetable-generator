using System;
using System.Collections.Generic;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Infrastructure.Csv;

internal sealed class CourseCsvImportState
{
    public CourseCsvImportOptions Options { get; }

    public List<CourseOffering> CourseOfferings { get; }

    public List<CourseImportDiagnostic> Diagnostics { get; }

    public EDiagnosticCollectionCompletion DiagnosticCollectionCompletion { get; private set; }

    public bool ShouldStopCollectingDiagnostics
    {
        get
        {
            return DiagnosticCollectionCompletion == EDiagnosticCollectionCompletion.MaximumCountReached;
        }
    }

    public CourseCsvImportState(CourseCsvImportOptions options)
    {
        if (options == null)
        {
            throw new ArgumentNullException(nameof(options));
        }

        Options = options;
        CourseOfferings = new List<CourseOffering>();
        Diagnostics = new List<CourseImportDiagnostic>();
        DiagnosticCollectionCompletion = EDiagnosticCollectionCompletion.Completed;
    }

    public bool TryAddDiagnostic(CourseImportDiagnostic diagnostic)
    {
        if (diagnostic == null)
        {
            throw new ArgumentNullException(nameof(diagnostic));
        }

        if (Diagnostics.Count >= Options.MaximumDiagnosticCount.Value)
        {
            DiagnosticCollectionCompletion = EDiagnosticCollectionCompletion.MaximumCountReached;
            return false;
        }

        Diagnostics.Add(diagnostic);
        return true;
    }
}
