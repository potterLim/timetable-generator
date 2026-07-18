using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Platform.Storage;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView : UserControl
{
    private readonly IControlPngExporter mPngExporter;

    private readonly AsyncDelegateCommand mExportCommand;

    private readonly ParameterizedCommand<PersonalScheduleId>
        mEditPersonalScheduleCommand;

    private readonly CancellationTokenSource mLifetimeCancellationSource;

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

    public ScheduleWorkspaceView()
    {
        mPngExporter = new AvaloniaControlPngExporter(PngExportScale.PRODUCT_QUALITY);
        mLifetimeCancellationSource = new CancellationTokenSource();
        mExportCommand = new AsyncDelegateCommand(exportScheduleAsync, showExportFailure);
        mEditPersonalScheduleCommand =
            new ParameterizedCommand<PersonalScheduleId>(
                beginEditPersonalSchedule);
        AvaloniaXamlLoader.Load(this);
        DetachedFromVisualTree += onDetachedFromVisualTree;
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
        TextBlock? statusTextOrNull = this.FindControl<TextBlock>("ExportStatusText");
        if (statusTextOrNull == null)
        {
            return;
        }

        statusTextOrNull.Text = string.Empty;
        statusTextOrNull.IsVisible = false;
        statusTextOrNull.Classes.Set("error", false);
        statusTextOrNull.Classes.Set("success", false);
    }

    private void showExportStatus(string message, EPngExportStatus status)
    {
        TextBlock? statusTextOrNull = this.FindControl<TextBlock>("ExportStatusText");
        if (statusTextOrNull == null)
        {
            return;
        }

        statusTextOrNull.Text = message;
        statusTextOrNull.IsVisible = true;
        statusTextOrNull.Classes.Set("error", status == EPngExportStatus.Failure);
        statusTextOrNull.Classes.Set("success", status == EPngExportStatus.Success);
    }

    private void onDetachedFromVisualTree(
        object? senderOrNull,
        VisualTreeAttachmentEventArgs eventArgs)
    {
        DetachedFromVisualTree -= onDetachedFromVisualTree;
        mLifetimeCancellationSource.Cancel();
        mLifetimeCancellationSource.Dispose();
    }
}
