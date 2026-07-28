using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class ConflictingCourseDefinitionException : Exception
{
    public CourseCode CourseCode { get; }

    public ECourseDefinitionField Field { get; }

    public SourceRecordNumber FirstSourceRecordNumber { get; }

    public SourceRecordNumber ConflictingSourceRecordNumber { get; }

    public ConflictingCourseDefinitionException(CourseCode courseCode, ECourseDefinitionField field, SourceRecordNumber firstSourceRecordNumber, SourceRecordNumber conflictingSourceRecordNumber)
        : base(createMessage(courseCode, field, firstSourceRecordNumber, conflictingSourceRecordNumber))
    {
        if (courseCode == null)
        {
            throw new ArgumentNullException(nameof(courseCode));
        }

        if (Enum.IsDefined(typeof(ECourseDefinitionField), field) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(field));
        }

        CourseCode = courseCode;
        Field = field;
        FirstSourceRecordNumber = firstSourceRecordNumber;
        ConflictingSourceRecordNumber = conflictingSourceRecordNumber;
    }

    private static string createMessage(CourseCode courseCode, ECourseDefinitionField field, SourceRecordNumber firstSourceRecordNumber, SourceRecordNumber conflictingSourceRecordNumber)
    {
        return "Course " + courseCode + " has conflicting " + field + " values at source records " + firstSourceRecordNumber + " and " + conflictingSourceRecordNumber + ".";
    }
}
