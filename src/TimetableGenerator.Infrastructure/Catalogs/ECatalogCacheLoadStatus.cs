namespace TimetableGenerator.Infrastructure.Catalogs;

public enum ECatalogCacheLoadStatus
{
    NotFound = 0,
    LoadedLatestGeneration = 1,
    RecoveredPreviousGeneration = 2,
}
