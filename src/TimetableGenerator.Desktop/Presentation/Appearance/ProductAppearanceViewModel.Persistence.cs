using System;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Threading;

using TimetableGenerator.Desktop.Product.Appearance;

namespace TimetableGenerator.Desktop.Presentation.Appearance;

internal sealed partial class ProductAppearanceViewModel
{
    private readonly IProductAppearanceSettingsStore mSettingsStore;

    private readonly object mPersistenceSync;

    private readonly DelegateCommand mRetryPersistenceCommand;

    private ProductAppearanceSettings? mPendingSettingsOrNull;

    private Task mPersistenceTask;

    private bool mIsPersistenceWorkerRunning;

    private string mPersistenceFailureMessage;

    public bool HasPersistenceFailure
    {
        get
        {
            return string.IsNullOrEmpty(mPersistenceFailureMessage) == false;
        }
    }

    public string PersistenceFailureMessage
    {
        get
        {
            return mPersistenceFailureMessage;
        }
    }

    public ICommand RetryPersistenceCommand
    {
        get
        {
            return mRetryPersistenceCommand;
        }
    }

    public async Task CompletePersistenceAsync()
    {
        while (true)
        {
            Task persistenceTask;
            lock (mPersistenceSync)
            {
                persistenceTask = mPersistenceTask;
            }

            await persistenceTask.ConfigureAwait(false);

            lock (mPersistenceSync)
            {
                bool isCurrentTaskComplete =
                    ReferenceEquals(persistenceTask, mPersistenceTask)
                    && mIsPersistenceWorkerRunning == false
                    && mPendingSettingsOrNull == null;
                if (isCurrentTaskComplete)
                {
                    return;
                }
            }
        }
    }

    private void retryPersistence()
    {
        schedulePersistence(mThemePreference);
    }

    private void schedulePersistence(
        EProductThemePreference themePreference)
    {
        ProductAppearanceSettings pendingSettings =
            new ProductAppearanceSettings(themePreference);
        lock (mPersistenceSync)
        {
            mPendingSettingsOrNull = pendingSettings;
            if (mIsPersistenceWorkerRunning)
            {
                return;
            }

            mIsPersistenceWorkerRunning = true;
            mPersistenceTask = Task.Run(persistPendingSettings);
        }
    }

    private void persistPendingSettings()
    {
        while (true)
        {
            ProductAppearanceSettings? pendingSettingsOrNull;
            lock (mPersistenceSync)
            {
                pendingSettingsOrNull = mPendingSettingsOrNull;
                mPendingSettingsOrNull = null;
                if (pendingSettingsOrNull == null)
                {
                    mIsPersistenceWorkerRunning = false;
                    return;
                }
            }

            ProductAppearanceSettingsException? failureOrNull = null;
            try
            {
                mSettingsStore.Save(pendingSettingsOrNull);
            }
            catch (ProductAppearanceSettingsException exception)
            {
                failureOrNull = exception;
                Trace.TraceWarning(
                    "The selected appearance preference could not be persisted: {0}",
                    exception);
            }

            ProductAppearanceSettings completedSettings = pendingSettingsOrNull;
            ProductAppearanceSettingsException? completedFailureOrNull =
                failureOrNull;
            Dispatcher.UIThread.Post(
                () => applyPersistenceResult(
                    completedSettings,
                    completedFailureOrNull),
                DispatcherPriority.Background);
        }
    }

    private void applyPersistenceResult(
        ProductAppearanceSettings completedSettings,
        ProductAppearanceSettingsException? failureOrNull)
    {
        if (completedSettings.ThemePreference != mThemePreference)
        {
            return;
        }

        if (failureOrNull == null)
        {
            clearPersistenceFailure();
            return;
        }

        mPersistenceFailureMessage =
            "화면 모드는 적용했지만 다음 실행을 위해 저장하지 못했습니다.";
        raisePropertyChanged(nameof(HasPersistenceFailure));
        raisePropertyChanged(nameof(PersistenceFailureMessage));
    }

    private void clearPersistenceFailure()
    {
        if (HasPersistenceFailure == false)
        {
            return;
        }

        mPersistenceFailureMessage = string.Empty;
        raisePropertyChanged(nameof(HasPersistenceFailure));
        raisePropertyChanged(nameof(PersistenceFailureMessage));
    }
}
