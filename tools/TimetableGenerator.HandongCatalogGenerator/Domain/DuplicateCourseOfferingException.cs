using System;

namespace TimetableGenerator.HandongCatalogGenerator.Domain;

internal sealed class DuplicateCourseOfferingException : Exception
{
    public CourseOfferingKey OfferingKey { get; }

    public SourceRecordNumber FirstSourceRecordNumber { get; }

    public SourceRecordNumber DuplicateSourceRecordNumber { get; }

    public DuplicateCourseOfferingException(CourseOfferingKey offeringKey, SourceRecordNumber firstSourceRecordNumber, SourceRecordNumber duplicateSourceRecordNumber)
        : base(createMessage(offeringKey, firstSourceRecordNumber, duplicateSourceRecordNumber))
    {
        OfferingKey = offeringKey;
        FirstSourceRecordNumber = firstSourceRecordNumber;
        DuplicateSourceRecordNumber = duplicateSourceRecordNumber;
    }

    private static string createMessage(CourseOfferingKey offeringKey, SourceRecordNumber firstSourceRecordNumber, SourceRecordNumber duplicateSourceRecordNumber)
    {
        return "Offering " + offeringKey.CourseCode + ":" + offeringKey.SectionCode + " is duplicated at source records " + firstSourceRecordNumber + " and " + duplicateSourceRecordNumber + ".";
    }
}
