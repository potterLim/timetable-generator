using System;

namespace TimetableGenerator.Infrastructure.Persistence;

public class WorkspaceDocumentException : Exception
{
    public WorkspaceDocumentException(string message)
        : base(message)
    {
    }

    public WorkspaceDocumentException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
