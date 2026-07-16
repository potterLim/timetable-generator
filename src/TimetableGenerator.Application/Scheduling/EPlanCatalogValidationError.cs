namespace TimetableGenerator.Application.Scheduling;

public enum EPlanCatalogValidationError
{
    None,
    CatalogBindingMismatch,
    CourseNotFound,
    OfferingNotFound,
    OfferingCourseMismatch,
    ScheduledChoiceHasNoProvidedTime,
    UnscheduledSelectionHasProvidedTime,
}
