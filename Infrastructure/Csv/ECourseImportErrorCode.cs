namespace TimetableGenerator.Infrastructure.Csv;

public enum ECourseImportErrorCode
{
    InvalidInputFilePath,
    FileNotFound,
    FileAccessDenied,
    FileReadFailed,
    InvalidUtf8Encoding,
    MissingHeader,
    InvalidHeader,
    MalformedCsvRecord,
    InvalidColumnCount,
    InvalidCourseChoiceGroupId,
    InvalidCourseSectionCode,
    InvalidCourseName,
    EmptyScheduleSlot,
    InvalidScheduleSlot,
    DuplicateScheduleSlot,
    InvalidClassroomLocation,
    NoCourseOfferings,
}
