using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Windows.Forms;

namespace TimetableGenerator
{
    /// <summary>
    /// 시간표 생성 및 검증을 담당하는 클래스
    /// </summary>
    public static class ScheduleGenerator
    {
        /// <summary>
        /// 유효한 시간표 조합을 생성합니다.
        /// </summary>
        /// <param name="courses">과목 목록</param>
        /// <returns>유효한 시간표 조합</returns>
        public static List<List<TimeSlot>> GenerateValidSchedules(List<Course> courses)
        {
            try
            {
                var groupedCourses = courses.GroupBy(c => c.CourseId);
                var combinations = new List<List<TimeSlot>>();

                foreach (var group in groupedCourses)
                {
                    var timeSlotCombinations = group.Select(course =>
                        course.TimeSlots.Select(ts => TimeSlotHelper.ParseTimeSlot(ts, course.Name + $" ({course.CourseId}-{course.Section})")).ToList()).ToList();

                    if (combinations.Count == 0)
                    {
                        combinations.AddRange(timeSlotCombinations.Select(ts => new List<TimeSlot>(ts)));
                    }
                    else
                    {
                        combinations = combinations.SelectMany(existing =>
                            timeSlotCombinations.Select(newSlot =>
                                existing.Concat(newSlot).ToList())).ToList();
                    }
                }

                return combinations.Where(TimeSlotHelper.IsValidSchedule).ToList();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "시간표를 생성하는 중 문제가 발생했습니다.\n" +
                    $"오류 내용: {ex.Message}",
                    "시간표 생성 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
                return null;
            }
        }

        /// <summary>
        /// 시간표 데이터를 테이블 형식으로 변환합니다.
        /// </summary>
        /// <param name="schedule">시간표 데이터</param>
        /// <returns>테이블 형식의 시간표</returns>
        public static DataTable Generate(List<TimeSlot> schedule)
        {
            try
            {
                var dataTable = new DataTable();

                var allDays = new List<string> { "월", "화", "수", "목", "금", "토", "일" };
                var maxPeriod = Math.Max(8, schedule.Any() ? schedule.Max(s => int.Parse(s.Period)) : 8);

                var daysToShow = new List<string> { "월", "화", "수", "목", "금" };

                bool hasSaturday = schedule.Any(s => s.Day == "토");
                bool hasSunday = schedule.Any(s => s.Day == "일");

                if (hasSunday)
                {
                    daysToShow.Add("토");
                    daysToShow.Add("일");
                }
                else if (hasSaturday)
                {
                    daysToShow.Add("토");
                }

                dataTable.Columns.Add("시간");
                foreach (var day in allDays)
                {
                    if (daysToShow.Contains(day))
                    {
                        dataTable.Columns.Add(day);
                    }
                }

                var headerRow = dataTable.NewRow();
                headerRow["시간"] = "시간";
                foreach (var day in daysToShow)
                {
                    headerRow[day] = day;
                }
                dataTable.Rows.Add(headerRow);

                for (int i = 1; i <= maxPeriod; i++)
                {
                    var row = dataTable.NewRow();
                    row["시간"] = $"{i}교시";
                    foreach (var day in daysToShow)
                    {
                        row[day] = "";
                    }
                    dataTable.Rows.Add(row);
                }

                foreach (var slot in schedule)
                {
                    int periodIndex = int.Parse(slot.Period);
                    string dayColumn = slot.Day;

                    if (!dataTable.Columns.Contains(dayColumn))
                    {
                        throw new Exception($"'{dayColumn}' 열은 테이블에 없습니다.");
                    }

                    string[] parts = slot.CourseName.Split(new[] { "(", "-", ")" }, StringSplitOptions.RemoveEmptyEntries);
                    if (parts.Length >= 3)
                    {
                        string courseName = parts[0].Trim();
                        string section = parts[2].Trim();
                        dataTable.Rows[periodIndex][dayColumn] = $"{courseName}({section})";
                    }
                    else
                    {
                        dataTable.Rows[periodIndex][dayColumn] = slot.CourseName;
                    }
                }

                return dataTable;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "시간표 데이터를 생성하는 중 문제가 발생했습니다.\n" +
                    $"오류 내용: {ex.Message}",
                    "시간표 데이터 생성 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
                return null;
            }
        }
    }

    /// <summary>
    /// 시간표 데이터 처리를 위한 헬퍼 클래스
    /// </summary>
    public static class TimeSlotHelper
    {
        /// <summary>
        /// 시간표 데이터를 TimeSlot 객체로 변환합니다.
        /// </summary>
        public static TimeSlot ParseTimeSlot(string timeSlot, string courseName)
        {
            try
            {
                if (!timeSlot.Contains("교시"))
                {
                    throw new FormatException($"시간표 데이터 형식이 잘못되었습니다: {timeSlot}");
                }

                var parts = timeSlot.Split(new[] { "교시" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length < 1 || parts[0].Length < 2)
                {
                    throw new FormatException($"시간표 데이터가 불완전합니다: {timeSlot}");
                }

                var fullDay = parts[0].Substring(0, parts[0].Length - 1);
                var period = parts[0][parts[0].Length - 1].ToString();

                string day;
                switch (fullDay)
                {
                    case "월요일": day = "월"; break;
                    case "화요일": day = "화"; break;
                    case "수요일": day = "수"; break;
                    case "목요일": day = "목"; break;
                    case "금요일": day = "금"; break;
                    case "토요일": day = "토"; break;
                    case "일요일": day = "일"; break;
                    default:
                        throw new FormatException($"잘못된 요일 형식: {fullDay}");
                }

                if (!int.TryParse(period, out _))
                {
                    throw new FormatException($"잘못된 교시 형식: {period}");
                }

                return new TimeSlot(day, period, courseName);
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "시간표 데이터를 읽는 중 문제가 발생했습니다.\n" +
                    $"오류 내용: {ex.Message}",
                    "시간표 데이터 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
                return null;
            }
        }

        /// <summary>
        /// 시간표의 유효성을 검사합니다.
        /// </summary>
        public static bool IsValidSchedule(List<TimeSlot> schedule)
        {
            try
            {
                var times = new HashSet<string>();
                foreach (var slot in schedule)
                {
                    string key = slot.Day + slot.Period;
                    if (times.Contains(key))
                        return false;
                    times.Add(key);
                }
                return true;
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "시간표를 검증하는 중 문제가 발생했습니다.\n" +
                    $"오류 내용: {ex.Message}",
                    "시간표 검증 오류",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Error);
                Environment.Exit(1);
                return false;
            }
        }
    }
}
