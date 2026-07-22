using System;
using System.Collections.Generic;
using System.IO;

namespace TimetableGenerator.Desktop.Exporting;

internal sealed class SchedulePngBatchDirectory : IDisposable
{
    private const string OWNERSHIP_MARKER_FILE_NAME = ".timetable-generator-exporting";

    private readonly List<string> mCreatedFilePaths;

    private readonly string mOwnershipMarkerPath;

    private bool mIsCommitted;

    private bool mIsDisposed;

    public string DirectoryPath { get; }

    internal SchedulePngBatchDirectory(string directoryPath)
    {
        if (string.IsNullOrWhiteSpace(directoryPath))
        {
            throw new ArgumentException("A PNG export directory path is required.", nameof(directoryPath));
        }

        DirectoryPath = Path.GetFullPath(directoryPath);
        if (Directory.Exists(DirectoryPath) == false)
        {
            throw new DirectoryNotFoundException("The owned PNG export directory does not exist.");
        }

        mOwnershipMarkerPath = Path.Combine(DirectoryPath, OWNERSHIP_MARKER_FILE_NAME);
        using (FileStream markerStream = new FileStream(
            mOwnershipMarkerPath,
            FileMode.CreateNew,
            FileAccess.Write,
            FileShare.None))
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

        string filePath = Path.GetFullPath(Path.Combine(DirectoryPath, fileName));
        string? parentPathOrNull = Path.GetDirectoryName(filePath);
        if (string.Equals(parentPathOrNull, DirectoryPath, StringComparison.OrdinalIgnoreCase) == false)
        {
            throw new ArgumentException(
                "PNG export files must remain inside the owned directory.",
                nameof(fileName));
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

    internal void commit()
    {
        throwIfUnavailable();
        deleteOwnershipMarker();
        mIsCommitted = true;
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
            File.Delete(mOwnershipMarkerPath);
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
            Directory.Delete(DirectoryPath, false);
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
