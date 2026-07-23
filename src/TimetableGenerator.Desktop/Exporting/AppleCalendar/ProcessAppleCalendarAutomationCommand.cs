using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class ProcessAppleCalendarAutomationCommand
    : IAppleCalendarAutomationCommand
{
    private const string OSASCRIPT_PATH = "/usr/bin/osascript";
    private const int BUFFER_SIZE = 8192;

    public bool IsAvailable
    {
        get
        {
            return OperatingSystem.IsMacOS()
                && File.Exists(OSASCRIPT_PATH);
        }
    }

    public async Task<string> ExecuteAsync(
        EAppleCalendarAutomationOperation operation,
        string requestJson,
        CancellationToken cancellationToken)
    {
        validateRequest(operation, requestJson);
        if (IsAvailable == false)
        {
            throw createUnavailableException(null);
        }

        string requestPath = createRequestPath();
        try
        {
            await writePrivateRequestAsync(
                    requestPath,
                    requestJson,
                    cancellationToken)
                .ConfigureAwait(false);
            return await executeProcessAsync(
                    operation,
                    requestPath,
                    cancellationToken)
                .ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (AppleCalendarNativeBridgeException)
        {
            throw;
        }
        catch (IOException exception)
        {
            throw createOperationFailure(exception);
        }
        catch (UnauthorizedAccessException exception)
        {
            throw createOperationFailure(exception);
        }
        finally
        {
            securelyDeleteRequest(requestPath);
        }
    }

    internal static ProcessStartInfo createStartInfo(
        EAppleCalendarAutomationOperation operation,
        string requestPath)
    {
        if (string.IsNullOrWhiteSpace(requestPath))
        {
            throw new ArgumentException(
                "Apple Calendar automation requires a request path.",
                nameof(requestPath));
        }

        ProcessStartInfo startInfo = new ProcessStartInfo();
        startInfo.FileName = OSASCRIPT_PATH;
        startInfo.UseShellExecute = false;
        startInfo.CreateNoWindow = true;
        startInfo.RedirectStandardOutput = true;
        startInfo.RedirectStandardError = true;
        startInfo.StandardOutputEncoding = new UTF8Encoding(false);
        startInfo.StandardErrorEncoding = new UTF8Encoding(false);
        startInfo.ArgumentList.Add("-l");
        startInfo.ArgumentList.Add("JavaScript");
        startInfo.ArgumentList.Add("-e");
        startInfo.ArgumentList.Add(AppleCalendarAutomationScript.SOURCE);
        startInfo.ArgumentList.Add(findOperationArgument(operation));
        startInfo.ArgumentList.Add(requestPath);
        return startInfo;
    }

    internal static FileStreamOptions createPrivateRequestFileOptions()
    {
        FileStreamOptions options = new FileStreamOptions();
        options.Mode = FileMode.CreateNew;
        options.Access = FileAccess.Write;
        options.Share = FileShare.None;
        options.BufferSize = BUFFER_SIZE;
        options.Options = FileOptions.Asynchronous | FileOptions.WriteThrough;
        if (OperatingSystem.IsWindows())
        {
            return options;
        }

        options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        return options;
    }

    private static async Task<string> executeProcessAsync(
        EAppleCalendarAutomationOperation operation,
        string requestPath,
        CancellationToken cancellationToken)
    {
        using (Process process = new Process())
        {
            process.StartInfo = createStartInfo(operation, requestPath);
            try
            {
                if (process.Start() == false)
                {
                    throw createUnavailableException(null);
                }
            }
            catch (Win32Exception exception)
            {
                throw createUnavailableException(exception);
            }
            catch (InvalidOperationException exception)
            {
                throw createUnavailableException(exception);
            }

            Task<string> standardOutputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
            Task<string> standardErrorTask = process.StandardError.ReadToEndAsync(cancellationToken);
            try
            {
                await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                terminateProcess(process);
                await waitForTerminatedProcessAsync(process).ConfigureAwait(false);
                throw;
            }

            string standardOutput = await standardOutputTask.ConfigureAwait(false);
            string standardError = await standardErrorTask.ConfigureAwait(false);
            if (process.ExitCode != 0)
            {
                throw createProcessFailure(standardError);
            }

            string normalizedOutput = standardOutput.Trim();
            if (normalizedOutput.Length == 0)
            {
                throw new AppleCalendarNativeBridgeException(
                    EAppleCalendarNativeFailureKind.OperationFailed,
                    "apple_calendar_automation_empty_response");
            }

            return normalizedOutput;
        }
    }

    private static async Task writePrivateRequestAsync(
        string requestPath,
        string requestJson,
        CancellationToken cancellationToken)
    {
        byte[] requestBytes = new UTF8Encoding(false).GetBytes(requestJson);
        await using (FileStream requestStream = new FileStream(
                requestPath,
                createPrivateRequestFileOptions()))
        {
            await requestStream.WriteAsync(requestBytes, cancellationToken).ConfigureAwait(false);
            await requestStream.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }

    private static void securelyDeleteRequest(string requestPath)
    {
        if (File.Exists(requestPath) == false)
        {
            return;
        }

        try
        {
            using (FileStream requestStream = new FileStream(
                    requestPath,
                    FileMode.Open,
                    FileAccess.Write,
                    FileShare.None))
            {
                byte[] zeroBuffer = new byte[BUFFER_SIZE];
                long remainingLength = requestStream.Length;
                requestStream.Position = 0;
                while (remainingLength > 0)
                {
                    int writeLength = (int)Math.Min(zeroBuffer.Length, remainingLength);
                    requestStream.Write(zeroBuffer, 0, writeLength);
                    remainingLength -= writeLength;
                }

                requestStream.Flush(true);
            }
        }
        catch (IOException)
        {
            // Continue to deletion when the best-effort overwrite is unavailable.
        }
        catch (UnauthorizedAccessException)
        {
            // Continue to deletion when the best-effort overwrite is unavailable.
        }

        try
        {
            File.Delete(requestPath);
        }
        catch (IOException)
        {
            // Temporary-file cleanup must not mask the original export result.
        }
        catch (UnauthorizedAccessException)
        {
            // Temporary-file cleanup must not mask the original export result.
        }
    }

    private static AppleCalendarNativeBridgeException createProcessFailure(
        string standardError)
    {
        if (containsAccessDenial(standardError))
        {
            return new AppleCalendarNativeBridgeException(
                EAppleCalendarNativeFailureKind.AccessDenied,
                "apple_calendar_automation_access_denied");
        }

        return new AppleCalendarNativeBridgeException(
            EAppleCalendarNativeFailureKind.OperationFailed,
            "apple_calendar_automation_process_failed");
    }

    private static AppleCalendarNativeBridgeException createOperationFailure(
        Exception innerException)
    {
        return new AppleCalendarNativeBridgeException(
            EAppleCalendarNativeFailureKind.OperationFailed,
            "apple_calendar_automation_io_failed",
            innerException);
    }

    private static bool containsAccessDenial(string value)
    {
        return value.Contains("-1743", StringComparison.OrdinalIgnoreCase)
            || value.Contains("not authorized", StringComparison.OrdinalIgnoreCase)
            || value.Contains("not permitted", StringComparison.OrdinalIgnoreCase);
    }

    private static void terminateProcess(Process process)
    {
        try
        {
            if (process.HasExited == false)
            {
                process.Kill(true);
            }
        }
        catch (InvalidOperationException)
        {
            // Cancellation cleanup is best effort; the original cancellation remains authoritative.
        }
        catch (NotSupportedException)
        {
            // Cancellation cleanup is best effort; the original cancellation remains authoritative.
        }
    }

    private static async Task waitForTerminatedProcessAsync(Process process)
    {
        try
        {
            await process.WaitForExitAsync(CancellationToken.None)
                .ConfigureAwait(false);
        }
        catch (InvalidOperationException)
        {
            // Cancellation cleanup is best effort; the original cancellation remains authoritative.
        }
    }

    private static string createRequestPath()
    {
        return Path.Combine(
            Path.GetTempPath(),
            "timetable-generator-apple-calendar-"
                + Guid.NewGuid().ToString("N")
                + ".json");
    }

    private static string findOperationArgument(
        EAppleCalendarAutomationOperation operation)
    {
        switch (operation)
        {
            case EAppleCalendarAutomationOperation.ListCalendars:
                return "list";
            case EAppleCalendarAutomationOperation.ApplyExport:
                return "apply";
            case EAppleCalendarAutomationOperation.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(operation));
        }
    }

    private static void validateRequest(
        EAppleCalendarAutomationOperation operation,
        string requestJson)
    {
        findOperationArgument(operation);
        if (string.IsNullOrWhiteSpace(requestJson))
        {
            throw new ArgumentException(
                "Apple Calendar automation requires a JSON request.",
                nameof(requestJson));
        }
    }

    private static AppleCalendarNativeBridgeException createUnavailableException(
        Exception? innerExceptionOrNull)
    {
        return new AppleCalendarNativeBridgeException(
            EAppleCalendarNativeFailureKind.Unavailable,
            "apple_calendar_automation_unavailable",
            innerExceptionOrNull);
    }
}
