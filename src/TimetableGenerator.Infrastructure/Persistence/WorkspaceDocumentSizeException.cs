namespace TimetableGenerator.Infrastructure.Persistence;

public sealed class WorkspaceDocumentSizeException : WorkspaceDocumentException
{
    public WorkspaceDocumentSizeException(string message)
        : base(message)
    {
    }
}
