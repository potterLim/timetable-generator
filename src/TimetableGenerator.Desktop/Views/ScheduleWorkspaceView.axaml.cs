using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;

using FluentIcons.Avalonia;
using FluentIcons.Common;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView : UserControl
{
    private static readonly TimeSpan EXPORT_STATUS_DURATION =
        TimeSpan.FromSeconds(3.5);

    private readonly IControlPngExporter mPngExporter;

    private readonly AsyncDelegateCommand mExportCommand;

    private readonly DelegateCommand mToggleSchedulePresentationCommand;

    private readonly ParameterizedCommand<PersonalScheduleId>
        mEditPersonalScheduleCommand;

    private readonly CancellationTokenSource mLifetimeCancellationSource;

    private readonly DispatcherTimer mExportStatusTimer;

    private EScheduleWorkspacePresentationMode mPresentationMode;

    public ICommand ExportCommand
    {
        get
        {
            return mExportCommand;
        }
    }

    public ICommand EditPersonalScheduleCommand
    {
        get
        {
            return mEditPersonalScheduleCommand;
        }
    }

    public ICommand ToggleSchedulePresentationCommand
    {
        get
        {
            return mToggleSchedulePresentationCommand;
        }
    }

    public ScheduleWorkspaceView()
    {
        mPngExporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);
        mLifetimeCancellationSource = new CancellationTokenSource();
        mExportStatusTimer = new DispatcherTimer();
        mExportStatusTimer.Interval = EXPORT_STATUS_DURATION;
        mExportStatusTimer.Tick += onExportStatusTimerTick;
        mPresentationMode = EScheduleWorkspacePresentationMode.Board;
        mExportCommand = new AsyncDelegateCommand(exportScheduleAsync, showExportFailure);
        mToggleSchedulePresentationCommand = new DelegateCommand(
            toggleSchedulePresentation);
        mEditPersonalScheduleCommand =
            new ParameterizedCommand<PersonalScheduleId>(
                beginEditPersonalSchedule);
        AvaloniaXamlLoader.Load(this);
        DetachedFromVisualTree += onDetachedFromVisualTree;
    }

    private void toggleSchedulePresentation()
    {
        EScheduleWorkspacePresentationMode nextMode =
            mPresentationMode == EScheduleWorkspacePresentationMode.Board
                ? EScheduleWorkspacePresentationMode.List
                : EScheduleWorkspacePresentationMode.Board;
        applyPresentationMode(nextMode);
    }

    private void applyPresentationMode(
        EScheduleWorkspacePresentationMode presentationMode)
    {
        ScheduleBoardView? scheduleBoardOrNull =
            this.FindControl<ScheduleBoardView>("ScheduleBoard");
        Border? scheduleListOrNull =
            this.FindControl<Border>("ScheduleListContainer");
        Button? modeButtonOrNull =
            this.FindControl<Button>("ScheduleViewModeButton");
        FluentIcon? modeIconOrNull =
            this.FindControl<FluentIcon>("ScheduleViewModeIcon");
        TextBlock? modeTextOrNull =
            this.FindControl<TextBlock>("ScheduleViewModeText");
        if (scheduleBoardOrNull == null
            || scheduleListOrNull == null
            || modeButtonOrNull == null
            || modeIconOrNull == null
            || modeTextOrNull == null)
        {
            throw new InvalidOperationException(
                "Schedule presentation controls are unavailable.");
        }

        bool isListMode =
            presentationMode == EScheduleWorkspacePresentationMode.List;
        scheduleBoardOrNull.IsVisible = isListMode == false;
        scheduleListOrNull.IsVisible = isListMode;
        modeIconOrNull.Icon = isListMode ? Icon.CalendarWeekStart : Icon.List;
        modeTextOrNull.Text = isListMode ? "시간표 보기" : "목록 보기";
        string automationName = isListMode
            ? "시간표를 주간 표로 보기"
            : "시간표를 목록으로 보기";
        string toolTip = isListMode ? "시간표로 보기" : "목록으로 보기";
        Avalonia.Automation.AutomationProperties.SetName(
            modeButtonOrNull,
            automationName);
        ToolTip.SetTip(modeButtonOrNull, toolTip);
        mPresentationMode = presentationMode;
    }

    private void beginEditPersonalSchedule(PersonalScheduleId scheduleId)
    {
        PlannerWorkspaceViewModel? workspaceOrNull =
            DataContext as PlannerWorkspaceViewModel;
        if (workspaceOrNull == null)
        {
            throw new InvalidOperationException(
                "Personal schedule editing requires a planning workspace.");
        }

        workspaceOrNull.BeginEditPersonalScheduleCommand.Execute(scheduleId);
    }

    private async Task exportScheduleAsync()
    {
        clearExportStatus();

        CancellationToken cancellationToken = mLifetimeCancellationSource.Token;
        cancellationToken.ThrowIfCancellationRequested();

        TopLevel? topLevelOrNull = TopLevel.GetTopLevel(this);
        if (topLevelOrNull == null)
        {
            throw new InvalidOperationException(
                "The schedule export view is not attached to a product window.");
        }

        FilePickerSaveOptions saveOptions = createSaveOptions();
        IStorageFile? destinationFileOrNull = await topLevelOrNull.StorageProvider
            .SaveFilePickerAsync(saveOptions);
        if (destinationFileOrNull == null)
        {
            return;
        }

        ScheduleBoardView? scheduleBoardOrNull = this.FindControl<ScheduleBoardView>(
            "ScheduleBoard");
        Canvas? pngExportHostOrNull = this.FindControl<Canvas>("PngExportHost");
        if (scheduleBoardOrNull == null || pngExportHostOrNull == null)
        {
            throw new InvalidOperationException(
                "The schedule board export surface could not be prepared.");
        }

        using (ScheduleBoardPngExportSnapshot snapshot =
            ScheduleBoardPngExportSnapshot.Create(
                pngExportHostOrNull,
                scheduleBoardOrNull))
        using (Stream destinationStream = await destinationFileOrNull.OpenWriteAsync())
        {
            await mPngExporter.ExportControlAsync(
                snapshot.Surface,
                destinationStream,
                cancellationToken);
            await destinationStream.FlushAsync(cancellationToken);
        }

        showExportStatus("PNG로 저장했습니다.", EPngExportStatus.Success);
    }

    private FilePickerSaveOptions createSaveOptions()
    {
        FilePickerSaveOptions options = new FilePickerSaveOptions();
        options.Title = "시간표를 PNG로 저장";
        options.DefaultExtension = "png";
        options.ShowOverwritePrompt = true;
        options.SuggestedFileName = createSuggestedFileName();
        FilePickerFileType pngFileType = new FilePickerFileType("PNG 이미지");
        pngFileType.Patterns = new string[] { "*.png" };
        pngFileType.MimeTypes = new string[] { "image/png" };
        pngFileType.AppleUniformTypeIdentifiers = new string[] { "public.png" };
        options.FileTypeChoices = new FilePickerFileType[] { pngFileType };
        options.SuggestedFileType = pngFileType;
        return options;
    }

    private string createSuggestedFileName()
    {
        PlannerWorkspaceViewModel? workspaceOrNull =
            DataContext as PlannerWorkspaceViewModel;
        if (workspaceOrNull == null)
        {
            return "시간표.png";
        }

        string fileName = workspaceOrNull.AcademicTermDisplayText
            + "-시간표-"
            + workspaceOrNull.ActivePlan.DisplayName;
        foreach (char invalidCharacter in Path.GetInvalidFileNameChars())
        {
            fileName = fileName.Replace(invalidCharacter, '-');
        }

        return fileName + ".png";
    }

    private void showExportFailure(Exception exception)
    {
        if (exception is OperationCanceledException)
        {
            return;
        }

        showExportStatus(
            "PNG를 저장하지 못했습니다. 다시 시도해 주세요.",
            EPngExportStatus.Failure);
    }

    private void clearExportStatus()
    {
        mExportStatusTimer.Stop();

        Border? statusToastOrNull = this.FindControl<Border>("ExportStatusToast");
        TextBlock? statusTextOrNull = this.FindControl<TextBlock>("ExportStatusText");
        if (statusToastOrNull == null || statusTextOrNull == null)
        {
            return;
        }

        statusTextOrNull.Text = string.Empty;
        statusTextOrNull.Classes.Set("error", false);
        statusTextOrNull.Classes.Set("success", false);
        statusToastOrNull.IsVisible = false;
        statusToastOrNull.Classes.Set("error", false);
        statusToastOrNull.Classes.Set("success", false);
    }

    private void showExportStatus(string message, EPngExportStatus status)
    {
        Border? statusToastOrNull = this.FindControl<Border>("ExportStatusToast");
        TextBlock? statusTextOrNull = this.FindControl<TextBlock>("ExportStatusText");
        FluentIcon? statusIconOrNull = this.FindControl<FluentIcon>(
            "ExportStatusIcon");
        if (statusToastOrNull == null
            || statusTextOrNull == null
            || statusIconOrNull == null)
        {
            return;
        }

        bool isFailure = status == EPngExportStatus.Failure;
        statusTextOrNull.Text = message;
        statusTextOrNull.Classes.Set("error", isFailure);
        statusTextOrNull.Classes.Set("success", isFailure == false);
        statusToastOrNull.Classes.Set("error", isFailure);
        statusToastOrNull.Classes.Set("success", isFailure == false);
        statusToastOrNull.IsVisible = true;
        statusIconOrNull.Icon = isFailure ? Icon.Warning : Icon.CheckmarkCircle;
        statusIconOrNull.Foreground = isFailure
            ? findBrush("ErrorBrush")
            : findBrush("SuccessBrush");
        mExportStatusTimer.Stop();
        mExportStatusTimer.Start();
    }

    private IBrush findBrush(string resourceKey)
    {
        object? resourceOrNull;
        bool hasResource = ResourceNodeExtensions.TryFindResource(
            this,
            resourceKey,
            ActualThemeVariant,
            out resourceOrNull);
        if (hasResource == false || resourceOrNull is not IBrush brush)
        {
            throw new InvalidOperationException(
                "Missing brush resource: " + resourceKey);
        }

        return brush;
    }

    private void onExportStatusTimerTick(
        object? senderOrNull,
        EventArgs eventArgs)
    {
        clearExportStatus();
    }

    private void onDetachedFromVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        DetachedFromVisualTree -= onDetachedFromVisualTree;
        mExportStatusTimer.Stop();
        mExportStatusTimer.Tick -= onExportStatusTimerTick;
        mLifetimeCancellationSource.Cancel();
        mLifetimeCancellationSource.Dispose();
    }
}
