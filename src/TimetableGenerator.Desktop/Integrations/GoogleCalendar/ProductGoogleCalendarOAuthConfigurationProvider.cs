using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class ProductGoogleCalendarOAuthConfigurationProvider
    : IGoogleCalendarOAuthConfigurationProvider
{
    private const int SCHEMA_VERSION_ONE = 1;
    private const int SCHEMA_VERSION_TWO = 2;
    private const long MAXIMUM_CONFIGURATION_FILE_BYTES = 16_384L;
    private const string LOCAL_CONFIGURATION_FILE_NAME = "google-calendar.local.json";
    private const string CLIENT_ID_ENVIRONMENT_VARIABLE_NAME = "TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_ID";
    private const string CLIENT_SECRET_ENVIRONMENT_VARIABLE_NAME = "TIMETABLE_GENERATOR_GOOGLE_CALENDAR_CLIENT_SECRET";

    private readonly Func<GoogleCalendarOAuthEnvironmentValues> mEnvironmentValuesProvider;
    private readonly GoogleCalendarOAuthConfigurationPath mLocalConfigurationPath;

    public ProductGoogleCalendarOAuthConfigurationProvider()
        : this(
            delegate
            {
                return new GoogleCalendarOAuthEnvironmentValues(
                    Environment.GetEnvironmentVariable(
                        CLIENT_ID_ENVIRONMENT_VARIABLE_NAME),
                    Environment.GetEnvironmentVariable(
                        CLIENT_SECRET_ENVIRONMENT_VARIABLE_NAME));
            },
            new GoogleCalendarOAuthConfigurationPath(
                Path.Combine(AppContext.BaseDirectory, LOCAL_CONFIGURATION_FILE_NAME)))
    {
    }

    internal ProductGoogleCalendarOAuthConfigurationProvider(
        Func<GoogleCalendarOAuthEnvironmentValues> environmentValuesProvider,
        GoogleCalendarOAuthConfigurationPath localConfigurationPath)
    {
        if (environmentValuesProvider == null)
        {
            throw new ArgumentNullException(nameof(environmentValuesProvider));
        }

        if (localConfigurationPath == null)
        {
            throw new ArgumentNullException(nameof(localConfigurationPath));
        }

        mEnvironmentValuesProvider = environmentValuesProvider;
        mLocalConfigurationPath = localConfigurationPath;
    }

    public GoogleCalendarOAuthConfiguration? GetConfigurationOrNull()
    {
        GoogleCalendarOAuthEnvironmentValues environmentValues = mEnvironmentValuesProvider();
        bool hasEnvironmentClientId = string.IsNullOrWhiteSpace(environmentValues.ClientIdOrNull) == false;
        bool hasEnvironmentClientSecret = string.IsNullOrWhiteSpace(environmentValues.ClientSecretOrNull) == false;
        if (hasEnvironmentClientId || hasEnvironmentClientSecret)
        {
            if (hasEnvironmentClientId == false)
            {
                return null;
            }

            try
            {
                GoogleOAuthClientSecret? clientSecretOrNull =
                    hasEnvironmentClientSecret
                        ? new GoogleOAuthClientSecret(
                            environmentValues.ClientSecretOrNull!)
                        : null;
                return new GoogleCalendarOAuthConfiguration(
                    new GoogleOAuthClientId(environmentValues.ClientIdOrNull!),
                    clientSecretOrNull);
            }
            catch (ArgumentException)
            {
                return null;
            }
        }

        if (File.Exists(mLocalConfigurationPath.Value) == false)
        {
            return null;
        }

        try
        {
            FileInfo fileInfo = new FileInfo(mLocalConfigurationPath.Value);
            if (fileInfo.Length > MAXIMUM_CONFIGURATION_FILE_BYTES)
            {
                return null;
            }

            using (FileStream stream = File.OpenRead(mLocalConfigurationPath.Value))
            using (JsonDocument document = JsonDocument.Parse(stream))
            {
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    return null;
                }

                HashSet<string> propertyNames = new HashSet<string>(StringComparer.Ordinal);
                int? schemaVersionOrNull = null;
                string? clientIdOrNull = null;
                string? clientSecretOrNull = null;
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
                        case "clientSecret":
                            if (property.Value.ValueKind == JsonValueKind.String)
                            {
                                clientSecretOrNull = property.Value.GetString();
                            }

                            break;
                        default:
                            return null;
                    }
                }

                bool isSchemaVersionOne = schemaVersionOrNull == SCHEMA_VERSION_ONE
                    && propertyNames.Count == 2
                    && propertyNames.Contains("schemaVersion")
                    && propertyNames.Contains("clientId")
                    && string.IsNullOrWhiteSpace(clientSecretOrNull);
                bool isSchemaVersionTwo = schemaVersionOrNull == SCHEMA_VERSION_TWO
                    && propertyNames.Count == 3
                    && propertyNames.Contains("schemaVersion")
                    && propertyNames.Contains("clientId")
                    && propertyNames.Contains("clientSecret")
                    && string.IsNullOrWhiteSpace(clientSecretOrNull) == false;
                if ((isSchemaVersionOne == false && isSchemaVersionTwo == false)
                    || string.IsNullOrWhiteSpace(clientIdOrNull))
                {
                    return null;
                }

                GoogleOAuthClientSecret? parsedClientSecretOrNull =
                    isSchemaVersionTwo
                        ? new GoogleOAuthClientSecret(clientSecretOrNull!)
                        : null;
                return new GoogleCalendarOAuthConfiguration(
                    new GoogleOAuthClientId(clientIdOrNull),
                    parsedClientSecretOrNull);
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
