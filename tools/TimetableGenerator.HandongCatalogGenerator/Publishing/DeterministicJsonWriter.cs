using System;
using System.IO;
using System.Text.Encodings.Web;
using System.Text.Json;

namespace TimetableGenerator.HandongCatalogGenerator.Publishing;

internal static class DeterministicJsonWriter
{
    private const byte LINE_FEED = (byte)'\n';

    public static byte[] Write(Action<Utf8JsonWriter> writeDocument)
    {
        ArgumentNullException.ThrowIfNull(writeDocument);

        using (MemoryStream output = new MemoryStream())
        {
            JsonWriterOptions options = new JsonWriterOptions
            {
                Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
                SkipValidation = false,
            };

            using (Utf8JsonWriter writer = new Utf8JsonWriter(output, options))
            {
                writeDocument(writer);
                writer.Flush();
            }

            return normalizeLineEndingsAndAppendFinalLineFeed(output.ToArray());
        }
    }

    private static byte[] normalizeLineEndingsAndAppendFinalLineFeed(byte[] content)
    {
        using (MemoryStream normalizedContent = new MemoryStream(content.Length + 1))
        {
            for (int byteIndex = 0; byteIndex < content.Length; ++byteIndex)
            {
                byte value = content[byteIndex];
                if (value != (byte)'\r')
                {
                    normalizedContent.WriteByte(value);
                    continue;
                }

                int followingByteIndex = byteIndex + 1;
                bool isCarriageReturnLineFeed = followingByteIndex < content.Length
                    && content[followingByteIndex] == LINE_FEED;
                if (isCarriageReturnLineFeed == false)
                {
                    throw new InvalidOperationException(
                        "Deterministic JSON contains a bare carriage return.");
                }
            }

            normalizedContent.WriteByte(LINE_FEED);
            return normalizedContent.ToArray();
        }
    }
}
