using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal interface IAppleCalendarImporter
{
    bool IsAvailable { get; }

    /// <summary>
    /// Opens an existing iCalendar file in Apple Calendar for user-confirmed import.
    /// </summary>
    /// <remarks>
    /// The caller owns the file and must keep it available after this method completes
    /// because LaunchServices may hand it to Calendar asynchronously.
    /// </remarks>
    Task OpenImportAsync(
        IcsCalendarFilePath calendarFilePath,
        CancellationToken cancellationToken);
}
