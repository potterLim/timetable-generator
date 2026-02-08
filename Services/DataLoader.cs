using System.Collections.Generic;
using System.IO;

namespace TimetableGenerator
{
    public class DataLoader
    {
        // CSV input contract:
        // - First row is a header and will be skipped.
        // - Each data row must contain 4 or 5 fields:
        //   (Required) CourseId, Section, Name, TimeSlots
        //   (Optional) Classroom ("건물명 호실" with a single whitespace)
        // - Parsing rule is a simple split by ',' with max 5 parts (not a full RFC CSV parser).
        //   Therefore: Section/Name/TimeSlots/Classroom must NOT contain commas.
        // - TimeSlots field uses '/' to separate multiple time slots.
        public bool TryLoadCoursesFromCsv(string inputFilePath, out List<Course> courses, out string errorMessage)
        {
            courses = null;
            errorMessage = null;

            if (string.IsNullOrWhiteSpace(inputFilePath))
            {
                errorMessage = "입력 파일 경로가 비어 있습니다.";
                return false;
            }

            if (!File.Exists(inputFilePath))
            {
                errorMessage = UiMessageBox.BuildFilePathMessage("입력 파일을 찾을 수 없습니다.", inputFilePath);
                return false;
            }

            List<Course> parsedCourses = new List<Course>();
            bool hasSkippedHeader = false;
            int lineNumber = 0;

            foreach (string rawLine in File.ReadLines(inputFilePath))
            {
                ++lineNumber;

                if (!hasSkippedHeader)
                {
                    hasSkippedHeader = true;
                    continue;
                }

                string line = rawLine;
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                string[] parts = line.Split(new[] { ',' }, 5);
                if (parts.Length < 4)
                {
                    errorMessage = buildCsvLineErrorMessage("CSV 데이터 형식이 올바르지 않습니다.", lineNumber, line);
                    return false;
                }

                int courseIdValue;
                if (!int.TryParse(parts[0].Trim(), out courseIdValue))
                {
                    errorMessage = buildCsvLineErrorMessage("과목 ID가 올바르지 않습니다.", lineNumber, line);
                    return false;
                }

                string sectionText = parts[1].Trim();
                string name = parts[2].Trim();
                string rawTimeSlots = parts[3].Trim();

                if (string.IsNullOrWhiteSpace(sectionText) || string.IsNullOrWhiteSpace(name))
                {
                    errorMessage = buildCsvLineErrorMessage("과목 정보가 올바르지 않습니다.", lineNumber, line);
                    return false;
                }

                if (string.IsNullOrWhiteSpace(rawTimeSlots))
                {
                    errorMessage = buildCsvLineErrorMessage("시간표 정보가 비어 있습니다.", lineNumber, line);
                    return false;
                }

                CourseId courseId = new CourseId(courseIdValue);
                CourseSection section = new CourseSection(sectionText);

                ClassroomLocation classroom = null;
                if (parts.Length >= 5)
                {
                    string rawClassroom = parts[4].Trim();
                    if (!string.IsNullOrWhiteSpace(rawClassroom))
                    {
                        ClassroomLocation parsed;
                        if (!ClassroomLocation.TryParse(rawClassroom, out parsed))
                        {
                            errorMessage = buildCsvLineErrorMessage("강의실 정보 형식이 올바르지 않습니다.", lineNumber, line);
                            return false;
                        }

                        classroom = parsed;
                    }
                }

                string[] timeSlotParts = rawTimeSlots.Split('/');
                List<string> timeSlots = new List<string>();

                for (int j = 0; j < timeSlotParts.Length; ++j)
                {
                    string token = timeSlotParts[j].Trim();
                    if (!string.IsNullOrWhiteSpace(token))
                    {
                        timeSlots.Add(token);
                    }
                }

                if (timeSlots.Count == 0)
                {
                    errorMessage = buildCsvLineErrorMessage("시간표 정보가 비어 있습니다.", lineNumber, line);
                    return false;
                }

                parsedCourses.Add(new Course(courseId, section, name, classroom, timeSlots, lineNumber));
            }

            if (parsedCourses.Count == 0)
            {
                errorMessage = "CSV 파일에 과목 데이터가 없습니다.";
                return false;
            }

            courses = parsedCourses;
            return true;
        }

        private static string buildCsvLineErrorMessage(string baseMessage, int lineNumber, string line)
        {
            string[] details = new[]
            {
                UiMessageBox.BuildLineNumberDetail(lineNumber)
            };

            return UiMessageBox.BuildDetailMessage(baseMessage, details, "오류 부분", line);
        }
    }
}
