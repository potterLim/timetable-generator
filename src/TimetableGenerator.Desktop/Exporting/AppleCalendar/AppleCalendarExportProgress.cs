using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed record AppleCalendarExportProgress
{
    public EAppleCalendarExportProgressStage Stage { get; }

    public AppleCalendarExportProgress(EAppleCalendarExportProgressStage stage)
    {
        if (stage is EAppleCalendarExportProgressStage.None || Enum.IsDefined(stage) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(stage));
        }

        Stage = stage;
    }
}
