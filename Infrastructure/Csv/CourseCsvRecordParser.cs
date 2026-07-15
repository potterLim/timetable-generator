using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TimetableGenerator.Core.Domain;
using CoreClassroomLocation = TimetableGenerator.Core.Domain.ClassroomLocation;
using CoreDay = TimetableGenerator.Core.Domain.EDay;
using CorePeriod = TimetableGenerator.Core.Domain.Period;

namespace TimetableGenerator.Infrastructure.Csv;

internal sealed class CourseCsvRecordParser
{
    private const int COURSE_CHOICE_GROUP_ID_INDEX = 0;
    private const int COURSE_SECTION_CODE_INDEX = 1;
    private const int COURSE_NAME_INDEX = 2;
    private const int SCHEDULE_SLOTS_INDEX = 3;
    private const int CLASSROOM_LOCATION_INDEX = 4;

    private static readonly Regex SCHEDULE_SLOT_PATTERN = new Regex(
        "\\A(월요일|화요일|수요일|목요일|금요일|토요일|일요일)([0-9]+)교시\\z",
        RegexOptions.Compiled | RegexOptions.CultureInvariant);

    public CourseCsvRecordParseResult ParseCourseOffering(
        string[] fields,
        CsvSourcePosition sourcePosition,
        CourseCsvSchema schema)
    {
        if (fields == null)
        {
            throw new ArgumentNullException(nameof(fields));
        }

        if (schema == null)
        {
            throw new ArgumentNullException(nameof(schema));
        }

        if (fields.Length != schema.ColumnCount)
        {
            throw new ArgumentException("The CSV record does not match its validated schema.", nameof(fields));
        }

        List<CourseImportDiagnostic> diagnostics = new List<CourseImportDiagnostic>();

        CourseChoiceGroupId choiceGroupId = parseCourseChoiceGroupId(
            fields[COURSE_CHOICE_GROUP_ID_INDEX],
            sourcePosition,
            diagnostics);
        CourseSectionCode sectionCodeOrNull = parseCourseSectionCodeOrNull(
            fields[COURSE_SECTION_CODE_INDEX],
            sourcePosition,
            diagnostics);
        CourseName courseNameOrNull = parseCourseNameOrNull(
            fields[COURSE_NAME_INDEX],
            sourcePosition,
            diagnostics);
        IReadOnlyList<ScheduleSlot> scheduleSlots = parseScheduleSlots(
            fields[SCHEDULE_SLOTS_INDEX],
            sourcePosition,
            diagnostics);

        ClassroomAssignment classroomAssignment = ClassroomAssignment.Unassigned;
        if (schema.HasClassroomLocationColumn)
        {
            classroomAssignment = parseClassroomAssignment(
                fields[CLASSROOM_LOCATION_INDEX],
                sourcePosition,
                diagnostics);
        }

        if (diagnostics.Count > 0)
        {
            return CourseCsvRecordParseResult.CreateFailure(diagnostics);
        }

        CourseOffering courseOffering = new CourseOffering(
            choiceGroupId,
            courseNameOrNull,
            sectionCodeOrNull,
            classroomAssignment,
            scheduleSlots);
        return CourseCsvRecordParseResult.CreateSuccess(courseOffering);
    }

    private static CourseChoiceGroupId parseCourseChoiceGroupId(
        string rawValue,
        CsvSourcePosition sourcePosition,
        ICollection<CourseImportDiagnostic> diagnostics)
    {
        string normalizedValue = getTrimmedFieldValue(rawValue);
        int parsedValue;
        bool isParsed = int.TryParse(
            normalizedValue,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out parsedValue);

        if (isParsed == false || parsedValue <= 0)
        {
            diagnostics.Add(createDiagnostic(
                ECourseImportErrorCode.InvalidCourseChoiceGroupId,
                sourcePosition,
                ECsvColumn.CourseChoiceGroupId,
                rawValue,
                "Course choice group IDs must be positive base-10 integers."));
            return default(CourseChoiceGroupId);
        }

        return new CourseChoiceGroupId(parsedValue);
    }

