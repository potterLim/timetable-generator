using System.IO;

namespace TimetableGenerator.Desktop.Storage;

internal sealed class BoundedLocalFileReadLimitException : IOException
{
    public long MaximumByteCount { get; }

    public BoundedLocalFileReadLimitException(long maximumByteCount)
        : base("The local file exceeds the configured size limit.")
    {
        MaximumByteCount = maximumByteCount;
    }
}
