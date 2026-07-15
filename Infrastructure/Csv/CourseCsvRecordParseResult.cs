using System;
using System.Collections.Generic;
using TimetableGenerator.Core.Domain;

namespace TimetableGenerator.Infrastructure.Csv;

internal sealed class CourseCsvRecordParseResult
{
    private readonly CourseOffering? mCourseOfferingOrNull;
    private readonly IReadOnlyList<CourseImportDiagnostic> mDiagnostics;

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
            return mCourseOfferingOrNull != null && mDiagnostics.Count == 0;
        }
    }

    private CourseCsvRecordParseResult(
        CourseOffering? courseOfferingOrNull,
        IEnumerable<CourseImportDiagnostic> diagnostics)
    {
        if (diagnostics == null)
        {
            throw new ArgumentNullException(nameof(diagnostics));
        }

        List<CourseImportDiagnostic> copiedDiagnostics = new List<CourseImportDiagnostic>();
        foreach (CourseImportDiagnostic diagnostic in diagnostics)
        {
            if (diagnostic == null)
            {
                throw new ArgumentException("CSV record results cannot contain null diagnostics.", nameof(diagnostics));
            }

            copiedDiagnostics.Add(diagnostic);
        }

        bool hasCourseOffering = courseOfferingOrNull != null;
        bool hasDiagnostics = copiedDiagnostics.Count > 0;
        if (hasCourseOffering == hasDiagnostics)
        {
            throw new ArgumentException("A CSV record result must contain either an offering or diagnostics.");
        }

        mCourseOfferingOrNull = courseOfferingOrNull;
        mDiagnostics = copiedDiagnostics.AsReadOnly();
    }

    public static CourseCsvRecordParseResult CreateSuccess(CourseOffering courseOffering)
    {
        if (courseOffering == null)
        {
            throw new ArgumentNullException(nameof(courseOffering));
        }

        List<CourseImportDiagnostic> diagnostics = new List<CourseImportDiagnostic>();
        return new CourseCsvRecordParseResult(courseOffering, diagnostics);
    }

    public static CourseCsvRecordParseResult CreateFailure(
        IEnumerable<CourseImportDiagnostic> diagnostics)
    {
        return new CourseCsvRecordParseResult(null, diagnostics);
    }

    public CourseOffering GetCourseOffering()
    {
        if (mCourseOfferingOrNull == null)
        {
            throw new InvalidOperationException("A failed CSV record does not contain a course offering.");
        }

        return mCourseOfferingOrNull;
    }
}
