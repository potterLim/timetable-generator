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

            // Grouping policy:
            // - Same CourseId means mutually exclusive sections (choose exactly one).
            // - Group order: first appearance order in the input list.
            // - Option order within a group: appearance order in the input list.
            Dictionary<CourseId, List<Course>> coursesById = new Dictionary<CourseId, List<Course>>();
            List<CourseId> groupOrder = new List<CourseId>();

            foreach (Course course in courses)
            {
                List<Course> group;
                if (!coursesById.TryGetValue(course.CourseId, out group))
                {
                    group = new List<Course>();
                    coursesById.Add(course.CourseId, group);
                    groupOrder.Add(course.CourseId);
                }

                group.Add(course);
            }

            // Parse policy:
            // - Each option corresponds to one CSV row (one section).
            // - Any parsing error stops generation and returns an error message.
            List<List<List<TimeSlot>>> optionsByGroup = new List<List<List<TimeSlot>>>(groupOrder.Count);

            foreach (CourseId courseId in groupOrder)
            {
                List<Course> groupCourses = coursesById[courseId];
                List<List<TimeSlot>> groupOptions = new List<List<TimeSlot>>(groupCourses.Count);

                foreach (Course course in groupCourses)
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

                optionsByGroup.Add(groupOptions);
            }

            // Generation policy:
            // - Enumeration order must match the Cartesian product defined by groupOrder and option order.
            // - A schedule is valid if no two TimeSlots share the same (Day, Period).
            List<List<TimeSlot>> result = new List<List<TimeSlot>>();
            List<TimeSlot> current = new List<TimeSlot>();
            HashSet<ScheduleSlotKey> occupied = new HashSet<ScheduleSlotKey>();

            buildSchedulesDfs(optionsByGroup, 0, current, occupied, result);

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
                if (slot.Period.Value > maxPeriod)
                {
                    maxPeriod = slot.Period.Value;
                }
            }

            const string PERIOD_COLUMN_NAME = "교시";

            newTable.Columns.Add(PERIOD_COLUMN_NAME);

            List<string> dayLabels = new List<string>(daysToShow.Count);
            foreach (EDay day in daysToShow)
            {
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
            headerRow[PERIOD_COLUMN_NAME] = string.Empty; // top-left header cell is intentionally blank

            foreach (string label in dayLabels)
            {
                headerRow[label] = label;
            }

            newTable.Rows.Add(headerRow);

            for (int i = 1; i <= maxPeriod; ++i)
            {
                DataRow row = newTable.NewRow();
                row[PERIOD_COLUMN_NAME] = i + "교시";

                foreach (string label in dayLabels)
                {
                    row[label] = null;
                }

                newTable.Rows.Add(row);
            }

            foreach (TimeSlot slot in schedule)
            {
                int rowIndex = slot.Period.Value;

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

        private static void buildSchedulesDfs(List<List<List<TimeSlot>>> optionsByGroup, int groupIndex, List<TimeSlot> current, HashSet<ScheduleSlotKey> occupied, List<List<TimeSlot>> result)
        {
            if (groupIndex >= optionsByGroup.Count)
            {
                result.Add(new List<TimeSlot>(current));
                return;
            }

            List<List<TimeSlot>> groupOptions = optionsByGroup[groupIndex];

            foreach (List<TimeSlot> option in groupOptions)
            {
                int addedCount = 0;
                bool canApply = true;

                foreach (TimeSlot slot in option)
                {
                    ScheduleSlotKey key = slot.GetCollisionKey();

                    if (occupied.Contains(key))
                    {
                        canApply = false;
                        break;
                    }

                    occupied.Add(key);
                    current.Add(slot);
                    ++addedCount;
                }

                if (canApply)
                {
                    buildSchedulesDfs(optionsByGroup, groupIndex + 1, current, occupied, result);
                }

                for (int i = 0; i < addedCount; ++i)
                {
                    int lastIndex = current.Count - 1;
                    TimeSlot lastSlot = current[lastIndex];

                    current.RemoveAt(lastIndex);
                    occupied.Remove(lastSlot.GetCollisionKey());
                }
            }
        }
    }
}