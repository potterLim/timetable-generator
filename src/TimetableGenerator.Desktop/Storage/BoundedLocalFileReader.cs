using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Storage;

internal static class BoundedLocalFileReader
{
    private const int BUFFER_SIZE_BYTES = 65_536;

    internal static byte[] readAllBytes(string path, long maximumByteCount)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        requireValidMaximumByteCount(maximumByteCount);
        using (FileStream source = new FileStream(path, createReadOptions(false)))
        {
            return readAllBytes(source, maximumByteCount);
        }
    }

    internal static byte[] readAllBytes(Stream source, long maximumByteCount)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        requireValidMaximumByteCount(maximumByteCount);
        using (MemoryStream destination = new MemoryStream())
        {
            byte[] buffer = new byte[BUFFER_SIZE_BYTES];
            while (true)
            {
                int requestedByteCount = getRequestedByteCount(destination.Length, maximumByteCount);
                int readByteCount = source.Read(buffer, 0, requestedByteCount);
                if (readByteCount == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length > maximumByteCount - readByteCount)
                {
                    throw new BoundedLocalFileReadLimitException(maximumByteCount);
                }

                destination.Write(buffer, 0, readByteCount);
            }
        }
    }

    internal static async Task<byte[]> readAllBytesAsync(string path, long maximumByteCount, CancellationToken cancellationToken)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        requireValidMaximumByteCount(maximumByteCount);
        cancellationToken.ThrowIfCancellationRequested();
        using (FileStream source = new FileStream(path, createReadOptions(true)))
        {
            return await readAllBytesAsync(source, maximumByteCount, cancellationToken).ConfigureAwait(false);
        }
    }

    internal static async Task<byte[]> readAllBytesAsync(Stream source, long maximumByteCount, CancellationToken cancellationToken)
    {
        if (source == null)
        {
            throw new ArgumentNullException(nameof(source));
        }

        requireValidMaximumByteCount(maximumByteCount);
        using (MemoryStream destination = new MemoryStream())
        {
            byte[] buffer = new byte[BUFFER_SIZE_BYTES];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int requestedByteCount = getRequestedByteCount(destination.Length, maximumByteCount);
                int readByteCount = await source.ReadAsync(buffer.AsMemory(0, requestedByteCount), cancellationToken).ConfigureAwait(false);
                cancellationToken.ThrowIfCancellationRequested();
                if (readByteCount == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length > maximumByteCount - readByteCount)
                {
                    throw new BoundedLocalFileReadLimitException(maximumByteCount);
                }

                destination.Write(buffer, 0, readByteCount);
            }
        }
    }

    private static FileStreamOptions createReadOptions(bool useAsynchronousIo)
    {
        FileOptions options = FileOptions.SequentialScan;
        if (useAsynchronousIo)
        {
            options |= FileOptions.Asynchronous;
        }

        return new FileStreamOptions
        {
            Mode = FileMode.Open,
            Access = FileAccess.Read,
            Share = FileShare.Read,
            BufferSize = BUFFER_SIZE_BYTES,
            Options = options,
        };
    }

    private static int getRequestedByteCount(long currentByteCount, long maximumByteCount)
    {
        long remainingByteCount = maximumByteCount - currentByteCount;
        if (remainingByteCount >= BUFFER_SIZE_BYTES)
        {
            return BUFFER_SIZE_BYTES;
        }

        return checked((int)remainingByteCount) + 1;
    }

    private static void requireValidMaximumByteCount(long maximumByteCount)
    {
        if (maximumByteCount <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumByteCount));
        }
    }
}
