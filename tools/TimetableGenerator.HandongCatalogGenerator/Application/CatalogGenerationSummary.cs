using TimetableGenerator.HandongCatalogGenerator.Domain;

namespace TimetableGenerator.HandongCatalogGenerator.Application;

internal sealed record CatalogGenerationSummary
{
    public CatalogItemCount CourseCount { get; }
    public CatalogItemCount OfferingCount { get; }
    public CatalogItemCount ScheduledOfferingCount { get; }
    public CatalogItemCount MeetingNotProvidedCount { get; }
    public CatalogItemCount RoomNotProvidedCount { get; }
    public CatalogItemCount InstructorUnconfirmedCount { get; }
    public CatalogItemCount EnglishScheduleMismatchCount { get; }

    public CatalogGenerationSummary(CourseCatalog catalog)
    {
        CourseCount = catalog.CourseCount;
        OfferingCount = catalog.OfferingCount;
        ScheduledOfferingCount = catalog.ScheduledOfferingCount;
        MeetingNotProvidedCount = catalog.MeetingNotProvidedCount;
        RoomNotProvidedCount = catalog.DataQuality.RoomNotProvidedCount;
        InstructorUnconfirmedCount = catalog.DataQuality.InstructorUnconfirmedCount;
        EnglishScheduleMismatchCount = catalog.DataQuality.EnglishScheduleMismatchCount;
    }
}
