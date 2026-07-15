using System;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class ScheduleGridViewModelFactoryTests
{
    [TestMethod]
    public void CreateBuildsFiveWeekdayColumnsAndEightDefaultPeriodRows()
    {
        CourseOffering courseOffering = createCourseOffering(
            1,
            "자료구조",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 1) });
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(
            new CourseOffering[] { courseOffering });

        ScheduleGridViewModel viewModel = ScheduleGridViewModelFactory.Create(generatedSchedule);

        Assert.HasCount(5, viewModel.DayColumns);
        Assert.HasCount(8, viewModel.PeriodRows);
        CollectionAssert.AreEqual(
            new EDay[]
            {
                EDay.Monday,
                EDay.Tuesday,
                EDay.Wednesday,
                EDay.Thursday,
                EDay.Friday,
            },
            getDisplayedDays(viewModel));
        CollectionAssert.AreEqual(
            new string[] { "월", "화", "수", "목", "금" },
            getDayDisplayNames(viewModel));
        Assert.AreEqual(8, viewModel.MaximumVisiblePeriod.Value);

        ScheduleCellViewModel mondayCell = viewModel.GetCell(EDay.Monday, new Period(1));
        ScheduleCellViewModel tuesdayCell = viewModel.GetCell(EDay.Tuesday, new Period(1));
        Assert.IsTrue(mondayCell.HasCourseOffering);
        Assert.AreSame(courseOffering, mondayCell.GetCourseOffering());
        Assert.IsFalse(tuesdayCell.HasCourseOffering);
        Assert.AreEqual(string.Empty, tuesdayCell.CourseDisplayName);
        Assert.ThrowsExactly<InvalidOperationException>(() => tuesdayCell.GetCourseOffering());
    }

    [TestMethod]
    public void CreateAddsOnlyWeekendDaysThatContainClasses()
    {
        CourseOffering sundayOffering = createCourseOffering(
            1,
            "일요일 세미나",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Sunday, 2) });
        GeneratedSchedule sundaySchedule = new GeneratedSchedule(
            new CourseOffering[] { sundayOffering });

        ScheduleGridViewModel sundayViewModel = ScheduleGridViewModelFactory.Create(sundaySchedule);

        Assert.HasCount(6, sundayViewModel.DayColumns);
        CollectionAssert.AreEqual(
            new EDay[]
            {
                EDay.Monday,
                EDay.Tuesday,
                EDay.Wednesday,
                EDay.Thursday,
                EDay.Friday,
                EDay.Sunday,
            },
            getDisplayedDays(sundayViewModel));

        CourseOffering weekendOffering = createCourseOffering(
            2,
            "주말 세미나",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[]
            {
                createScheduleSlot(EDay.Saturday, 2),
                createScheduleSlot(EDay.Sunday, 3),
            });
        GeneratedSchedule weekendSchedule = new GeneratedSchedule(
            new CourseOffering[] { weekendOffering });

        ScheduleGridViewModel weekendViewModel = ScheduleGridViewModelFactory.Create(weekendSchedule);

        Assert.HasCount(7, weekendViewModel.DayColumns);
        Assert.AreEqual(EDay.Saturday, weekendViewModel.DayColumns[5].Day);
        Assert.AreEqual(EDay.Sunday, weekendViewModel.DayColumns[6].Day);
        Assert.IsTrue(weekendViewModel.Summary.HasWeekendClasses);
    }

    [TestMethod]
    public void CreateExpandsRowsThroughTheHighestScheduledPeriod()
    {
        CourseOffering ninthPeriodOffering = createCourseOffering(
            1,
            "야간 강의",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 9) });
        GeneratedSchedule ninthPeriodSchedule = new GeneratedSchedule(
            new CourseOffering[] { ninthPeriodOffering });

        ScheduleGridViewModel ninthPeriodViewModel = ScheduleGridViewModelFactory.Create(
            ninthPeriodSchedule);

        Assert.HasCount(9, ninthPeriodViewModel.PeriodRows);
        Assert.AreEqual(9, ninthPeriodViewModel.MaximumVisiblePeriod.Value);

        CourseOffering tenthPeriodOffering = createCourseOffering(
            2,
            "심야 강의",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Tuesday, 10) });
        GeneratedSchedule tenthPeriodSchedule = new GeneratedSchedule(
            new CourseOffering[] { tenthPeriodOffering });

        ScheduleGridViewModel tenthPeriodViewModel = ScheduleGridViewModelFactory.Create(
            tenthPeriodSchedule);

        Assert.HasCount(10, tenthPeriodViewModel.PeriodRows);
        Assert.AreEqual(new TimeOnly(22, 0), tenthPeriodViewModel.PeriodRows[9].TimeRange.StartTime);
        Assert.AreEqual(new TimeOnly(23, 15), tenthPeriodViewModel.PeriodRows[9].TimeRange.EndTime);
    }

    [TestMethod]
    public void CreateOmitsTheDefaultSectionAndDisplaysExplicitSections()
    {
        CourseOffering defaultSectionOffering = createCourseOffering(
            1,
            "자료구조",
            "00",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 1) });
        CourseOffering explicitSectionOffering = createCourseOffering(
            2,
            "알고리즘",
            "02",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Tuesday, 1) });
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(
            new CourseOffering[] { defaultSectionOffering, explicitSectionOffering });

        ScheduleGridViewModel viewModel = ScheduleGridViewModelFactory.Create(generatedSchedule);

        Assert.AreEqual(
            "자료구조",
            viewModel.GetCell(EDay.Monday, new Period(1)).CourseDisplayName);
        Assert.AreEqual(
            "알고리즘 (02)",
            viewModel.GetCell(EDay.Tuesday, new Period(1)).CourseDisplayName);
    }

    [TestMethod]
    public void CreatePreservesTypedClassroomAndCourseOfferingInformation()
    {
        BuildingName buildingName = new BuildingName("공학관");
        RoomIdentifier roomIdentifier = new RoomIdentifier("101");
        ClassroomLocation classroomLocation = new ClassroomLocation(buildingName, roomIdentifier);
        ClassroomAssignment classroomAssignment = ClassroomAssignment.CreateAssigned(classroomLocation);
        CourseOffering courseOffering = createCourseOffering(
            1,
            "자료구조",
            "01",
            classroomAssignment,
            new ScheduleSlot[] { createScheduleSlot(EDay.Wednesday, 2) });
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(
            new CourseOffering[] { courseOffering });

        ScheduleGridViewModel viewModel = ScheduleGridViewModelFactory.Create(generatedSchedule);
        ScheduleCellViewModel cell = viewModel.GetCell(EDay.Wednesday, new Period(2));

        Assert.IsTrue(cell.HasClassroom);
        Assert.AreEqual(classroomAssignment, cell.ClassroomAssignment);
        Assert.AreEqual("공학관 101", cell.GetClassroomDisplayText());
        Assert.AreSame(courseOffering, cell.GetCourseOffering());
    }

    [TestMethod]
    public void CreateBuildsASelectedCourseAndActiveDaySummary()
    {
        CourseOffering firstOffering = createCourseOffering(
            1,
            "자료구조",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[]
            {
                createScheduleSlot(EDay.Monday, 1),
                createScheduleSlot(EDay.Wednesday, 2),
            });
        CourseOffering secondOffering = createCourseOffering(
            2,
            "알고리즘",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Saturday, 3) });
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(
            new CourseOffering[] { firstOffering, secondOffering });

        ScheduleGridViewModel viewModel = ScheduleGridViewModelFactory.Create(generatedSchedule);

        Assert.AreEqual(2, viewModel.Summary.SelectedCourseCount);
        Assert.AreEqual(3, viewModel.Summary.ScheduledMeetingCount);
        Assert.AreEqual(3, viewModel.Summary.ActiveDayCount);
        CollectionAssert.AreEqual(
            new EDay[] { EDay.Monday, EDay.Wednesday, EDay.Saturday },
            getActiveDays(viewModel));
    }

    [TestMethod]
    public void CreateRejectsPeriodsOutsideThePresentationTimePolicy()
    {
        CourseOffering outOfRangeOffering = createCourseOffering(
            1,
            "범위 밖 강의",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 11) });
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(
            new CourseOffering[] { outOfRangeOffering });

        Assert.ThrowsExactly<ArgumentOutOfRangeException>(
            () => ScheduleGridViewModelFactory.Create(generatedSchedule));
    }

    [TestMethod]
    public void ScheduleGridCollectionsAreReadOnly()
    {
        CourseOffering courseOffering = createCourseOffering(
            1,
            "자료구조",
            "01",
            ClassroomAssignment.Unassigned,
            new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 1) });
        GeneratedSchedule generatedSchedule = new GeneratedSchedule(
            new CourseOffering[] { courseOffering });
        ScheduleGridViewModel viewModel = ScheduleGridViewModelFactory.Create(generatedSchedule);

        IList<ScheduleDayColumnViewModel> dayColumns =
            (IList<ScheduleDayColumnViewModel>)viewModel.DayColumns;
        IList<SchedulePeriodRowViewModel> periodRows =
            (IList<SchedulePeriodRowViewModel>)viewModel.PeriodRows;
        IList<ScheduleCellViewModel> cells =
            (IList<ScheduleCellViewModel>)viewModel.PeriodRows[0].Cells;

        Assert.ThrowsExactly<NotSupportedException>(
            () => dayColumns.Add(viewModel.DayColumns[0]));
        Assert.ThrowsExactly<NotSupportedException>(
            () => periodRows.Add(viewModel.PeriodRows[0]));
        Assert.ThrowsExactly<NotSupportedException>(
            () => cells.Add(viewModel.PeriodRows[0].Cells[0]));
    }

    private static EDay[] getDisplayedDays(ScheduleGridViewModel viewModel)
    {
        List<EDay> displayedDays = new List<EDay>(viewModel.DayColumns.Count);
        foreach (ScheduleDayColumnViewModel dayColumn in viewModel.DayColumns)
        {
            displayedDays.Add(dayColumn.Day);
        }

        return displayedDays.ToArray();
    }

    private static string[] getDayDisplayNames(ScheduleGridViewModel viewModel)
    {
        List<string> displayNames = new List<string>(viewModel.DayColumns.Count);
        foreach (ScheduleDayColumnViewModel dayColumn in viewModel.DayColumns)
        {
            displayNames.Add(dayColumn.DisplayName);
        }

        return displayNames.ToArray();
    }

    private static EDay[] getActiveDays(ScheduleGridViewModel viewModel)
    {
        List<EDay> activeDays = new List<EDay>(viewModel.Summary.ActiveDays.Count);
        foreach (EDay activeDay in viewModel.Summary.ActiveDays)
        {
            activeDays.Add(activeDay);
        }

        return activeDays.ToArray();
    }

    private static ScheduleSlot createScheduleSlot(EDay day, int periodValue)
    {
        return new ScheduleSlot(day, new Period(periodValue));
    }

    private static CourseOffering createCourseOffering(
        int choiceGroupIdValue,
        string courseNameValue,
        string sectionCodeValue,
        ClassroomAssignment classroomAssignment,
        IEnumerable<ScheduleSlot> scheduleSlots)
    {
        CourseChoiceGroupId choiceGroupId = new CourseChoiceGroupId(choiceGroupIdValue);
        CourseName courseName = new CourseName(courseNameValue);
        CourseSectionCode sectionCode = new CourseSectionCode(sectionCodeValue);
        return new CourseOffering(
            choiceGroupId,
            courseName,
            sectionCode,
            classroomAssignment,
            scheduleSlots);
    }
}
