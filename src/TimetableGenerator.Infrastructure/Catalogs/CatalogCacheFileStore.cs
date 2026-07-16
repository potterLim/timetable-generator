using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class CatalogCacheFileStore
{
    private const int FILE_BUFFER_SIZE = 16_384;
    private const int LOCK_RETRY_DELAY_MILLISECONDS = 50;
    private const int MAXIMUM_LOCK_ATTEMPTS = 100;
    private const int MAXIMUM_RETAINED_GENERATIONS = 5;

    private readonly string mDirectoryPath;

    private readonly string mBaseFileName;

    private readonly string mFileExtension;

    private readonly string mGenerationSearchPattern;

    private readonly string mLockPath;

    private readonly CatalogSynchronizationLimits mLimits;

    private readonly CatalogCacheBinaryCodec mCodec;

    private readonly SemaphoreSlim mAccessGate;

    public CatalogCacheFileStore(
        CatalogCacheFilePath basePath,
        CatalogSynchronizationLimits limits)
    {
        if (basePath == null)
        {
            throw new ArgumentNullException(nameof(basePath));
        }

        if (limits == null)
        {
            throw new ArgumentNullException(nameof(limits));
        }

        string? directoryPathOrNull = Path.GetDirectoryName(basePath.Value);
        if (directoryPathOrNull == null)
        {
            throw new ArgumentException(
                "Catalog cache paths must include a directory.",
                nameof(basePath));
        }

        mDirectoryPath = directoryPathOrNull;
        mBaseFileName = Path.GetFileNameWithoutExtension(basePath.Value);
        mFileExtension = Path.GetExtension(basePath.Value);
        mGenerationSearchPattern = mBaseFileName + ".g*" + mFileExtension;
        mLockPath = Path.Combine(mDirectoryPath, mBaseFileName + ".lock");
        mLimits = limits;
        mCodec = new CatalogCacheBinaryCodec(limits);
        mAccessGate = new SemaphoreSlim(1, 1);
    }

    public async Task<CatalogCacheLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(mDirectoryPath) == false)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (FileStream processLock = await acquireCrossProcessLockAsync(
                cancellationToken).ConfigureAwait(false))
            {
                return await loadWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CatalogCacheUpgradeRequiredException)
        {
            throw;
        }
        catch (CatalogCachePersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            throw new CatalogCachePersistenceException(
                "The catalog cache generations could not be loaded.",
                exception);
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    public async Task<CatalogCacheLoadResult> LoadMatchingAsync(
        PlanCatalogBinding catalogBinding,
        CancellationToken cancellationToken)
    {
        if (catalogBinding == null)
        {
            throw new ArgumentNullException(nameof(catalogBinding));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (Directory.Exists(mDirectoryPath) == false)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            using (FileStream processLock = await acquireCrossProcessLockAsync(
                cancellationToken).ConfigureAwait(false))
            {
                return await loadMatchingWithoutLockAsync(
                    catalogBinding,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CatalogCacheUpgradeRequiredException)
        {
            throw;
        }
        catch (CatalogCachePersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            throw new CatalogCachePersistenceException(
                "The catalog cache generations could not be searched.",
                exception);
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    public async Task SaveAsync(
        VerifiedCatalogPackage package,
        CancellationToken cancellationToken)
    {
        if (package == null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(mDirectoryPath);
            using (FileStream processLock = await acquireCrossProcessLockAsync(
                cancellationToken).ConfigureAwait(false))
            {
                await saveWithoutLockAsync(package, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (CatalogCachePersistenceException)
        {
            throw;
        }
        catch (CatalogCacheDocumentException exception)
        {
            throw new CatalogCachePersistenceException(
                "The verified catalog package does not fit the configured cache.",
                exception);
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            throw new CatalogCachePersistenceException(
                "The verified catalog package could not be cached atomically.",
                exception);
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    private static int compareGenerationFilesDescending(
        CatalogCacheGenerationFile first,
        CatalogCacheGenerationFile second)
    {
        return second.Generation.Value.CompareTo(first.Generation.Value);
    }

    private static bool isFileSystemException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException;
    }

    private static bool isRecoverableGenerationException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is CatalogCacheDocumentException;
    }

    private static async Task writeDurableFileAsync(
        string path,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        FileOptions fileOptions = FileOptions.Asynchronous | FileOptions.WriteThrough;
        using (FileStream outputStream = new FileStream(
            path,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            FILE_BUFFER_SIZE,
            fileOptions))
        {
            await outputStream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
            await outputStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            outputStream.Flush(true);
        }
    }

    private async Task<FileStream> acquireCrossProcessLockAsync(
        CancellationToken cancellationToken)
    {
        IOException? lastExceptionOrNull = null;
        for (int attempt = 0; attempt < MAXIMUM_LOCK_ATTEMPTS; ++attempt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    mLockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.Asynchronous);
            }
            catch (IOException exception)
            {
                lastExceptionOrNull = exception;
                await Task.Delay(
                    LOCK_RETRY_DELAY_MILLISECONDS,
                    cancellationToken).ConfigureAwait(false);
            }
        }

        InvalidOperationException fallbackException = new InvalidOperationException(
            "The catalog cache lock attempt ended without an I/O exception.");
        Exception innerException = fallbackException;
        if (lastExceptionOrNull != null)
        {
            innerException = lastExceptionOrNull;
        }

        throw new CatalogCachePersistenceException(
            "Another application instance is using the catalog cache.",
            innerException);
    }

    private List<CatalogCacheGenerationFile> getGenerationFiles()
    {
        List<CatalogCacheGenerationFile> generationFiles =
            new List<CatalogCacheGenerationFile>();
        IEnumerable<string> paths = Directory.EnumerateFiles(
            mDirectoryPath,
            mGenerationSearchPattern,
            SearchOption.TopDirectoryOnly);
        foreach (string path in paths)
        {
            CatalogCacheGeneration generation;
            if (tryParseGenerationPath(path, out generation))
            {
                generationFiles.Add(new CatalogCacheGenerationFile(generation, path));
            }
        }

        generationFiles.Sort(compareGenerationFilesDescending);
        return generationFiles;
    }

    private async Task<CatalogCacheLoadResult> loadWithoutLockAsync(
        CancellationToken cancellationToken)
    {
        List<CatalogCacheGenerationFile> generationFiles = getGenerationFiles();
        if (generationFiles.Count == 0)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        List<Exception> failures = new List<Exception>();
        for (int index = 0; index < generationFiles.Count; ++index)
        {
            CatalogCacheGenerationFile generationFile = generationFiles[index];
            try
            {
                CatalogCacheDocument document = await readDocumentAsync(
                    generationFile.Path,
                    cancellationToken).ConfigureAwait(false);
                requireMatchingGeneration(document, generationFile);
                if (index == 0)
                {
                    return CatalogCacheLoadResult.createLoadedLatestGeneration(
                        document.Package);
                }

                return CatalogCacheLoadResult.createRecoveredPreviousGeneration(
                    document.Package);
            }
            catch (UnsupportedCatalogCacheSchemaVersionException exception)
            {
                throw createUpgradeRequiredException(exception);
            }
            catch (Exception exception) when (isRecoverableGenerationException(exception))
            {
                failures.Add(exception);
            }
        }

        throw new CatalogCachePersistenceException(
            "No valid catalog cache generation could be loaded.",
            new AggregateException(failures));
    }

    private async Task<CatalogCacheLoadResult> loadMatchingWithoutLockAsync(
        PlanCatalogBinding catalogBinding,
        CancellationToken cancellationToken)
    {
        List<CatalogCacheGenerationFile> generationFiles = getGenerationFiles();
        if (generationFiles.Count == 0)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        List<Exception> failures = new List<Exception>();
        CatalogCacheLoadResult? matchingResultOrNull = null;
        int validGenerationCount = 0;
        for (int index = 0; index < generationFiles.Count; ++index)
        {
            CatalogCacheGenerationFile generationFile = generationFiles[index];
            try
            {
                CatalogCacheDocument document = await readDocumentAsync(
                    generationFile.Path,
                    cancellationToken).ConfigureAwait(false);
                requireMatchingGeneration(document, generationFile);
                ++validGenerationCount;

                if (matchingResultOrNull == null
                    && hasMatchingCatalogBinding(document.Package, catalogBinding))
                {
                    if (index == 0)
                    {
                        matchingResultOrNull =
                            CatalogCacheLoadResult.createLoadedLatestGeneration(
                                document.Package);
                    }
                    else
                    {
                        matchingResultOrNull =
                            CatalogCacheLoadResult.createRecoveredPreviousGeneration(
                                document.Package);
                    }
                }
            }
            catch (UnsupportedCatalogCacheSchemaVersionException exception)
            {
                throw createUpgradeRequiredException(exception);
            }
            catch (Exception exception) when (isRecoverableGenerationException(exception))
            {
                failures.Add(exception);
            }
        }

        if (matchingResultOrNull != null)
        {
            return matchingResultOrNull;
        }

        if (validGenerationCount > 0)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        throw new CatalogCachePersistenceException(
            "No valid catalog cache generation could be searched.",
            new AggregateException(failures));
    }

    private async Task<CatalogCacheDocument> readDocumentAsync(
        string path,
        CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new FileInfo(path);
        long documentLength = fileInfo.Length;
        if (documentLength > mLimits.MaximumCacheDocumentBytes)
        {
            throw new CatalogCacheDocumentSizeException(
                "The catalog cache document exceeds the configured size limit.");
        }

        byte[] content = await File.ReadAllBytesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        CatalogCacheDocument document = mCodec.Deserialize(content);
        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }

    private async Task saveWithoutLockAsync(
        VerifiedCatalogPackage package,
        CancellationToken cancellationToken)
    {
        List<CatalogCacheGenerationFile> generationFiles = getGenerationFiles();
        bool shouldWriteGeneration = await shouldWriteGenerationAsync(
            generationFiles,
            package,
            cancellationToken).ConfigureAwait(false);
        if (shouldWriteGeneration == false)
        {
            return;
        }

        CatalogCacheGeneration nextGeneration = new CatalogCacheGeneration(1L);
        if (generationFiles.Count > 0)
        {
            nextGeneration = generationFiles[0].Generation.GetNext();
        }

        CatalogCacheDocument document = new CatalogCacheDocument(nextGeneration, package);
        byte[] content = mCodec.Serialize(document);
        string finalPath = createGenerationPath(nextGeneration);
        string temporaryPath = finalPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await writeDurableFileAsync(
                temporaryPath,
                content,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath);

            CatalogCacheDocument verifiedDocument;
            try
            {
                verifiedDocument = await readDocumentAsync(
                    finalPath,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (isRecoverableGenerationException(exception))
            {
                tryDeleteFile(finalPath);
                throw new CatalogCachePersistenceException(
                    "The committed catalog cache generation could not be read back.",
                    exception);
            }

            if (verifiedDocument.Generation != nextGeneration)
            {
                tryDeleteFile(finalPath);
                throw new CatalogCachePersistenceException(
                    "The committed catalog cache generation could not be verified.",
                    new InvalidDataException(finalPath));
            }

            pruneOldGenerations();
        }
        finally
        {
            tryDeleteFile(temporaryPath);
        }
    }

    private async Task<bool> shouldWriteGenerationAsync(
        IReadOnlyList<CatalogCacheGenerationFile> generationFiles,
        VerifiedCatalogPackage package,
        CancellationToken cancellationToken)
    {
        for (int index = 0; index < generationFiles.Count; ++index)
        {
            CatalogCacheGenerationFile generationFile = generationFiles[index];
            try
            {
                CatalogCacheDocument document = await readDocumentAsync(
                    generationFile.Path,
                    cancellationToken).ConfigureAwait(false);
                requireMatchingGeneration(document, generationFile);
                bool isNewestGeneration = index == 0;
                bool isSameCatalogArtifact = hasSameCatalogArtifact(
                    document.Package,
                    package);
                return isNewestGeneration == false || isSameCatalogArtifact == false;
            }
            catch (UnsupportedCatalogCacheSchemaVersionException exception)
            {
                throw createUpgradeRequiredException(exception);
            }
            catch (CatalogCacheDocumentSizeException exception)
            {
                throw new CatalogCachePersistenceException(
                    "A catalog cache generation is too large to replace safely.",
                    exception);
            }
            catch (Exception exception) when (isRecoverableGenerationException(exception))
            {
                // Continue through damaged generations so a newer schema cannot be hidden below one.
            }
        }

        return true;
    }

    private static bool hasSameCatalogArtifact(
        VerifiedCatalogPackage first,
        VerifiedCatalogPackage second)
    {
        return first.Entry.CatalogId == second.Entry.CatalogId
            && first.Entry.Revision == second.Entry.Revision
            && first.Entry.File.Sha256 == second.Entry.File.Sha256;
    }

    private static bool hasMatchingCatalogBinding(
        VerifiedCatalogPackage package,
        PlanCatalogBinding catalogBinding)
    {
        return package.Entry.CatalogId == catalogBinding.CatalogId
            && package.Entry.Term == catalogBinding.Term
            && package.Entry.Revision == catalogBinding.Revision;
    }

    private static void requireMatchingGeneration(
        CatalogCacheDocument document,
        CatalogCacheGenerationFile generationFile)
    {
        if (document.Generation != generationFile.Generation)
        {
            throw new CatalogCacheDocumentException(
                "The catalog cache generation does not match its file name.");
        }
    }

    private static CatalogCacheUpgradeRequiredException createUpgradeRequiredException(
        UnsupportedCatalogCacheSchemaVersionException exception)
    {
        return new CatalogCacheUpgradeRequiredException(exception.SchemaVersion, exception);
    }

    private string createGenerationPath(CatalogCacheGeneration generation)
    {
        string fileName = mBaseFileName
            + "."
            + generation.FileComponent
            + mFileExtension;
        return Path.Combine(mDirectoryPath, fileName);
    }

    private void pruneOldGenerations()
    {
        try
        {
            List<CatalogCacheGenerationFile> generationFiles = getGenerationFiles();
            for (int index = MAXIMUM_RETAINED_GENERATIONS;
                index < generationFiles.Count;
                ++index)
            {
                tryDeleteFile(generationFiles[index].Path);
            }
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            // Retention cleanup is non-critical after a new generation is verified.
        }
    }

    private bool tryParseGenerationPath(
        string path,
        out CatalogCacheGeneration generation)
    {
        generation = default(CatalogCacheGeneration);
        string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(path);
        string expectedPrefix = mBaseFileName + ".";
        if (fileNameWithoutExtension.StartsWith(
            expectedPrefix,
            StringComparison.Ordinal) == false)
        {
            return false;
        }

        string generationComponent = fileNameWithoutExtension.Substring(
            expectedPrefix.Length);
        return CatalogCacheGeneration.TryParseFileComponent(
            generationComponent,
            out generation);
    }

    private void tryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            // A stale temporary or old generation is safer than masking the primary result.
        }
    }
}
