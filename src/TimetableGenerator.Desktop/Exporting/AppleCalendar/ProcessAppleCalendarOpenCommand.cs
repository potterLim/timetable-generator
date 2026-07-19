using System;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class ProcessAppleCalendarOpenCommand : IAppleCalendarOpenCommand
{
    private const string APPLE_CALENDAR_BUNDLE_IDENTIFIER = "com.apple.iCal";
    private const string MAC_OS_OPEN_COMMAND_PATH = "/usr/bin/open";

    public async Task RunAsync(
        IcsCalendarFilePath calendarFilePath,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(calendarFilePath);

        cancellationToken.ThrowIfCancellationRequested();

        ProcessStartInfo startInfo = CreateStartInfo(calendarFilePath);
        Process? processOrNull;

        try
        {
            processOrNull = Process.Start(startInfo);
        }
        catch (Exception exception) when (
            exception is InvalidOperationException ||
            exception is System.ComponentModel.Win32Exception)
        {
            throw new AppleCalendarImportException(
                "Apple Calendar could not be opened.",
                exception);
        }

        if (processOrNull == null)
        {
            throw new AppleCalendarImportException(
                "The macOS calendar import command did not start.");
        }

        using (Process process = processOrNull)
        {
            string standardError = await process.StandardError.ReadToEndAsync(
                cancellationToken);
            await process.WaitForExitAsync(cancellationToken);

            if (process.ExitCode != 0)
            {
                string failureMessage = createFailureMessage(standardError);
                throw new AppleCalendarImportException(failureMessage);
            }
        }
    }

    internal static ProcessStartInfo CreateStartInfo(
        IcsCalendarFilePath calendarFilePath)
    {
        ArgumentNullException.ThrowIfNull(calendarFilePath);

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = MAC_OS_OPEN_COMMAND_PATH;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardError = true;
        startInfo.ArgumentList.Add("-b");
        startInfo.ArgumentList.Add(APPLE_CALENDAR_BUNDLE_IDENTIFIER);
        startInfo.ArgumentList.Add(calendarFilePath.Value);
        return startInfo;
    }

    private static string createFailureMessage(string standardError)
    {
        string trimmedError = standardError.Trim();
        if (trimmedError.Length == 0)
        {
            return "Apple Calendar rejected the calendar import request.";
        }

        return "Apple Calendar rejected the calendar import request: " +
            trimmedError;
    }
}
