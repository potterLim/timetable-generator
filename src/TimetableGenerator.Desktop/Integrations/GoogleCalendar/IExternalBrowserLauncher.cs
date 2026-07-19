using System;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal interface IExternalBrowserLauncher
{
    void Launch(Uri uri);
}
