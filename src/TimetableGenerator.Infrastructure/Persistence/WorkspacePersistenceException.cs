using System;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed class WorkspacePersistenceException : Exception
{
    public WorkspacePersistenceException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
