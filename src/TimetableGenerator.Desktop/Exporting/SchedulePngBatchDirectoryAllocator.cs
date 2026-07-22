using System;
using System.IO;
using System.Threading;

using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting;

internal static class SchedulePngBatchDirectoryAllocator
{
    private const int MAXIMUM_SUFFIX_ATTEMPTS = 10_000;

    internal static SchedulePngBatchDirectory createUnique(
        string parentDirectoryPath,
        PlanName planName,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(parentDirectoryPath))
        {
            throw new ArgumentException(
                "A parent PNG export directory is required.",
                nameof(parentDirectoryPath));
        }

        ArgumentNullException.ThrowIfNull(planName);
        string fullParentPath = Path.GetFullPath(parentDirectoryPath);
        if (Directory.Exists(fullParentPath) == false)
        {
            throw new DirectoryNotFoundException("The selected PNG export directory does not exist.");
        }

        for (int attempt = 1;
            attempt <= MAXIMUM_SUFFIX_ATTEMPTS;
            ++attempt)
        {
            cancellationToken.ThrowIfCancellationRequested();
            string folderName = SchedulePngFileNameFactory.CreateBatchFolderName(planName, attempt);
            string directoryPath = Path.Combine(fullParentPath, folderName);
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

        throw new IOException("A unique PNG export directory could not be reserved.");
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
