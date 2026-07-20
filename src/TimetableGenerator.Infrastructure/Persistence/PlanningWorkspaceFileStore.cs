using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Infrastructure.Storage;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed class PlanningWorkspaceFileStore : IPlanningWorkspaceStore
{
    private readonly GenerationFileStorage mFileStorage;

    private readonly PlanningWorkspaceJsonCodec mCodec;

    private readonly WorkspaceDocumentSizeLimit mDocumentSizeLimit;

    public PlanningWorkspaceFileStore(
        WorkspaceFilePath basePath,
        PlanningWorkspaceJsonCodec codec,
        WorkspaceDocumentSizeLimit documentSizeLimit)
    {
        if (basePath == null)
        {
            throw new ArgumentNullException(nameof(basePath));
        }

        if (codec == null)
        {
            throw new ArgumentNullException(nameof(codec));
        }

        if (documentSizeLimit.IsValid == false)
        {
            throw new ArgumentException(
                "Planning workspace stores require a valid document size limit.",
                nameof(documentSizeLimit));
        }

        string? directoryPathOrNull = Path.GetDirectoryName(basePath.Value);
        if (directoryPathOrNull == null)
        {
            throw new ArgumentException(
                "Workspace file paths must include a directory.",
                nameof(basePath));
        }

        GenerationFileStoragePath storagePath = new GenerationFileStoragePath(
            basePath.Value);
        mFileStorage = new GenerationFileStorage(storagePath);
        mCodec = codec;
        mDocumentSizeLimit = documentSizeLimit;
    }

    public async Task<PlanningWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (mFileStorage.HasDirectory() == false)
        {
            return PlanningWorkspaceLoadResult.CreateNotFound();
        }

        try
        {
            using (GenerationFileStorageAccess storageAccess =
                await mFileStorage.AcquireExistingDirectoryAsync(
                cancellationToken).ConfigureAwait(false))
            {
                return await loadWithoutLockAsync(cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PlanningWorkspaceUpgradeRequiredException)
        {
            throw;
        }
        catch (WorkspacePersistenceException)
        {
            throw;
        }
        catch (GenerationFileStorageLockException exception)
        {
            throw new WorkspacePersistenceException(
                "Another application instance is using the planning workspace.",
                exception.Failure);
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            throw new WorkspacePersistenceException(
                "The planning workspace generations could not be loaded.",
                exception);
        }
    }

    public async Task<PlanningWorkspaceConcurrencyToken> SaveAsync(
        PlanningWorkspace workspace,
        PlanningWorkspaceConcurrencyToken expectedToken,
        CancellationToken cancellationToken)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        try
        {
            using (GenerationFileStorageAccess storageAccess =
                await mFileStorage.AcquireCreatingDirectoryAsync(
                cancellationToken).ConfigureAwait(false))
            {
                return await saveWithoutLockAsync(
                    workspace,
                    expectedToken,
                    cancellationToken).ConfigureAwait(false);
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (WorkspacePersistenceException)
        {
            throw;
        }
        catch (GenerationFileStorageLockException exception)
        {
            throw new WorkspacePersistenceException(
                "Another application instance is using the planning workspace.",
                exception.Failure);
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            throw new WorkspacePersistenceException(
                "The planning workspace could not be saved atomically.",
                exception);
        }
    }

    private static bool isRecoverableGenerationException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is WorkspaceDocumentException;
    }

    private async Task<PlanningWorkspaceLoadResult> loadWithoutLockAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GenerationFile> generationFiles =
            mFileStorage.GetGenerationFiles();
        if (generationFiles.Count == 0)
        {
            return PlanningWorkspaceLoadResult.CreateNotFound();
        }

        List<Exception> failures = new List<Exception>();
        for (int index = 0; index < generationFiles.Count; ++index)
        {
            GenerationFile generationFile = generationFiles[index];
            try
            {
                PlanningWorkspaceDocument document = await readDocumentAsync(
                    generationFile.Path,
                    cancellationToken).ConfigureAwait(false);
                if (document.Generation.Value != generationFile.Generation.Value)
                {
                    throw new WorkspaceDocumentException(
                        "The workspace document generation does not match its file name.");
                }

                if (index == 0)
                {
                    return PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(
                        document.Workspace,
                        createConcurrencyToken(generationFiles));
                }

                return PlanningWorkspaceLoadResult.CreateRecoveredPreviousGeneration(
                    document.Workspace,
                    createConcurrencyToken(generationFiles));
            }
            catch (UnsupportedWorkspaceSchemaVersionException exception)
            {
                throw new PlanningWorkspaceUpgradeRequiredException(
                    exception.SchemaVersion,
                    exception);
            }
            catch (Exception exception) when (isRecoverableGenerationException(exception))
            {
                failures.Add(exception);
            }
        }

        throw new WorkspacePersistenceException(
            "No valid planning workspace generation could be loaded.",
            new AggregateException(failures));
    }

    private async Task<PlanningWorkspaceDocument> readDocumentAsync(
        GenerationFilePath path,
        CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new FileInfo(path.Value);
        long documentLength = fileInfo.Length;
        if (documentLength > mDocumentSizeLimit.Bytes)
        {
            throw new WorkspaceDocumentSizeException(
                "The planning workspace document exceeds the product size limit.");
        }

        byte[] content = await File.ReadAllBytesAsync(path.Value, cancellationToken)
            .ConfigureAwait(false);
        return mCodec.Deserialize(content);
    }

    private async Task<PlanningWorkspaceConcurrencyToken> saveWithoutLockAsync(
        PlanningWorkspace workspace,
        PlanningWorkspaceConcurrencyToken expectedToken,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<GenerationFile> generationFiles =
            mFileStorage.GetGenerationFiles();
        PlanningWorkspaceConcurrencyToken actualToken =
            createConcurrencyToken(generationFiles);
        if (actualToken != expectedToken)
        {
            throw new PlanningWorkspaceConcurrencyException(
                expectedToken,
                actualToken);
        }

        await ensureLatestGenerationAllowsSaveAsync(
            generationFiles,
            cancellationToken).ConfigureAwait(false);
        WorkspaceGeneration nextGeneration = getNextGeneration(generationFiles);
        FileGeneration nextFileGeneration = new FileGeneration(nextGeneration.Value);

        PlanningWorkspaceDocument document = new PlanningWorkspaceDocument(
            nextGeneration,
            workspace);
        byte[] content = mCodec.Serialize(document);
        if (content.LongLength > mDocumentSizeLimit.Bytes)
        {
            throw new WorkspacePersistenceException(
                "The planning workspace exceeds the product size limit.",
                new InvalidOperationException(content.LongLength.ToString()));
        }

        GenerationFile committedGeneration = await mFileStorage.CommitAsync(
            nextFileGeneration,
            content,
            cancellationToken).ConfigureAwait(false);

        PlanningWorkspaceDocument verifiedDocument;
        try
        {
            verifiedDocument = await readDocumentAsync(
                committedGeneration.Path,
                CancellationToken.None).ConfigureAwait(false);
        }
        catch (Exception exception) when (isRecoverableGenerationException(exception))
        {
            mFileStorage.TryDeleteGeneration(committedGeneration);
            throw new WorkspacePersistenceException(
                "The committed workspace generation could not be read back.",
                exception);
        }

        if (verifiedDocument.Generation != nextGeneration)
        {
            mFileStorage.TryDeleteGeneration(committedGeneration);
            throw new WorkspacePersistenceException(
                "The committed workspace generation could not be verified.",
                new InvalidDataException(committedGeneration.Path.Value));
        }

        mFileStorage.PruneGenerations();
        return new PlanningWorkspaceConcurrencyToken(nextGeneration.Value);
    }

    private async Task ensureLatestGenerationAllowsSaveAsync(
        IReadOnlyList<GenerationFile> generationFiles,
        CancellationToken cancellationToken)
    {
        if (generationFiles.Count == 0)
        {
            return;
        }

        foreach (GenerationFile generationFile in generationFiles)
        {
            try
            {
                PlanningWorkspaceDocument document = await readDocumentAsync(
                    generationFile.Path,
                    cancellationToken).ConfigureAwait(false);
                if (document.Generation.Value == generationFile.Generation.Value)
                {
                    return;
                }
            }
            catch (UnsupportedWorkspaceSchemaVersionException exception)
            {
                throw new PlanningWorkspaceUpgradeRequiredException(
                    exception.SchemaVersion,
                    exception);
            }
            catch (WorkspaceDocumentSizeException exception)
            {
                throw new WorkspacePersistenceException(
                    "A newer workspace generation is too large to replace safely.",
                    exception);
            }
            catch (WorkspaceDocumentException)
            {
                // Corrupt generations are skipped so older version markers remain visible.
            }
        }
    }

    private static WorkspaceGeneration getNextGeneration(
        IReadOnlyList<GenerationFile> generationFiles)
    {
        if (generationFiles.Count == 0)
        {
            return new WorkspaceGeneration(1L);
        }

        WorkspaceGeneration latestGeneration = new WorkspaceGeneration(
            generationFiles[0].Generation.Value);
        return latestGeneration.GetNext();
    }

    private static PlanningWorkspaceConcurrencyToken createConcurrencyToken(
        IReadOnlyList<GenerationFile> generationFiles)
    {
        if (generationFiles.Count == 0)
        {
            return PlanningWorkspaceConcurrencyToken.MissingWorkspace;
        }

        return new PlanningWorkspaceConcurrencyToken(
            generationFiles[0].Generation.Value);
    }

}
