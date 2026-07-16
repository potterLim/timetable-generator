using System;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Application;

internal sealed record CatalogGenerationRequest
{
    public CatalogSourceFilePath SourceFilePath { get; }
    public AcademicTerm Term { get; }
    public CatalogRevision Revision { get; }
    public CatalogOutputRootPath OutputRootPath { get; }

    public CatalogGenerationRequest(
        CatalogSourceFilePath sourceFilePath,
        AcademicTerm term,
        CatalogRevision revision,
        CatalogOutputRootPath outputRootPath)
    {
        if (sourceFilePath == null)
        {
            throw new ArgumentNullException(nameof(sourceFilePath));
        }

        if (term.AcademicYear.Value == 0 || term.Semester.Value == 0)
        {
            throw new ArgumentException("The academic term must be initialized.", nameof(term));
        }

        if (revision.Value == 0)
        {
            throw new ArgumentException("The catalog revision must be initialized.", nameof(revision));
        }

        if (outputRootPath == null)
        {
            throw new ArgumentNullException(nameof(outputRootPath));
        }

        SourceFilePath = sourceFilePath;
        Term = term;
        Revision = revision;
        OutputRootPath = outputRootPath;
    }
}
