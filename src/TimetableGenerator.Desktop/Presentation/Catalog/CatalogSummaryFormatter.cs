using System;
using System.Collections.Generic;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Domain.Scheduling;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal static class CatalogSummaryFormatter
{
    private const string INSTRUCTOR_UNCONFIRMED_SUMMARY = "담당교원 미확정";
    private const string INSTRUCTOR_NOT_PROVIDED_SUMMARY = "담당교원 정보 없음";
    private const string LOCATION_NOT_PROVIDED_SUMMARY = "강의실 미정";
    private const string SCHEDULE_NOT_PROVIDED_SUMMARY =
        "시간 미정 (충돌 자동 검증 제외)";

    public static string FormatInstructorSummary(
        InstructorAssignmentMetadata instructorAssignment)
    {
        if (instructorAssignment == null)
        {
            throw new ArgumentNullException(nameof(instructorAssignment));
        }

        switch (instructorAssignment.Status)
        {
            case EInstructorAssignmentStatus.Confirmed:
                return instructorAssignment.GetDisplayText().Value;
            case EInstructorAssignmentStatus.Unconfirmed:
                return INSTRUCTOR_UNCONFIRMED_SUMMARY;
            case EInstructorAssignmentStatus.NotProvided:
                return INSTRUCTOR_NOT_PROVIDED_SUMMARY;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(instructorAssignment),
                    instructorAssignment.Status,
                    "Unknown instructor assignment status.");
        }
    }

    public static string FormatLocationSummary(LocationAssignmentMetadata location)
    {
        if (location == null)
        {
            throw new ArgumentNullException(nameof(location));
        }

        switch (location.Status)
        {
            case ELocationAssignmentStatus.Assigned:
                return location.GetDisplayText().Value;
            case ELocationAssignmentStatus.NotProvided:
                return LOCATION_NOT_PROVIDED_SUMMARY;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(location),
                    location.Status,
                    "Unknown location assignment status.");
        }
    }

    public static string FormatScheduleSummary(MeetingSchedule meetingSchedule)
    {
        if (meetingSchedule == null)
        {
            throw new ArgumentNullException(nameof(meetingSchedule));
        }

        if (meetingSchedule.Status == EMeetingScheduleStatus.NotProvided)
        {
            return SCHEDULE_NOT_PROVIDED_SUMMARY;
        }

        List<MeetingSlot> orderedSlots = new List<MeetingSlot>(meetingSchedule.Slots);
        orderedSlots.Sort(compareMeetingSlots);

        List<string> slotSummaries = new List<string>();
        foreach (MeetingSlot slot in orderedSlots)
        {
            string daySummary = findKoreanDaySummary(slot.Day);
            slotSummaries.Add(daySummary + " " + slot.Period + "교시");
        }

        return string.Join(", ", slotSummaries);
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

    private static string findKoreanDaySummary(EDay day)
    {
        switch (day)
        {
            case EDay.Monday:
                return "월";
            case EDay.Tuesday:
                return "화";
            case EDay.Wednesday:
                return "수";
            case EDay.Thursday:
                return "목";
            case EDay.Friday:
                return "금";
            case EDay.Saturday:
                return "토";
            case EDay.Sunday:
                return "일";
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(day),
                    day,
                    "Unknown academic day.");
        }
    }
}
