using System;

namespace TimetableGenerator.Infrastructure.Storage;

internal sealed class GenerationFileStorageLockException : Exception
{
    public Exception Failure { get; }

    public GenerationFileStorageLockException(Exception failure)
        : base("Another process is using the generation file storage.", failure)
    {
        if (failure == null)
        {
            throw new ArgumentNullException(nameof(failure));
        }

        Failure = failure;
    }
}
