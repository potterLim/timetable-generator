using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed class GenerationFileStorage
{
    private const int FILE_BUFFER_SIZE = 16_384;
    private const int LOCK_RETRY_DELAY_MILLISECONDS = 50;
    private const int MAXIMUM_LOCK_ATTEMPTS = 100;
    private const int MAXIMUM_RETAINED_GENERATIONS = 5;

    private readonly GenerationFileStoragePath mStoragePath;

    private readonly SemaphoreSlim mAccessGate;

    public GenerationFileStorage(GenerationFileStoragePath storagePath)
    {
        if (storagePath == null)
        {
            throw new ArgumentNullException(nameof(storagePath));
        }

        mStoragePath = storagePath;
        mAccessGate = new SemaphoreSlim(1, 1);
    }

    public bool HasDirectory()
    {
        return Directory.Exists(mStoragePath.DirectoryPath);
    }

    public Task<GenerationFileStorageAccess> AcquireExistingDirectoryAsync(
        CancellationToken cancellationToken)
    {
        return acquireAccessAsync(EGenerationDirectoryPreparation.UseExisting, cancellationToken);
    }

    public Task<GenerationFileStorageAccess> AcquireCreatingDirectoryAsync(
        CancellationToken cancellationToken)
    {
        return acquireAccessAsync(EGenerationDirectoryPreparation.Create, cancellationToken);
    }

    public IReadOnlyList<GenerationFile> GetGenerationFiles()
    {
        List<GenerationFile> generationFiles = new List<GenerationFile>();
        IEnumerable<string> paths = Directory.EnumerateFiles(
            mStoragePath.DirectoryPath,
            mStoragePath.GenerationSearchPattern,
            SearchOption.TopDirectoryOnly);
        foreach (string path in paths)
        {
            FileGeneration generation;
            if (mStoragePath.TryParseGenerationFilePath(path, out generation))
            {
                GenerationFilePath generationPath = new GenerationFilePath(path);
                generationFiles.Add(new GenerationFile(generation, generationPath));
            }
        }

        generationFiles.Sort(compareGenerationFilesDescending);
        return generationFiles;
    }

    public async Task<GenerationFile> CommitAsync(
        FileGeneration generation,
        ReadOnlyMemory<byte> content,
        CancellationToken cancellationToken)
    {
        GenerationFilePath finalPath = mStoragePath.CreateGenerationFilePath(generation);
        string temporaryPath = finalPath.Value
            + "."
            + Guid.NewGuid().ToString("N")
            + ".tmp";
        try
        {
            await writeDurableFileAsync(temporaryPath, content, cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(temporaryPath, finalPath.Value);
            return new GenerationFile(generation, finalPath);
        }
        finally
        {
            tryDeleteFile(temporaryPath);
        }
    }

    public void TryDeleteGeneration(GenerationFile generationFile)
    {
        if (generationFile == null)
        {
            throw new ArgumentNullException(nameof(generationFile));
        }

        tryDeleteFile(generationFile.Path.Value);
    }

    public void PruneGenerations()
    {
        PruneGenerations(new GenerationFileRetentionSet());
    }

    public void PruneGenerations(GenerationFileRetentionSet additionallyRetainedGenerations)
    {
        if (additionallyRetainedGenerations == null)
        {
            throw new ArgumentNullException(nameof(additionallyRetainedGenerations));
        }

        try
        {
            IReadOnlyList<GenerationFile> generationFiles = GetGenerationFiles();
            for (int index = MAXIMUM_RETAINED_GENERATIONS; index < generationFiles.Count; ++index)
            {
                GenerationFile generationFile = generationFiles[index];
                if (additionallyRetainedGenerations.ShouldRetain(generationFile) == false)
                {
                    tryDeleteFile(generationFile.Path.Value);
                }
            }
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            // Retention cleanup is non-critical after a new generation is verified.
        }
    }

    private static int compareGenerationFilesDescending(GenerationFile first, GenerationFile second)
    {
        return second.Generation.Value.CompareTo(first.Generation.Value);
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

    private static void tryDeleteFile(string path)
    {
        try
        {
            File.Delete(path);
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            // A stale temporary or old generation is safer than masking the primary result.
        }
    }

    private async Task<GenerationFileStorageAccess> acquireAccessAsync(
        EGenerationDirectoryPreparation directoryPreparation,
        CancellationToken cancellationToken)
    {
        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            switch (directoryPreparation)
            {
                case EGenerationDirectoryPreparation.UseExisting:
                    break;
                case EGenerationDirectoryPreparation.Create:
                    Directory.CreateDirectory(mStoragePath.DirectoryPath);
                    break;
                default:
                    throw new InvalidOperationException("The generation directory preparation is unsupported.");
            }

            FileStream processLock = await acquireCrossProcessLockAsync(cancellationToken).ConfigureAwait(false);
            pruneTemporaryFiles();
            return new GenerationFileStorageAccess(processLock, mAccessGate);
        }
        catch
        {
            mAccessGate.Release();
            throw;
        }
    }

    private async Task<FileStream> acquireCrossProcessLockAsync(CancellationToken cancellationToken)
    {
        IOException? lastExceptionOrNull = null;
        for (int attempt = 0; attempt < MAXIMUM_LOCK_ATTEMPTS; ++attempt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    mStoragePath.LockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    1,
                    FileOptions.Asynchronous);
            }
            catch (IOException exception)
            {
                lastExceptionOrNull = exception;
                await Task.Delay(LOCK_RETRY_DELAY_MILLISECONDS, cancellationToken).ConfigureAwait(false);
            }
        }

        InvalidOperationException fallbackException = new InvalidOperationException("The generation file lock attempt ended without an I/O exception.");
        Exception failure = fallbackException;
        if (lastExceptionOrNull != null)
        {
            failure = lastExceptionOrNull;
        }

        throw new GenerationFileStorageLockException(failure);
    }

    private void pruneTemporaryFiles()
    {
        try
        {
            IEnumerable<string> temporaryPaths = Directory.EnumerateFiles(
                mStoragePath.DirectoryPath,
                mStoragePath.TemporaryFileSearchPattern,
                SearchOption.TopDirectoryOnly);
            foreach (string temporaryPath in temporaryPaths)
            {
                tryDeleteFile(temporaryPath);
            }
        }
        catch (Exception exception) when (
            FileSystemExceptionClassifier.IsFileSystemException(exception))
        {
            // Stale temporary files must not prevent workspace recovery or a new save.
        }
    }
}
