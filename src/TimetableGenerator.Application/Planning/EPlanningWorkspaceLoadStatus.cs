namespace TimetableGenerator.Application.Planning;

public enum EPlanningWorkspaceLoadStatus
{
    NotFound = 0,
    LoadedLatestGeneration = 1,
    RecoveredPreviousGeneration = 2,
}
