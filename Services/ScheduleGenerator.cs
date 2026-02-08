using System.Collections.Generic;
using System.Data;

namespace TimetableGenerator
{
    public static class ScheduleGenerator
    {
        public static bool TryGenerateValidSchedules(List<Course> courses, out List<List<TimeSlot>> validSchedules, out string errorMessage)
        {
            validSchedules = null;
            errorMessage = null;

            if (courses == null || courses.Count == 0)
            {
                errorMessage = "과목 데이터가 없습니다.";
                return false;
            }

            Dictionary<int, List<Course>> coursesById = new Dictionary<int, List<Course>>();

            foreach (Course course in courses)
            {
                if (!coursesById.ContainsKey(course.CourseId))
                {
                    coursesById.Add(course.CourseId, new List<Course>());
                }

                coursesById[course.CourseId].Add(course);
            }

            List<List<TimeSlot>> combinations = new List<List<TimeSlot>>();

            foreach (KeyValuePair<int, List<Course>> group in coursesById)
            {
                List<List<TimeSlot>> groupOptions = new List<List<TimeSlot>>();

                foreach (Course course in group.Value)
                {
                    List<TimeSlot> slots = new List<TimeSlot>();

                    foreach (string rawTimeSlot in course.TimeSlots)
                    {
                        TimeSlot slot;
                        string parseError;

                        if (!TimeSlotHelper.TryParseTimeSlot(rawTimeSlot, course.CourseId, course.Name, course.Section, course.Classroom, course.SourceLineNumber, out slot, out parseError))
                        {
                            errorMessage = parseError;
                            return false;
                        }

                        slots.Add(slot);
                    }

                    groupOptions.Add(slots);
                }

                if (combinations.Count == 0)
                {
                    foreach (List<TimeSlot> option in groupOptions)
                    {
                        combinations.Add(new List<TimeSlot>(option));
                    }
                }
                else
                {
                    List<List<TimeSlot>> next = new List<List<TimeSlot>>();

                    foreach (List<TimeSlot> existing in combinations)
                    {
                        foreach (List<TimeSlot> option in groupOptions)
                        {
                            List<TimeSlot> merged = new List<TimeSlot>(existing.Count + option.Count);
                            merged.AddRange(existing);
                            merged.AddRange(option);
                            next.Add(merged);
                        }
                    }

                    combinations = next;
                }
            }

            List<List<TimeSlot>> result = new List<List<TimeSlot>>();

            foreach (List<TimeSlot> schedule in combinations)
            {
                if (isValidSchedule(schedule))
                {
                    result.Add(schedule);
                }
            }

            validSchedules = result;
            return true;
        }

        public static bool TryGenerateTable(List<TimeSlot> schedule, out DataTable table, out string errorMessage)
        {
            table = null;
            errorMessage = null;

            if (schedule == null)
            {
                errorMessage = "시간표 데이터가 없습니다.";
                return false;
            }

            DataTable newTable = new DataTable();

            List<EDay> daysToShow = new List<EDay>()
            {
                EDay.Monday, EDay.Tuesday, EDay.Wednesday, EDay.Thursday, EDay.Friday
            };

            bool hasSaturday = false;
            bool hasSunday = false;

            foreach (TimeSlot slot in schedule)
            {
                if (slot.Day == EDay.Saturday)
                {
                    hasSaturday = true;
                }
                else if (slot.Day == EDay.Sunday)
                {
                    hasSunday = true;
                }
            }

            if (hasSunday)
            {
                daysToShow.Add(EDay.Saturday);
                daysToShow.Add(EDay.Sunday);
            }
            else if (hasSaturday)
            {
                daysToShow.Add(EDay.Saturday);
            }

            int maxPeriod = 8;

            foreach (TimeSlot slot in schedule)
            {
                if (slot.Period > maxPeriod)
                {
                    maxPeriod = slot.Period;
                }
            }

            const string PERIOD_COLUMN_NAME = "교시";

            newTable.Columns.Add(PERIOD_COLUMN_NAME);

            List<string> dayLabels = new List<string>(daysToShow.Count);
            for (int i = 0; i < daysToShow.Count; ++i)
            {
                EDay day = daysToShow[i];

                string label;
                if (!day.TryGetLabel(out label))
                {
                    errorMessage = "요일 라벨 변환 실패: day=" + day;
                    return false;
                }

                dayLabels.Add(label);
                newTable.Columns.Add(label, typeof(object));
            }

            DataRow headerRow = newTable.NewRow();
            headerRow[PERIOD_COLUMN_NAME] = PERIOD_COLUMN_NAME;

            for (int i = 0; i < dayLabels.Count; ++i)
            {
                headerRow[dayLabels[i]] = dayLabels[i];
            }

            newTable.Rows.Add(headerRow);

            for (int i = 1; i <= maxPeriod; ++i)
            {
                DataRow row = newTable.NewRow();
                row[PERIOD_COLUMN_NAME] = i + "교시";

                for (int j = 0; j < dayLabels.Count; ++j)
                {
                    row[dayLabels[j]] = null;
                }

                newTable.Rows.Add(row);
            }

            foreach (TimeSlot slot in schedule)
            {
                int rowIndex = slot.Period;

                string columnName;
                if (!slot.Day.TryGetLabel(out columnName))
                {
                    errorMessage = "요일 라벨 변환 실패: day=" + slot.Day;
                    return false;
                }

                if (newTable.Columns.Contains(columnName))
                {
                    newTable.Rows[rowIndex][columnName] = slot.ToCellContent();
                }
            }

            table = newTable;
            return true;
        }

        private static bool isValidSchedule(List<TimeSlot> schedule)
        {
            HashSet<string> occupied = new HashSet<string>();

            foreach (TimeSlot slot in schedule)
            {
                string key = slot.GetCollisionKey();
                if (occupied.Contains(key))
                {
                    return false;
                }

                occupied.Add(key);
            }

            return true;
        }
    }
}
