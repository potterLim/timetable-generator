namespace TimetableGenerator.Application.Planning;

public enum EPlanningWorkspaceCatalogRebindStatus
{
    Rebound = 0,
    MixedCatalogBindings = 1,
    CourseNotFound = 2,
    OfferingNotFound = 3,
    OfferingCourseMismatch = 4,
    ScheduledChoiceHasNoProvidedTime = 5,
    UnscheduledSelectionHasProvidedTime = 6,
}
