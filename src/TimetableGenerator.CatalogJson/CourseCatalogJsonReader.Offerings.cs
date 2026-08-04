using System.Collections.Generic;
using System.Text.Json;
using TimetableGenerator.CatalogJson.Internal;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.CatalogJson;

public static partial class CourseCatalogJsonReader
{
    private static void parseOfferings(
        JsonElement offeringsElement,
        InstitutionId institutionId,
        AcademicTerm term,
        IReadOnlyDictionary<CourseId, CourseCode> courseCodesById,
        ICollection<CatalogOffering> offerings,
        ICollection<CatalogOfferingMetadata> offeringMetadata)
    {
        int offeringIndex = 0;
        foreach (JsonElement offeringElement in offeringsElement.EnumerateArray())
        {
            string offeringPath = "$.offerings[" + offeringIndex + "]";
            CatalogOffering offering;
            CatalogOfferingMetadata metadata;
            parseOffering(
                offeringElement,
                offeringPath,
                institutionId,
                term,
                courseCodesById,
                out offering,
                out metadata);
            offerings.Add(offering);
            offeringMetadata.Add(metadata);
            ++offeringIndex;
        }

        if (offerings.Count == 0)
        {
            throw new CatalogJsonFormatException("$.offerings", "at least one offering is required.");
        }
    }

