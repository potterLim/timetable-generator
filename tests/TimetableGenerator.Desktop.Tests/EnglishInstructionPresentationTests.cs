using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

using Avalonia.Automation;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.Threading;
using Avalonia.VisualTree;

using TimetableGenerator.CatalogJson;
using TimetableGenerator.Desktop.Presentation.Catalog;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Desktop.Tests.Presentation.Catalog;
using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class EnglishInstructionPresentationTests
{
    [Fact]
    public void UniformPercentageUsesOneCultureInvariantValue()
    {
        EnglishInstructionPercentageRange range = EnglishInstructionPercentageRange.Create(
            new EnglishInstructionPercentage[]
            {
                new EnglishInstructionPercentage(12.5m),
                new EnglishInstructionPercentage(12.5m),
            });

        Assert.True(range.IsUniform);
        Assert.Equal("영어 12.5%", range.DisplayText);
        Assert.Equal("영어 강의 비율 12.5%", range.AccessibleText);
    }

    [Fact]
    public void MixedPercentagesPreserveZeroAndUseMinimumToMaximumRange()
    {
        EnglishInstructionPercentageRange range = EnglishInstructionPercentageRange.Create(
            new EnglishInstructionPercentage[]
            {
                new EnglishInstructionPercentage(100m),
                new EnglishInstructionPercentage(0m),
                new EnglishInstructionPercentage(50m),
            });

        Assert.False(range.IsUniform);
        Assert.Equal(new EnglishInstructionPercentage(0m), range.Minimum);
        Assert.Equal(new EnglishInstructionPercentage(100m), range.Maximum);
        Assert.Equal("영어 0–100%", range.DisplayText);
        Assert.Equal("영어 강의 비율 0%에서 100%", range.AccessibleText);
    }

    [Fact]
    public void CurrentCultureDoesNotChangePercentagePunctuation()
    {
        CultureInfo originalCulture = CultureInfo.CurrentCulture;
        CultureInfo originalUiCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentCulture = new CultureInfo("de-DE");
            CultureInfo.CurrentUICulture = new CultureInfo("de-DE");
            EnglishInstructionPercentageRange range = new EnglishInstructionPercentageRange(new EnglishInstructionPercentage(12.5m), new EnglishInstructionPercentage(37.5m));

            Assert.Equal("영어 12.5–37.5%", range.DisplayText);
        }
        finally
        {
            CultureInfo.CurrentCulture = originalCulture;
            CultureInfo.CurrentUICulture = originalUiCulture;
        }
    }

    [Fact]
    public void AllUnscheduledOptionsExposeExactPercentagesWithoutAggregate()
    {
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(CatalogProjectionTestFixture.CreateDocument());
        CatalogCourseProjection sourceCourse = catalogProjection.Courses.Single(
            candidate => candidate.Course.Code.Value == "BFT30009");
        CatalogCourseProjection projectedCourse = replaceEnglishPercentages(
            sourceCourse,
            new EnglishInstructionPercentage[]
            {
                new EnglishInstructionPercentage(0m),
                new EnglishInstructionPercentage(100m),
            });
        CourseSearchItem course = new CourseSearchItem(projectedCourse);

        Assert.All(
            projectedCourse.Offerings,
            offering => Assert.False(offering.Offering.MeetingSchedule.IsScheduled));
        Assert.Equal("2개 분반 · 1학점", course.CourseBrowserMetadataDisplayText);
        Assert.DoesNotContain("영어", course.CourseBrowserAccessibleName, StringComparison.Ordinal);
        Assert.DoesNotContain("영어", course.InstructorCreditDisplayText, StringComparison.Ordinal);
        Assert.Collection(
            course.SelectionOptions,
            option =>
            {
                Assert.True(option.IsDirectAdd);
                Assert.Equal(new EnglishInstructionPercentage(0m), option.ExactEnglishInstructionPercentageOrNull);
                Assert.EndsWith("영어 0%", option.DisplayName);
                Assert.Contains("영어 강의 비율 0%", option.AccessibleName, StringComparison.Ordinal);
            },
            option =>
            {
                Assert.True(option.IsDirectAdd);
                Assert.Equal(new EnglishInstructionPercentage(100m), option.ExactEnglishInstructionPercentageOrNull);
                Assert.EndsWith("영어 100%", option.DisplayName);
                Assert.Contains("영어 강의 비율 100%", option.AccessibleName, StringComparison.Ordinal);
            });

        course.SelectedSelectionOption = course.SelectionOptions[1];

        Assert.Equal("영어 강의 비율 100%", course.EnglishInstructionAccessibleText);
        Assert.Equal(course.Name + " 수강 선택 설정 열기", course.AddButtonAccessibleName);
        Assert.Equal("분반별 선호를 설정합니다.", course.AddButtonHelpText);
        Assert.Equal("수강 선택 설정", course.AddButtonToolTipText);
    }

    [Fact]
    public void MixedScheduledAndUnscheduledOptionsExposeTheirExactPercentages()
    {
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(CatalogProjectionTestFixture.CreateDocumentWithScheduledAlternativeCourse());
        CatalogCourseProjection sourceCourse = catalogProjection.Courses.Single(
            candidate => candidate.Course.Code.Value == "BFT30009");
        CatalogCourseProjection projectedCourse = replaceEnglishPercentages(
            sourceCourse,
            new EnglishInstructionPercentage[]
            {
                new EnglishInstructionPercentage(12.5m),
                new EnglishInstructionPercentage(100m),
            });
        CourseSearchItem course = new CourseSearchItem(projectedCourse);

        Assert.Equal(1, course.ScheduledOfferingCount);
        Assert.Equal("2개 분반 · 1학점", course.CourseBrowserMetadataDisplayText);
        Assert.DoesNotContain("영어", course.CourseBrowserAccessibleName, StringComparison.Ordinal);
        Assert.Collection(
            course.SelectionOptions,
            scheduledOption =>
            {
                Assert.True(scheduledOption.IsDirectAdd);
                Assert.False(scheduledOption.IsTimeNotProvided);
                Assert.EndsWith("영어 12.5%", scheduledOption.DisplayName);
                Assert.Contains("영어 강의 비율 12.5%", scheduledOption.AccessibleName, StringComparison.Ordinal);
            },
            unscheduledOption =>
            {
                Assert.True(unscheduledOption.IsDirectAdd);
                Assert.True(unscheduledOption.IsTimeNotProvided);
                Assert.EndsWith("영어 100%", unscheduledOption.DisplayName);
                Assert.Contains("영어 강의 비율 100%", unscheduledOption.AccessibleName, StringComparison.Ordinal);
            });

        Assert.Equal(course.Name + " 수강 선택 설정 열기", course.AddButtonAccessibleName);
        Assert.Equal("분반별 선호를 설정합니다.", course.AddButtonHelpText);
        Assert.Equal("수강 선택 설정", course.AddButtonToolTipText);
        course.SelectedSelectionOption = course.SelectionOptions[1];
        Assert.Equal(course.Name + " 수강 선택 설정 열기", course.AddButtonAccessibleName);
    }

    [Fact]
    public void MultipleScheduledAlternativesKeepAggregatePercentageHidden()
    {
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(CatalogProjectionTestFixture.CreateDocument());
        CatalogCourseProjection sourceCourse = catalogProjection.Courses.Single(
            candidate => candidate.Course.Code.Value == "CSE10001");
        CatalogCourseProjection projectedCourse = replaceEnglishPercentages(
            sourceCourse,
            new EnglishInstructionPercentage[]
            {
                new EnglishInstructionPercentage(12.5m),
                new EnglishInstructionPercentage(100m),
            });
        CourseSearchItem course = new CourseSearchItem(projectedCourse);
        CourseSelectionOption preferenceEditorOption = Assert.Single(course.SelectionOptions);

        Assert.Equal("2개 분반 · 3학점", course.CourseBrowserMetadataDisplayText);
        Assert.False(preferenceEditorOption.IsDirectAdd);
        Assert.Null(preferenceEditorOption.ExactEnglishInstructionPercentageOrNull);
        Assert.DoesNotContain("영어", preferenceEditorOption.DisplayName, StringComparison.Ordinal);
        Assert.Equal(string.Empty, course.EnglishInstructionAccessibleText);
        Assert.DoesNotContain("영어", course.AddButtonAccessibleName, StringComparison.Ordinal);
        Assert.Equal("분반별 선호를 설정합니다.", course.AddButtonHelpText);
    }

    [Fact]
    public void SingleOfferingCourseBrowserRetainsExactPercentage()
    {
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(CatalogProjectionTestFixture.CreateDocument());
        CatalogCourseProjection sourceCourse = catalogProjection.Courses.Single(
            candidate => candidate.Course.Code.Value == "BFT30009");
        CatalogCourseProjection singleOfferingCourse = new CatalogCourseProjection(sourceCourse.Course, sourceCourse.Accent, new CatalogOfferingProjection[] { sourceCourse.Offerings[0] });
        CatalogCourseProjection projectedCourse = replaceEnglishPercentages(
            singleOfferingCourse,
            new EnglishInstructionPercentage[]
            {
                new EnglishInstructionPercentage(12.5m),
            });
        CourseSearchItem course = new CourseSearchItem(projectedCourse);

        Assert.True(course.HasSingleOfferingDetails);
        Assert.Equal(course.InstructorCreditDisplayText + " · 영어 12.5%", course.CourseBrowserMetadataDisplayText);
        Assert.Equal("영어 강의 비율 12.5%", course.EnglishInstructionAccessibleText);
        Assert.Contains("영어 강의 비율 12.5%", course.CourseBrowserAccessibleName, StringComparison.Ordinal);
    }

    [Fact]
    public void CourseChoiceRowsExposeEachOfferingPercentageExactly()
    {
        CourseCatalogProjection catalogProjection = CourseCatalogProjector.Project(CatalogProjectionTestFixture.CreateDocument());
        CatalogCourseProjection sourceCourse = catalogProjection.Courses.Single(
            candidate => candidate.Course.Code.Value == "CSE10001");
        CatalogCourseProjection projectedCourse = replaceEnglishPercentages(
            sourceCourse,
            new EnglishInstructionPercentage[]
            {
                new EnglishInstructionPercentage(12.5m),
                new EnglishInstructionPercentage(100m),
            });
        CourseChoiceDraftCourseItem draft = CourseChoiceDraftCourseItem.CreateNew(projectedCourse);

        Assert.Collection(
            draft.Offerings,
            offering =>
            {
                Assert.Equal("영어 12.5%", offering.EnglishInstructionDisplayText);
                Assert.Contains("영어 강의 비율 12.5%", offering.PreferenceAccessibleName, StringComparison.Ordinal);
            },
            offering =>
            {
                Assert.Equal("영어 100%", offering.EnglishInstructionDisplayText);
                Assert.Contains("영어 강의 비율 100%", offering.PreferenceAccessibleName, StringComparison.Ordinal);
            });
    }

    [AvaloniaFact]
    public void CourseBrowserAndChoiceEditorRenderPercentageTextAndAccessibility()
    {
        using (PlannerWorkspaceViewModel workspace = PlannerWorkspaceTestFactory.CreateWorkspace(CatalogProjectionTestFixture.CreateDocument()))
        {
            workspace.ActivePlan = workspace.Plans[1];
            workspace.SearchText = "프로그래밍";
            CourseBrowserView courseBrowser = new CourseBrowserView();
            courseBrowser.DataContext = workspace;
            Window courseBrowserWindow = createWindow(courseBrowser);

            try
            {
                courseBrowserWindow.Show();
                Dispatcher.UIThread.RunJobs();

                CourseSearchItem course = Assert.Single(workspace.VisibleCourses);
                TextBlock metadata = courseBrowser.GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Single(
                        candidate => candidate.Text
                            == course.CourseBrowserMetadataDisplayText);
                Border courseCard = courseBrowser.GetVisualDescendants()
                    .OfType<Border>()
                    .Single(candidate => candidate.Classes.Contains("course-item"));

                Assert.True(metadata.IsVisible);
                Assert.Equal(course.CourseBrowserAccessibleName, AutomationProperties.GetName(courseCard));
            }
            finally
            {
                courseBrowserWindow.Close();
            }

            CourseSearchItem programming = Assert.Single(workspace.VisibleCourses);
            workspace.AddCourseCommand.Execute(programming);
            CourseChoiceEditorView courseChoiceEditor = new CourseChoiceEditorView();
            courseChoiceEditor.DataContext = workspace;
            Window courseChoiceWindow = createWindow(courseChoiceEditor);

            try
            {
                courseChoiceWindow.Show();
                Dispatcher.UIThread.RunJobs();

                TextBlock[] percentageTexts = courseChoiceEditor
                    .GetVisualDescendants()
                    .OfType<TextBlock>()
                    .Where(candidate => candidate.Text == "영어 0%")
                    .ToArray();
                Border[] accessibleRows = courseChoiceEditor
                    .GetVisualDescendants()
                    .OfType<Border>()
                    .Where(
                        candidate => AutomationProperties.GetName(candidate)
                            is string accessibleName
                            && accessibleName.Contains(
                                "영어 강의 비율 0%",
                                StringComparison.Ordinal))
                    .ToArray();

                Assert.Equal(2, percentageTexts.Length);
                Assert.All(percentageTexts, candidate => Assert.True(candidate.IsVisible));
                Assert.Equal(2, accessibleRows.Length);
            }
            finally
            {
                courseChoiceWindow.Close();
            }
        }
    }

    private static CatalogCourseProjection replaceEnglishPercentages(CatalogCourseProjection sourceCourse, IReadOnlyList<EnglishInstructionPercentage> percentages)
    {
        if (sourceCourse.Offerings.Count != percentages.Count)
        {
            throw new ArgumentException("Each projected offering requires one English instruction percentage.", nameof(percentages));
        }

        List<CatalogOfferingProjection> offerings = new List<CatalogOfferingProjection>();
        for (int index = 0; index < sourceCourse.Offerings.Count; ++index)
        {
            CatalogOfferingProjection sourceOffering = sourceCourse.Offerings[index];
            CatalogOfferingMetadata sourceMetadata = sourceOffering.Metadata;
            CatalogOfferingInstructionMetadata sourceInstruction = sourceMetadata.Instruction;
            CatalogOfferingInstructionMetadata instruction = new CatalogOfferingInstructionMetadata(sourceInstruction.InstructorAssignment, percentages[index], sourceInstruction.Grading);
            CatalogOfferingMetadata metadata = new CatalogOfferingMetadata(
                sourceMetadata.OfferingId,
                sourceMetadata.Classification,
                instruction,
                sourceMetadata.Logistics,
                sourceMetadata.Capacity,
                sourceMetadata.Details,
                sourceMetadata.SourceRecordNumber);
            offerings.Add(new CatalogOfferingProjection(sourceOffering.Offering, metadata));
        }

        return new CatalogCourseProjection(sourceCourse.Course, sourceCourse.Accent, offerings);
    }

    private static Window createWindow(Control content)
    {
        Window window = new Window();
        window.Width = 1_000.0;
        window.Height = 760.0;
        window.Content = content;
        return window;
    }
}
