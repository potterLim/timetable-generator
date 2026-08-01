using System;
using System.Net.Http;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed partial class GoogleCalendarOAuthTests
{
    private static string extractInlineElement(string html, string elementName)
    {
        string openingTag = "<" + elementName + ">";
        string closingTag = "</" + elementName + ">";
        int contentStartIndex = html.IndexOf(openingTag, StringComparison.Ordinal);
        Assert.True(contentStartIndex >= 0, "The callback page does not contain an inline " + elementName + ".");
        contentStartIndex += openingTag.Length;
        int contentEndIndex = html.IndexOf(closingTag, contentStartIndex, StringComparison.Ordinal);
        Assert.True(contentEndIndex >= contentStartIndex, "The callback page contains an incomplete " + elementName + ".");
        return html[contentStartIndex..contentEndIndex];
    }

    private static GoogleCalendarOAuthClient createInteractiveClient(HttpMessageHandler handler)
    {
        return new GoogleCalendarOAuthClient(new HttpClient(handler), new FixedConfigurationProvider(new GoogleCalendarOAuthConfiguration(new GoogleOAuthClientId("client.apps.googleusercontent.com"))), new RecordingCodeProvider());
    }
}
