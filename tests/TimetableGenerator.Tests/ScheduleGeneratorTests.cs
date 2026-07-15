using System;
using System.Collections.Generic;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Core.Application.Scheduling;
using TimetableGenerator.Core.Domain;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class ScheduleGeneratorTests
{
    [TestMethod]
    public void GenerateSchedulesPreservesInputGroupAndOptionOrder()
    {
        IReadOnlyList<CourseOffering> courseOfferings = createCartesianProductOfferings();
        ScheduleGenerator scheduleGenerator = new ScheduleGenerator();

        ScheduleGenerationResult result = scheduleGenerator.GenerateSchedules(
            courseOfferings,
            CancellationToken.None);

        Assert.AreEqual(EScheduleGenerationCompletion.Completed, result.Completion);
        CollectionAssert.AreEqual(
            new string[] { "A,C", "A,D", "B,C", "B,D" },
            getScheduleNames(result));
    }

    [TestMethod]
    public void GenerateSchedulesProducesTheSameOrderAcrossRepeatedCalls()
    {
        IReadOnlyList<CourseOffering> courseOfferings = createCartesianProductOfferings();
        ScheduleGenerator scheduleGenerator = new ScheduleGenerator();

        ScheduleGenerationResult firstResult = scheduleGenerator.GenerateSchedules(
            courseOfferings,
            CancellationToken.None);
        ScheduleGenerationResult secondResult = scheduleGenerator.GenerateSchedules(
            courseOfferings,
            CancellationToken.None);

        CollectionAssert.AreEqual(
            getScheduleNames(firstResult),
            getScheduleNames(secondResult));

        IList<GeneratedSchedule> exposedSchedules =
            (IList<GeneratedSchedule>)firstResult.Schedules;
        Assert.ThrowsExactly<NotSupportedException>(
            () => exposedSchedules.Add(firstResult.Schedules[0]));
    }

    [TestMethod]
    public void GenerateSchedulesRollsBackAfterAPartiallyCollidingOption()
    {
        CourseOffering occupiedMonday = createCourseOffering(
            1,
            "Base",
            new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 1) });
        CourseOffering collidingOption = createCourseOffering(
            2,
            "Collision",
            new ScheduleSlot[]
            {
                createScheduleSlot(EDay.Tuesday, 1),
                createScheduleSlot(EDay.Monday, 1),
            });
        CourseOffering validOption = createCourseOffering(
            2,
            "Valid",
            new ScheduleSlot[] { createScheduleSlot(EDay.Tuesday, 1) });
        List<CourseOffering> courseOfferings = new List<CourseOffering>()
        {
            occupiedMonday,
            collidingOption,
            validOption,
        };
        ScheduleGenerator scheduleGenerator = new ScheduleGenerator();

        ScheduleGenerationResult result = scheduleGenerator.GenerateSchedules(
            courseOfferings,
            CancellationToken.None);

        Assert.HasCount(1, result.Schedules);
        Assert.AreEqual("Base,Valid", getScheduleName(result.Schedules[0]));
    }

    [TestMethod]
    public void GenerateSchedulesReportsAReachedLimitOnlyWhenMoreResultsExist()
    {
        IReadOnlyList<CourseOffering> courseOfferings = createCartesianProductOfferings();
        ScheduleGenerator scheduleGenerator = new ScheduleGenerator();
        ScheduleGenerationOptions truncatedOptions = new ScheduleGenerationOptions(
            new ScheduleCountLimit(2));
        ScheduleGenerationOptions exactOptions = new ScheduleGenerationOptions(
            new ScheduleCountLimit(4));

        ScheduleGenerationResult truncatedResult = scheduleGenerator.GenerateSchedules(
            courseOfferings,
            truncatedOptions,
            CancellationToken.None);
        ScheduleGenerationResult exactResult = scheduleGenerator.GenerateSchedules(
            courseOfferings,
            exactOptions,
            CancellationToken.None);

        Assert.HasCount(2, truncatedResult.Schedules);
        Assert.AreEqual(
            EScheduleGenerationCompletion.MaximumScheduleCountReached,
            truncatedResult.Completion);
        CollectionAssert.AreEqual(
            new string[] { "A,C", "A,D" },
            getScheduleNames(truncatedResult));
        Assert.HasCount(4, exactResult.Schedules);
        Assert.AreEqual(EScheduleGenerationCompletion.Completed, exactResult.Completion);
    }

    [TestMethod]
    public void GenerateSchedulesReturnsACanceledTypedResultForPreCanceledTokens()
    {
        IReadOnlyList<CourseOffering> courseOfferings = createCartesianProductOfferings();
        ScheduleGenerator scheduleGenerator = new ScheduleGenerator();
        using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
        {
            cancellationTokenSource.Cancel();

            ScheduleGenerationResult result = scheduleGenerator.GenerateSchedules(
                courseOfferings,
                cancellationTokenSource.Token);

            Assert.AreEqual(EScheduleGenerationCompletion.Canceled, result.Completion);
            Assert.IsTrue(result.IsCanceled);
            Assert.IsEmpty(result.Schedules);
        }
    }

    private static IReadOnlyList<CourseOffering> createCartesianProductOfferings()
    {
        return new List<CourseOffering>()
        {
            createCourseOffering(20, "A", new ScheduleSlot[] { createScheduleSlot(EDay.Monday, 1) }),
            createCourseOffering(20, "B", new ScheduleSlot[] { createScheduleSlot(EDay.Tuesday, 1) }),
            createCourseOffering(10, "C", new ScheduleSlot[] { createScheduleSlot(EDay.Wednesday, 1) }),
            createCourseOffering(10, "D", new ScheduleSlot[] { createScheduleSlot(EDay.Thursday, 1) }),
        }.AsReadOnly();
    }

    private static string[] getScheduleNames(ScheduleGenerationResult result)
    {
        List<string> scheduleNames = new List<string>(result.Schedules.Count);
        foreach (GeneratedSchedule generatedSchedule in result.Schedules)
        {
            scheduleNames.Add(getScheduleName(generatedSchedule));
        }

        return scheduleNames.ToArray();
    }

    private static string getScheduleName(GeneratedSchedule generatedSchedule)
    {
        List<string> courseNames = new List<string>(generatedSchedule.CourseOfferings.Count);
        foreach (CourseOffering courseOffering in generatedSchedule.CourseOfferings)
        {
            courseNames.Add(courseOffering.Name.Value);
        }

        return string.Join(",", courseNames);
    }

    private static ScheduleSlot createScheduleSlot(EDay day, int periodValue)
    {
        return new ScheduleSlot(day, new Period(periodValue));
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
