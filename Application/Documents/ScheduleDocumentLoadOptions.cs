using System;
using TimetableGenerator.Core.Application.Scheduling;
using TimetableGenerator.Infrastructure.Csv;

namespace TimetableGenerator.Application.Documents;

public sealed class ScheduleDocumentLoadOptions
{
    public CourseCsvImportOptions CourseImportOptions { get; }

    public ScheduleGenerationOptions ScheduleGenerationOptions { get; }

    public ScheduleDocumentLoadOptions(
        CourseCsvImportOptions courseImportOptions,
        ScheduleGenerationOptions scheduleGenerationOptions)
    {
        if (courseImportOptions == null)
        {
            throw new ArgumentNullException(nameof(courseImportOptions));
        }

        if (scheduleGenerationOptions == null)
        {
            throw new ArgumentNullException(nameof(scheduleGenerationOptions));
        }

        CourseImportOptions = courseImportOptions;
        ScheduleGenerationOptions = scheduleGenerationOptions;
    }

    public static ScheduleDocumentLoadOptions CreateDefault()
    {
        CourseCsvImportOptions courseImportOptions = CourseCsvImportOptions.CreateDefault();
        ScheduleGenerationOptions scheduleGenerationOptions =
            ScheduleGenerationOptions.CreateDefault();
        return new ScheduleDocumentLoadOptions(
            courseImportOptions,
            scheduleGenerationOptions);
    }
}
