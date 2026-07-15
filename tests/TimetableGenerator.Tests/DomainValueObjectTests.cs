using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Core.Domain;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class DomainValueObjectTests
{
    [TestMethod]
    public void CourseChoiceGroupIdRejectsNonPositiveValues()
    {
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CourseChoiceGroupId(0));
        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new CourseChoiceGroupId(-1));
        Assert.IsFalse(default(CourseChoiceGroupId).IsValid);
    }

    [TestMethod]
    public void TextValueObjectsNormalizeAndValidateInput()
    {
        CourseName courseName = new CourseName("  자료구조  ");
        CourseSectionCode defaultSectionCode = new CourseSectionCode("00");
        CourseSectionCode sectionCodeWithLeadingZero = new CourseSectionCode(" 01 ");

        Assert.AreEqual("자료구조", courseName.Value);
        Assert.IsTrue(defaultSectionCode.IsDefault);
        Assert.AreEqual("01", sectionCodeWithLeadingZero.Value);
        Assert.IsFalse(sectionCodeWithLeadingZero.IsDefault);
        Assert.ThrowsExactly<ArgumentException>(() => new CourseName("  "));
        Assert.ThrowsExactly<ArgumentException>(() => new CourseSectionCode(string.Empty));
    }

    [TestMethod]
    public void ScheduleSlotRejectsUndefinedDaysAndDefaultPeriods()
    {
        Period period = new Period(1);

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => new ScheduleSlot(EDay.None, period));
        Assert.ThrowsExactly<ArgumentException>(
            () => new ScheduleSlot(EDay.Monday, default(Period)));
        Assert.IsFalse(default(ScheduleSlot).IsValid);
    }

    [TestMethod]
    public void ClassroomAssignmentRepresentsAssignedAndUnassignedStates()
    {
        ClassroomAssignment unassignedClassroom = ClassroomAssignment.Unassigned;
        BuildingName buildingName = new BuildingName("Engineering Hall");
        RoomIdentifier roomIdentifier = new RoomIdentifier("101");
        ClassroomLocation classroomLocation = new ClassroomLocation(buildingName, roomIdentifier);
        ClassroomAssignment assignedClassroom = ClassroomAssignment.CreateAssigned(classroomLocation);

        Assert.IsFalse(unassignedClassroom.IsAssigned);
        Assert.ThrowsExactly<InvalidOperationException>(
            () => unassignedClassroom.GetClassroomLocation());
        Assert.IsTrue(assignedClassroom.IsAssigned);
        Assert.AreSame(classroomLocation, assignedClassroom.GetClassroomLocation());
        Assert.AreEqual("Engineering Hall 101", classroomLocation.ToDisplayText());
    }

    [TestMethod]
    public void CourseOfferingDefensivelyCopiesScheduleSlots()
    {
        List<ScheduleSlot> mutableScheduleSlots = new List<ScheduleSlot>()
        {
            createScheduleSlot(EDay.Monday, 1),
        };
        CourseOffering courseOffering = createCourseOffering(
            1,
            "자료구조",
            mutableScheduleSlots);

        mutableScheduleSlots.Add(createScheduleSlot(EDay.Tuesday, 2));

        Assert.HasCount(1, courseOffering.ScheduleSlots);
        IList<ScheduleSlot> exposedScheduleSlots = (IList<ScheduleSlot>)courseOffering.ScheduleSlots;
        Assert.ThrowsExactly<NotSupportedException>(
            () => exposedScheduleSlots.Add(createScheduleSlot(EDay.Wednesday, 3)));
    }

    [TestMethod]
    public void CourseOfferingRejectsEmptyDuplicateAndDefaultScheduleSlots()
    {
        List<ScheduleSlot> noScheduleSlots = new List<ScheduleSlot>();
        ScheduleSlot mondayFirstPeriod = createScheduleSlot(EDay.Monday, 1);
        List<ScheduleSlot> duplicateScheduleSlots = new List<ScheduleSlot>()
        {
            mondayFirstPeriod,
            mondayFirstPeriod,
        };
        List<ScheduleSlot> defaultScheduleSlots = new List<ScheduleSlot>()
        {
            default(ScheduleSlot),
        };

        Assert.ThrowsExactly<ArgumentException>(
            () => createCourseOffering(1, "자료구조", noScheduleSlots));
        Assert.ThrowsExactly<ArgumentException>(
            () => createCourseOffering(1, "자료구조", duplicateScheduleSlots));
        Assert.ThrowsExactly<ArgumentException>(
            () => createCourseOffering(1, "자료구조", defaultScheduleSlots));
    }

    [TestMethod]
    public void GeneratedScheduleRejectsDuplicateGroupsAndOverlappingSlots()
    {
        ScheduleSlot mondayFirstPeriod = createScheduleSlot(EDay.Monday, 1);
        CourseOffering firstOffering = createCourseOffering(
            1,
            "자료구조",
            new ScheduleSlot[] { mondayFirstPeriod });
        CourseOffering sameGroupOffering = createCourseOffering(
            1,
            "알고리즘",
            new ScheduleSlot[] { createScheduleSlot(EDay.Tuesday, 2) });
        CourseOffering overlappingOffering = createCourseOffering(
            2,
            "데이터베이스",
            new ScheduleSlot[] { mondayFirstPeriod });

        Assert.ThrowsExactly<ArgumentException>(
            () => new GeneratedSchedule(new CourseOffering[] { firstOffering, sameGroupOffering }));
        Assert.ThrowsExactly<ArgumentException>(
            () => new GeneratedSchedule(new CourseOffering[] { firstOffering, overlappingOffering }));
    }

    [TestMethod]
    public void GeneratedScheduleDefensivelyCopiesCourseOfferings()
    {
        CourseOffering firstOffering = createCourseOffering(
            1,
            "자료구조",
            new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 1) });
        List<CourseOffering> mutableCourseOfferings = new List<CourseOffering>()
        {
            firstOffering,
        };
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(mutableCourseOfferings);

        mutableCourseOfferings.Clear();

        Assert.HasCount(1, generatedSchedule.CourseOfferings);
        IList<CourseOffering> exposedCourseOfferings =
            (IList<CourseOffering>)generatedSchedule.CourseOfferings;
        Assert.ThrowsExactly<NotSupportedException>(
            () => exposedCourseOfferings.Add(firstOffering));
    }

    private static ScheduleSlot createScheduleSlot(EDay day, int periodValue)
    {
        Period period = new Period(periodValue);
        return new ScheduleSlot(day, period);
    }

    private static CourseOffering createCourseOffering(
        int choiceGroupIdValue,
        string courseNameValue,
        IEnumerable<ScheduleSlot> scheduleSlots)
    {
        CourseChoiceGroupId choiceGroupId = new CourseChoiceGroupId(choiceGroupIdValue);
        CourseName courseName = new CourseName(courseNameValue);
        CourseSectionCode sectionCode = new CourseSectionCode("01");
        return new CourseOffering(choiceGroupId, courseName, sectionCode, scheduleSlots);
    }
}
