using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.RegularExpressions;
using TimetableGenerator.HandongCatalogGenerator.Domain;
using TimetableGenerator.HandongCatalogGenerator.Handong.Source;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Normalization;

internal sealed class HandongScheduleNormalizer
{
    private static readonly Regex KOREAN_SLOT_FORMAT = new Regex("^(?<day>[월화수목금토일])(?<period>[0-9]+)$", RegexOptions.CultureInvariant);

    private static readonly Regex ENGLISH_SLOT_FORMAT = new Regex("^(?<day>Mon|Tue|Wed|Thu|Fri|Sat|Sun)(?<period>[0-9]+)$", RegexOptions.CultureInvariant);

    public HandongScheduleNormalizationResult NormalizeSchedule(HandongRawOfferingRow row)
    {
        IReadOnlyList<string> lines = HandongCellValueReader.getNonEmptyLines(row, EHandongColumn.Period);
        if (lines.Count == 0)
        {
            return new HandongScheduleNormalizationResult(MeetingSchedule.NotProvided, EEnglishScheduleComparison.NotApplicable);
        }

        string koreanSourceText = lines[0];
        List<MeetingSlot> koreanSlots = parseKoreanSlots(koreanSourceText, row);
        sortMeetingSlots(koreanSlots);

        KoreanScheduleText sourceText = new KoreanScheduleText(koreanSourceText);
        MeetingSchedule schedule = MeetingSchedule.CreateScheduled(sourceText, koreanSlots);
        EEnglishScheduleComparison englishScheduleComparison = compareEnglishSchedule(lines, koreanSlots);
        return new HandongScheduleNormalizationResult(schedule, englishScheduleComparison);
    }

    private static List<MeetingSlot> parseKoreanSlots(string sourceText, HandongRawOfferingRow row)
    {
        string[] tokens = sourceText.Split(',', StringSplitOptions.None);
        List<MeetingSlot> slots = new List<MeetingSlot>();
        HashSet<MeetingSlot> uniqueSlots = new HashSet<MeetingSlot>();
        foreach (string sourceToken in tokens)
        {
            string token = sourceToken.Trim();
            Match slotMatch = KOREAN_SLOT_FORMAT.Match(token);
            if (slotMatch.Success == false)
            {
                throw new InvalidHandongSourceRecordException(row.SourceRecordNumber, EHandongColumn.Period, "Invalid Korean meeting token: " + token);
            }

            EDay day = parseKoreanDay(slotMatch.Groups["day"].Value, row);
            AcademicPeriod period = parsePeriod(slotMatch.Groups["period"].Value, row);
            MeetingSlot slot = new MeetingSlot(day, period);
            if (uniqueSlots.Add(slot) == false)
            {
                throw new InvalidHandongSourceRecordException(row.SourceRecordNumber, EHandongColumn.Period, "The Korean schedule contains a duplicate meeting slot: " + token);
            }

            slots.Add(slot);
        }

        if (slots.Count == 0)
        {
            throw new InvalidHandongSourceRecordException(row.SourceRecordNumber, EHandongColumn.Period, "A provided Korean schedule must contain at least one meeting slot.");
        }

        return slots;
    }

    private static EDay parseKoreanDay(string sourceValue, HandongRawOfferingRow row)
    {
        switch (sourceValue)
        {
            case "월":
                return EDay.Monday;
            case "화":
                return EDay.Tuesday;
            case "수":
                return EDay.Wednesday;
            case "목":
                return EDay.Thursday;
            case "금":
                return EDay.Friday;
            case "토":
                return EDay.Saturday;
            case "일":
                return EDay.Sunday;
            default:
                throw new InvalidHandongSourceRecordException(row.SourceRecordNumber, EHandongColumn.Period, "Unsupported Korean day: " + sourceValue);
        }
    }

