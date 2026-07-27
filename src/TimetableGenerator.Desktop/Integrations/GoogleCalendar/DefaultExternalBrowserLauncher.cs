using System;
using System.Diagnostics;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class DefaultExternalBrowserLauncher : IExternalBrowserLauncher
{
    private readonly Func<ProcessStartInfo, Process?> mProcessStarter;

    public DefaultExternalBrowserLauncher()
        : this(Process.Start)
    {
    }

    internal DefaultExternalBrowserLauncher(Func<ProcessStartInfo, Process?> processStarter)
    {
        if (processStarter == null)
        {
            throw new ArgumentNullException(nameof(processStarter));
        }

        mProcessStarter = processStarter;
    }

    public void Launch(Uri uri)
    {
        if (uri == null)
        {
            throw new ArgumentNullException(nameof(uri));
        }

        ProcessStartInfo startInfo = new ProcessStartInfo(uri.AbsoluteUri)
        {
            UseShellExecute = true,
        };
        Process? processOrNull = mProcessStarter(startInfo);
        processOrNull?.Dispose();
    }
}
