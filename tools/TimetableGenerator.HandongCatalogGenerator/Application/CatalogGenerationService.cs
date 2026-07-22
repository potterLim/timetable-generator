using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.HandongCatalogGenerator.Application.Errors;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;
using TimetableGenerator.HandongCatalogGenerator.Publishing;

namespace TimetableGenerator.HandongCatalogGenerator.Application;

internal sealed class CatalogGenerationService
{
    public async Task<CatalogGenerationResult> GenerateAsync(
        CatalogGenerationRequest request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        HandongExportDocument sourceDocument = await readSourceAsync(
            request.SourceFilePath,
            cancellationToken).ConfigureAwait(false);
        validateAcademicTerm(sourceDocument, request.Term);
        CourseCatalog catalog = normalizeCatalog(sourceDocument);
        byte[] catalogContent = serializeCatalog(catalog, request.Term, request.Revision, sourceDocument);
        CatalogPackageWriteResult package = await publishCatalogAsync(
            request,
            catalog,
            catalogContent,
            cancellationToken).ConfigureAwait(false);

        return new CatalogGenerationResult(
            new CatalogArtifactPath(package.CatalogPath),
            new CatalogArtifactPath(package.IndexPath),
            package.CatalogFileSize,
            package.IndexFileSize,
            package.CatalogSha256,
            Sha256Digest.Parse(sourceDocument.SourceSha256Hex),
            new CatalogGenerationSummary(catalog));
    }

    private static async Task<HandongExportDocument> readSourceAsync(
        CatalogSourceFilePath sourceFilePath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(sourceFilePath.Value) == false)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.SourceFileNotFound,
                ECatalogGeneratorExitCode.SourceFailure,
                "The Handong source file does not exist: " + sourceFilePath.Value);
        }

        try
        {
            return await HandongExportReader.ReadAsync(sourceFilePath, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (HandongSourceFormatException exception)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.SourceSchemaMismatch,
                ECatalogGeneratorExitCode.SourceFailure,
                exception.Message,
                exception);
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.SourceReadFailed,
                ECatalogGeneratorExitCode.SourceFailure,
                "The Handong source file could not be read: " + sourceFilePath.Value,
                exception);
        }
    }

    private static void validateAcademicTerm(
        HandongExportDocument sourceDocument,
        AcademicTerm requestedTerm)
    {
        foreach (AcademicTerm sourceTerm in sourceDocument.AcademicTerms)
        {
            if (sourceTerm != requestedTerm)
            {
                throw new CatalogGenerationException(
                    ECatalogGenerationErrorCode.TermMismatch,
                    ECatalogGeneratorExitCode.DataValidationFailed,
                    "The source contains term " + sourceTerm.Id
                    + " but --term specifies " + requestedTerm.Id + ".");
            }
        }
    }

    private static CourseCatalog normalizeCatalog(HandongExportDocument sourceDocument)
    {
        try
        {
            HandongCatalogNormalizer normalizer = new HandongCatalogNormalizer();
            return normalizer.NormalizeCatalog(sourceDocument);
        }
        catch (InvalidHandongSourceRecordException exception)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.InvalidSourceRecord,
                ECatalogGeneratorExitCode.DataValidationFailed,
                exception.Message,
                exception);
        }
        catch (DuplicateCourseOfferingException exception)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.DuplicateOffering,
                ECatalogGeneratorExitCode.DataValidationFailed,
                exception.Message,
                exception);
        }
        catch (ConflictingCourseDefinitionException exception)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.ConflictingCourseDefinition,
                ECatalogGeneratorExitCode.DataValidationFailed,
                exception.Message,
                exception);
        }
        catch (Exception exception) when (
            exception is ArgumentException || exception is FormatException)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.InvalidSourceRecord,
                ECatalogGeneratorExitCode.DataValidationFailed,
                "The source contains a value that cannot be normalized: " + exception.Message,
                exception);
        }
    }

    private static byte[] serializeCatalog(
        CourseCatalog catalog,
        AcademicTerm term,
        CatalogRevision revision,
        HandongExportDocument sourceDocument)
    {
        try
        {
            return CatalogJsonWriter.Write(catalog, term, revision, sourceDocument);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException || exception is ArgumentOutOfRangeException)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.CatalogSerializationFailed,
                ECatalogGeneratorExitCode.DataValidationFailed,
                "The normalized catalog cannot be serialized: " + exception.Message,
                exception);
        }
    }

    private static async Task<CatalogPackageWriteResult> publishCatalogAsync(
        CatalogGenerationRequest request,
        CourseCatalog catalog,
        ReadOnlyMemory<byte> catalogContent,
        CancellationToken cancellationToken)
    {
        try
        {
            return await CatalogPackageWriter.WriteAsync(
                request.OutputRootPath,
                request.Term,
                request.Revision,
                catalog,
                catalogContent,
                cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CatalogOutputConflictException exception)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.OutputConflict,
                ECatalogGeneratorExitCode.OutputFailure,
                exception.Message,
                exception);
        }
        catch (CatalogIndexFormatException exception)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.InvalidExistingIndex,
                ECatalogGeneratorExitCode.OutputFailure,
                exception.Message,
                exception);
        }
        catch (Exception exception) when (
            exception is IOException || exception is UnauthorizedAccessException)
        {
            throw new CatalogGenerationException(
                ECatalogGenerationErrorCode.OutputWriteFailed,
                ECatalogGeneratorExitCode.OutputFailure,
                "The catalog package could not be written: " + exception.Message,
                exception);
        }
    }
}
