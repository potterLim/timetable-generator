using System;
using System.Threading;

using TimetableGenerator.Desktop.Views;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class MainWindowShutdownPolicyTests
{
    [Fact]
    public void OperatingSystemShutdownContinuesWhenACancellationCallbackFails()
    {
        using (CancellationTokenSource cancellationSource = new CancellationTokenSource())
        using (cancellationSource.Token.Register(
            delegate
            {
                throw new InvalidOperationException("Expected cancellation callback failure.");
            }))
        {
            Exception? exceptionOrNull = Record.Exception(
                delegate
                {
                    MainWindow.cancelShutdownModeForOperatingSystemShutdown(cancellationSource);
                });

            Assert.Null(exceptionOrNull);
            Assert.True(cancellationSource.IsCancellationRequested);
        }
    }
}
