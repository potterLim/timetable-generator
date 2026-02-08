using System;

namespace TimetableGenerator
{
    public static class TimeSlotHelper
    {
        // Expected format: "{요일}{교시}교시"
        // Examples: "월요일1교시", "화요일3교시"
        // - Day must be one of: 월요일/화요일/수요일/목요일/금요일/토요일/일요일
        // - Period must be a positive integer
        // Note: This parser intentionally rejects abbreviated day names ("월", "화") to avoid ambiguity.
        public static bool TryParseTimeSlot(string rawTimeSlot, int courseId, string courseName, string section, ClassroomLocation classroom, int sourceLineNumber, out TimeSlot timeSlot, out string errorMessage)
        {
            timeSlot = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(rawTimeSlot))
            {
                errorMessage = buildTimeSlotErrorMessage("시간표 데이터가 비어 있습니다.", rawTimeSlot, courseId, courseName, section, sourceLineNumber);
                return false;
            }

            int index = rawTimeSlot.IndexOf("교시", StringComparison.Ordinal);
            if (index < 0)
            {
                errorMessage = buildTimeSlotErrorMessage("시간표 데이터 형식이 올바르지 않습니다.", rawTimeSlot, courseId, courseName, section, sourceLineNumber);
                return false;
            }

            string left = rawTimeSlot.Substring(0, index).Trim();
            if (left.Length < 2)
            {
                errorMessage = buildTimeSlotErrorMessage("시간표 데이터가 불완전합니다.", rawTimeSlot, courseId, courseName, section, sourceLineNumber);
                return false;
            }

            int digitStartIndex = left.Length;
            while (digitStartIndex > 0)
            {
                char c = left[digitStartIndex - 1];
                if (c < '0' || c > '9')
                {
                    break;
                }

                --digitStartIndex;
            }

            if (digitStartIndex <= 0 || digitStartIndex >= left.Length)
            {
                errorMessage = buildTimeSlotErrorMessage("시간표 데이터 형식이 올바르지 않습니다.", rawTimeSlot, courseId, courseName, section, sourceLineNumber);
                return false;
            }

            string koreanDay = left.Substring(0, digitStartIndex);
            string periodText = left.Substring(digitStartIndex);

            EDay day;
            if (!DayExtensions.TryParseKoreanDay(koreanDay, out day) || day == EDay.None)
            {
                errorMessage = buildTimeSlotErrorMessage("요일 정보가 올바르지 않습니다.", rawTimeSlot, courseId, courseName, section, sourceLineNumber);
                return false;
            }

            int period;
            if (!int.TryParse(periodText, out period))
            {
                errorMessage = buildTimeSlotErrorMessage("교시 정보가 올바르지 않습니다.", rawTimeSlot, courseId, courseName, section, sourceLineNumber);
                return false;
            }

            if (period <= 0)
            {
                errorMessage = buildTimeSlotErrorMessage("교시는 1 이상이어야 합니다.", rawTimeSlot, courseId, courseName, section, sourceLineNumber);
                return false;
            }

            timeSlot = new TimeSlot(day, period, courseId, courseName, section, classroom);
            return true;
        }

        private static string buildTimeSlotErrorMessage(string baseMessage, string rawTimeSlot, int courseId, string courseName, string section, int sourceLineNumber)
        {
            string safeRaw = rawTimeSlot ?? string.Empty;
            string safeName = courseName ?? string.Empty;
            string safeSection = section ?? string.Empty;

            string[] details = new[]
            {
                UiMessageBox.BuildLineNumberDetail(sourceLineNumber),
                "과목: " + safeName,
                "분반: " + safeSection,
                "과목 ID: " + courseId
            };

            return UiMessageBox.BuildDetailMessage(baseMessage, details, "오류 부분", safeRaw);
        }
    }
}
