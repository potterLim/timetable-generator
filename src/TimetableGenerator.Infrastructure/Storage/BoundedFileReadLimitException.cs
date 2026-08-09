using System.IO;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed class BoundedFileReadLimitException : IOException
{
    public long MaximumByteCount { get; }

    public BoundedFileReadLimitException(long maximumByteCount)
        : base("The file exceeds the configured size limit.")
    {
        MaximumByteCount = maximumByteCount;
    }
}