    private static void parseOffering(
        JsonElement element,
        string path,
        InstitutionId institutionId,
        AcademicTerm term,
        IReadOnlyDictionary<CourseId, CourseCode> courseCodesById,
        out CatalogOffering offering,
        out CatalogOfferingMetadata metadata)
    {
        StrictJsonObject offeringObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "offeringId",
                "courseId",
                "sectionCode",
                "requirementType",
                "offeringUnitName",
                "instructionSession",
                "instructorAssignment",
                "schedule",
                "location",
                "seatCapacity",
                "currentEnrollment",
                "englishInstructionPercentage",
                "generalEducationCategory",
                "grading",
                "details",
                "sourceRecordNumber",
            });

        OfferingId offeringId = new OfferingId(offeringObject.GetString("offeringId"));
        CourseId courseId = new CourseId(offeringObject.GetString("courseId"));
        CourseCode? courseCodeOrNull;
        bool hasCourseCode = courseCodesById.TryGetValue(courseId, out courseCodeOrNull);
        if (hasCourseCode == false || courseCodeOrNull == null)
        {
            throw new CatalogJsonFormatException(offeringObject.GetPropertyPath("courseId"), "offerings must reference a course in the catalog.");
        }

        CourseSectionCode sectionCode = new CourseSectionCode(offeringObject.GetString("sectionCode"));
        string expectedOfferingId = CatalogJsonValueParser.BuildOfferingId(institutionId, term, courseCodeOrNull, sectionCode);
        CatalogJsonValueParser.RequireExactString(offeringId.Value, expectedOfferingId, offeringObject.GetPropertyPath("offeringId"));

        CatalogOfferingClassificationMetadata classification = parseClassification(offeringObject);
        InstructorAssignmentMetadata instructorAssignment = parseInstructorAssignment(offeringObject.GetElement("instructorAssignment"), offeringObject.GetPropertyPath("instructorAssignment"));
        GradingMetadata grading = parseGrading(offeringObject.GetElement("grading"), offeringObject.GetPropertyPath("grading"));
        EnglishInstructionPercentage englishInstructionPercentage = new EnglishInstructionPercentage(offeringObject.GetDecimal("englishInstructionPercentage"));
        CatalogOfferingInstructionMetadata instruction = new CatalogOfferingInstructionMetadata(instructorAssignment, englishInstructionPercentage, grading);

        KoreanScheduleSourceText? scheduleSourceTextOrNull;
        MeetingSchedule meetingSchedule = parseMeetingSchedule(offeringObject.GetElement("schedule"), offeringObject.GetPropertyPath("schedule"), out scheduleSourceTextOrNull);
        LocationAssignmentMetadata location = parseLocation(offeringObject.GetElement("location"), offeringObject.GetPropertyPath("location"));
        CatalogOfferingLogisticsMetadata logistics;
        if (scheduleSourceTextOrNull == null)
        {
            logistics = CatalogOfferingLogisticsMetadata.CreateWithoutProvidedSchedule(location);
        }
        else
        {
            logistics = CatalogOfferingLogisticsMetadata.CreateScheduled(scheduleSourceTextOrNull, location);
        }

        CatalogOfferingCapacityMetadata capacity = parseCapacity(offeringObject);
        OfferingDetailsMetadata details = parseDetails(offeringObject.GetElement("details"), offeringObject.GetPropertyPath("details"));
        SourceRecordNumber sourceRecordNumber = new SourceRecordNumber(offeringObject.GetInt64("sourceRecordNumber"));

        offering = new CatalogOffering(offeringId, courseId, sectionCode, meetingSchedule);
        metadata = new CatalogOfferingMetadata(
            offeringId,
            classification,
            instruction,
            logistics,
            capacity,
            details,
            sourceRecordNumber);
    }

    private static CatalogOfferingClassificationMetadata parseClassification(StrictJsonObject offeringObject)
    {
        ERequirementType requirementType = parseRequirementType(offeringObject.GetString("requirementType"), offeringObject.GetPropertyPath("requirementType"));
        OfferingUnitName offeringUnitName = new OfferingUnitName(offeringObject.GetString("offeringUnitName"));
        EInstructionSession instructionSession = parseInstructionSession(offeringObject.GetString("instructionSession"), offeringObject.GetPropertyPath("instructionSession"));
        string? categoryNameOrNull = offeringObject.GetNullableStringOrNull("generalEducationCategory");
        if (categoryNameOrNull == null)
        {
            return CatalogOfferingClassificationMetadata.CreateWithoutGeneralEducationCategory(requirementType, offeringUnitName, instructionSession);
        }

        GeneralEducationCategoryName categoryName = new GeneralEducationCategoryName(categoryNameOrNull);
        return CatalogOfferingClassificationMetadata.CreateWithGeneralEducationCategory(requirementType, offeringUnitName, instructionSession, categoryName);
    }

    private static InstructorAssignmentMetadata parseInstructorAssignment(JsonElement element, string path)
    {
        StrictJsonObject assignmentObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "status",
                "displayText",
                "additionalInstructorCount",
            });
        EInstructorAssignmentStatus status = parseInstructorAssignmentStatus(assignmentObject.GetString("status"), assignmentObject.GetPropertyPath("status"));
        string? displayTextOrNull = assignmentObject.GetNullableStringOrNull("displayText");
        int? additionalInstructorCountOrNull = assignmentObject.GetNullableInt32OrNull("additionalInstructorCount");

        switch (status)
        {
            case EInstructorAssignmentStatus.Confirmed:
                if (displayTextOrNull == null || additionalInstructorCountOrNull.HasValue == false)
                {
                    throw new CatalogJsonFormatException(path, "confirmed instructors require display text and an additional instructor count.");
                }

                InstructorDisplayText displayText = new InstructorDisplayText(displayTextOrNull);
                AdditionalInstructorCount additionalInstructorCount = new AdditionalInstructorCount(additionalInstructorCountOrNull.Value);
                return InstructorAssignmentMetadata.CreateConfirmed(displayText, additionalInstructorCount);
            case EInstructorAssignmentStatus.Unconfirmed:
                requireMissingInstructorValues(displayTextOrNull, additionalInstructorCountOrNull, path);
                return InstructorAssignmentMetadata.Unconfirmed;
            case EInstructorAssignmentStatus.NotProvided:
                requireMissingInstructorValues(displayTextOrNull, additionalInstructorCountOrNull, path);
                return InstructorAssignmentMetadata.NotProvided;
            default:
                throw new CatalogJsonFormatException(path, "the instructor status is invalid.");
        }
    }

    private static void requireMissingInstructorValues(string? displayTextOrNull, int? additionalInstructorCountOrNull, string path)
    {
        if (displayTextOrNull != null || additionalInstructorCountOrNull.HasValue)
        {
            throw new CatalogJsonFormatException(path, "unconfirmed or missing instructors must use null detail values.");
        }
    }

    private static MeetingSchedule parseMeetingSchedule(JsonElement element, string path, out KoreanScheduleSourceText? scheduleSourceTextOrNull)
    {
        StrictJsonObject scheduleObject = StrictJsonObject.Create(
            element,
            path,
            new string[]
            {
                "status",
                "sourceTextKo",
                "slots",
            });
        EMeetingScheduleStatus status = parseMeetingScheduleStatus(scheduleObject.GetString("status"), scheduleObject.GetPropertyPath("status"));
        string? sourceTextOrNull = scheduleObject.GetNullableStringOrNull("sourceTextKo");
        List<MeetingSlot> slots = parseMeetingSlots(scheduleObject.GetArray("slots"), scheduleObject.GetPropertyPath("slots"));

        switch (status)
        {
            case EMeetingScheduleStatus.Scheduled:
                if (sourceTextOrNull == null || slots.Count == 0)
                {
                    throw new CatalogJsonFormatException(path, "scheduled meetings require Korean source text and at least one slot.");
                }

                scheduleSourceTextOrNull = new KoreanScheduleSourceText(sourceTextOrNull);
                return MeetingSchedule.CreateScheduled(slots);
            case EMeetingScheduleStatus.NotProvided:
                if (sourceTextOrNull != null || slots.Count != 0)
                {
                    throw new CatalogJsonFormatException(path, "time-not-provided meetings require null source text and an empty slot array.");
                }

                scheduleSourceTextOrNull = null;
                return MeetingSchedule.NotProvided;
            default:
                throw new CatalogJsonFormatException(path, "the meeting schedule status is invalid.");
        }
    }

    private static List<MeetingSlot> parseMeetingSlots(JsonElement slotsElement, string path)
    {
        List<MeetingSlot> slots = new List<MeetingSlot>();
        int slotIndex = 0;
        foreach (JsonElement slotElement in slotsElement.EnumerateArray())
        {
            string slotPath = path + "[" + slotIndex + "]";
            StrictJsonObject slotObject = StrictJsonObject.Create(
                slotElement,
                slotPath,
                new string[]
                {
                    "day",
                    "period",
                });
            EDay day = parseDay(slotObject.GetString("day"), slotObject.GetPropertyPath("day"));
            AcademicPeriod period = new AcademicPeriod(slotObject.GetInt32("period"));
            slots.Add(new MeetingSlot(day, period));
            ++slotIndex;
        }

        return slots;
    }

    private static LocationAssignmentMetadata parseLocation(JsonElement element, string path)
    {
        StrictJsonObject locationObject = StrictJsonObject.Create(element, path, new string[] { "status", "displayText" });
        ELocationAssignmentStatus status = parseLocationAssignmentStatus(locationObject.GetString("status"), locationObject.GetPropertyPath("status"));
        string? displayTextOrNull = locationObject.GetNullableStringOrNull("displayText");
        switch (status)
        {
            case ELocationAssignmentStatus.Assigned:
                if (displayTextOrNull == null)
                {
                    throw new CatalogJsonFormatException(locationObject.GetPropertyPath("displayText"), "assigned locations require display text.");
                }

                return LocationAssignmentMetadata.CreateAssigned(new ClassroomDisplayText(displayTextOrNull));
            case ELocationAssignmentStatus.NotProvided:
                if (displayTextOrNull != null)
                {
                    throw new CatalogJsonFormatException(locationObject.GetPropertyPath("displayText"), "locations without provided data require a null display value.");
                }

                return LocationAssignmentMetadata.NotProvided;
            default:
                throw new CatalogJsonFormatException(path, "the location assignment status is invalid.");
        }
    }

    private static CatalogOfferingCapacityMetadata parseCapacity(StrictJsonObject offeringObject)
    {
        OfferingSeatCapacity seatCapacity = new OfferingSeatCapacity(offeringObject.GetInt32("seatCapacity"));
        int? enrollmentCountOrNull = offeringObject.GetNullableInt32OrNull("currentEnrollment");
        if (enrollmentCountOrNull.HasValue == false)
        {
            return CatalogOfferingCapacityMetadata.CreateWithoutCurrentEnrollment(seatCapacity);
        }

        OfferingEnrollmentCount enrollmentCount = new OfferingEnrollmentCount(enrollmentCountOrNull.Value);
        return CatalogOfferingCapacityMetadata.CreateWithCurrentEnrollment(seatCapacity, enrollmentCount);
    }

    private static GradingMetadata parseGrading(JsonElement element, string path)
    {
        StrictJsonObject gradingObject = StrictJsonObject.Create(element, path, new string[] { "type", "passFailOptionAvailable" });
        EGradingType gradingType = parseGradingType(gradingObject.GetString("type"), gradingObject.GetPropertyPath("type"));
        bool isPassFailOptionAvailable = gradingObject.GetBoolean("passFailOptionAvailable");
        if (isPassFailOptionAvailable)
        {
            return new GradingMetadata(gradingType, EPassFailOptionAvailability.Available);
        }

        return new GradingMetadata(gradingType, EPassFailOptionAvailability.Unavailable);
    }

    private static OfferingDetailsMetadata parseDetails(JsonElement element, string path)
    {
        StrictJsonObject detailsObject = StrictJsonObject.Create(element, path, new string[] { "syllabusUrl", "remarksAvailable" });
        detailsObject.RequireNull("syllabusUrl");
        bool areRemarksAvailable = detailsObject.GetBoolean("remarksAvailable");
        if (areRemarksAvailable)
        {
            return new OfferingDetailsMetadata(ERemarksAvailability.Available);
        }

        return new OfferingDetailsMetadata(ERemarksAvailability.Unavailable);
    }

    private static ERequirementType parseRequirementType(string value, string path)
    {
        switch (value)
        {
            case "generalRequired":
                return ERequirementType.GeneralRequired;
            case "generalElectiveRequired":
                return ERequirementType.GeneralElectiveRequired;
            case "generalElective":
                return ERequirementType.GeneralElective;
            case "majorRequired":
                return ERequirementType.MajorRequired;
            case "majorElective":
                return ERequirementType.MajorElective;
            case "freeElective":
                return ERequirementType.FreeElective;
            default:
                throw new CatalogJsonFormatException(path, "the requirement type is not supported.");
        }
    }

    private static EInstructionSession parseInstructionSession(string value, string path)
    {
        switch (value)
        {
            case "daytime":
                return EInstructionSession.Daytime;
            case "evening":
                return EInstructionSession.Evening;
            default:
                throw new CatalogJsonFormatException(path, "the instruction session is not supported.");
        }
    }

    private static EInstructorAssignmentStatus parseInstructorAssignmentStatus(string value, string path)
    {
        switch (value)
        {
            case "confirmed":
                return EInstructorAssignmentStatus.Confirmed;
            case "unconfirmed":
                return EInstructorAssignmentStatus.Unconfirmed;
            case "notProvided":
                return EInstructorAssignmentStatus.NotProvided;
            default:
                throw new CatalogJsonFormatException(path, "the instructor status is not supported.");
        }
    }

    private static EMeetingScheduleStatus parseMeetingScheduleStatus(string value, string path)
    {
        switch (value)
        {
            case "scheduled":
                return EMeetingScheduleStatus.Scheduled;
            case "notProvided":
                return EMeetingScheduleStatus.NotProvided;
            default:
                throw new CatalogJsonFormatException(path, "the schedule status is not supported.");
        }
    }

    private static ELocationAssignmentStatus parseLocationAssignmentStatus(string value, string path)
    {
        switch (value)
        {
            case "assigned":
                return ELocationAssignmentStatus.Assigned;
            case "notProvided":
                return ELocationAssignmentStatus.NotProvided;
            default:
                throw new CatalogJsonFormatException(path, "the location status is not supported.");
        }
    }

    private static EGradingType parseGradingType(string value, string path)
    {
        switch (value)
        {
            case "letter":
                return EGradingType.Letter;
            case "passFail":
                return EGradingType.PassFail;
            default:
                throw new CatalogJsonFormatException(path, "the grading type is not supported.");
        }
    }

    private static EDay parseDay(string value, string path)
    {
        switch (value)
        {
            case "monday":
                return EDay.Monday;
            case "tuesday":
                return EDay.Tuesday;
            case "wednesday":
                return EDay.Wednesday;
            case "thursday":
                return EDay.Thursday;
            case "friday":
                return EDay.Friday;
            case "saturday":
                return EDay.Saturday;
            case "sunday":
                return EDay.Sunday;
            default:
                throw new CatalogJsonFormatException(path, "the weekday is not supported.");
        }
    }
}
