using System;
using System.Buffers;
using System.Text.Json;

using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Storage;

internal sealed class ProductAppearanceSettingsJsonCodec
{
    private const int SCHEMA_VERSION = 1;

    private const string SYSTEM_THEME_VALUE = "system";

    private const string LIGHT_THEME_VALUE = "light";

    private const string DARK_THEME_VALUE = "dark";

    public byte[] Serialize(ProductAppearanceSettings settings)
    {
        if (settings == null)
        {
            throw new ArgumentNullException(nameof(settings));
        }

        ArrayBufferWriter<byte> buffer = new ArrayBufferWriter<byte>();
        JsonWriterOptions options = default(JsonWriterOptions);
        options.Indented = true;
        using (Utf8JsonWriter writer = new Utf8JsonWriter(buffer, options))
        {
            writer.WriteStartObject();
            writer.WriteNumber("schemaVersion", SCHEMA_VERSION);
            writer.WriteString(
                "themePreference",
                findSerializedThemePreference(settings.ThemePreference));
            writer.WriteEndObject();
            writer.Flush();
        }

        return buffer.WrittenSpan.ToArray();
    }

    public ProductAppearanceSettings Deserialize(ReadOnlyMemory<byte> content)
    {
        if (content.IsEmpty)
        {
            throw new ProductAppearanceSettingsException(
                "The appearance settings document is empty.");
        }

        try
        {
            using (JsonDocument document = JsonDocument.Parse(content))
            {
                return readSettings(document.RootElement);
            }
        }
        catch (JsonException exception)
        {
            throw new ProductAppearanceSettingsException(
                "The appearance settings document is not valid JSON.",
                exception);
        }
    }

    private static ProductAppearanceSettings readSettings(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            throw new ProductAppearanceSettingsException(
                "The appearance settings document must be a JSON object.");
        }

        bool hasSchemaVersion = false;
        bool hasThemePreference = false;
        int schemaVersion = 0;
        EProductThemePreference themePreference =
            EProductThemePreference.System;
        foreach (JsonProperty property in element.EnumerateObject())
        {
            switch (property.Name)
            {
                case "schemaVersion":
                    if (hasSchemaVersion)
                    {
                        throw createDuplicatePropertyException(property.Name);
                    }

                    schemaVersion = readSchemaVersion(property.Value);
                    hasSchemaVersion = true;
                    break;
                case "themePreference":
                    if (hasThemePreference)
                    {
                        throw createDuplicatePropertyException(property.Name);
                    }

                    themePreference = readThemePreference(property.Value);
                    hasThemePreference = true;
                    break;
                default:
                    throw new ProductAppearanceSettingsException(
                        "The appearance settings document contains the unknown property '"
                        + property.Name
                        + "'.");
            }
        }

        if (hasSchemaVersion == false)
        {
            throw createMissingPropertyException("schemaVersion");
        }

        if (hasThemePreference == false)
        {
            throw createMissingPropertyException("themePreference");
        }

        if (schemaVersion != SCHEMA_VERSION)
        {
            throw new ProductAppearanceSettingsException(
                "The appearance settings schema version is not supported: "
                + schemaVersion
                + ".");
        }

        return new ProductAppearanceSettings(themePreference);
    }

    private static int readSchemaVersion(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Number)
        {
            throw new ProductAppearanceSettingsException(
                "schemaVersion must be a JSON number.");
        }

        int schemaVersion;
        if (element.TryGetInt32(out schemaVersion) == false)
        {
            throw new ProductAppearanceSettingsException(
                "schemaVersion must be a 32-bit integer.");
        }

        return schemaVersion;
    }

    private static EProductThemePreference readThemePreference(
        JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.String)
        {
            throw new ProductAppearanceSettingsException(
                "themePreference must be a JSON string.");
        }

        string? serializedPreferenceOrNull = element.GetString();
        switch (serializedPreferenceOrNull)
        {
            case SYSTEM_THEME_VALUE:
                return EProductThemePreference.System;
            case LIGHT_THEME_VALUE:
                return EProductThemePreference.Light;
            case DARK_THEME_VALUE:
                return EProductThemePreference.Dark;
            default:
                throw new ProductAppearanceSettingsException(
                    "themePreference must be 'system', 'light', or 'dark'.");
        }
    }

    private static string findSerializedThemePreference(
        EProductThemePreference themePreference)
    {
        switch (themePreference)
        {
            case EProductThemePreference.System:
                return SYSTEM_THEME_VALUE;
            case EProductThemePreference.Light:
                return LIGHT_THEME_VALUE;
            case EProductThemePreference.Dark:
                return DARK_THEME_VALUE;
            default:
                throw new ArgumentOutOfRangeException(
                    nameof(themePreference),
                    themePreference,
                    "Unknown product theme preference.");
        }
    }

    private static ProductAppearanceSettingsException
        createDuplicatePropertyException(string propertyName)
    {
        return new ProductAppearanceSettingsException(
            "The appearance settings document contains the duplicate property '"
            + propertyName
            + "'.");
    }

    private static ProductAppearanceSettingsException
        createMissingPropertyException(string propertyName)
    {
        return new ProductAppearanceSettingsException(
            "The appearance settings document is missing the required property '"
            + propertyName
            + "'.");
    }
}
