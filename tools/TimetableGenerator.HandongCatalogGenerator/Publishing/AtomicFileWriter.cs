using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal static class AtomicFileWriter
{
    public static async Task WriteAsync(
        string destinationPath,
        ReadOnlyMemory<byte> content,
        EExistingFileBehavior existingFileBehavior,
        CancellationToken cancellationToken)
    {
        string? directoryPathOrNull = Path.GetDirectoryName(destinationPath);
        if (string.IsNullOrEmpty(directoryPathOrNull))
        {
            throw new ArgumentException("The destination path must include a directory.", nameof(destinationPath));
        }

        Directory.CreateDirectory(directoryPathOrNull);
        string temporaryPath = Path.Combine(directoryPathOrNull, "." + Path.GetFileName(destinationPath) + "." + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            await File.WriteAllBytesAsync(temporaryPath, content, cancellationToken);
            if (existingFileBehavior == EExistingFileBehavior.Replace)
            {
                File.Move(temporaryPath, destinationPath, true);
            }
            else
            {
                File.Move(temporaryPath, destinationPath, false);
            }
        }
        finally
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
    }
}
