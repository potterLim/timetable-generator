using System;
using System.IO;
using System.Threading;

namespace TimetableGenerator.Desktop.Exporting;

internal static class SchedulePngBatchDirectoryAllocator
{
    private const int MAXIMUM_SUFFIX_ATTEMPTS = 10_000;

    private const string STAGING_DIRECTORY_PREFIX = ".timetable-generator-png-staging-";

    internal static SchedulePngBatchDirectory createStaging(string parentDirectoryPath, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentDirectoryPath))
        {
            throw new ArgumentException("A parent PNG export directory is required.", nameof(parentDirectoryPath));
        }

        string fullParentPath = Path.GetFullPath(parentDirectoryPath);
        if (Directory.Exists(fullParentPath) == false)
        {
            throw new DirectoryNotFoundException("The selected PNG export directory does not exist.");
        }

        for (int attempt = 1; attempt <= MAXIMUM_SUFFIX_ATTEMPTS; ++attempt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string directoryPath = Path.Combine(fullParentPath, STAGING_DIRECTORY_PREFIX + Guid.NewGuid().ToString("N"));
            if (Directory.Exists(directoryPath) || File.Exists(directoryPath))
            {
                continue;
            }

            try
            {
                Directory.CreateDirectory(directoryPath);
                return new SchedulePngBatchDirectory(directoryPath);
            }
            catch (IOException)
            {
                tryDeleteEmptyDirectory(directoryPath);
            }
        }

        throw new IOException("A temporary PNG export staging directory could not be reserved.");
    }

    private static void tryDeleteEmptyDirectory(string directoryPath)
    {
        try
        {
            Directory.Delete(directoryPath, false);
        }
        catch (IOException)
        {
            // A competing export may have removed or claimed the directory first.
        }
        catch (UnauthorizedAccessException)
        {
            // Cleanup is best effort and must not replace the allocation failure.
        }
    }
}
