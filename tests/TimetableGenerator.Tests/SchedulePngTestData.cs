using System.Collections.Generic;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGeneratorCore.Tests;

internal static class SchedulePngTestData
{
    internal static ScheduleGridViewModel createScheduleGrid(
        string courseNameValue,
        EDay day,
        int periodValue)
    {
        BuildingName buildingName = new BuildingName("공학관");
        RoomIdentifier roomIdentifier = new RoomIdentifier("101");
        ClassroomLocation classroomLocation = new ClassroomLocation(
            buildingName,
            roomIdentifier);
        ClassroomAssignment classroomAssignment = ClassroomAssignment.CreateAssigned(
            classroomLocation);
        ScheduleSlot scheduleSlot = new ScheduleSlot(day, new Period(periodValue));
        CourseOffering courseOffering = new CourseOffering(
            new CourseChoiceGroupId(1),
            new CourseName(courseNameValue),
            new CourseSectionCode("01"),
            classroomAssignment,
            new ScheduleSlot[] { scheduleSlot });
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(
            new CourseOffering[] { courseOffering });
        return ScheduleGridViewModelFactory.Create(generatedSchedule);
    }

    internal static IReadOnlyList<ScheduleGridViewModel> createScheduleGrids()
    {
        List<ScheduleGridViewModel> scheduleGrids = new List<ScheduleGridViewModel>()
        {
            createScheduleGrid("자료구조", EDay.Monday, 1),
            createScheduleGrid("알고리즘", EDay.Thursday, 3),
        };
        return scheduleGrids.AsReadOnly();
    }
}
