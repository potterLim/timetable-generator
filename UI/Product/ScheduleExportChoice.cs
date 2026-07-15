using System;
using TimetableGenerator.Infrastructure.Exporting;

namespace TimetableGenerator.UI.Product;

internal sealed class ScheduleExportChoice
{
    internal EScheduleExportScope Scope { get; }

    internal ScheduleExportDirectoryPath DestinationDirectory { get; }

    internal ScheduleExportChoice(
        EScheduleExportScope scope,
        ScheduleExportDirectoryPath destinationDirectory)
    {
        if (Enum.IsDefined(scope) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(scope));
        }

        if (destinationDirectory.IsValid == false)
        {
            throw new ArgumentException("A valid export directory is required.", nameof(destinationDirectory));
        }

        Scope = scope;
        DestinationDirectory = destinationDirectory;
    }
}
