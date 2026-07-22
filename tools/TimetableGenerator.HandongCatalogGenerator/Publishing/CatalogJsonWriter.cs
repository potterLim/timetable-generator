using System;
using System.Text.Json;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal static class CatalogJsonWriter
{
    private const int SCHEMA_VERSION = 1;

    public static byte[] Write(
        CourseCatalog catalog,
        AcademicTerm term,
        CatalogRevision revision,
        HandongExportDocument sourceDocument)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(sourceDocument);

        return DeterministicJsonWriter.Write(
            writer => writeDocument(writer, catalog, term, revision, sourceDocument));
    }

    private static void writeDocument(
        Utf8JsonWriter writer,
        CourseCatalog catalog,
        AcademicTerm term,
        CatalogRevision revision,
        HandongExportDocument sourceDocument)
    {
        writer.WriteStartObject();
        writer.WriteString("documentType", "courseCatalog");
        writer.WriteNumber("schemaVersion", SCHEMA_VERSION);
        writer.WriteString("catalogId", CatalogFileLayout.GetCatalogId(term, revision));
        writer.WriteNumber("revision", revision.Value);
        writeInstitution(writer);
        writeTerm(writer, term);
        writeSource(writer, term, sourceDocument);
        writeConverter(writer);
        writeCounts(writer, catalog);
        writeDataQuality(writer, catalog.DataQuality);
        writeCourses(writer, catalog.Courses);
        writeOfferings(writer, term, catalog.Offerings);
        writer.WriteEndObject();
    }

    private static void writeInstitution(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("institution");
        writer.WriteString("id", CatalogFileLayout.INSTITUTION_ID);
        writer.WriteStartObject("name");
        writer.WriteString("ko", CatalogFileLayout.INSTITUTION_NAME_KO);
        writer.WriteString("en", CatalogFileLayout.INSTITUTION_NAME_EN);
        writer.WriteEndObject();
        writer.WriteEndObject();
    }

    private static void writeTerm(Utf8JsonWriter writer, AcademicTerm term)
    {
        writer.WriteStartObject("term");
        writer.WriteString("id", term.Id);
        writer.WriteNumber("academicYear", term.AcademicYear.Value);
        writer.WriteNumber("semester", term.Semester.Value);
        writer.WriteEndObject();
    }

    private static void writeSource(
        Utf8JsonWriter writer,
        AcademicTerm term,
        HandongExportDocument sourceDocument)
    {
        writer.WriteStartObject("source");
        writer.WriteString("providerId", CatalogFileLayout.INSTITUTION_ID);
        writer.WriteString("logicalFileName", "hgu-" + term.Id + "-source.xls");
        writer.WriteString("declaredExtension", "xls");
        writer.WriteString("detectedMediaType", "text/html");
        writer.WriteString("declaredCharset", sourceDocument.DeclaredCharset);
        writer.WriteString("decodedWith", "windows-949");
        writer.WriteNumber("sizeBytes", sourceDocument.SizeBytes);
        writer.WriteString("sha256", sourceDocument.SourceSha256Hex);
        writer.WriteEndObject();
    }

    private static void writeConverter(Utf8JsonWriter writer)
    {
        writer.WriteStartObject("converter");
        writer.WriteString("id", "handong-course-catalog-importer");
        writer.WriteString("version", "1.0.0");
        writer.WriteEndObject();
    }

    private static void writeCounts(Utf8JsonWriter writer, CourseCatalog catalog)
    {
        writer.WriteStartObject("counts");
        writer.WriteNumber("courses", catalog.CourseCount.Value);
        writer.WriteNumber("offerings", catalog.OfferingCount.Value);
        writer.WriteNumber("scheduledOfferings", catalog.ScheduledOfferingCount.Value);
        writer.WriteNumber("meetingNotProvided", catalog.MeetingNotProvidedCount.Value);
        writer.WriteEndObject();
    }

    private static void writeDataQuality(Utf8JsonWriter writer, CatalogDataQuality dataQuality)
    {
        writer.WriteStartObject("dataQuality");
        writer.WriteString(
            "scheduleNormalizationSource",
            getScheduleNormalizationSourceName(dataQuality.ScheduleNormalizationSource));
        writer.WriteNumber("sourceEnglishScheduleMismatch", dataQuality.EnglishScheduleMismatchCount.Value);
        writer.WriteNumber("roomNotProvided", dataQuality.RoomNotProvidedCount.Value);
        writer.WriteNumber("enrollmentNotProvided", dataQuality.EnrollmentNotProvidedCount.Value);
        writer.WriteNumber("instructorUnconfirmed", dataQuality.InstructorUnconfirmedCount.Value);
        writer.WriteNumber("multiInstructorDisplay", dataQuality.MultiInstructorDisplayCount.Value);
        writer.WriteNumber("sourceRemarkLookupOnly", dataQuality.SourceRemarkLookupOnlyCount.Value);
        writer.WriteStartArray("manualReview");
        foreach (CatalogManualReview manualReview in dataQuality.ManualReviews)
        {
            writeManualReview(writer, manualReview);
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void writeManualReview(Utf8JsonWriter writer, CatalogManualReview manualReview)
    {
        writer.WriteStartObject();
        writer.WriteString("courseId", CatalogFileLayout.GetCourseId(manualReview.CourseCode));
        writer.WriteString("field", getManualReviewFieldName(manualReview.Field));
        writer.WriteString("reason", getManualReviewReasonName(manualReview.Reason));
        writer.WriteString("sourceValue", manualReview.SourceValue.Value);
        writer.WriteEndObject();
    }

    private static void writeCourses(
        Utf8JsonWriter writer,
        System.Collections.Generic.IReadOnlyList<CatalogCourse> courses)
    {
        writer.WriteStartArray("courses");
        foreach (CatalogCourse course in courses)
        {
            writer.WriteStartObject();
            writer.WriteString("courseId", CatalogFileLayout.GetCourseId(course.Code));
            writer.WriteString("code", course.Code.Value);
            writer.WriteStartObject("name");
            writer.WriteString("ko", course.KoreanName.Value);
            writer.WriteString("en", course.EnglishName.Value);
            writer.WriteEndObject();
            writer.WriteNumber("credits", course.Credits.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void writeOfferings(
        Utf8JsonWriter writer,
        AcademicTerm term,
        System.Collections.Generic.IReadOnlyList<CatalogOffering> offerings)
    {
        writer.WriteStartArray("offerings");
        foreach (CatalogOffering offering in offerings)
        {
            writeOffering(writer, term, offering);
        }

        writer.WriteEndArray();
    }

    private static void writeOffering(
        Utf8JsonWriter writer,
        AcademicTerm term,
        CatalogOffering offering)
    {
        writer.WriteStartObject();
        writer.WriteString(
            "offeringId",
            CatalogFileLayout.GetOfferingId(
                term,
                offering.Key.CourseCode,
                offering.Key.SectionCode));
        writer.WriteString("courseId", CatalogFileLayout.GetCourseId(offering.Key.CourseCode));
        writer.WriteString("sectionCode", offering.Key.SectionCode.Value);
        writer.WriteString(
            "requirementType",
            getRequirementTypeName(offering.Classification.RequirementType));
        writer.WriteString("offeringUnitName", offering.Classification.OfferingUnitName.Value);
        writer.WriteString(
            "instructionSession",
            getInstructionSessionName(offering.Classification.InstructionSession));
        writeInstructorAssignment(writer, offering.Instruction.InstructorAssignment);
        writeSchedule(writer, offering.Logistics.Schedule);
        writeLocation(writer, offering.Logistics.Location);
        writer.WriteNumber("seatCapacity", offering.Capacity.SeatCapacity.Value);
        writeEnrollment(writer, offering.Capacity.Enrollment);
        writer.WriteNumber(
            "englishInstructionPercentage",
            offering.Instruction.EnglishInstructionPercentage.Value);
        writeGeneralEducationCategory(writer, offering.Classification.GeneralEducationCategory);
        writeGradingPolicy(writer, offering.Instruction.GradingPolicy);
        writeDetails(writer, offering.Details);
        writer.WriteNumber("sourceRecordNumber", offering.SourceRecordNumber.Value);
        writer.WriteEndObject();
    }

    private static void writeInstructorAssignment(
        Utf8JsonWriter writer,
        InstructorAssignment assignment)
    {
        writer.WriteStartObject("instructorAssignment");
        writer.WriteString("status", getInstructorAssignmentStatusName(assignment.Status));
        if (assignment.Status == EInstructorAssignmentStatus.Confirmed)
        {
            writer.WriteString("displayText", assignment.GetDisplayText().Value);
            writer.WriteNumber("additionalInstructorCount", assignment.GetAdditionalInstructorCount().Value);
        }
        else
        {
            writer.WriteNull("displayText");
            writer.WriteNull("additionalInstructorCount");
        }

        writer.WriteEndObject();
    }

    private static void writeSchedule(Utf8JsonWriter writer, MeetingSchedule schedule)
    {
        writer.WriteStartObject("schedule");
        writer.WriteString("status", getMeetingScheduleStatusName(schedule.Status));
        if (schedule.Status == EMeetingScheduleStatus.Scheduled)
        {
            writer.WriteString("sourceTextKo", schedule.GetSourceText().Value);
        }
        else
        {
            writer.WriteNull("sourceTextKo");
        }

        writer.WriteStartArray("slots");
        foreach (MeetingSlot slot in schedule.Slots)
        {
            writer.WriteStartObject();
            writer.WriteString("day", getDayName(slot.Day));
            writer.WriteNumber("period", slot.Period.Value);
            writer.WriteEndObject();
        }

        writer.WriteEndArray();
        writer.WriteEndObject();
    }

    private static void writeLocation(Utf8JsonWriter writer, LocationAssignment location)
    {
        writer.WriteStartObject("location");
        writer.WriteString("status", getLocationAssignmentStatusName(location.Status));
        if (location.Status == ELocationAssignmentStatus.Assigned)
        {
            writer.WriteString("displayText", location.GetDisplayText().Value);
        }
        else
        {
            writer.WriteNull("displayText");
        }

        writer.WriteEndObject();
    }

    private static void writeEnrollment(Utf8JsonWriter writer, EnrollmentSnapshot enrollment)
    {
        if (enrollment.Status == EEnrollmentStatus.Provided)
        {
            writer.WriteNumber("currentEnrollment", enrollment.GetCount().Value);
        }
        else
        {
            writer.WriteNull("currentEnrollment");
        }
    }

    private static void writeGeneralEducationCategory(
        Utf8JsonWriter writer,
        GeneralEducationCategoryAssignment category)
    {
        if (category.Status == EGeneralEducationCategoryStatus.Provided)
        {
            writer.WriteString("generalEducationCategory", category.GetCategoryName().Value);
        }
        else
        {
            writer.WriteNull("generalEducationCategory");
        }
    }

    private static void writeGradingPolicy(Utf8JsonWriter writer, GradingPolicy gradingPolicy)
    {
        if (gradingPolicy.PassFailOptionAvailability == EPassFailOptionAvailability.NotProvided)
        {
            throw new InvalidOperationException("The catalog schema requires pass/fail option availability.");
        }

        writer.WriteStartObject("grading");
        writer.WriteString("type", getGradingTypeName(gradingPolicy.GradingType));
        writer.WriteBoolean(
            "passFailOptionAvailable",
            gradingPolicy.PassFailOptionAvailability == EPassFailOptionAvailability.Available);
        writer.WriteEndObject();
    }

    private static void writeDetails(Utf8JsonWriter writer, OfferingDetails details)
    {
        if (details.SyllabusAvailability == ESyllabusAvailability.Available)
        {
            throw new InvalidOperationException("The source does not expose a safe public syllabus URL.");
        }

        writer.WriteStartObject("details");
        writer.WriteNull("syllabusUrl");
        writer.WriteBoolean("remarksAvailable", details.AreRemarksAvailable);
        writer.WriteEndObject();
    }

    private static string getRequirementTypeName(ERequirementType value)
    {
        return value switch
        {
            ERequirementType.GeneralRequired => "generalRequired",
            ERequirementType.GeneralElectiveRequired => "generalElectiveRequired",
            ERequirementType.GeneralElective => "generalElective",
            ERequirementType.MajorRequired => "majorRequired",
            ERequirementType.MajorElective => "majorElective",
            ERequirementType.FreeElective => "freeElective",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown requirement type."),
        };
    }

    private static string getInstructionSessionName(EInstructionSession value)
    {
        return value switch
        {
            EInstructionSession.Daytime => "daytime",
            EInstructionSession.Evening => "evening",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown instruction session."),
        };
    }

    private static string getInstructorAssignmentStatusName(EInstructorAssignmentStatus value)
    {
        return value switch
        {
            EInstructorAssignmentStatus.Confirmed => "confirmed",
            EInstructorAssignmentStatus.Unconfirmed => "unconfirmed",
            EInstructorAssignmentStatus.NotProvided => "notProvided",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown instructor status."),
        };
    }

    private static string getMeetingScheduleStatusName(EMeetingScheduleStatus value)
    {
        return value switch
        {
            EMeetingScheduleStatus.Scheduled => "scheduled",
            EMeetingScheduleStatus.NotProvided => "notProvided",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown schedule status."),
        };
    }

    private static string getLocationAssignmentStatusName(ELocationAssignmentStatus value)
    {
        return value switch
        {
            ELocationAssignmentStatus.Assigned => "assigned",
            ELocationAssignmentStatus.NotProvided => "notProvided",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown location status."),
        };
    }

    private static string getGradingTypeName(EGradingType value)
    {
        return value switch
        {
            EGradingType.Letter => "letter",
            EGradingType.PassFail => "passFail",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown grading type."),
        };
    }

    private static string getDayName(EDay value)
    {
        return value switch
        {
            EDay.Monday => "monday",
            EDay.Tuesday => "tuesday",
            EDay.Wednesday => "wednesday",
            EDay.Thursday => "thursday",
            EDay.Friday => "friday",
            EDay.Saturday => "saturday",
            EDay.Sunday => "sunday",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown weekday."),
        };
    }

    private static string getScheduleNormalizationSourceName(EScheduleNormalizationSource value)
    {
        return value switch
        {
            EScheduleNormalizationSource.KoreanPeriodText => "koreanPeriodText",
            _ => throw new ArgumentOutOfRangeException(
                nameof(value),
                value,
                "Unknown schedule normalization source."),
        };
    }

    private static string getManualReviewFieldName(EManualReviewField value)
    {
        return value switch
        {
            EManualReviewField.EnglishCourseName => "name.en",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown review field."),
        };
    }

    private static string getManualReviewReasonName(EManualReviewReason value)
    {
        return value switch
        {
            EManualReviewReason.UnexpectedQuestionMarkInSource => "unexpectedQuestionMarkInSource",
            _ => throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown review reason."),
        };
    }
}