    private static AcademicPeriod parsePeriod(string sourceValue, HandongRawOfferingRow row)
    {
        int periodValue;
        bool isPeriodParsed = int.TryParse(sourceValue, NumberStyles.None, CultureInfo.InvariantCulture, out periodValue);
        if (isPeriodParsed == false)
        {
            throw new InvalidHandongSourceRecordException(row.SourceRecordNumber, EHandongColumn.Period, "The academic period is not numeric: " + sourceValue);
        }

        try
        {
            return new AcademicPeriod(periodValue);
        }
        catch (ArgumentOutOfRangeException exception)
        {
            throw new InvalidHandongSourceRecordException(row.SourceRecordNumber, EHandongColumn.Period, exception.Message);
        }
    }

    private static EEnglishScheduleComparison compareEnglishSchedule(IReadOnlyList<string> lines, IReadOnlyList<MeetingSlot> koreanSlots)
    {
        if (lines.Count < 2)
        {
            return EEnglishScheduleComparison.DiffersFromKoreanSchedule;
        }

        List<MeetingSlot> englishSlots;
        bool isEnglishScheduleParsed = tryParseEnglishSlots(lines[1], out englishSlots);
        if (isEnglishScheduleParsed == false)
        {
            return EEnglishScheduleComparison.DiffersFromKoreanSchedule;
        }

        sortMeetingSlots(englishSlots);
        if (englishSlots.Count != koreanSlots.Count)
        {
            return EEnglishScheduleComparison.DiffersFromKoreanSchedule;
        }

        for (int slotIndex = 0; slotIndex < koreanSlots.Count; ++slotIndex)
        {
            if (koreanSlots[slotIndex] != englishSlots[slotIndex])
            {
                return EEnglishScheduleComparison.DiffersFromKoreanSchedule;
            }
        }

        return EEnglishScheduleComparison.MatchesKoreanSchedule;
    }

    private static bool tryParseEnglishSlots(string sourceText, out List<MeetingSlot> slots)
    {
        slots = new List<MeetingSlot>();
        HashSet<MeetingSlot> uniqueSlots = new HashSet<MeetingSlot>();
        string[] tokens = sourceText.Split(',', StringSplitOptions.None);
        foreach (string sourceToken in tokens)
        {
            string token = sourceToken.Trim();
            Match slotMatch = ENGLISH_SLOT_FORMAT.Match(token);
            if (slotMatch.Success == false)
            {
                return false;
            }

            EDay day;
            bool isDayParsed = tryParseEnglishDay(slotMatch.Groups["day"].Value, out day);
            int periodValue;
            bool isPeriodParsed = int.TryParse(slotMatch.Groups["period"].Value, NumberStyles.None, CultureInfo.InvariantCulture, out periodValue);
            if (isDayParsed == false || isPeriodParsed == false || periodValue < 1 || periodValue > 10)
            {
                return false;
            }

            MeetingSlot slot = new MeetingSlot(day, new AcademicPeriod(periodValue));
            if (uniqueSlots.Add(slot) == false)
            {
                return false;
            }

            slots.Add(slot);
        }

        return slots.Count > 0;
    }

    private static bool tryParseEnglishDay(string sourceValue, out EDay day)
    {
        switch (sourceValue)
        {
            case "Mon":
                day = EDay.Monday;
                return true;
            case "Tue":
                day = EDay.Tuesday;
                return true;
            case "Wed":
                day = EDay.Wednesday;
                return true;
            case "Thu":
                day = EDay.Thursday;
                return true;
            case "Fri":
                day = EDay.Friday;
                return true;
            case "Sat":
                day = EDay.Saturday;
                return true;
            case "Sun":
                day = EDay.Sunday;
                return true;
            default:
                day = EDay.Monday;
                return false;
        }
    }

    private static void sortMeetingSlots(List<MeetingSlot> slots)
    {
        slots.Sort(compareMeetingSlots);
    }

    private static int compareMeetingSlots(MeetingSlot left, MeetingSlot right)
    {
        int dayComparison = left.Day.CompareTo(right.Day);
        if (dayComparison != 0)
        {
            return dayComparison;
        }

        return left.Period.Value.CompareTo(right.Period.Value);
    }
}
