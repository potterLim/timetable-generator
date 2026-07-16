using System;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed class UnsupportedWorkspaceSchemaVersionException :
    WorkspaceDocumentException
{
    public int SchemaVersion { get; }

    public UnsupportedWorkspaceSchemaVersionException(int schemaVersion)
        : base("The planning workspace schema version is not supported.")
    {
        SchemaVersion = schemaVersion;
    }
}
