using System;
using System.IO;

namespace TimetableGenerator.Infrastructure.Storage;

internal static class FileSystemExceptionClassifier
{
    public static bool IsFileSystemException(Exception exception)
    {
        return exception is IOException
            || exception is UnauthorizedAccessException
            || exception is NotSupportedException;
    }
}
