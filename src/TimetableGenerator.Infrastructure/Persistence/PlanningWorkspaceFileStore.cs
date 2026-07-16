using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Application.Planning;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed class PlanningWorkspaceFileStore : IPlanningWorkspaceStore
{
    private const int FILE_BUFFER_SIZE = 16_384;
    private const int MAXIMUM_RETAINED_GENERATIONS = 5;
    private const int MAXIMUM_LOCK_ATTEMPTS = 100;
    private const int LOCK_RETRY_DELAY_MILLISECONDS = 50;

    private readonly string mDirectoryPath;

    private readonly string mBaseFileName;

    private readonly string mFileExtension;

    private readonly string mGenerationSearchPattern;

    private readonly string mLockPath;

    private readonly PlanningWorkspaceJsonCodec mCodec;

    private readonly WorkspaceDocumentSizeLimit mDocumentSizeLimit;

    private readonly SemaphoreSlim mAccessGate;

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

        mDirectoryPath = directoryPathOrNull;
        mBaseFileName = Path.GetFileNameWithoutExtension(basePath.Value);
        mFileExtension = Path.GetExtension(basePath.Value);
        mGenerationSearchPattern = mBaseFileName + ".g*" + mFileExtension;
        mLockPath = Path.Combine(mDirectoryPath, mBaseFileName + ".lock");
        mCodec = codec;
        mDocumentSizeLimit = documentSizeLimit;
        mAccessGate = new SemaphoreSlim(1, 1);
    }

    public async Task<PlanningWorkspaceLoadResult> LoadAsync(
        CancellationToken cancellationToken)
    {
        if (Directory.Exists(mDirectoryPath) == false)
        {
            return PlanningWorkspaceLoadResult.CreateNotFound();
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
        catch (PlanningWorkspaceUpgradeRequiredException)
        {
            throw;
        }
        catch (WorkspacePersistenceException)
        {
            throw;
        }
        catch (Exception exception) when (isFileSystemException(exception))
        {
            throw new WorkspacePersistenceException(
                "The planning workspace generations could not be loaded.",
                exception);
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    public async Task SaveAsync(
        PlanningWorkspace workspace,
        CancellationToken cancellationToken)
    {
        if (workspace == null)
        {
            throw new ArgumentNullException(nameof(workspace));
        }

        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Directory.CreateDirectory(mDirectoryPath);
            using (FileStream processLock = await acquireCrossProcessLockAsync(
                cancellationToken).ConfigureAwait(false))
            {
                await saveWithoutLockAsync(workspace, cancellationToken)
                    .ConfigureAwait(false);
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
        catch (Exception exception) when (isFileSystemException(exception))
        {
            throw new WorkspacePersistenceException(
                "The planning workspace could not be saved atomically.",
                exception);
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    private static int compareGenerationFilesDescending(
        WorkspaceGenerationFile first,
        WorkspaceGenerationFile second)
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
            || exception is WorkspaceDocumentException;
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
        for (int attempt = 0; attempt < MAXIMUM_LOCK_ATTEMPTS; attempt++)
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
            "The workspace lock attempt ended without an I/O exception.");
        Exception innerException = fallbackException;
        if (lastExceptionOrNull != null)
        {
            innerException = lastExceptionOrNull;
        }

        throw new WorkspacePersistenceException(
            "Another application instance is using the planning workspace.",
            innerException);
    }

    private List<WorkspaceGenerationFile> getGenerationFiles()
    {
        List<WorkspaceGenerationFile> generationFiles =
            new List<WorkspaceGenerationFile>();
        IEnumerable<string> paths = Directory.EnumerateFiles(
            mDirectoryPath,
            mGenerationSearchPattern,
            SearchOption.TopDirectoryOnly);
        foreach (string path in paths)
        {
            WorkspaceGeneration generation;
            if (tryParseGenerationPath(path, out generation))
            {
                generationFiles.Add(new WorkspaceGenerationFile(generation, path));
            }
        }

        generationFiles.Sort(compareGenerationFilesDescending);
        return generationFiles;
    }

    private async Task<PlanningWorkspaceLoadResult> loadWithoutLockAsync(
        CancellationToken cancellationToken)
    {
        List<WorkspaceGenerationFile> generationFiles = getGenerationFiles();
        if (generationFiles.Count == 0)
        {
            return PlanningWorkspaceLoadResult.CreateNotFound();
        }

        List<Exception> failures = new List<Exception>();
        for (int index = 0; index < generationFiles.Count; index++)
        {
            WorkspaceGenerationFile generationFile = generationFiles[index];
            try
            {
                PlanningWorkspaceDocument document = await readDocumentAsync(
                    generationFile.Path,
                    cancellationToken).ConfigureAwait(false);
                if (document.Generation != generationFile.Generation)
                {
                    throw new WorkspaceDocumentException(
                        "The workspace document generation does not match its file name.");
                }

                if (index == 0)
                {
                    return PlanningWorkspaceLoadResult.CreateLoadedLatestGeneration(
                        document.Workspace);
                }

                return PlanningWorkspaceLoadResult.CreateRecoveredPreviousGeneration(
                    document.Workspace);
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
        string path,
        CancellationToken cancellationToken)
    {
        FileInfo fileInfo = new FileInfo(path);
        long documentLength = fileInfo.Length;
        if (documentLength > mDocumentSizeLimit.Bytes)
        {
            throw new WorkspaceDocumentSizeException(
                "The planning workspace document exceeds the product size limit.");
        }

        byte[] content = await File.ReadAllBytesAsync(path, cancellationToken)
            .ConfigureAwait(false);
        return mCodec.Deserialize(content);
    }

    private async Task saveWithoutLockAsync(
        PlanningWorkspace workspace,
        CancellationToken cancellationToken)
    {
        List<WorkspaceGenerationFile> generationFiles = getGenerationFiles();
        await ensureLatestGenerationAllowsSaveAsync(
            generationFiles,
            cancellationToken).ConfigureAwait(false);
        WorkspaceGeneration nextGeneration = new WorkspaceGeneration(1);
        if (generationFiles.Count > 0)
        {
            nextGeneration = generationFiles[0].Generation.GetNext();
        }

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

            PlanningWorkspaceDocument verifiedDocument;
            try
            {
                verifiedDocument = await readDocumentAsync(
                    finalPath,
                    CancellationToken.None).ConfigureAwait(false);
            }
            catch (Exception exception) when (isRecoverableGenerationException(exception))
            {
                tryDeleteFile(finalPath);
                throw new WorkspacePersistenceException(
                    "The committed workspace generation could not be read back.",
                    exception);
            }

            if (verifiedDocument.Generation != nextGeneration)
            {
                throw new WorkspacePersistenceException(
                    "The committed workspace generation could not be verified.",
                    new InvalidDataException(finalPath));
            }

            pruneOldGenerations();
        }
        finally
        {
            tryDeleteFile(temporaryPath);
        }
    }

    private string createGenerationPath(WorkspaceGeneration generation)
    {
        string fileName = mBaseFileName
            + "."
            + generation.FileComponent
            + mFileExtension;
        return Path.Combine(mDirectoryPath, fileName);
    }

    private async Task ensureLatestGenerationAllowsSaveAsync(
        IReadOnlyList<WorkspaceGenerationFile> generationFiles,
        CancellationToken cancellationToken)
    {
        if (generationFiles.Count == 0)
        {
            return;
        }

        foreach (WorkspaceGenerationFile generationFile in generationFiles)
        {
            try
            {
                PlanningWorkspaceDocument document = await readDocumentAsync(
                    generationFile.Path,
                    cancellationToken).ConfigureAwait(false);
                if (document.Generation == generationFile.Generation)
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

    private void pruneOldGenerations()
    {
        try
        {
            List<WorkspaceGenerationFile> generationFiles = getGenerationFiles();
            for (int index = MAXIMUM_RETAINED_GENERATIONS;
                index < generationFiles.Count;
                index++)
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
        out WorkspaceGeneration generation)
    {
        generation = default(WorkspaceGeneration);
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
        return WorkspaceGeneration.TryParseFileComponent(
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
