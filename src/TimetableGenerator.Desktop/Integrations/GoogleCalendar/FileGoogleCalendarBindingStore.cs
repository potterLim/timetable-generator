using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Integrations.GoogleCalendar;

internal sealed class FileGoogleCalendarBindingStore : IGoogleCalendarBindingStore, IDisposable
{
    private const int SCHEMA_VERSION = 1;
    private const long MAXIMUM_FILE_SIZE_BYTES = 262_144L;

    private static readonly JsonSerializerOptions JSON_OPTIONS = new JsonSerializerOptions
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    private readonly GoogleCalendarBindingFilePath mPath;
    private readonly SemaphoreSlim mAccessGate;
    private bool mIsDisposed;

    public FileGoogleCalendarBindingStore(GoogleCalendarBindingFilePath path)
    {
        if (path == null)
        {
            throw new ArgumentNullException(nameof(path));
        }

        mPath = path;
        mAccessGate = new SemaphoreSlim(1, 1);
    }

    public async Task<GoogleCalendarId?> GetCalendarIdOrNullAsync(
        PlanId planId,
        CancellationToken cancellationToken)
    {
        ensureValidPlanId(planId);
        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<PlanId, GoogleCalendarId> bindings = await loadAsync(
                cancellationToken).ConfigureAwait(false);
            GoogleCalendarId? calendarIdOrNull;
            return bindings.TryGetValue(planId, out calendarIdOrNull)
                ? calendarIdOrNull
                : null;
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    public async Task SaveCalendarIdAsync(
        PlanId planId,
        GoogleCalendarId calendarId,
        CancellationToken cancellationToken)
    {
        ensureValidPlanId(planId);
        if (calendarId == null)
        {
            throw new ArgumentNullException(nameof(calendarId));
        }

        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<PlanId, GoogleCalendarId> bindings = await loadAsync(
                cancellationToken).ConfigureAwait(false);
            bindings[planId] = calendarId;
            await saveAsync(bindings, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    public async Task DeleteCalendarIdAsync(
        PlanId planId,
        CancellationToken cancellationToken)
    {
        ensureValidPlanId(planId);
        await mAccessGate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            Dictionary<PlanId, GoogleCalendarId> bindings = await loadAsync(
                cancellationToken).ConfigureAwait(false);
            if (bindings.Remove(planId))
            {
                await saveAsync(bindings, cancellationToken).ConfigureAwait(false);
            }
        }
        finally
        {
            mAccessGate.Release();
        }
    }

    public void Dispose()
    {
        if (mIsDisposed)
        {
            return;
        }

        mAccessGate.Dispose();
        mIsDisposed = true;
    }

    private async Task<Dictionary<PlanId, GoogleCalendarId>> loadAsync(
        CancellationToken cancellationToken)
    {
        if (File.Exists(mPath.Value) == false)
        {
            return new Dictionary<PlanId, GoogleCalendarId>();
        }

        try
        {
            FileInfo fileInfo = new FileInfo(mPath.Value);
            if (fileInfo.Length > MAXIMUM_FILE_SIZE_BYTES)
            {
                throw new InvalidDataException(
                    "The Google Calendar binding file exceeds the product size limit.");
            }

            byte[] content = await File.ReadAllBytesAsync(
                mPath.Value,
                cancellationToken).ConfigureAwait(false);
            BindingDocument? documentOrNull = JsonSerializer.Deserialize<BindingDocument>(
                content,
                JSON_OPTIONS);
            if (documentOrNull == null || documentOrNull.SchemaVersion != SCHEMA_VERSION)
            {
                throw new InvalidDataException(
                    "The Google Calendar binding file has an unsupported format.");
            }

            Dictionary<PlanId, GoogleCalendarId> bindings =
                new Dictionary<PlanId, GoogleCalendarId>();
            foreach (BindingEntry entry in documentOrNull.Bindings)
            {
                Guid planIdValue;
                if (Guid.TryParseExact(entry.PlanId, "N", out planIdValue) == false)
                {
                    throw new InvalidDataException(
                        "The Google Calendar binding file contains an invalid plan ID.");
                }

                PlanId planId = new PlanId(planIdValue);
                if (bindings.TryAdd(planId, new GoogleCalendarId(entry.CalendarId)) == false)
                {
                    throw new InvalidDataException(
                        "The Google Calendar binding file contains duplicate plan IDs.");
                }
            }

            return bindings;
        }
        catch (Exception exception) when (
            exception is JsonException
            || exception is ArgumentException)
        {
            throw new InvalidDataException(
                "The Google Calendar binding file contains invalid values.",
                exception);
        }
    }

    private async Task saveAsync(
        IReadOnlyDictionary<PlanId, GoogleCalendarId> bindings,
        CancellationToken cancellationToken)
    {
        string? directoryPathOrNull = Path.GetDirectoryName(mPath.Value);
        if (directoryPathOrNull == null)
        {
            throw new InvalidOperationException(
                "The Google Calendar binding path does not contain a directory.");
        }

        Directory.CreateDirectory(directoryPathOrNull);
        List<BindingEntry> entries = new List<BindingEntry>(bindings.Count);
        foreach (KeyValuePair<PlanId, GoogleCalendarId> binding in bindings)
        {
            entries.Add(
                new BindingEntry(
                    binding.Key.Value.ToString("N"),
                    binding.Value.Value));
        }

        entries.Sort(
            delegate (BindingEntry left, BindingEntry right)
            {
                return string.CompareOrdinal(left.PlanId, right.PlanId);
            });
        BindingDocument document = new BindingDocument(SCHEMA_VERSION, entries);
        byte[] content = JsonSerializer.SerializeToUtf8Bytes(document, JSON_OPTIONS);
        string temporaryPath = mPath.Value + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            await using (FileStream stream = new FileStream(
                temporaryPath,
                FileMode.CreateNew,
                FileAccess.Write,
                FileShare.None,
                16_384,
                FileOptions.Asynchronous | FileOptions.WriteThrough))
            {
                await stream.WriteAsync(content, cancellationToken).ConfigureAwait(false);
                await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                stream.Flush(true);
            }

            File.Move(temporaryPath, mPath.Value, true);
        }
        finally
        {
            tryDeleteTemporaryFile(temporaryPath);
        }
    }

    private static void tryDeleteTemporaryFile(string temporaryPath)
    {
        try
        {
            if (File.Exists(temporaryPath))
            {
                File.Delete(temporaryPath);
            }
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static void ensureValidPlanId(PlanId planId)
    {
        if (planId.IsValid == false)
        {
            throw new ArgumentException(
                "Google Calendar bindings require a valid plan ID.",
                nameof(planId));
        }
    }

    private sealed class BindingDocument
    {
        [JsonPropertyName("schemaVersion")]
        public int SchemaVersion { get; }

        [JsonPropertyName("bindings")]
        public IReadOnlyList<BindingEntry> Bindings { get; }

        [JsonConstructor]
        public BindingDocument(int schemaVersion, IReadOnlyList<BindingEntry> bindings)
        {
            if (bindings == null)
            {
                throw new ArgumentNullException(nameof(bindings));
            }

            SchemaVersion = schemaVersion;
            Bindings = bindings;
        }
    }

    private sealed class BindingEntry
    {
        [JsonPropertyName("planId")]
        public string PlanId { get; }

        [JsonPropertyName("calendarId")]
        public string CalendarId { get; }

        [JsonConstructor]
        public BindingEntry(string planId, string calendarId)
        {
            if (planId == null)
            {
                throw new ArgumentNullException(nameof(planId));
            }

            if (calendarId == null)
            {
                throw new ArgumentNullException(nameof(calendarId));
            }

            PlanId = planId;
            CalendarId = calendarId;
        }
    }
}
