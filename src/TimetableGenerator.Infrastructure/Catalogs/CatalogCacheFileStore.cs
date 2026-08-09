using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Storage;

namespace TimetableGenerator.Infrastructure.Catalogs;

public sealed class CatalogCacheFileStore
{
    private readonly GenerationFileStorage mFileStorage;

    private readonly CatalogSynchronizationLimits mLimits;

    private readonly CatalogCacheBinaryCodec mCodec;

    public CatalogCacheFileStore(CatalogCacheFilePath basePath, CatalogSynchronizationLimits limits)
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
            throw new ArgumentException("Catalog cache paths must include a directory.", nameof(basePath));
        }

        GenerationFileStoragePath storagePath = new GenerationFileStoragePath(basePath.Value);
        mFileStorage = new GenerationFileStorage(storagePath);
        mLimits = limits;
        mCodec = new CatalogCacheBinaryCodec(limits);
    }

    public async Task<CatalogCacheLoadResult> LoadAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (mFileStorage.HasDirectory() == false)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        try
        {
            using (GenerationFileStorageAccess storageAccess = await mFileStorage.AcquireExistingDirectoryAsync(cancellationToken).ConfigureAwait(false))
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
        catch (GenerationFileStorageLockException exception)
        {
            throw new CatalogCachePersistenceException("Another application instance is using the catalog cache.", exception.Failure);
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            throw new CatalogCachePersistenceException("The catalog cache generations could not be loaded.", exception);
        }
    }

    public async Task<CatalogCacheLoadResult> LoadMatchingAsync(PlanCatalogBinding catalogBinding, CancellationToken cancellationToken)
    {
        if (catalogBinding == null)
        {
            throw new ArgumentNullException(nameof(catalogBinding));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (mFileStorage.HasDirectory() == false)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        try
        {
            using (GenerationFileStorageAccess storageAccess = await mFileStorage.AcquireExistingDirectoryAsync(cancellationToken).ConfigureAwait(false))
            {
                return await loadMatchingWithoutLockAsync(catalogBinding, cancellationToken).ConfigureAwait(false);
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
        catch (GenerationFileStorageLockException exception)
        {
            throw new CatalogCachePersistenceException("Another application instance is using the catalog cache.", exception.Failure);
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            throw new CatalogCachePersistenceException("The catalog cache generations could not be searched.", exception);
        }
    }

    public async Task SaveAsync(VerifiedCatalogPackage package, CancellationToken cancellationToken)
    {
        await saveAsync(package, null, cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveRetainingAsync(VerifiedCatalogPackage package, PlanCatalogBinding protectedBinding, CancellationToken cancellationToken)
    {
        if (package == null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        if (protectedBinding == null)
        {
            throw new ArgumentNullException(nameof(protectedBinding));
        }

        await saveAsync(package, protectedBinding, cancellationToken).ConfigureAwait(false);
    }

    private async Task saveAsync(VerifiedCatalogPackage package, PlanCatalogBinding? protectedBindingOrNull, CancellationToken cancellationToken)
    {
        if (package == null)
        {
            throw new ArgumentNullException(nameof(package));
        }

        try
        {
            using (GenerationFileStorageAccess storageAccess = await mFileStorage.AcquireCreatingDirectoryAsync(cancellationToken).ConfigureAwait(false))
            {
                await saveWithoutLockAsync(package, protectedBindingOrNull, cancellationToken).ConfigureAwait(false);
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
            throw new CatalogCachePersistenceException("The verified catalog package does not fit the configured cache.", exception);
        }
        catch (GenerationFileStorageLockException exception)
        {
            throw new CatalogCachePersistenceException("Another application instance is using the catalog cache.", exception.Failure);
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            throw new CatalogCachePersistenceException("The verified catalog package could not be cached atomically.", exception);
        }
    }

    private static bool isRecoverableGenerationException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is CatalogCacheDocumentException;
    }

    private static bool isRetentionReadException(Exception exception)
    {
        return isRecoverableGenerationException(exception)
            || exception is CatalogCacheDocumentSizeException
            || exception is UnsupportedCatalogCacheSchemaVersionException;
    }

    private async Task<CatalogCacheLoadResult> loadWithoutLockAsync(CancellationToken cancellationToken)
    {
        IReadOnlyList<GenerationFile> generationFiles = mFileStorage.GetGenerationFiles();
        if (generationFiles.Count == 0)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        List<Exception> failures = new List<Exception>();
        for (int index = 0; index < generationFiles.Count; ++index)
        {
            GenerationFile generationFile = generationFiles[index];
            try
            {
                CatalogCacheDocument document = await readDocumentAsync(generationFile.Path, cancellationToken).ConfigureAwait(false);
                requireMatchingGeneration(document, generationFile);
                if (index == 0)
                {
                    return CatalogCacheLoadResult.createLoadedLatestGeneration(document.Package);
                }

                return CatalogCacheLoadResult.createRecoveredPreviousGeneration(document.Package);
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

        throw new CatalogCachePersistenceException("No valid catalog cache generation could be loaded.", new AggregateException(failures));
    }

    private async Task<CatalogCacheLoadResult> loadMatchingWithoutLockAsync(PlanCatalogBinding catalogBinding, CancellationToken cancellationToken)
    {
        IReadOnlyList<GenerationFile> generationFiles = mFileStorage.GetGenerationFiles();
        if (generationFiles.Count == 0)
        {
            return CatalogCacheLoadResult.createNotFound();
        }

        List<Exception> failures = new List<Exception>();
        CatalogCacheLoadResult? matchingResultOrNull = null;
        int validGenerationCount = 0;
        for (int index = 0; index < generationFiles.Count; ++index)
        {
            GenerationFile generationFile = generationFiles[index];
            try
            {
                CatalogCacheDocument document = await readDocumentAsync(generationFile.Path, cancellationToken).ConfigureAwait(false);
                requireMatchingGeneration(document, generationFile);
                ++validGenerationCount;

                if (matchingResultOrNull == null && hasMatchingCatalogBinding(document.Package, catalogBinding))
                {
                    if (index == 0)
                    {
                        matchingResultOrNull = CatalogCacheLoadResult.createLoadedLatestGeneration(document.Package);
                    }
                    else
                    {
                        matchingResultOrNull = CatalogCacheLoadResult.createRecoveredPreviousGeneration(document.Package);
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

        throw new CatalogCachePersistenceException("No valid catalog cache generation could be searched.", new AggregateException(failures));
    }

    private async Task<CatalogCacheDocument> readDocumentAsync(GenerationFilePath path, CancellationToken cancellationToken)
    {
        byte[] content;
        try
        {
            content = await BoundedFileReader.readAllBytesAsync(path.Value, mLimits.MaximumCacheDocumentBytes, cancellationToken).ConfigureAwait(false);
        }
        catch (BoundedFileReadLimitException)
        {
            throw new CatalogCacheDocumentSizeException("The catalog cache document exceeds the configured size limit.");
        }

        CatalogCacheDocument document = mCodec.Deserialize(content);
        cancellationToken.ThrowIfCancellationRequested();
        return document;
    }

    private async Task saveWithoutLockAsync(VerifiedCatalogPackage package, PlanCatalogBinding? protectedBindingOrNull, CancellationToken cancellationToken)
    {
        IReadOnlyList<GenerationFile> generationFiles = mFileStorage.GetGenerationFiles();
        bool shouldWriteGeneration = await shouldWriteGenerationAsync(generationFiles, package, cancellationToken).ConfigureAwait(false);
        if (shouldWriteGeneration == false)
        {
            return;
        }

        CatalogCacheGeneration nextGeneration = getNextGeneration(generationFiles);
        FileGeneration nextFileGeneration = new FileGeneration(nextGeneration.Value);

        CatalogCacheDocument document = new CatalogCacheDocument(nextGeneration, package);
        byte[] content = mCodec.Serialize(document);
        if (content.LongLength > mLimits.MaximumCacheDocumentBytes)
        {
            throw new CatalogCacheDocumentSizeException("The catalog cache document exceeds the configured size limit.");
        }

        GenerationFile committedGeneration = await mFileStorage.CommitAsync(nextFileGeneration, content, cancellationToken).ConfigureAwait(false);

        CatalogCacheDocument verifiedDocument;
        try
        {
            verifiedDocument = await readDocumentAsync(committedGeneration.Path, CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (isRecoverableGenerationException(exception))
        {
            mFileStorage.TryDeleteGeneration(committedGeneration);
            throw new CatalogCachePersistenceException("The committed catalog cache generation could not be read back.", exception);
        }

        if (verifiedDocument.Generation != nextGeneration)
        {
            mFileStorage.TryDeleteGeneration(committedGeneration);
            throw new CatalogCachePersistenceException("The committed catalog cache generation could not be verified.", new InvalidDataException(committedGeneration.Path.Value));
        }

        await pruneOldGenerationsAsync(protectedBindingOrNull).ConfigureAwait(false);
    }

    private async Task<bool> shouldWriteGenerationAsync(IReadOnlyList<GenerationFile> generationFiles, VerifiedCatalogPackage package, CancellationToken cancellationToken)
    {
        for (int index = 0; index < generationFiles.Count; ++index)
        {
            GenerationFile generationFile = generationFiles[index];
            try
            {
                CatalogCacheDocument document = await readDocumentAsync(generationFile.Path, cancellationToken).ConfigureAwait(false);
                requireMatchingGeneration(document, generationFile);
                bool isNewestGeneration = index == 0;
                bool isSameCatalogArtifact = hasSameCatalogArtifact(document.Package, package);
                return isNewestGeneration == false || isSameCatalogArtifact == false;
            }
            catch (UnsupportedCatalogCacheSchemaVersionException exception)
            {
                throw createUpgradeRequiredException(exception);
            }
            catch (CatalogCacheDocumentSizeException exception)
            {
                throw new CatalogCachePersistenceException("A catalog cache generation is too large to replace safely.", exception);
            }
            catch (Exception exception) when (isRecoverableGenerationException(exception))
            {
                // Continue through damaged generations so a newer schema cannot be hidden below one.
            }
        }

        return true;
    }

    private static bool hasSameCatalogArtifact(VerifiedCatalogPackage first, VerifiedCatalogPackage second)
    {
        return first.Entry.CatalogId == second.Entry.CatalogId
            && first.Entry.Revision == second.Entry.Revision
            && first.Entry.File.Sha256 == second.Entry.File.Sha256;
    }

    private static bool hasMatchingCatalogBinding(VerifiedCatalogPackage package, PlanCatalogBinding catalogBinding)
    {
        return package.CreatePlanCatalogBinding() == catalogBinding;
    }

    private static void requireMatchingGeneration(CatalogCacheDocument document, GenerationFile generationFile)
    {
        if (document.Generation.Value != generationFile.Generation.Value)
        {
            throw new CatalogCacheDocumentException("The catalog cache generation does not match its file name.");
        }
    }

    private static CatalogCacheUpgradeRequiredException createUpgradeRequiredException(UnsupportedCatalogCacheSchemaVersionException exception)
    {
        return new CatalogCacheUpgradeRequiredException(exception.SchemaVersion, exception);
    }

    private static CatalogCacheGeneration getNextGeneration(IReadOnlyList<GenerationFile> generationFiles)
    {
        if (generationFiles.Count == 0)
        {
            return new CatalogCacheGeneration(1L);
        }

        CatalogCacheGeneration latestGeneration = new CatalogCacheGeneration(generationFiles[0].Generation.Value);
        return latestGeneration.GetNext();
    }

    private async Task pruneOldGenerationsAsync(PlanCatalogBinding? protectedBindingOrNull)
    {
        try
        {
            GenerationFileRetentionSet retainedGenerations = new GenerationFileRetentionSet();
            if (protectedBindingOrNull == null)
            {
                mFileStorage.PruneGenerations(retainedGenerations);
                return;
            }

            IReadOnlyList<GenerationFile> generationFiles = mFileStorage.GetGenerationFiles();
            foreach (GenerationFile generationFile in generationFiles)
            {
                try
                {
                    CatalogCacheDocument document = await readDocumentAsync(generationFile.Path, CancellationToken.None).ConfigureAwait(false);
                    requireMatchingGeneration(document, generationFile);
                    if (hasMatchingCatalogBinding(document.Package, protectedBindingOrNull))
                    {
                        retainedGenerations.Retain(generationFile);
                        break;
                    }
                }
                catch (Exception exception) when (
                    isRetentionReadException(exception))
                {
                    retainedGenerations.Retain(generationFile);
                }
            }

            mFileStorage.PruneGenerations(retainedGenerations);
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            // Retention cleanup is non-critical after a new generation is verified.
        }
    }
}
