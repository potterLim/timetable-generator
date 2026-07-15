using System;

namespace TimetableGenerator.Infrastructure.Exporting;

public sealed class SchedulePngExportArtifact
{
    public ScheduleExportNumber ScheduleNumber { get; }

    public SchedulePngExportArtifactFilePath FilePath { get; }

    public ESchedulePngExportArtifactKind Kind { get; }

    internal SchedulePngExportArtifact(
        ScheduleExportNumber scheduleNumber,
        SchedulePngExportArtifactFilePath filePath,
        ESchedulePngExportArtifactKind kind)
    {
        if (scheduleNumber.IsValid == false)
        {
            throw new ArgumentException(
                "Export artifacts require a valid schedule number.",
                nameof(scheduleNumber));
        }

        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        if (Enum.IsDefined(kind) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        ScheduleNumber = scheduleNumber;
        FilePath = filePath;
        Kind = kind;
    }
}