    private static CourseSectionCode parseCourseSectionCodeOrNull(
        string rawValue,
        CsvSourcePosition sourcePosition,
        ICollection<CourseImportDiagnostic> diagnostics)
    {
        string normalizedValue = getTrimmedFieldValue(rawValue);
        if (normalizedValue.Length == 0)
        {
            diagnostics.Add(createDiagnostic(
                ECourseImportErrorCode.InvalidCourseSectionCode,
                sourcePosition,
                ECsvColumn.CourseSectionCode,
                rawValue,
                "Course section codes cannot be empty."));
            return null;
        }

        return new CourseSectionCode(normalizedValue);
    }

    private static CourseName parseCourseNameOrNull(
        string rawValue,
        CsvSourcePosition sourcePosition,
        ICollection<CourseImportDiagnostic> diagnostics)
    {
        string normalizedValue = getTrimmedFieldValue(rawValue);
        if (normalizedValue.Length == 0)
        {
            diagnostics.Add(createDiagnostic(
                ECourseImportErrorCode.InvalidCourseName,
                sourcePosition,
                ECsvColumn.CourseName,
                rawValue,
                "Course names cannot be empty."));
            return null;
        }

        return new CourseName(normalizedValue);
    }

    private static IReadOnlyList<ScheduleSlot> parseScheduleSlots(
        string rawValue,
        CsvSourcePosition sourcePosition,
        ICollection<CourseImportDiagnostic> diagnostics)
    {
        string normalizedValue = getTrimmedFieldValue(rawValue);
        List<ScheduleSlot> scheduleSlots = new List<ScheduleSlot>();
        HashSet<ScheduleSlot> uniqueScheduleSlots = new HashSet<ScheduleSlot>();

        if (normalizedValue.Length == 0)
        {
            diagnostics.Add(createDiagnostic(
                ECourseImportErrorCode.EmptyScheduleSlot,
                sourcePosition,
                ECsvColumn.ScheduleSlots,
                rawValue,
                "At least one schedule slot is required."));
            return scheduleSlots.AsReadOnly();
        }

        string[] scheduleSlotTokens = normalizedValue.Split(
            new char[] { '/' },
            StringSplitOptions.None);

        foreach (string rawScheduleSlotToken in scheduleSlotTokens)
        {
            string scheduleSlotToken = rawScheduleSlotToken.Trim();
            if (scheduleSlotToken.Length == 0)
            {
                diagnostics.Add(createDiagnostic(
                    ECourseImportErrorCode.EmptyScheduleSlot,
                    sourcePosition,
                    ECsvColumn.ScheduleSlots,
                    rawScheduleSlotToken,
                    "Schedule slot separators cannot contain empty entries."));
                continue;
            }

            ScheduleSlot scheduleSlot;
            bool isParsed = tryParseScheduleSlot(scheduleSlotToken, out scheduleSlot);
            if (isParsed == false)
            {
                diagnostics.Add(createDiagnostic(
                    ECourseImportErrorCode.InvalidScheduleSlot,
                    sourcePosition,
                    ECsvColumn.ScheduleSlots,
                    rawScheduleSlotToken,
                    "Schedule slots must exactly match {Korean day}{positive period}교시."));
                continue;
            }

            if (uniqueScheduleSlots.Add(scheduleSlot) == false)
            {
                diagnostics.Add(createDiagnostic(
                    ECourseImportErrorCode.DuplicateScheduleSlot,
                    sourcePosition,
                    ECsvColumn.ScheduleSlots,
                    rawScheduleSlotToken,
                    "A course offering cannot repeat a schedule slot."));
                continue;
            }

            scheduleSlots.Add(scheduleSlot);
        }

        return scheduleSlots.AsReadOnly();
    }

