using System;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class ExportedSchedulePng
{
    public ScheduleExportNumber ScheduleNumber { get; }

    public SchedulePngOutputFilePath OutputFilePath { get; }

    internal ExportedSchedulePng(
        ScheduleExportNumber scheduleNumber,
        SchedulePngOutputFilePath outputFilePath)
    {
        if (scheduleNumber.IsValid == false)
        {
            throw new ArgumentException("A valid schedule number is required.", nameof(scheduleNumber));
        }

        if (outputFilePath == null)
        {
            throw new ArgumentNullException(nameof(outputFilePath));
        }

        ScheduleNumber = scheduleNumber;
        OutputFilePath = outputFilePath;
    }
}
