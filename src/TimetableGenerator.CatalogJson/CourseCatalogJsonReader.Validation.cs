using System.Collections.Generic;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.CatalogJson;

public static partial class CourseCatalogJsonReader
{
    private static void validateDocumentConsistency(
        CatalogDocumentCounts counts,
        CatalogDataQualityMetadata dataQuality,
        IReadOnlyCollection<CatalogCourse> courses,
        IReadOnlyList<CatalogOffering> offerings,
        IReadOnlyList<CatalogOfferingMetadata> offeringMetadata)
    {
        if (counts.CourseCount != courses.Count)
        {
            throw new CatalogJsonFormatException(
                "$.counts.courses",
                "the declared course count does not match the courses array.");
        }

        if (counts.OfferingCount != offerings.Count)
        {
            throw new CatalogJsonFormatException(
                "$.counts.offerings",
                "the declared offering count does not match the offerings array.");
        }

        int scheduledOfferingCount = 0;
        int meetingNotProvidedCount = 0;
        int roomNotProvidedCount = 0;
        int enrollmentNotProvidedCount = 0;
        int instructorUnconfirmedCount = 0;
        int multiInstructorDisplayCount = 0;
        int sourceRemarkLookupOnlyCount = 0;
        HashSet<SourceRecordNumber> sourceRecordNumbers = new HashSet<SourceRecordNumber>();

        for (int offeringIndex = 0; offeringIndex < offerings.Count; ++offeringIndex)
        {
            CatalogOffering offering = offerings[offeringIndex];
            CatalogOfferingMetadata metadata = offeringMetadata[offeringIndex];
            validateOfferingMetadataPair(offering, metadata, offeringIndex);
            if (offering.MeetingSchedule.Status == EMeetingScheduleStatus.Scheduled)
            {
                ++scheduledOfferingCount;
            }
            else
            {
                ++meetingNotProvidedCount;
            }

            if (metadata.Logistics.Location.Status == ELocationAssignmentStatus.NotProvided)
            {
                ++roomNotProvidedCount;
            }

            if (metadata.Capacity.HasCurrentEnrollment == false)
            {
                ++enrollmentNotProvidedCount;
            }

            InstructorAssignmentMetadata instructor = metadata.Instruction.InstructorAssignment;
            if (instructor.Status == EInstructorAssignmentStatus.Unconfirmed)
            {
                ++instructorUnconfirmedCount;
            }

            if (instructor.Status == EInstructorAssignmentStatus.Confirmed
                && instructor.GetAdditionalInstructorCount().Value > 0)
            {
                ++multiInstructorDisplayCount;
            }

            if (metadata.Details.AreRemarksAvailable)
            {
                ++sourceRemarkLookupOnlyCount;
            }

            if (sourceRecordNumbers.Add(metadata.SourceRecordNumber) == false)
            {
                throw new CatalogJsonFormatException(
                    "$.offerings[" + offeringIndex + "].sourceRecordNumber",
                    "source record numbers must be unique.");
            }
        }

        requireCount(
            counts.ScheduledOfferingCount,
            scheduledOfferingCount,
            "$.counts.scheduledOfferings");
        requireCount(
            counts.MeetingNotProvidedCount,
            meetingNotProvidedCount,
            "$.counts.meetingNotProvided");
        requireCount(
            dataQuality.RoomNotProvidedCount,
            roomNotProvidedCount,
            "$.dataQuality.roomNotProvided");
        requireCount(
            dataQuality.EnrollmentNotProvidedCount,
            enrollmentNotProvidedCount,
            "$.dataQuality.enrollmentNotProvided");
        requireCount(
            dataQuality.InstructorUnconfirmedCount,
            instructorUnconfirmedCount,
            "$.dataQuality.instructorUnconfirmed");
        requireCount(
            dataQuality.MultiInstructorDisplayCount,
            multiInstructorDisplayCount,
            "$.dataQuality.multiInstructorDisplay");
        requireCount(
            dataQuality.SourceRemarkLookupOnlyCount,
            sourceRemarkLookupOnlyCount,
            "$.dataQuality.sourceRemarkLookupOnly");
    }

    private static void validateOfferingMetadataPair(
        CatalogOffering offering,
        CatalogOfferingMetadata metadata,
        int offeringIndex)
    {
        string offeringPath = "$.offerings[" + offeringIndex + "]";
        if (offering.Id != metadata.OfferingId)
        {
            throw new CatalogJsonFormatException(
                offeringPath,
                "offering metadata must preserve the matching offering ID.");
        }

        bool hasScheduledSource = metadata.Logistics.HasScheduleSourceText;
        if (offering.MeetingSchedule.IsScheduled != hasScheduledSource)
        {
            throw new CatalogJsonFormatException(
                offeringPath + ".schedule",
                "schedule source metadata must match the meeting schedule status.");
        }
    }

    private static void requireCount(int declaredCount, int actualCount, string path)
    {
        if (declaredCount != actualCount)
        {
            throw new CatalogJsonFormatException(
                path,
                "the declared count does not match the parsed offering states.");
        }
    }
}
