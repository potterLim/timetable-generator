namespace TimetableGenerator.Desktop.Presentation.Models;

internal enum EPersonalScheduleDraftValidationError
{
    None = 0,
    TitleRequired = 1,
    TitleInvalid = 2,
    DayRequired = 3,
    StartTimeRequired = 4,
    EndTimeRequired = 5,
    StartTimePrecisionInvalid = 6,
    EndTimePrecisionInvalid = 7,
    EndNotAfterStart = 8,
    DurationTooShort = 9,
    SectionInvalid = 10,
    InstructorInvalid = 11,
    LocationInvalid = 12,
    Overlap = 13,
}
