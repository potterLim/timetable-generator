using System;
using System.ComponentModel;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class DefaultGoogleCalendarWebNavigator
    : IGoogleCalendarWebNavigator
{
    private static readonly Uri GOOGLE_CALENDAR_DESTINATION = new Uri(
        "https://calendar.google.com/calendar/r",
        UriKind.Absolute);

    private readonly IExternalBrowserLauncher mBrowserLauncher;

    public DefaultGoogleCalendarWebNavigator()
        : this(new DefaultExternalBrowserLauncher())
    {
    }

    internal DefaultGoogleCalendarWebNavigator(
        IExternalBrowserLauncher browserLauncher)
    {
        if (browserLauncher == null)
        {
            throw new ArgumentNullException(nameof(browserLauncher));
        }

        mBrowserLauncher = browserLauncher;
    }

    public bool TryOpen()
    {
        try
        {
            mBrowserLauncher.Launch(GOOGLE_CALENDAR_DESTINATION);
            return true;
        }
        catch (Win32Exception)
        {
            return false;
        }
        catch (InvalidOperationException)
        {
            return false;
        }
        catch (PlatformNotSupportedException)
        {
            return false;
        }
    }
}
