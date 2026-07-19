using System;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal static class AppleCalendarRuntimePlatformPolicy
{
    public static EAppleCalendarRuntimePlatform FindCurrentPlatform()
    {
        if (OperatingSystem.IsMacOS())
        {
            return EAppleCalendarRuntimePlatform.MacOS;
        }

        return EAppleCalendarRuntimePlatform.Unsupported;
    }
}
