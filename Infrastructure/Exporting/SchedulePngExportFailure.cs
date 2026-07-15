using System;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportFailure
{
    public ScheduleExportNumber ScheduleNumber { get; }

    public SchedulePngRequestedFileName RequestedFileName { get; }

    public string Message { get; }

    internal SchedulePngExportFailure(
        ScheduleExportNumber scheduleNumber,
        SchedulePngRequestedFileName requestedFileName,
        string message)
    {
        if (scheduleNumber.IsValid == false)
        {
            throw new ArgumentException("A valid schedule number is required.", nameof(scheduleNumber));
        }

        if (requestedFileName == null)
        {
            throw new ArgumentNullException(nameof(requestedFileName));
        }

        if (string.IsNullOrWhiteSpace(message))
        {
            throw new ArgumentException("Export failure messages cannot be empty.", nameof(message));
        }

        ScheduleNumber = scheduleNumber;
        RequestedFileName = requestedFileName;
        Message = message;
    }
}
