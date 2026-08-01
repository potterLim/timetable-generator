using System;
using System.Text.Json;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.AppleCalendar;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting.AppleCalendar;

public sealed class NativeEventKitCalendarCommandTests
{
    [Fact]
    public async Task NativeCAbiLoadsAndReturnsOwnedJsonForAnInvalidRequestAsync()
    {
        if (OperatingSystem.IsMacOSVersionAtLeast(14) == false)
        {
            return;
        }

        NativeEventKitCalendarCommand command = new NativeEventKitCalendarCommand();

        Assert.True(command.IsAvailable);
        string responseJson = await command.ExecuteAsync("{", TestContext.Current.CancellationToken);

        using (JsonDocument response = JsonDocument.Parse(responseJson))
        {
            Assert.Equal(1, response.RootElement.GetProperty("schemaVersion").GetInt32());
            Assert.Equal("invalid_request", response.RootElement.GetProperty("status").GetString());
            Assert.Equal("eventkit_request_json_invalid", response.RootElement.GetProperty("diagnosticCode").GetString());
        }
    }
}
