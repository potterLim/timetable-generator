namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal interface IAppleCalendarOwnershipRegistryStore
{
    AppleCalendarOwnershipRegistryDocument Load();

    void Save(AppleCalendarOwnershipRegistryDocument document);
}
