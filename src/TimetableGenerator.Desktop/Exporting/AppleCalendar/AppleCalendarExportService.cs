using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Exporting.AppleCalendar;

internal sealed class AppleCalendarExportService : IAppleCalendarExporter
{
    private const int MAXIMUM_DESTINATION_ATTEMPTS = 3;
    private const string DESTINATION_CHANGED_DIAGNOSTIC_CODE = "eventkit_calendar_destination_changed";

    private readonly IAppleCalendarNativeBridge mNativeBridge;
    private readonly IAppleCalendarExportLeaseProvider mExportLeaseProvider;

    public bool IsAvailable
    {
        get
        {
            return mNativeBridge.IsAvailable;
        }
    }

    public AppleCalendarExportService(IAppleCalendarNativeBridge nativeBridge)
        : this(nativeBridge, NoOpAppleCalendarExportLeaseProvider.Instance)
    {
    }

    internal AppleCalendarExportService(IAppleCalendarNativeBridge nativeBridge, IAppleCalendarExportLeaseProvider exportLeaseProvider)
    {
        if (nativeBridge == null)
        {
            throw new ArgumentNullException(nameof(nativeBridge));
        }

        if (exportLeaseProvider == null)
        {
            throw new ArgumentNullException(nameof(exportLeaseProvider));
        }

        mNativeBridge = nativeBridge;
        mExportLeaseProvider = exportLeaseProvider;
    }

    public async Task<AppleCalendarExportResult> ExportAsync(
        CalendarExportDocument document,
        ICalendarNameConflictResolver conflictResolver,
        CancellationToken cancellationToken,
        IProgress<AppleCalendarExportProgress>? progressOrNull = null)
    {
        if (document == null)
        {
            throw new ArgumentNullException(nameof(document));
        }

        if (conflictResolver == null)
        {
            throw new ArgumentNullException(nameof(conflictResolver));
        }

        cancellationToken.ThrowIfCancellationRequested();
        if (IsAvailable == false)
        {
            return AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.Unavailable, "apple_calendar_native_bridge_unavailable");
        }

