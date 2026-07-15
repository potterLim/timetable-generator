using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal static class CatalogPackageWriter
{
    public static async Task<CatalogPackageWriteResult> WriteAsync(
        CatalogOutputRootPath outputRootPath,
        AcademicTerm term,
        CatalogRevision revision,
        CatalogPublicationTime publicationTime,
        CourseCatalog catalog,
        ReadOnlyMemory<byte> catalogContent,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(outputRootPath);
        ArgumentNullException.ThrowIfNull(catalog);

        string catalogPath = CatalogFileLayout.GetCatalogPath(outputRootPath, term, revision);
        string indexPath = CatalogFileLayout.GetIndexPath(outputRootPath);
        IReadOnlyList<CatalogIndexEntry> existingEntries = await readExistingEntriesAsync(
            indexPath,
            cancellationToken).ConfigureAwait(false);

        await ensureImmutableCatalogAsync(
            catalogPath,
            catalogContent,
            cancellationToken).ConfigureAwait(false);

        Sha256Digest catalogSha256 = Sha256Digest.Compute(catalogContent.Span);
        CatalogFileSize catalogFileSize = new CatalogFileSize(catalogContent.Length);
        CatalogIndexEntry currentEntry = new CatalogIndexEntry(
            term,
            revision,
            publicationTime,
            catalogFileSize,
            catalogSha256,
            catalog.CourseCount,
            catalog.OfferingCount);
        CatalogIndexDocument indexDocument = CatalogIndexDocument.CreateWithUpsertedEntry(
            publicationTime,
            currentEntry,
            existingEntries);
        byte[] indexContent = CatalogIndexJsonWriter.Write(indexDocument);
        await AtomicFileWriter.WriteAsync(
            indexPath,
            indexContent,
            EExistingFileBehavior.Replace,
            cancellationToken).ConfigureAwait(false);

        return new CatalogPackageWriteResult(
            catalogPath,
            indexPath,
            catalogFileSize,
            new CatalogFileSize(indexContent.LongLength),
            catalogSha256);
    }

    private static async Task<IReadOnlyList<CatalogIndexEntry>> readExistingEntriesAsync(
        string indexPath,
        CancellationToken cancellationToken)
    {
        if (File.Exists(indexPath) == false)
        {
            return Array.Empty<CatalogIndexEntry>();
        }

        byte[] existingContent = await File.ReadAllBytesAsync(
            indexPath,
            cancellationToken).ConfigureAwait(false);
        CatalogIndexDocument existingDocument = CatalogIndexReader.Read(existingContent);

        return existingDocument.Entries;
    }

    private static async Task ensureImmutableCatalogAsync(
        string catalogPath,
        ReadOnlyMemory<byte> catalogContent,
        CancellationToken cancellationToken)
    {
        if (File.Exists(catalogPath))
        {
            await ensureExistingContentMatchesAsync(
                catalogPath,
                catalogContent,
                cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            await AtomicFileWriter.WriteAsync(
                catalogPath,
                catalogContent,
                EExistingFileBehavior.Reject,
                cancellationToken).ConfigureAwait(false);
        }
        catch (IOException) when (File.Exists(catalogPath))
        {
            await ensureExistingContentMatchesAsync(
                catalogPath,
                catalogContent,
                cancellationToken).ConfigureAwait(false);
        }
    }

    private static async Task ensureExistingContentMatchesAsync(
        string catalogPath,
        ReadOnlyMemory<byte> expectedContent,
        CancellationToken cancellationToken)
    {
        byte[] existingContent = await File.ReadAllBytesAsync(
            catalogPath,
            cancellationToken).ConfigureAwait(false);
        if (expectedContent.Span.SequenceEqual(existingContent) == false)
        {
            throw new CatalogOutputConflictException(catalogPath);
        }
    }
}
