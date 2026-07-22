using System.Collections.Generic;
using System.Text.Json;

namespace TimetableGenerator.Infrastructure.Persistence;

public sealed partial class PlanningWorkspaceJsonCodec
{
    private static Dictionary<string, JsonElement> readExactObject(
        JsonElement element,
        string context,
        IReadOnlyList<string> expectedPropertyNames)
    {
        requireValueKind(element, JsonValueKind.Object, context);
        HashSet<string> expectedNames = new HashSet<string>(
            expectedPropertyNames,
            System.StringComparer.Ordinal);
        Dictionary<string, JsonElement> properties = new Dictionary<string, JsonElement>(System.StringComparer.Ordinal);
        foreach (JsonProperty property in element.EnumerateObject())
        {
            if (expectedNames.Contains(property.Name) == false)
            {
                throw new WorkspaceDocumentException(
                    context
                    + " contains the unknown property '"
                    + property.Name
                    + "'.");
            }

            if (properties.TryAdd(property.Name, property.Value) == false)
            {
                throw new WorkspaceDocumentException(
                    context
                    + " contains the duplicate property '"
                    + property.Name
                    + "'.");
            }
        }

        foreach (string expectedPropertyName in expectedPropertyNames)
        {
            if (properties.ContainsKey(expectedPropertyName) == false)
            {
                throw new WorkspaceDocumentException(
                    context
                    + " is missing the required property '"
                    + expectedPropertyName
                    + "'.");
            }
        }

        return properties;
    }

    private static string readString(JsonElement element, string context)
    {
        requireValueKind(element, JsonValueKind.String, context);
        string? value = element.GetString();
        if (value == null)
        {
            throw new WorkspaceDocumentException(context + " cannot be null.");
        }

        return value;
    }

    private static int readInt32(JsonElement element, string context)
    {
        requireValueKind(element, JsonValueKind.Number, context);
        int value;
        if (element.TryGetInt32(out value) == false)
        {
            throw new WorkspaceDocumentException(context + " must be a 32-bit integer.");
        }

        return value;
    }

    private static long readInt64(JsonElement element, string context)
    {
        requireValueKind(element, JsonValueKind.Number, context);
        long value;
        if (element.TryGetInt64(out value) == false)
        {
            throw new WorkspaceDocumentException(context + " must be a 64-bit integer.");
        }

        return value;
    }

    private static void requireValueKind(
        JsonElement element,
        JsonValueKind expectedKind,
        string context)
    {
        if (element.ValueKind != expectedKind)
        {
            throw new WorkspaceDocumentException(
                context + " must be a JSON " + expectedKind + " value.");
        }
    }
}
