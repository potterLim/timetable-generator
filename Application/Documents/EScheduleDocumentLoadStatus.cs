namespace TimetableGenerator.Application.Documents;

public enum EScheduleDocumentLoadStatus
{
    Loaded,
    LoadedWithMaximumScheduleCountReached,
    ImportFailed,
    NoValidSchedules,
    UnsupportedAcademicPeriod,
    Canceled,
}
