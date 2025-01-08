using System.Collections.Generic;

namespace TimetableGenerator
{
    /// <summary>
    /// 과목 데이터를 표현하는 클래스
    /// </summary>
    public class Course
    {
        public int CourseId { get; private set; }
        public string Section { get; private set; }
        public string Name { get; private set; }
        public List<string> TimeSlots { get; private set; }

        public Course(int courseId, string section, string name, List<string> timeSlots)
        {
            CourseId = courseId;
            Section = section;
            Name = name;
            TimeSlots = timeSlots;
        }
    }

    /// <summary>
    /// 시간표의 특정 시간대 정보를 표현하는 클래스
    /// </summary>
    public class TimeSlot
    {
        public string Day { get; private set; }
        public string Period { get; private set; }
        public string CourseName { get; private set; }

        public TimeSlot(string day, string period, string courseName)
        {
            Day = day;
            Period = period;
            CourseName = courseName;
        }
    }
}