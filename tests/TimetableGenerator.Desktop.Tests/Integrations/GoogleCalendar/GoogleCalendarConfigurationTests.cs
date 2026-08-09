using System;
using System.IO;
using System.Text;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;
using Xunit;

namespace TimetableGenerator.Desktop.Tests.Integrations.GoogleCalendar;

public sealed class GoogleCalendarConfigurationTests
{
    [Fact]
    public void MissingConfigurationReturnsNullWithoutFailure()
    {
        GoogleCalendarOAuthConfigurationPath path = createTemporaryPath();
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues(null, null);
            },
            path);

        GoogleCalendarOAuthConfiguration? configurationOrNull = provider.GetConfigurationOrNull();

        Assert.Null(configurationOrNull);
    }

    [Fact]
    public void LocalUntrackedConfigurationProvidesDesktopClientId()
    {
        GoogleCalendarOAuthConfigurationPath path = createTemporaryPath();
        string directoryPath = getDirectoryPath(path);
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path.Value, "{\"schemaVersion\":1,\"clientId\":\"desktop-client.apps.googleusercontent.com\"}", Encoding.UTF8);
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues(null, null);
            },
            path);

        try
        {
            GoogleCalendarOAuthConfiguration? configurationOrNull = provider.GetConfigurationOrNull();

            Assert.NotNull(configurationOrNull);
            Assert.Equal("desktop-client.apps.googleusercontent.com", configurationOrNull.ClientId.Value);
            Assert.Null(configurationOrNull.ClientSecretOrNull);
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public void VersionTwoLocalConfigurationProvidesDesktopClientCredentials()
    {
        GoogleCalendarOAuthConfigurationPath path = createTemporaryPath();
        string directoryPath = getDirectoryPath(path);
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path.Value, "{\"schemaVersion\":2,\"clientId\":\"desktop-client.apps.googleusercontent.com\"," + "\"clientSecret\":\"native-client-secret\"}", Encoding.UTF8);
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues(null, null);
            },
            path);

        try
        {
            GoogleCalendarOAuthConfiguration? configurationOrNull = provider.GetConfigurationOrNull();

            Assert.NotNull(configurationOrNull);
            Assert.Equal("desktop-client.apps.googleusercontent.com", configurationOrNull.ClientId.Value);
            Assert.Equal("native-client-secret", configurationOrNull.ClientSecretOrNull?.Value);
            Assert.Equal("[redacted]", configurationOrNull.ClientSecretOrNull?.ToString());
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Theory]
    [InlineData("{\"schemaVersion\":2,\"clientId\":\"client.apps.googleusercontent.com\"}")]
    [InlineData("{\"schemaVersion\":2,\"clientSecret\":\"secret\"}")]
    [InlineData("{\"schemaVersion\":1,\"clientId\":\"client\",\"secret\":\"forbidden\"}")]
    [InlineData("{\"schemaVersion\":1,\"clientId\":\"client.apps.googleusercontent.com\",\"clientSecret\":\"forbidden\"}")]
    [InlineData("{\"schemaVersion\":1}")]
    [InlineData("{\"schemaVersion\":1,\"clientId\":\"not-a-desktop-client\"}")]
    public void ExpandedOrMalformedLocalConfigurationFailsClosed(string json)
    {
        GoogleCalendarOAuthConfigurationPath path = createTemporaryPath();
        string directoryPath = getDirectoryPath(path);
        Directory.CreateDirectory(directoryPath);
        File.WriteAllText(path.Value, json, Encoding.UTF8);
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues(null, null);
            },
            path);

        try
        {
            Assert.Null(provider.GetConfigurationOrNull());
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public void OversizedLocalConfigurationFailsClosed()
    {
        GoogleCalendarOAuthConfigurationPath path = createTemporaryPath();
        string directoryPath = getDirectoryPath(path);
        Directory.CreateDirectory(directoryPath);
        File.WriteAllBytes(path.Value, new byte[16_385]);
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues(null, null);
            },
            path);

        try
        {
            Assert.Null(provider.GetConfigurationOrNull());
        }
        finally
        {
            Directory.Delete(directoryPath, true);
        }
    }

    [Fact]
    public void InvalidEnvironmentClientIdFailsClosed()
    {
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues("invalid\r\nclient-id", null);
            },
            createTemporaryPath());

        Assert.Null(provider.GetConfigurationOrNull());
    }

    [Fact]
    public void EnvironmentCredentialsOverrideLocalConfiguration()
    {
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues("environment-client.apps.googleusercontent.com", "environment-secret");
            },
            createTemporaryPath());

        GoogleCalendarOAuthConfiguration? configurationOrNull = provider.GetConfigurationOrNull();

        Assert.NotNull(configurationOrNull);
        Assert.Equal("environment-client.apps.googleusercontent.com", configurationOrNull.ClientId.Value);
        Assert.Equal("environment-secret", configurationOrNull.ClientSecretOrNull?.Value);
    }

    [Fact]
    public void EnvironmentSecretWithoutClientIdFailsClosed()
    {
        ProductGoogleCalendarOAuthConfigurationProvider provider = new ProductGoogleCalendarOAuthConfigurationProvider(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues(null, "orphaned-secret");
            },
            createTemporaryPath());

        Assert.Null(provider.GetConfigurationOrNull());
    }

    private static GoogleCalendarOAuthConfigurationPath createTemporaryPath()
    {
        string path = Path.Combine(Path.GetTempPath(), "TimetableGenerator.Desktop.Tests", Guid.NewGuid().ToString("N"), "google-calendar.local.json");
        return new GoogleCalendarOAuthConfigurationPath(path);
    }

    private static string getDirectoryPath(GoogleCalendarOAuthConfigurationPath path)
    {
        string? directoryPathOrNull = Path.GetDirectoryName(path.Value);
        if (directoryPathOrNull == null)
        {
            throw new InvalidOperationException("The test configuration path does not contain a directory.");
        }

        return directoryPathOrNull;
    }
}
