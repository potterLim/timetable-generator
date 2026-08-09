using System;
using System.Threading;
using System.Threading.Tasks;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Exporting.Calendar;
using TimetableGenerator.Desktop.Integrations.GoogleCalendar;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView
{
    private async Task exportGoogleCalendarAsync()
    {
        if (tryBeginExportOperation() == false)
        {
            return;
        }

        try
        {
            CancellationToken cancellationToken = getActiveExportCancellationToken();
            cancellationToken.ThrowIfCancellationRequested();
            CalendarExportDocument document = createCalendarExportDocument(ECalendarExportProvider.Google);
            GoogleCalendarExportPlan exportPlan = GoogleCalendarExportPlan.CreateFromDocument(document);
            showPersistentExportStatus("Google 캘린더로 내보내는 중입니다.", EExportStatus.Information);
            GoogleCalendarExportResult result = await mGoogleCalendarExporter.ExportAsync(exportPlan, this, cancellationToken);
            showGoogleCalendarExportResult(result);
            if (result.Status == EGoogleCalendarExportStatus.Success)
            {
                _ = mGoogleCalendarWebNavigator.TryOpen();
            }
        }
        finally
        {
            completeExportOperation();
        }
    }

    private void showGoogleCalendarExportResult(GoogleCalendarExportResult result)
    {
        if (result == null)
        {
            throw new ArgumentNullException(nameof(result));
        }

        switch (result.Status)
        {
            case EGoogleCalendarExportStatus.Success:
                showTransientExportStatus("Google 캘린더로 내보냈습니다.", EExportStatus.Success);
                break;
            case EGoogleCalendarExportStatus.NotConfigured:
                showTransientExportStatus("Google 캘린더 연결을 아직 사용할 수 없습니다.", EExportStatus.Information);
                break;
            case EGoogleCalendarExportStatus.AuthenticationCancelled:
            case EGoogleCalendarExportStatus.Cancelled:
                clearExportStatus();
                break;
            case EGoogleCalendarExportStatus.AuthenticationFailed:
                if (string.Equals(result.DiagnosticCodeOrNull, "authorization_timeout", StringComparison.Ordinal))
                {
                    showPersistentExportStatus("Google 로그인 시간이 만료되었습니다. 다시 시도해 주세요.", EExportStatus.Failure);
                }
                else
                {
                    showPersistentExportStatus("Google 캘린더 연결을 완료하지 못했습니다.", EExportStatus.Failure);
                }

                break;
            case EGoogleCalendarExportStatus.AccessDenied:
                showPersistentExportStatus("Google 캘린더 권한을 확인해 주세요.", EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.NetworkFailed:
                showPersistentExportStatus("Google 캘린더에 연결하지 못했습니다. 네트워크를 확인해 주세요.", EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.Failed:
                showPersistentExportStatus("Google 캘린더에 반영하지 못했습니다. 다시 시도해 주세요.", EExportStatus.Failure);
                break;
            case EGoogleCalendarExportStatus.None:
            default:
                throw new ArgumentOutOfRangeException(nameof(result), result.Status, "Unknown Google Calendar export status.");
        }
    }

    private void showGoogleCalendarExportFailure(Exception exception)
    {
        showCalendarExportFailure(exception, "Google 캘린더에 반영하지 못했습니다. 다시 시도해 주세요.");
    }
}
