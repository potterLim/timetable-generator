using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class ProductGoogleCalendarOAuthConfigurationProvider
    : IGoogleCalendarOAuthConfigurationProvider
{
    private const int SCHEMA_VERSION = 1;
    private const long MAXIMUM_CONFIGURATION_FILE_BYTES = 16_384L;
    private const string LOCAL_CONFIGURATION_FILE_NAME = "google-calendar.local.json";
    private const string CLIENT_ID_ENVIRONMENT_VARIABLE_NAME =
        "TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_ID";

    private readonly Func<string?> mEnvironmentClientIdProvider;
    private readonly string mLocalConfigurationPath;

    public ProductGoogleCalendarOAuthConfigurationProvider()
        : this(
            delegate
            {
                return Environment.GetEnvironmentVariable(
                    CLIENT_ID_ENVIRONMENT_VARIABLE_NAME);
            },
            Path.Combine(AppContext.BaseDirectory, LOCAL_CONFIGURATION_FILE_NAME))
    {
    }

    internal ProductGoogleCalendarOAuthConfigurationProvider(
        Func<string?> environmentClientIdProvider,
        string localConfigurationPath)
    {
        if (environmentClientIdProvider == null)
        {
            throw new ArgumentNullException(nameof(environmentClientIdProvider));
        }

        if (localConfigurationPath == null)
        {
            throw new ArgumentNullException(nameof(localConfigurationPath));
        }

        mEnvironmentClientIdProvider = environmentClientIdProvider;
        mLocalConfigurationPath = Path.GetFullPath(localConfigurationPath);
    }

    public GoogleCalendarOAuthConfiguration? GetConfigurationOrNull()
    {
        string? environmentClientIdOrNull = mEnvironmentClientIdProvider();
        if (string.IsNullOrWhiteSpace(environmentClientIdOrNull) == false)
        {
            try
            {
                return new GoogleCalendarOAuthConfiguration(
                    new GoogleOAuthClientId(environmentClientIdOrNull));
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        if (File.Exists(mLocalConfigurationPath) == false)
        {
            return null;
        }

        try
        {
            FileInfo fileInfo = new FileInfo(mLocalConfigurationPath);
            if (fileInfo.Length > MAXIMUM_CONFIGURATION_FILE_BYTES)
            {
                return null;
            }

            using (FileStream stream = File.OpenRead(mLocalConfigurationPath))
            using (JsonDocument document = JsonDocument.Parse(stream))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                HashSet<string> propertyNames = new HashSet<string>(
                    StringComparer.Ordinal);
                int? schemaVersionOrNull = null;
                string? clientIdOrNull = null;
                foreach (JsonProperty property in document.RootElement.EnumerateObject())
                {
                    if (propertyNames.Add(property.Name) == false)
                    {
                        return null;
                    }

                    switch (property.Name)
                    {
                        case "schemaVersion":
                            int schemaVersion;
                            if (property.Value.TryGetInt32(out schemaVersion))
                            {
                                schemaVersionOrNull = schemaVersion;
                            }

                            break;
                        case "clientId":
                            if (property.Value.ValueKind == JsonValueKind.String)
                            {
                                clientIdOrNull = property.Value.GetString();
                            }

                            break;
                        default:
                            return null;
                    }
                }

                if (schemaVersionOrNull != SCHEMA_VERSION
                    || string.IsNullOrWhiteSpace(clientIdOrNull))
                {
                    return null;
                }

                return new GoogleCalendarOAuthConfiguration(
                    new GoogleOAuthClientId(clientIdOrNull));
            }
        }
        catch (Exception exception) when (
            exception is IOException
            || exception is UnauthorizedAccessException
            || exception is JsonException
            || exception is ArgumentException)
        {
            return null;
        }
    }
}
