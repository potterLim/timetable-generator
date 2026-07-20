using System;
using System.IO;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal static class GoogleHttpResponseBodyReader
{
    private const int BUFFER_SIZE_BYTES = 81_920;

    public static async Task<byte[]> ReadAsync(
        HttpContent content,
        long maximumByteCount,
        CancellationToken cancellationToken)
    {
        if (content == null)
        {
            throw new ArgumentNullException(nameof(content));
        }

        if (maximumByteCount <= 0L)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumByteCount));
        }

        long? contentLengthOrNull = content.Headers.ContentLength;
        if (contentLengthOrNull.HasValue
            && contentLengthOrNull.Value > maximumByteCount)
        {
            throw new GoogleHttpResponseBodyLimitExceededException(
                maximumByteCount);
        }

        using (Stream source = await content.ReadAsStreamAsync(
            cancellationToken).ConfigureAwait(false))
        using (MemoryStream destination = createDestination(
            contentLengthOrNull,
            maximumByteCount))
        {
            byte[] buffer = new byte[BUFFER_SIZE_BYTES];
            while (true)
            {
                int readByteCount = await source.ReadAsync(
                    buffer.AsMemory(0, buffer.Length),
                    cancellationToken).ConfigureAwait(false);
                if (readByteCount == 0)
                {
                    return destination.ToArray();
                }

                if (destination.Length > maximumByteCount - readByteCount)
                {
                    throw new GoogleHttpResponseBodyLimitExceededException(
                        maximumByteCount);
                }

                await destination.WriteAsync(
                    buffer.AsMemory(0, readByteCount),
                    cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private static MemoryStream createDestination(
        long? contentLengthOrNull,
        long maximumByteCount)
    {
        if (contentLengthOrNull.HasValue == false)
        {
            return new MemoryStream();
        }

        long initialCapacity = Math.Min(
            contentLengthOrNull.Value,
            Math.Min(maximumByteCount, int.MaxValue));
        return new MemoryStream(checked((int)initialCapacity));
    }
}
