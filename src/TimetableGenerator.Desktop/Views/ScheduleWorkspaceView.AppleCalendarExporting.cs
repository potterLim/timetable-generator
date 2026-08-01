using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Exporting.AppleCalendar;
using TimetableGenerator.Desktop.Exporting.Calendar;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView
{
    private async Task exportAppleCalendarAsync()
    {
        if (tryBeginExportOperation() == false)
        {
            return;
        }

        try
        {
            CancellationToken cancellationToken = mLifetimeCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            CalendarExportDocument document = createCalendarExportDocument(ECalendarExportProvider.Apple);
            Progress<AppleCalendarExportProgress> progress = new Progress<AppleCalendarExportProgress>(showAppleCalendarExportProgress);
            showPersistentExportStatus("Apple 캘린더 접근 권한과 기존 일정을 확인하는 중입니다.", EExportStatus.Information);
            AppleCalendarExportResult result = await mAppleCalendarExporter.ExportAsync(document, this, cancellationToken, progress);
            showAppleCalendarExportResult(result);
        }
        finally
        {
            completeExportOperation();
        }
    }

    private void showAppleCalendarExportResult(AppleCalendarExportResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        switch (result.Status)
        {
            case EAppleCalendarExportStatus.Success:
                showTransientExportStatus("Apple 캘린더로 내보냈습니다.", EExportStatus.Success);
                break;
            case EAppleCalendarExportStatus.Cancelled:
                clearExportStatus();
                break;
            case EAppleCalendarExportStatus.Unavailable:
                showTransientExportStatus("Apple 캘린더를 사용할 수 없습니다.", EExportStatus.Information);
                break;
            case EAppleCalendarExportStatus.AccessDenied:
                showPersistentExportStatus("시스템 설정의 개인정보 보호 및 보안에서 Timetable Generator의 캘린더 접근을 허용해 주세요.", EExportStatus.Failure);
                break;
            case EAppleCalendarExportStatus.Failed:
                showPersistentExportStatus(getAppleCalendarFailureMessage(result.DiagnosticCodeOrNull), EExportStatus.Failure);
                break;
            case EAppleCalendarExportStatus.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown Apple Calendar export status.");
        }
    }

    private static string getAppleCalendarFailureMessage(string? diagnosticCodeOrNull)
    {
        switch (diagnosticCodeOrNull)
        {
            case "apple_calendar_registry_finalize_failed":
                return "일정이 저장되었을 수 있습니다. Apple 캘린더에서 확인한 뒤 다시 시도해 주세요.";
            case "eventkit_reconciliation_ambiguous":
            case "apple_calendar_pending_operation_conflict":
                return "Apple 캘린더에서 해당 시간표 캘린더를 확인하고, 중복 일정이 있으면 정리한 뒤 다시 시도해 주세요.";
            case "eventkit_reconciliation_identifier_changed":
            case "apple_calendar_registered_identifier_unavailable":
            case "eventkit_calendar_registration_ambiguous":
                return "이전에 내보낸 시간표를 안전하게 확인할 수 없습니다. Apple 캘린더에서 해당 시간표를 확인한 뒤 다시 시도해 주세요.";
            case "apple_calendar_registry_rebind_failed":
            case "apple_calendar_registry_cleanup_failed":
                return "Apple 캘린더 연결 정보를 저장하지 못했습니다. 기기의 저장 공간을 확인한 뒤 다시 시도해 주세요.";
            default:
                return "Apple 캘린더로 내보내지 못했습니다. 다시 시도해 주세요.";
        }
    }

    private void showAppleCalendarExportProgress(AppleCalendarExportProgress progress)
    {
        if (progress == null)
        {
            throw new ArgumentNullException(nameof(progress));
        }

        switch (progress.Stage)
        {
            case EAppleCalendarExportProgressStage.CheckingCalendar:
                showPersistentExportStatus("Apple 캘린더 접근 권한과 기존 일정을 확인하는 중입니다.", EExportStatus.Information);
                break;
            case EAppleCalendarExportProgressStage.SavingEvents:
                showPersistentExportStatus("Apple 캘린더에 시간표를 저장하는 중입니다.", EExportStatus.Information);
                break;
            case EAppleCalendarExportProgressStage.Finalizing:
                showPersistentExportStatus("Apple 캘린더 내보내기를 마무리하는 중입니다.", EExportStatus.Information);
                break;
            case EAppleCalendarExportProgressStage.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(progress), progress.Stage, "Unknown Apple Calendar export progress stage.");
        }
    }

    private void showAppleCalendarExportFailure(Exception exception)
    {
        showCalendarExportFailure(exception, "Apple 캘린더로 내보내지 못했습니다. 다시 시도해 주세요.");
    }
}
