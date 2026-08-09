using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Input;

using Avalonia.Controls;
using Avalonia.Platform.Storage;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Presentation;
using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Desktop.Presentation.ViewModels;
using TimetableGenerator.Domain.Planning;

namespace TimetableGenerator.Desktop.Views;

internal sealed partial class ScheduleWorkspaceView
{
    private readonly IControlPngExporter mPngExporter;

    private readonly AsyncDelegateCommand mExportPngCommand;

    private readonly AsyncDelegateCommand mExportAllPngCommand;

    public ICommand ExportPngCommand
    {
        get
        {
            return mExportPngCommand;
        }
    }

    public ICommand ExportAllPngCommand
    {
        get
        {
            return mExportAllPngCommand;
        }
    }

    private async Task exportPngAsync()
    {
        if (tryBeginExportOperation() == false)
        {
            return;
        }

        try
        {
            CancellationToken cancellationToken = getActiveExportCancellationToken();
            cancellationToken.ThrowIfCancellationRequested();
            PlannerWorkspaceViewModel workspace = getRequiredWorkspace();
            ScheduleBoardPresentation? exportPresentationOrNull = workspace.DisplayedScheduleBoard;
            if (exportPresentationOrNull == null)
            {
                throw new InvalidOperationException("PNG export requires an active timetable presentation.");
            }

            Canvas pngExportHost = getRequiredPngExportHost();
            TopLevel topLevel = getRequiredExportTopLevel();
            IStorageFile? destinationFileOrNull = await topLevel.StorageProvider.SaveFilePickerAsync(createPngSaveOptions(exportPresentationOrNull.PlanName));
            if (destinationFileOrNull == null)
            {
                return;
            }

            using (destinationFileOrNull)
            {
                if (hasPngFileNameExtension(destinationFileOrNull.Name) == false)
                {
                    showPersistentExportStatus("파일 이름을 .png로 끝내 주세요.", EExportStatus.Failure);
                    return;
                }

                cancellationToken.ThrowIfCancellationRequested();
                showPersistentExportStatus("현재 시간표 PNG를 저장하는 중입니다.", EExportStatus.Information);
                using (ScheduleBoardPngExportSnapshot snapshot = ScheduleBoardPngExportSnapshot.create(pngExportHost, exportPresentationOrNull))
                using (MemoryStream encodedPngStream = new MemoryStream())
                {
                    await exportSnapshotAsync(snapshot, encodedPngStream, cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    encodedPngStream.Position = 0;
                    using (Stream destinationStream = await destinationFileOrNull.OpenWriteAsync())
                    {
                        await encodedPngStream.CopyToAsync(destinationStream);
                        await destinationStream.FlushAsync(CancellationToken.None);
                    }
                }
            }

            showTransientExportStatus("PNG 이미지로 저장했습니다.", EExportStatus.Success);
        }
        finally
        {
            completeExportOperation();
        }
    }

    private async Task exportAllPngAsync()
    {
        if (tryBeginExportOperation() == false)
        {
            return;
        }

        try
        {
            CancellationToken cancellationToken = getActiveExportCancellationToken();
            cancellationToken.ThrowIfCancellationRequested();
            PlannerWorkspaceViewModel workspace = getRequiredWorkspace();
            if (workspace.CanExportAllPngCandidates == false)
            {
                return;
            }

            SchedulePngExportBatch exportBatch = new SchedulePngExportBatch(workspace.PngExportCandidates);

            TopLevel topLevel = getRequiredExportTopLevel();
            System.Collections.Generic.IReadOnlyList<IStorageFolder> selectedFolders = await topLevel.StorageProvider.OpenFolderPickerAsync(createPngBatchFolderPickerOptions());
            if (selectedFolders.Count == 0)
            {
                return;
            }

            disposeUnselectedFolders(selectedFolders);
            using (IStorageFolder selectedFolder = selectedFolders[0])
            {
                cancellationToken.ThrowIfCancellationRequested();
                showPersistentExportStatus("모든 가능한 시간표 PNG를 저장하는 중입니다.", EExportStatus.Information);
                string? parentDirectoryPathOrNull = selectedFolder.TryGetLocalPath();
                if (parentDirectoryPathOrNull == null)
                {
                    throw new NotSupportedException("Batch PNG export requires a local desktop folder.");
                }

                using (SchedulePngBatchDirectory batchDirectory = SchedulePngBatchDirectoryAllocator.createStaging(parentDirectoryPathOrNull, cancellationToken))
                {
                    SchedulePngBatchWriter writer = new SchedulePngBatchWriter(mPngExporter);
                    await writer.exportAsync(
                        exportBatch,
                        batchDirectory,
                        getRequiredPngExportHost(),
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    batchDirectory.commitAsUniqueBatch(exportBatch.PlanName, cancellationToken);
                }
            }

            showTransientExportStatus("가능한 시간표 " + exportBatch.Candidates.Count + "개를 PNG로 저장했습니다.", EExportStatus.Success);
        }
        finally
        {
            completeExportOperation();
        }
    }

    private async Task exportSnapshotAsync(ScheduleBoardPngExportSnapshot snapshot, Stream destinationStream, CancellationToken cancellationToken)
    {
        await mPngExporter.ExportControlAsync(snapshot.Surface, destinationStream, cancellationToken);
        await destinationStream.FlushAsync(cancellationToken);
    }

    private TopLevel getRequiredExportTopLevel()
    {
        TopLevel? topLevelOrNull = TopLevel.GetTopLevel(this);
        if (topLevelOrNull == null)
        {
            throw new InvalidOperationException("The schedule export view is not attached to a product window.");
        }

        return topLevelOrNull;
    }

    private ScheduleBoardView getRequiredScheduleBoard()
    {
        ScheduleBoardView? scheduleBoardOrNull = this.FindControl<ScheduleBoardView>("ScheduleBoard");
        if (scheduleBoardOrNull == null)
        {
            throw new InvalidOperationException("The schedule board export surface could not be prepared.");
        }

        return scheduleBoardOrNull;
    }

    private Canvas getRequiredPngExportHost()
    {
        Canvas? pngExportHostOrNull = this.FindControl<Canvas>("PngExportHost");
        if (pngExportHostOrNull == null)
        {
            throw new InvalidOperationException("The PNG export host could not be prepared.");
        }

        return pngExportHostOrNull;
    }

    private static FilePickerSaveOptions createPngSaveOptions(PlanName planName)
    {
        return createPngSaveOptions(planName, OperatingSystem.IsMacOS());
    }

    internal static FilePickerSaveOptions createPngSaveOptions(PlanName planName, bool isMacOS)
    {
        ArgumentNullException.ThrowIfNull(planName);
        FilePickerSaveOptions options = new FilePickerSaveOptions();
        options.Title = "시간표를 PNG 이미지로 저장";
        options.DefaultExtension = "png";
        options.ShowOverwritePrompt = true;
        options.SuggestedFileName = SchedulePngFileNameFactory.Create(planName);

        // Avalonia 12.1.0 leaves its NSSavePanel file-type accessory view active
        // after dismissal. The suggested name and post-pick validation retain the
        // PNG contract without installing that native view on macOS.
        if (isMacOS == false)
        {
            FilePickerFileType pngFileType = new FilePickerFileType("PNG 이미지");
            pngFileType.Patterns = new string[] { "*.png" };
            pngFileType.MimeTypes = new string[] { "image/png" };
            pngFileType.AppleUniformTypeIdentifiers = new string[] { "public.png" };
            options.FileTypeChoices = new FilePickerFileType[] { pngFileType };
            options.SuggestedFileType = pngFileType;
        }

        return options;
    }

    internal static bool hasPngFileNameExtension(string fileName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fileName);
        return string.Equals(Path.GetExtension(fileName), ".png", StringComparison.OrdinalIgnoreCase);
    }

    private static FolderPickerOpenOptions createPngBatchFolderPickerOptions()
    {
        FolderPickerOpenOptions options = new FolderPickerOpenOptions();
        options.Title = "가능한 시간표를 저장할 폴더 선택";
        options.AllowMultiple = false;
        return options;
    }

    private static void disposeUnselectedFolders(System.Collections.Generic.IReadOnlyList<IStorageFolder> folders)
    {
        for (int folderIndex = 1; folderIndex < folders.Count; ++folderIndex)
        {
            folders[folderIndex].Dispose();
        }
    }

    private void showPngExportFailure(Exception exception)
    {
        if (exception is SchedulePngBatchExportException batchException)
        {
            showPersistentExportStatus(formatPngBatchFailureMessage(batchException), EExportStatus.Failure);
            return;
        }

        showExportFailure(exception, "PNG 이미지를 저장하지 못했습니다. 다시 시도해 주세요.");
    }

    internal static string formatPngBatchFailureMessage(SchedulePngBatchExportException exception)
    {
        ArgumentNullException.ThrowIfNull(exception);
        return "가능한 시간표 " + exception.SuccessfulCount + "개 저장에 성공하고 " + exception.FailedCount + "개 저장에 실패했습니다. " + "완성된 폴더는 만들지 않았습니다. 다시 시도해 주세요.";
    }
}
