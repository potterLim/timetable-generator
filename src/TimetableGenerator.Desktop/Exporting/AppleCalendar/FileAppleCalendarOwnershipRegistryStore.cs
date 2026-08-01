using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class FileAppleCalendarOwnershipRegistryStore : IAppleCalendarOwnershipRegistryStore
{
    private const int WRITE_BUFFER_SIZE = 16_384;

    private static readonly JsonSerializerOptions JSON_OPTIONS = createJsonOptions();

    private readonly AppleCalendarOwnershipRegistryFilePath mFilePath;

    public FileAppleCalendarOwnershipRegistryStore(AppleCalendarOwnershipRegistryFilePath filePath)
    {
        if (filePath == null)
        {
            throw new ArgumentNullException(nameof(filePath));
        }

        mFilePath = filePath;
    }

    public AppleCalendarOwnershipRegistryDocument Load()
    {
        if (File.Exists(mFilePath.Value) == false)
        {
            return AppleCalendarOwnershipRegistryDocument.CreateEmpty();
        }

        try
        {
            byte[] content = File.ReadAllBytes(mFilePath.Value);
            AppleCalendarOwnershipRegistryDocument? documentOrNull = JsonSerializer.Deserialize<AppleCalendarOwnershipRegistryDocument>(content, JSON_OPTIONS);
            if (documentOrNull == null)
            {
                throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry is empty.");
            }

            return documentOrNull;
        }
        catch (JsonException exception)
        {
            throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry is not valid JSON.", exception);
        }
        catch (NotSupportedException exception)
        {
            throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry contains unsupported values.", exception);
        }
    }

    public void Save(AppleCalendarOwnershipRegistryDocument document)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        string? directoryPathOrNull = Path.GetDirectoryName(mFilePath.Value);
        if (directoryPathOrNull == null)
        {
            throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry has no parent directory.");
        }

        Directory.CreateDirectory(directoryPathOrNull);
        string temporaryPath = mFilePath.Value + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            byte[] content = JsonSerializer.SerializeToUtf8Bytes(document, JSON_OPTIONS);
            using (FileStream output = new FileStream(temporaryPath, createPrivateFileOptions()))
            {
                output.Write(content, 0, content.Length);
                output.Flush(true);
            }

            File.Move(temporaryPath, mFilePath.Value, true);
        }
        catch (Exception exception) when (exception is JsonException || exception is NotSupportedException)
        {
            throw new AppleCalendarOwnershipRegistryException("The Apple Calendar ownership registry could not be serialized.", exception);
        }
        finally
        {
            tryDelete(temporaryPath);
        }
    }

    internal static FileStreamOptions createPrivateFileOptions()
    {
        FileStreamOptions options = new FileStreamOptions
        {
            Mode = FileMode.CreateNew,
            Access = FileAccess.Write,
            Share = FileShare.None,
            BufferSize = WRITE_BUFFER_SIZE,
            Options = FileOptions.WriteThrough,
        };
        if (OperatingSystem.IsMacOS() || OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD())
        {
            options.UnixCreateMode = UnixFileMode.UserRead | UnixFileMode.UserWrite;
        }

        return options;
    }

    private static JsonSerializerOptions createJsonOptions()
    {
        return new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            PropertyNameCaseInsensitive = false,
            WriteIndented = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
        };
    }

    private static void tryDelete(string path)
    {
        try
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            // Temporary registry cleanup must not hide the original operation result.
        }
    }
}
