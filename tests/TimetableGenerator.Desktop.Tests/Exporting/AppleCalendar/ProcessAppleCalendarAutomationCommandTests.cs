using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class ProcessAppleCalendarAutomationCommandTests
{
    [Fact]
    public void PrivateRequestOptionsCreateAWriteOnlyExclusiveFile()
    {
        FileStreamOptions options = ProcessAppleCalendarAutomationCommand.createPrivateRequestFileOptions();

        Assert.Equal(FileMode.CreateNew, options.Mode);
        Assert.Equal(FileAccess.Write, options.Access);
        Assert.Equal(FileShare.None, options.Share);
        Assert.True(options.Options.HasFlag(FileOptions.Asynchronous));
        Assert.True(options.Options.HasFlag(FileOptions.WriteThrough));

        if (OperatingSystem.IsWindows())
        {
            Assert.Null(options.UnixCreateMode);
            return;
        }

        Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, options.UnixCreateMode);
    }

    [Fact]
    public void PrivateRequestFileStartsOwnerOnlyWhileCreationStreamIsOpen()
    {
        if (OperatingSystem.IsWindows())
        {
            return;
        }

        string requestPath = Path.Combine(Path.GetTempPath(), "timetable-generator-apple-calendar-test-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            FileStreamOptions options = ProcessAppleCalendarAutomationCommand.createPrivateRequestFileOptions();
            using (FileStream requestStream = new FileStream(requestPath, options))
            {
                UnixFileMode createdMode = File.GetUnixFileMode(requestPath);

                Assert.Equal(UnixFileMode.UserRead | UnixFileMode.UserWrite, createdMode);
                Assert.True(requestStream.CanWrite);
                Assert.False(requestStream.CanRead);
            }
        }
        finally
        {
            if (File.Exists(requestPath))
            {
                File.Delete(requestPath);
            }
        }
    }

    [Fact]
    public async Task SuccessfulProcessUsesAndRemovesOwnerOnlyRequestFileAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        using (TemporaryShellCommand command = new TemporaryShellCommand(
            """
            #!/bin/sh
            /usr/bin/stat -f '%Lp' "$1"
            """))
        {
            ProcessAppleCalendarAutomationCommand automationCommand = command.CreateAutomationCommand();

            string response = await automationCommand.ExecuteAsync(
                EAppleCalendarAutomationOperation.ListCalendars,
                """{"operation":"list"}""",
                TestContext.Current.CancellationToken);

            Assert.Equal("600", response);
            Assert.False(File.Exists(command.RequestPath));
        }
    }

    [Fact]
    public async Task AccessDeniedProcessRemovesOwnerOnlyRequestFileAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        using (TemporaryShellCommand command = new TemporaryShellCommand(
            """
            #!/bin/sh
            /usr/bin/stat -f '%Lp' "$1" > "$2"
            printf '%s\n' 'Not authorized (-1743)' >&2
            exit 1
            """))
        {
            ProcessAppleCalendarAutomationCommand automationCommand = command.CreateAutomationCommand(command.ModePath);

            AppleCalendarNativeBridgeException exception =
                await Assert.ThrowsAsync<AppleCalendarNativeBridgeException>(
                    async delegate
                    {
                        await automationCommand.ExecuteAsync(
                            EAppleCalendarAutomationOperation.ApplyExport,
                            """{"operation":"apply"}""",
                            TestContext.Current.CancellationToken);
                    });

            Assert.Equal(EAppleCalendarNativeFailureKind.AccessDenied, exception.FailureKind);
            Assert.Equal("apple_calendar_automation_access_denied", exception.DiagnosticCode);
            Assert.Equal("600", (await File.ReadAllTextAsync(command.ModePath, TestContext.Current.CancellationToken)).Trim());
            Assert.False(File.Exists(command.RequestPath));
        }
    }

    [Fact]
    public async Task CancellationKillsChildProcessAndRemovesOwnerOnlyRequestFileAsync()
    {
        if (OperatingSystem.IsMacOS() == false)
        {
            return;
        }

        int childProcessId = 0;
        using (TemporaryShellCommand command = new TemporaryShellCommand(
            """
            #!/bin/sh
            /usr/bin/stat -f '%Lp' "$1" > "$2"
            /bin/sleep 300 &
            child_pid=$!
            printf '%s\n' "$child_pid" > "$3"
            wait "$child_pid"
            """))
        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        {
            ProcessAppleCalendarAutomationCommand automationCommand = command.CreateAutomationCommand(command.ModePath, command.ChildProcessIdPath);
            Task<string> executionTask = automationCommand.ExecuteAsync(
                EAppleCalendarAutomationOperation.ApplyExport,
                """{"operation":"apply"}""",
                cancellationSource.Token);

            try
            {
                await waitForFileAsync(command.ChildProcessIdPath, TestContext.Current.CancellationToken);
                string childProcessIdText = await File.ReadAllTextAsync(command.ChildProcessIdPath, TestContext.Current.CancellationToken);
                childProcessId = int.Parse(childProcessIdText.Trim(), System.Globalization.CultureInfo.InvariantCulture);

                Assert.Equal("600", (await File.ReadAllTextAsync(command.ModePath, TestContext.Current.CancellationToken)).Trim());
                Assert.True(isProcessRunning(childProcessId));

                cancellationSource.Cancel();

                await Assert.ThrowsAnyAsync<OperationCanceledException>(
                    async delegate
                    {
                        await executionTask;
                    });
                await waitForProcessExitAsync(childProcessId, TestContext.Current.CancellationToken);

                Assert.False(isProcessRunning(childProcessId));
                Assert.False(File.Exists(command.RequestPath));
            }
            finally
            {
                cancellationSource.Cancel();
                terminateProcessIfRunning(childProcessId);
                try
                {
                    await executionTask;
                }
                catch (OperationCanceledException)
                {
                }
                catch (AppleCalendarNativeBridgeException)
                {
                }
            }
        }
    }

    private static async Task waitForFileAsync(string path, CancellationToken cancellationToken)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (File.Exists(path) == false)
        {
            if (timeout.Elapsed >= TimeSpan.FromSeconds(5.0))
            {
                throw new TimeoutException("The temporary shell command did not create its marker file.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25.0), cancellationToken);
        }
    }

    private static async Task waitForProcessExitAsync(int processId, CancellationToken cancellationToken)
    {
        Stopwatch timeout = Stopwatch.StartNew();
        while (isProcessRunning(processId))
        {
            if (timeout.Elapsed >= TimeSpan.FromSeconds(5.0))
            {
                throw new TimeoutException("The child process survived Apple automation cancellation.");
            }

            await Task.Delay(TimeSpan.FromMilliseconds(25.0), cancellationToken);
        }
    }

    private static bool isProcessRunning(int processId)
    {
        if (processId <= 0)
        {
            return false;
        }

        try
        {
            using (Process process = Process.GetProcessById(processId))
            {
                return process.HasExited == false;
            }
        }
        catch (ArgumentException)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (Win32Exception)
        {
            return false;
        }
    }

    private static void terminateProcessIfRunning(int processId)
    {
        if (processId <= 0)
        {
            return;
        }

        try
        {
            using (Process process = Process.GetProcessById(processId))
            {
                if (process.HasExited == false)
                {
                    process.Kill(true);
                    process.WaitForExit(5000);
                }
            }
        }
        catch (ArgumentException)
        {
        }
        catch (InvalidOperationException)
        {
        }
        catch (Win32Exception)
        {
        }
    }

    private sealed class TemporaryShellCommand : IDisposable
    {
        private readonly string mDirectoryPath;

        public string RequestPath { get; }

        public string ModePath { get; }

        public string ChildProcessIdPath { get; }

        public string ExecutablePath { get; }

        public TemporaryShellCommand(string script)
        {
            mDirectoryPath = Path.Combine(Path.GetTempPath(), "TimetableGenerator.Desktop.Tests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(mDirectoryPath);
            RequestPath = Path.Combine(mDirectoryPath, "request.json");
            ModePath = Path.Combine(mDirectoryPath, "request.mode");
            ChildProcessIdPath = Path.Combine(mDirectoryPath, "child.pid");
            ExecutablePath = Path.Combine(mDirectoryPath, "automation-test.sh");
            File.WriteAllText(ExecutablePath, script, new UTF8Encoding(false));
            if (OperatingSystem.IsWindows())
            {
                throw new PlatformNotSupportedException("Temporary shell commands require Unix file modes.");
            }

            File.SetUnixFileMode(ExecutablePath, UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute);
        }

        public ProcessAppleCalendarAutomationCommand CreateAutomationCommand(params string[] additionalArguments)
        {
            return new ProcessAppleCalendarAutomationCommand(
                delegate (
                    EAppleCalendarAutomationOperation operation,
                    string requestPath)
                {
                    ProcessStartInfo startInfo = new ProcessStartInfo();
                    startInfo.FileName = ExecutablePath;
                    startInfo.UseShellExecute = false;
                    startInfo.CreateNoWindow = true;
                    startInfo.RedirectStandardOutput = true;
                    startInfo.RedirectStandardError = true;
                    startInfo.StandardOutputEncoding = new UTF8Encoding(false);
                    startInfo.StandardErrorEncoding = new UTF8Encoding(false);
                    startInfo.ArgumentList.Add(requestPath);
                    foreach (string argument in additionalArguments)
                    {
                        startInfo.ArgumentList.Add(argument);
                    }

                    return startInfo;
                },
                delegate
                {
                    return RequestPath;
                },
                delegate
                {
                    return true;
                });
        }

        public void Dispose()
        {
            if (Directory.Exists(mDirectoryPath))
            {
                Directory.Delete(mDirectoryPath, true);
            }
        }
    }
}
