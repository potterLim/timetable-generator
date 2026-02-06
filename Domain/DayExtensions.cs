namespace TimetableGenerator
{
    public static class DayExtensions
    {
        public static bool TryGetLabel(this EDay day, out string label)
        {
            label = null;

            switch (day)
            {
                case EDay.Monday:
                    label = "월";
                    return true;
                case EDay.Tuesday:
                    label = "화";
                    return true;
                case EDay.Wednesday:
                    label = "수";
                    return true;
                case EDay.Thursday:
                    label = "목";
                    return true;
                case EDay.Friday:
                    label = "금";
                    return true;
                case EDay.Saturday:
                    label = "토";
                    return true;
                case EDay.Sunday:
                    label = "일";
                    return true;
                case EDay.None:
                /* intentional fallthrough */
                default:
                    return false;
            }
        }

        // Accepts full Korean day names only (e.g. "월요일").
        // Keep this in sync with the CSV time slot format.
        public static bool TryParseKoreanDay(string koreanDay, out EDay day)
        {
            day = EDay.None;

            if (string.IsNullOrWhiteSpace(koreanDay))
            {
                return false;
            }

            string input = koreanDay.Trim();

            switch (input)
            {
                case "월요일":
                    day = EDay.Monday;
                    return true;
                case "화요일":
                    day = EDay.Tuesday;
                    return true;
                case "수요일":
                    day = EDay.Wednesday;
                    return true;
                case "목요일":
                    day = EDay.Thursday;
                    return true;
                case "금요일":
                    day = EDay.Friday;
                    return true;
                case "토요일":
                    day = EDay.Saturday;
                    return true;
                case "일요일":
                    day = EDay.Sunday;
                    return true;
                default:
                    return false;
            }
        }
    }
}
