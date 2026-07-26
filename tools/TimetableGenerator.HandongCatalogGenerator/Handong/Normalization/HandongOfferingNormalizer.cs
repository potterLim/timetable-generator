using System;
using System.Collections.Generic;
using System.Globalization;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongOfferingNormalizer
{
    private readonly HandongCourseNameNormalizer mCourseNameNormalizer;
    private readonly HandongOfferingInformationNormalizer mOfferingInformationNormalizer;
    private readonly HandongScheduleNormalizer mScheduleNormalizer;

    public HandongOfferingNormalizer()
    {
        mCourseNameNormalizer = new HandongCourseNameNormalizer();
        mOfferingInformationNormalizer = new HandongOfferingInformationNormalizer();
        mScheduleNormalizer = new HandongScheduleNormalizer();
    }

    public HandongOfferingNormalizationResult NormalizeOffering(HandongRawOfferingRow row)
    {
        if (row == null)
        {
            throw new ArgumentNullException(nameof(row));
        }

        CourseCode courseCode = new CourseCode(HandongCellValueReader.getRequiredSingleLine(row, EHandongColumn.CourseCode));
        CourseSectionCode sectionCode = new CourseSectionCode(HandongCellValueReader.getRequiredSingleLine(row, EHandongColumn.Section));
        validateSourceLinkIdentity(row, courseCode, sectionCode);
        HandongCourseNameNormalizationResult courseNames = mCourseNameNormalizer.NormalizeCourseName(row);
        CourseCredits credits = parseCredits(row);

        CatalogCourse course = new CatalogCourse(
            courseCode,
            courseNames.KoreanName,
            courseNames.EnglishName,
            credits,
            row.SourceRecordNumber);

        ERequirementType requirementType = parseRequirementType(row);
        HandongOfferingInformationNormalizationResult offeringInformation = mOfferingInformationNormalizer.NormalizeOfferingInformation(row);
        GeneralEducationCategoryAssignment generalEducationCategory = normalizeGeneralEducationCategory(row);
        OfferingClassification classification = new OfferingClassification(
            requirementType,
            offeringInformation.OfferingUnitName,
            offeringInformation.InstructionSession,
            generalEducationCategory);

        HandongScheduleNormalizationResult scheduleResult = mScheduleNormalizer.NormalizeSchedule(row);
        LocationAssignment location = normalizeLocation(row);
        OfferingLogistics logistics = new OfferingLogistics(scheduleResult.Schedule, location);

        OfferingCapacity capacity = new OfferingCapacity(parseSeatCapacity(row), normalizeEnrollment(row));
        OfferingInstruction instruction = new OfferingInstruction(
            offeringInformation.InstructorAssignment,
            parseEnglishInstructionPercentage(row),
            normalizeGradingPolicy(row));
        OfferingDetails details = normalizeOfferingDetails(row);

        CourseOfferingKey offeringKey = new CourseOfferingKey(courseCode, sectionCode);
        CatalogOffering offering = new CatalogOffering(
            offeringKey,
            classification,
            instruction,
            logistics,
            capacity,
            details,
            row.SourceRecordNumber);
        return new HandongOfferingNormalizationResult(
            course,
            offering,
            scheduleResult.EnglishScheduleComparison);
    }

    private static CourseCredits parseCredits(HandongRawOfferingRow row)
    {
        string sourceValue = HandongCellValueReader.getRequiredSingleLine(row, EHandongColumn.Credits);
        decimal creditValue;
        bool isCreditParsed = decimal.TryParse(sourceValue, NumberStyles.AllowDecimalPoint, CultureInfo.InvariantCulture, out creditValue);
        if (isCreditParsed == false)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.Credits,
                "The course credit value is invalid.");
        }

        try
        {
            return new CourseCredits(creditValue);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.Credits,
                exception.Message);
        }
    }

    private static void validateSourceLinkIdentity(
        HandongRawOfferingRow row,
        CourseCode courseCode,
        CourseSectionCode sectionCode)
    {
        HandongSourceLinkMetadata? sourceLinkMetadataOrNull = row.SourceLinkMetadataOrNull;
        if (sourceLinkMetadataOrNull == null)
        {
            return;
        }

        if (sourceLinkMetadataOrNull.CourseCode != courseCode)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.CourseCode,
                "The table course code differs from the source-link course code.");
        }

        if (sourceLinkMetadataOrNull.CourseSectionCode != sectionCode)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.Section,
                "The table section code differs from the source-link section code.");
        }
    }

    private static ERequirementType parseRequirementType(HandongRawOfferingRow row)
    {
        string sourceValue = HandongCellValueReader.getRequiredSingleLine(row, EHandongColumn.Classification);
        switch (sourceValue)
        {
            case "교필":
                return ERequirementType.GeneralRequired;
            case "교선":
                return ERequirementType.GeneralElective;
            case "교선필":
                return ERequirementType.GeneralElectiveRequired;
            case "전필":
                return ERequirementType.MajorRequired;
            case "전선":
                return ERequirementType.MajorElective;
            case "자선":
                return ERequirementType.FreeElective;
            default:
                throw new InvalidHandongSourceRecordException(
                    row.SourceRecordNumber,
                    EHandongColumn.Classification,
                    "Unsupported course classification: " + sourceValue);
        }
    }

    private static LocationAssignment normalizeLocation(HandongRawOfferingRow row)
    {
        IReadOnlyList<string> lines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.Classroom);
        if (lines.Count == 0)
        {
            return LocationAssignment.NotProvided;
        }

        if (lines.Count != 1)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.Classroom,
                "A classroom must contain at most one semantic line.");
        }

        return LocationAssignment.CreateAssigned(new ClassroomDisplayText(lines[0]));
    }

    private static SeatCapacity parseSeatCapacity(HandongRawOfferingRow row)
    {
        string sourceValue = HandongCellValueReader.getRequiredSingleLine(row, EHandongColumn.Capacity);
        int capacityValue = parseNonnegativeInteger(sourceValue, row, EHandongColumn.Capacity, "seat capacity");
        return new SeatCapacity(capacityValue);
    }

    private static EnrollmentSnapshot normalizeEnrollment(HandongRawOfferingRow row)
    {
        IReadOnlyList<string> lines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.Enrollment);
        if (lines.Count == 0)
        {
            return EnrollmentSnapshot.NotProvided;
        }

        if (lines.Count != 1)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.Enrollment,
                "Enrollment must contain at most one line.");
        }

        int enrollmentValue = parseNonnegativeInteger(lines[0], row, EHandongColumn.Enrollment, "enrollment count");
        return EnrollmentSnapshot.CreateProvided(new EnrollmentCount(enrollmentValue));
    }

    private static EnglishInstructionPercentage parseEnglishInstructionPercentage(
        HandongRawOfferingRow row)
    {
        string sourceValue = HandongCellValueReader.getRequiredSingleLine(row, EHandongColumn.EnglishInstruction);
        if (sourceValue.EndsWith('%') == false)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.EnglishInstruction,
                "English instruction percentages must end with a percent sign.");
        }

        string numericValue = sourceValue.Substring(0, sourceValue.Length - 1).Trim();
        int percentageValue = parseNonnegativeInteger(numericValue, row, EHandongColumn.EnglishInstruction, "English instruction percentage");
        try
        {
            return new EnglishInstructionPercentage(percentageValue);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.EnglishInstruction,
                exception.Message);
        }
    }

    private static GeneralEducationCategoryAssignment normalizeGeneralEducationCategory(
        HandongRawOfferingRow row)
    {
        IReadOnlyList<string> lines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.GeneralEducationPractical);
        if (lines.Count == 0)
        {
            return GeneralEducationCategoryAssignment.NotProvided;
        }

        if (lines.Count != 1)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.GeneralEducationPractical,
                "A general education category must contain at most one semantic line.");
        }

        GeneralEducationCategoryName categoryName = new GeneralEducationCategoryName(lines[0]);
        return GeneralEducationCategoryAssignment.CreateProvided(categoryName);
    }

    private static GradingPolicy normalizeGradingPolicy(HandongRawOfferingRow row)
    {
        EGradingType gradingType = parseGradingType(row);
        EPassFailOptionAvailability passFailOptionAvailability = parsePassFailOptionAvailability(row);
        return new GradingPolicy(gradingType, passFailOptionAvailability);
    }

    private static EGradingType parseGradingType(HandongRawOfferingRow row)
    {
        string sourceValue = HandongCellValueReader.getRequiredSingleLine(row, EHandongColumn.GradingType);
        switch (sourceValue)
        {
            case "A+":
                return EGradingType.Letter;
            case "PF":
                return EGradingType.PassFail;
            default:
                throw new InvalidHandongSourceRecordException(
                    row.SourceRecordNumber,
                    EHandongColumn.GradingType,
                    "Unsupported grading type: " + sourceValue);
        }
    }

    private static EPassFailOptionAvailability parsePassFailOptionAvailability(
        HandongRawOfferingRow row)
    {
        IReadOnlyList<string> lines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.PassFailAvailable);
        if (lines.Count == 0)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.PassFailAvailable,
                "Pass/fail option availability is required.");
        }

        if (lines.Count != 1)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.PassFailAvailable,
                "Pass/fail option availability must contain at most one line.");
        }

        switch (lines[0])
        {
            case "Y":
                return EPassFailOptionAvailability.Available;
            case "N":
                return EPassFailOptionAvailability.Unavailable;
            default:
                throw new InvalidHandongSourceRecordException(
                    row.SourceRecordNumber,
                    EHandongColumn.PassFailAvailable,
                    "Unsupported pass/fail option value: " + lines[0]);
        }
    }

    private static OfferingDetails normalizeOfferingDetails(HandongRawOfferingRow row)
    {
        IReadOnlyList<string> syllabusLines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.Syllabus);
        if (syllabusLines.Count != 0)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.Syllabus,
                "The export unexpectedly contains syllabus data that cannot be published safely.");
        }

        IReadOnlyList<string> noteLines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.Notes);
        ERemarksAvailability remarksAvailability;
        if (noteLines.Count == 0)
        {
            remarksAvailability = ERemarksAvailability.NotProvided;
        }
        else if (noteLines.Count == 1 && string.Equals(noteLines[0], "조회", StringComparison.Ordinal))
        {
            remarksAvailability = ERemarksAvailability.LookupAvailable;
        }
        else
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                EHandongColumn.Notes,
                "The export contains an unsupported remarks value.");
        }

        return new OfferingDetails(ESyllabusAvailability.NotProvided, remarksAvailability);
    }

    private static int parseNonnegativeInteger(
        string sourceValue,
        HandongRawOfferingRow row,
        EHandongColumn column,
        string valueDescription)
    {
        int parsedValue;
        bool isParsed = int.TryParse(sourceValue, NumberStyles.None, CultureInfo.InvariantCulture, out parsedValue);
        if (isParsed == false || parsedValue < 0)
        {
            throw new InvalidHandongSourceRecordException(
                row.SourceRecordNumber,
                column,
                "Invalid " + valueDescription + ".");
        }

        return parsedValue;
    }
}