        if (document.Events.Count == 0)
        {
            return AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.Failed, "apple_calendar_export_requires_events");
        }

        try
        {
            await using (IAppleCalendarExportLease exportLease = await mExportLeaseProvider.AcquireAsync(cancellationToken).ConfigureAwait(false))
            {
                return await exportWithConflictResolutionAsync(document, conflictResolver, cancellationToken, progressOrNull).ConfigureAwait(false);
            }
        }
        catch (AppleCalendarNativeBridgeException exception)
        {
            return createFailureResult(exception);
        }
        catch (Exception exception) when (exception is IOException || exception is UnauthorizedAccessException)
        {
            return AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.Failed, "apple_calendar_local_state_failed");
        }
    }

    private async Task<AppleCalendarExportResult> exportWithConflictResolutionAsync(
        CalendarExportDocument document,
        ICalendarNameConflictResolver conflictResolver,
        CancellationToken cancellationToken,
        IProgress<AppleCalendarExportProgress>? progressOrNull)
    {
        AppleCalendarNativeBridgeException? latestConflictExceptionOrNull = null;
        List<PlanName> unavailableDestinationNames = new List<PlanName>();
        for (int attempt = 0; attempt < MAXIMUM_DESTINATION_ATTEMPTS; ++attempt)
        {
            reportProgress(progressOrNull, EAppleCalendarExportProgressStage.CheckingCalendar);
            if (attempt == 0)
            {
                await mNativeBridge.ReconcilePendingOperationAsync(cancellationToken);
            }

            IReadOnlyList<AppleCalendarDescriptor> calendars = await getValidatedCalendarsAsync(document, cancellationToken);
            AppleCalendarExportMutation? mutationOrNull = await createMutationOrNullAsync(document, calendars, unavailableDestinationNames, conflictResolver, cancellationToken);
            if (mutationOrNull == null)
            {
                return AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.Cancelled, null);
            }

            try
            {
                reportProgress(progressOrNull, EAppleCalendarExportProgressStage.SavingEvents);
                AppleCalendarNativeExportResult nativeResult = await mNativeBridge.ApplyExportAsync(mutationOrNull, cancellationToken);
                reportProgress(progressOrNull, EAppleCalendarExportProgressStage.Finalizing);
                ensureNativeResultMatchesMutation(nativeResult, mutationOrNull);
                return AppleCalendarExportResult.Complete(nativeResult);
            }
            catch (AppleCalendarNativeBridgeException exception) when (exception.FailureKind == EAppleCalendarNativeFailureKind.CalendarChanged)
            {
                latestConflictExceptionOrNull = exception;
                if (mutationOrNull.Kind == EAppleCalendarExportMutationKind.CreateNew && string.Equals(exception.DiagnosticCode, DESTINATION_CHANGED_DIAGNOSTIC_CODE, StringComparison.Ordinal))
                {
                    addUnavailableDestinationName(unavailableDestinationNames, mutationOrNull.DestinationName);
                }
            }
        }

        if (latestConflictExceptionOrNull == null)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.CalendarChanged, "apple_calendar_destination_changed");
        }

        throw latestConflictExceptionOrNull;
    }

    private async Task<AppleCalendarExportMutation?> createMutationOrNullAsync(
        CalendarExportDocument document,
        IReadOnlyList<AppleCalendarDescriptor> calendars,
        IReadOnlyList<PlanName> unavailableDestinationNames,
        ICalendarNameConflictResolver conflictResolver,
        CancellationToken cancellationToken)
    {
        IReadOnlyList<AppleCalendarDescriptor> matchingCalendars = findMatchingCalendars(document.CalendarName, calendars);
        if (matchingCalendars.Count == 0 && containsSameName(document.CalendarName, unavailableDestinationNames) == false)
        {
            return AppleCalendarExportMutation.CreateNew(document, document.CalendarName);
        }

        AppleCalendarDescriptor? replaceableCalendarOrNull = findSoleReplaceableCalendarOrNull(matchingCalendars);
        ECalendarReplacementAvailability replacementAvailability;
        if (replaceableCalendarOrNull != null)
        {
            replacementAvailability = ECalendarReplacementAvailability.Available;
        }
        else
        {
            replacementAvailability = ECalendarReplacementAvailability.Unavailable;
        }

        PlanName nextAvailableName = CalendarNameConflictPolicy.FindNextAvailableName(document.CalendarName, getCalendarNames(calendars, unavailableDestinationNames));
        CalendarNameConflict conflict = new CalendarNameConflict(ECalendarExportProvider.Apple, document.CalendarName, nextAvailableName, replacementAvailability);
        ECalendarNameConflictResolution resolution = await conflictResolver.ResolveAsync(conflict, cancellationToken);
        CalendarNameConflictPolicy.EnsureResolutionIsSupported(conflict, resolution);

        switch (resolution)
        {
            case ECalendarNameConflictResolution.ReplaceExisting:
                return AppleCalendarExportMutation.ReplaceExisting(
                    document,
                    document.CalendarName,
                    replaceableCalendarOrNull!.CalendarId,
                    replaceableCalendarOrNull.SourceIdentifier,
                    replaceableCalendarOrNull.ManagedPlanIdOrNull!.Value);
            case ECalendarNameConflictResolution.CreateWithAvailableName:
                return AppleCalendarExportMutation.CreateNew(document, nextAvailableName);
            case ECalendarNameConflictResolution.Cancel:
                return null;
            case ECalendarNameConflictResolution.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(resolution), resolution, "A supported Apple Calendar conflict resolution is required.");
        }
    }

    private async Task<IReadOnlyList<AppleCalendarDescriptor>> getValidatedCalendarsAsync(CalendarExportDocument document, CancellationToken cancellationToken)
    {
        IReadOnlyList<AppleCalendarDescriptor>? calendarsOrNull = await mNativeBridge.GetCalendarsAsync(document, cancellationToken);
        if (calendarsOrNull == null)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_invalid_snapshot");
        }

        List<AppleCalendarDescriptor> calendars = new List<AppleCalendarDescriptor>(calendarsOrNull.Count);
        HashSet<AppleCalendarId> calendarIds = new HashSet<AppleCalendarId>();
        foreach (AppleCalendarDescriptor? calendarOrNull in calendarsOrNull)
        {
            if (calendarOrNull == null || calendarIds.Add(calendarOrNull.CalendarId) == false)
            {
                throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_invalid_snapshot");
            }

            calendars.Add(calendarOrNull);
        }

        return calendars.AsReadOnly();
    }

    private static IReadOnlyList<AppleCalendarDescriptor> findMatchingCalendars(PlanName requestedName, IReadOnlyList<AppleCalendarDescriptor> calendars)
    {
        List<AppleCalendarDescriptor> matchingCalendars = new List<AppleCalendarDescriptor>();
        foreach (AppleCalendarDescriptor calendar in calendars)
        {
            PlanName? displayNameOrNull = tryCreatePlanNameOrNull(calendar.DisplayName);
            if (displayNameOrNull != null && CalendarNameConflictPolicy.IsSameName(requestedName, displayNameOrNull))
            {
                matchingCalendars.Add(calendar);
            }
        }

        return matchingCalendars.AsReadOnly();
    }

    private static AppleCalendarDescriptor? findSoleReplaceableCalendarOrNull(IReadOnlyList<AppleCalendarDescriptor> matchingCalendars)
    {
        if (matchingCalendars.Count != 1 || matchingCalendars[0].CanReplace == false)
        {
            return null;
        }

        return matchingCalendars[0];
    }

    private static IReadOnlyList<PlanName> getCalendarNames(IReadOnlyList<AppleCalendarDescriptor> calendars, IReadOnlyList<PlanName> unavailableDestinationNames)
    {
        List<PlanName> calendarNames = new List<PlanName>(calendars.Count + unavailableDestinationNames.Count);
        foreach (AppleCalendarDescriptor calendar in calendars)
        {
            PlanName? displayNameOrNull = tryCreatePlanNameOrNull(calendar.DisplayName);
            if (displayNameOrNull != null)
            {
                calendarNames.Add(displayNameOrNull);
            }
        }
        calendarNames.AddRange(unavailableDestinationNames);

        return calendarNames.AsReadOnly();
    }

    private static bool containsSameName(PlanName requestedName, IReadOnlyList<PlanName> names)
    {
        foreach (PlanName name in names)
        {
            if (CalendarNameConflictPolicy.IsSameName(requestedName, name))
            {
                return true;
            }
        }

        return false;
    }

    private static void addUnavailableDestinationName(List<PlanName> unavailableDestinationNames, PlanName destinationName)
    {
        if (containsSameName(destinationName, unavailableDestinationNames) == false)
        {
            unavailableDestinationNames.Add(destinationName);
        }
    }

    private static PlanName? tryCreatePlanNameOrNull(string value)
    {
        string normalizedValue = value.Trim();
        if (normalizedValue.Length == 0 || normalizedValue.Length > PlanName.MAXIMUM_LENGTH)
        {
            return null;
        }

        return new PlanName(normalizedValue);
    }

    private static void ensureNativeResultMatchesMutation(AppleCalendarNativeExportResult nativeResult, AppleCalendarExportMutation mutation)
    {
        if (nativeResult == null)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_invalid_export_result");
        }

        if (CalendarNameConflictPolicy.IsSameName(nativeResult.CalendarName, mutation.DestinationName) == false)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_destination_mismatch");
        }

        if (mutation.Kind == EAppleCalendarExportMutationKind.ReplaceExisting && nativeResult.CalendarId != mutation.ExistingCalendarIdOrNull)
        {
            throw new AppleCalendarNativeBridgeException(EAppleCalendarNativeFailureKind.OperationFailed, "apple_calendar_destination_mismatch");
        }
    }

    private static AppleCalendarExportResult createFailureResult(AppleCalendarNativeBridgeException exception)
    {
        switch (exception.FailureKind)
        {
            case EAppleCalendarNativeFailureKind.AccessDenied:
                return AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.AccessDenied, exception.DiagnosticCode);
            case EAppleCalendarNativeFailureKind.Unavailable:
                return AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.Unavailable, exception.DiagnosticCode);
            case EAppleCalendarNativeFailureKind.CalendarChanged:
            case EAppleCalendarNativeFailureKind.OperationFailed:
                return AppleCalendarExportResult.Fail(EAppleCalendarExportStatus.Failed, exception.DiagnosticCode);
            case EAppleCalendarNativeFailureKind.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(exception), exception.FailureKind, "Apple Calendar failures require a supported failure kind.");
        }
    }

    private static void reportProgress(IProgress<AppleCalendarExportProgress>? progressOrNull, EAppleCalendarExportProgressStage stage)
    {
        progressOrNull?.Report(new AppleCalendarExportProgress(stage));
    }
}
