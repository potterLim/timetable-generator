using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting;

internal sealed class SchedulePngBatchDirectory : IDisposable
{
    private const int MAXIMUM_SUFFIX_ATTEMPTS = 10_000;

    private const string OWNERSHIP_MARKER_FILE_NAME = ".timetable-generator-exporting";

    private readonly List<string> mCreatedFilePaths;

    private string mDirectoryPath;

    private bool mIsCommitted;

    private bool mIsDisposed;

    public string DirectoryPath
    {
        get
        {
            return mDirectoryPath;
        }
    }

    internal SchedulePngBatchDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A PNG export directory path is required.", nameof(directoryPath));
        }

        mDirectoryPath = Path.GetFullPath(directoryPath);
        if (Directory.Exists(mDirectoryPath) == false)
        {
            throw new DirectoryNotFoundException("The owned PNG export directory does not exist.");
        }

        string ownershipMarkerPath = getOwnershipMarkerPath();
        using (FileStream markerStream = new FileStream(ownershipMarkerPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
        {
        }

        mCreatedFilePaths = new List<string>();
        mIsCommitted = false;
        mIsDisposed = false;
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        if (mIsCommitted == false)
        {
            deleteCreatedFiles();
        }

        deleteOwnershipMarker();
        if (mIsCommitted == false)
        {
            tryDeleteEmptyDirectory();
        }

        mIsDisposed = true;
    }

    internal Stream createFile(string fileName)
    {
        throwIfUnavailable();
        if (string.IsNullOrWhiteSpace(fileName))
        {
            throw new ArgumentException("A PNG export file name is required.", nameof(fileName));
        }

        string filePath = Path.GetFullPath(Path.Combine(mDirectoryPath, fileName));
        string? parentPathOrNull = Path.GetDirectoryName(filePath);
        if (string.Equals(parentPathOrNull, mDirectoryPath, StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException("PNG export files must remain inside the owned directory.", nameof(fileName));
        }

        FileStream stream = new FileStream(
            filePath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None,
            65_536,
            FileOptions.Asynchronous);
        mCreatedFilePaths.Add(filePath);
        return stream;
    }

    internal void commitAsUniqueBatch(PlanName planName, CancellationToken cancellationToken)
    {
        throwIfUnavailable();
        ArgumentNullException.ThrowIfNull(planName);
        string? parentDirectoryPathOrNull = Path.GetDirectoryName(mDirectoryPath);
        if (string.IsNullOrWhiteSpace(parentDirectoryPathOrNull))
        {
            throw new InvalidOperationException("The staged PNG export directory requires a parent directory.");
        }

        for (int attempt = 1; attempt <= MAXIMUM_SUFFIX_ATTEMPTS; ++attempt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string folderName = SchedulePngFileNameFactory.CreateBatchFolderName(planName, attempt);
            string destinationDirectoryPath = Path.Combine(parentDirectoryPathOrNull, folderName);
            if (Directory.Exists(destinationDirectoryPath) || File.Exists(destinationDirectoryPath))
            {
                continue;
            }

            try
            {
                Directory.Move(mDirectoryPath, destinationDirectoryPath);
                mDirectoryPath = destinationDirectoryPath;
                deleteOwnershipMarker();
                mIsCommitted = true;
                return;
            }
            catch (IOException)
                when (Directory.Exists(destinationDirectoryPath) || File.Exists(destinationDirectoryPath))
            {
                // A competing export claimed this name after the existence check.
            }
        }

        throw new IOException("A unique PNG export directory could not be committed.");
    }

    private void throwIfUnavailable()
    {
        if (mIsDisposed)
        {
            throw new ObjectDisposedException(nameof(SchedulePngBatchDirectory));
        }

        if (mIsCommitted)
        {
            throw new InvalidOperationException("A committed PNG export directory cannot be modified.");
        }
    }

    private void deleteCreatedFiles()
    {
        foreach (string filePath in mCreatedFilePaths)
        {
            try
            {
                File.Delete(filePath);
            }
            catch (IOException)
            {
                // Rollback is best effort and must not mask the original export failure.
            }
            catch (UnauthorizedAccessException)
            {
                // Rollback is best effort and must not mask the original export failure.
            }
        }
    }

    private void deleteOwnershipMarker()
    {
        try
        {
            File.Delete(getOwnershipMarkerPath());
        }
        catch (IOException)
        {
            // Rollback is best effort and must not mask the original export failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Rollback is best effort and must not mask the original export failure.
        }
    }

    private void tryDeleteEmptyDirectory()
    {
        try
        {
            Directory.Delete(mDirectoryPath, false);
        }
        catch (IOException)
        {
            // Rollback is best effort and must not mask the original export failure.
        }
        catch (UnauthorizedAccessException)
        {
            // Rollback is best effort and must not mask the original export failure.
        }
    }

    private string getOwnershipMarkerPath()
    {
        return Path.Combine(mDirectoryPath, OWNERSHIP_MARKER_FILE_NAME);
    }
}
