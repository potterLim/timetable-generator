using System;
using System.Diagnostics;

using TimetableGenerator.Application.Planning;
using TimetableGenerator.Desktop.Configuration;
using TimetableGenerator.Desktop.Product.Loading;
using TimetableGenerator.Infrastructure.Catalogs;
using TimetableGenerator.Infrastructure.Persistence;

namespace TimetableGenerator.Desktop.Product;

internal sealed partial class ProductShellViewModel
{
    private void showFailure(Exception exception)
    {
        Trace.TraceError("The product workspace could not be loaded: {0}", exception);
        mWorkspaceOrNull = null;
        mState = EProductShellState.Error;
        mStatusTitle = "과목 데이터를 불러오지 못했습니다";
        mStatusMessage = findFailureMessage(exception);
        raiseStatePropertiesChanged();
        raisePropertyChanged(nameof(WorkspaceOrNull));
        raisePropertyChanged(nameof(StatusTitle));
        raisePropertyChanged(nameof(StatusMessage));
    }

    private void showUnexpectedFailure(Exception exception)
    {
        showFailure(exception);
    }

    private static string findFailureMessage(Exception exception)
    {
        if (exception is CatalogSourceConfigurationException)
        {
            return "이 설치본에 과목 데이터 주소가 설정되지 않았습니다. 배포 설정을 확인한 뒤 다시 시도해 주세요.";
        }

        RemoteCatalogSynchronizationException? synchronizationExceptionOrNull = exception as RemoteCatalogSynchronizationException;
        if (synchronizationExceptionOrNull != null)
        {
            switch (synchronizationExceptionOrNull.FailureKind)
            {
                case ERemoteCatalogSynchronizationFailureKind.Network:
                    return "학교 과목 데이터 서버에 연결할 수 없습니다. 인터넷 연결을 확인한 뒤 다시 시도해 주세요.";
                case ERemoteCatalogSynchronizationFailureKind.LocalPersistence:
                    return "검증한 과목 데이터를 이 기기에 저장하지 못했습니다. 폴더 권한과 남은 용량을 확인해 주세요.";
                case ERemoteCatalogSynchronizationFailureKind.InvalidRemoteData:
                case ERemoteCatalogSynchronizationFailureKind.ResourceLimit:
                case ERemoteCatalogSynchronizationFailureKind.SecurityPolicy:
                    return "서버의 과목 데이터를 안전하게 검증할 수 없습니다. 기존 데이터는 변경하지 않았습니다.";
                default:
                    throw new ArgumentOutOfRangeException(
                        nameof(exception),
                        synchronizationExceptionOrNull.FailureKind,
                        "Unknown remote catalog synchronization failure kind.");
            }
        }

        if (exception is ProductWorkspaceCatalogCompatibilityException)
        {
            return "저장된 시간표를 현재 과목 데이터와 안전하게 연결할 수 없습니다. 기존 시간표는 변경하지 않았습니다.";
        }

        if (exception is PlanningWorkspaceConcurrencyException)
        {
            return "다른 앱 창에서 시간표가 변경되었습니다. 이 창을 닫고 다시 열어 최신 내용을 불러와 주세요.";
        }

        if (exception is CatalogCacheUpgradeRequiredException
            || exception is PlanningWorkspaceUpgradeRequiredException)
        {
            return "더 새로운 버전에서 저장한 데이터입니다. 앱을 업데이트한 뒤 다시 열어 주세요.";
        }

        if (exception is CatalogCachePersistenceException || exception is WorkspacePersistenceException)
        {
            return "이 기기의 저장 공간에 접근할 수 없습니다. 폴더 권한과 남은 용량을 확인한 뒤 다시 시도해 주세요.";
        }

        return "저장된 데이터가 손상되었거나 접근할 수 없습니다. 잠시 후 다시 시도해 주세요.";
    }
}