    private static bool tryParseScheduleSlot(string scheduleSlotText, out ScheduleSlot scheduleSlot)
    {
        scheduleSlot = default(ScheduleSlot);

        Match match = SCHEDULE_SLOT_PATTERN.Match(scheduleSlotText);
        if (match.Success == false)
        {
            return false;
        }

        int periodValue;
        bool isPeriodParsed = int.TryParse(
            match.Groups[2].Value,
            NumberStyles.None,
            CultureInfo.InvariantCulture,
            out periodValue);
        if (isPeriodParsed == false || periodValue <= 0)
        {
            return false;
        }

        CoreDay day = parseKoreanDay(match.Groups[1].Value);
        CorePeriod period = new CorePeriod(periodValue);
        scheduleSlot = new ScheduleSlot(day, period);
        return true;
    }

    private static CoreDay parseKoreanDay(string koreanDay)
    {
        switch (koreanDay)
        {
            case "월요일":
                return CoreDay.Monday;
            case "화요일":
                return CoreDay.Tuesday;
            case "수요일":
                return CoreDay.Wednesday;
            case "목요일":
                return CoreDay.Thursday;
            case "금요일":
                return CoreDay.Friday;
            case "토요일":
                return CoreDay.Saturday;
            case "일요일":
                return CoreDay.Sunday;
            default:
                throw new InvalidOperationException("The validated Korean day was not recognized.");
        }
    }

    private static ClassroomAssignment parseClassroomAssignment(
        string rawValue,
        CsvSourcePosition sourcePosition,
        ICollection<CourseImportDiagnostic> diagnostics)
    {
        string normalizedValue = getTrimmedFieldValue(rawValue);
        if (normalizedValue.Length == 0)
        {
            return ClassroomAssignment.Unassigned;
        }

        int separatorIndex = findLastWhitespaceIndex(normalizedValue);
        if (separatorIndex <= 0 || separatorIndex >= normalizedValue.Length - 1)
        {
            diagnostics.Add(createDiagnostic(
                ECourseImportErrorCode.InvalidClassroomLocation,
                sourcePosition,
                ECsvColumn.ClassroomLocation,
                rawValue,
                "Classroom locations require a building name and room identifier."));
            return ClassroomAssignment.Unassigned;
        }

        string buildingNameText = normalizedValue.Substring(0, separatorIndex).Trim();
        string roomIdentifierText = normalizedValue.Substring(separatorIndex + 1).Trim();
        if (buildingNameText.Length == 0 || roomIdentifierText.Length == 0)
        {
            diagnostics.Add(createDiagnostic(
                ECourseImportErrorCode.InvalidClassroomLocation,
                sourcePosition,
                ECsvColumn.ClassroomLocation,
                rawValue,
                "Classroom locations require a building name and room identifier."));
            return ClassroomAssignment.Unassigned;
        }

        BuildingName buildingName = new BuildingName(buildingNameText);
        RoomIdentifier roomIdentifier = new RoomIdentifier(roomIdentifierText);
        CoreClassroomLocation classroomLocation = new CoreClassroomLocation(buildingName, roomIdentifier);
        return ClassroomAssignment.CreateAssigned(classroomLocation);
    }

    private static int findLastWhitespaceIndex(string value)
    {
        for (int index = value.Length - 1; index >= 0; --index)
        {
            if (char.IsWhiteSpace(value[index]))
            {
                return index;
            }
        }

        return -1;
    }

    private static string getTrimmedFieldValue(string rawValue)
    {
        if (rawValue == null)
        {
            return string.Empty;
        }

        return rawValue.Trim();
    }

    private static CourseImportDiagnostic createDiagnostic(
        ECourseImportErrorCode errorCode,
        CsvSourcePosition sourcePosition,
        ECsvColumn column,
        string rawValue,
        string technicalDetails)
    {
        string safeRawValue = rawValue;
        if (safeRawValue == null)
        {
            safeRawValue = string.Empty;
        }

        return new CourseImportDiagnostic(
            errorCode,
            sourcePosition,
            column,
            safeRawValue,
            technicalDetails);
    }
}
