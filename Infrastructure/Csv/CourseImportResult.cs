using System;
using System.Collections.Generic;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Infrastructure.Csv;

public sealed class CourseImportResult
{
    private readonly IReadOnlyList<CourseOffering> mCourseOfferings;
    private readonly IReadOnlyList<CourseImportDiagnostic> mDiagnostics;

    public IReadOnlyList<CourseOffering> CourseOfferings
    {
        get
        {
            return mCourseOfferings;
        }
    }

    public IReadOnlyList<CourseImportDiagnostic> Diagnostics
    {
        get
        {
            return mDiagnostics;
        }
    }

    public bool IsSuccessful
    {
        get
        {
            return mDiagnostics.Count == 0;
        }
    }

    public EDiagnosticCollectionCompletion DiagnosticCollectionCompletion { get; }

    public bool HasReachedDiagnosticLimit
    {
        get
        {
            return DiagnosticCollectionCompletion == EDiagnosticCollectionCompletion.MaximumCountReached;
        }
    }

    internal CourseImportResult(
        IEnumerable<CourseOffering> courseOfferings,
        IEnumerable<CourseImportDiagnostic> diagnostics,
        EDiagnosticCollectionCompletion diagnosticCollectionCompletion)
    {
        if (courseOfferings == null)
        {
            throw new ArgumentNullException(nameof(courseOfferings));
        }

        if (diagnostics == null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        if (Enum.IsDefined(
            typeof(EDiagnosticCollectionCompletion),
            diagnosticCollectionCompletion) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(diagnosticCollectionCompletion));
        }

        List<CourseOffering> copiedCourseOfferings = new List<CourseOffering>();
        foreach (CourseOffering courseOffering in courseOfferings)
        {
            if (courseOffering == null)
            {
                throw new ArgumentException("Import results cannot contain null course offerings.", nameof(courseOfferings));
            }

            copiedCourseOfferings.Add(courseOffering);
        }

        List<CourseImportDiagnostic> copiedDiagnostics = new List<CourseImportDiagnostic>();
        foreach (CourseImportDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic == null)
            {
                throw new ArgumentException("Import results cannot contain null diagnostics.", nameof(diagnostics));
            }

            copiedDiagnostics.Add(diagnostic);
        }

        bool hasCourseOfferings = copiedCourseOfferings.Count > 0;
        bool hasDiagnostics = copiedDiagnostics.Count > 0;
        if (hasCourseOfferings == hasDiagnostics)
        {
            throw new ArgumentException(
                "An import result must expose either imported offerings or diagnostics.");
        }

        mCourseOfferings = copiedCourseOfferings.AsReadOnly();
        mDiagnostics = copiedDiagnostics.AsReadOnly();
        DiagnosticCollectionCompletion = diagnosticCollectionCompletion;
    }
}
