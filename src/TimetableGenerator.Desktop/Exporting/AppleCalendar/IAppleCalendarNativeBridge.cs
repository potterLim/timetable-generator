using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

/// <summary>
/// Provides the privileged macOS boundary used to inspect and mutate Apple Calendar.
/// </summary>
/// <remarks>
/// Implementations must revalidate the destination immediately before mutation.
/// If the name or ownership precondition no longer matches, they must report
/// <see cref="EAppleCalendarNativeFailureKind.CalendarChanged"/> without modifying data.
/// Replacement may delete only content belonging to a calendar that the implementation
/// has positively identified as managed by this application.
/// </remarks>
internal interface IAppleCalendarNativeBridge
{
    bool IsAvailable { get; }

    Task<IReadOnlyList<AppleCalendarDescriptor>> GetCalendarsAsync(CancellationToken cancellationToken);

    Task<AppleCalendarNativeExportResult> ApplyExportAsync(
        AppleCalendarExportMutation mutation,
        CancellationToken cancellationToken);
}
