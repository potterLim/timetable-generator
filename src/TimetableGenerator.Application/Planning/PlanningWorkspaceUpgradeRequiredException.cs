using System;

namespace TimetableGenerator.Application.Planning;

public sealed class PlanningWorkspaceUpgradeRequiredException : Exception
{
    public int UnsupportedSchemaVersion { get; }

    public PlanningWorkspaceUpgradeRequiredException(int unsupportedSchemaVersion, Exception innerException)
        : base("The planning workspace was created by a newer application version.", innerException)
    {
        UnsupportedSchemaVersion = unsupportedSchemaVersion;
    }
}
