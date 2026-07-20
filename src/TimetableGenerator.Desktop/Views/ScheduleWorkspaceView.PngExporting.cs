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
            CancellationToken cancellationToken =
                mLifetimeCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            PlannerWorkspaceViewModel workspace = getRequiredWorkspace();
            ScheduleBoardPresentation? exportPresentationOrNull =
                workspace.DisplayedScheduleBoard;
            if (exportPresentationOrNull == null)
            {
                throw new InvalidOperationException(
                    "PNG export requires an active timetable presentation.");
            }

            ScheduleBoardView scheduleBoard = getRequiredScheduleBoard();
            Canvas pngExportHost = getRequiredPngExportHost();
            TopLevel topLevel = getRequiredExportTopLevel();
            IStorageFile? destinationFileOrNull =
                await topLevel.StorageProvider.SaveFilePickerAsync(
                    createPngSaveOptions(exportPresentationOrNull.PlanName));
            if (destinationFileOrNull == null)
            {
                return;
            }

            cancellationToken.ThrowIfCancellationRequested();
            using (destinationFileOrNull)
            using (ScheduleBoardPngExportSnapshot snapshot =
                ScheduleBoardPngExportSnapshot.create(
                    pngExportHost,
                    exportPresentationOrNull,
                    scheduleBoard))
            using (Stream destinationStream =
                await destinationFileOrNull.OpenWriteAsync())
            {
                await exportSnapshotAsync(
                    snapshot,
                    destinationStream,
                    cancellationToken);
            }

            showTransientExportStatus(
                "PNG 이미지로 저장했습니다.",
                EExportStatus.Success);
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
            CancellationToken cancellationToken =
                mLifetimeCancellationSource.Token;
            cancellationToken.ThrowIfCancellationRequested();
            PlannerWorkspaceViewModel workspace = getRequiredWorkspace();
            if (workspace.CanExportAllPngCandidates == false)
            {
                return;
            }

            SchedulePngExportBatch exportBatch =
                new SchedulePngExportBatch(workspace.PngExportCandidates);

            TopLevel topLevel = getRequiredExportTopLevel();
            System.Collections.Generic.IReadOnlyList<IStorageFolder>
                selectedFolders =
                await topLevel.StorageProvider.OpenFolderPickerAsync(
                    createPngBatchFolderPickerOptions());
            if (selectedFolders.Count == 0)
            {
                return;
            }

            disposeUnselectedFolders(selectedFolders);
            using (IStorageFolder selectedFolder = selectedFolders[0])
            {
                cancellationToken.ThrowIfCancellationRequested();
                string? parentDirectoryPathOrNull =
                    selectedFolder.TryGetLocalPath();
                if (parentDirectoryPathOrNull == null)
                {
                    throw new NotSupportedException(
                        "Batch PNG export requires a local desktop folder.");
                }

                using (SchedulePngBatchDirectory batchDirectory =
                    SchedulePngBatchDirectoryAllocator.createUnique(
                        parentDirectoryPathOrNull,
                        exportBatch.PlanName,
                        cancellationToken))
                {
                    SchedulePngBatchWriter writer =
                        new SchedulePngBatchWriter(mPngExporter);
                    await writer.exportAsync(
                        exportBatch,
                        batchDirectory,
                        getRequiredScheduleBoard(),
                        getRequiredPngExportHost(),
                        cancellationToken);
                    cancellationToken.ThrowIfCancellationRequested();
                    batchDirectory.commit();
                }
            }

            showTransientExportStatus(
                exportBatch.Candidates.Count
                    + "개의 시간표 이미지를 저장했습니다.",
                EExportStatus.Success);
        }
        finally
        {
            completeExportOperation();
        }
    }

    private async Task exportSnapshotAsync(
        ScheduleBoardPngExportSnapshot snapshot,
        Stream destinationStream,
        CancellationToken cancellationToken)
    {
        await mPngExporter.ExportControlAsync(
            snapshot.Surface,
            destinationStream,
            cancellationToken);
        await destinationStream.FlushAsync(cancellationToken);
    }

    private TopLevel getRequiredExportTopLevel()
    {
        TopLevel? topLevelOrNull = TopLevel.GetTopLevel(this);
        if (topLevelOrNull == null)
        {
            throw new InvalidOperationException(
                "The schedule export view is not attached to a product window.");
        }

        return topLevelOrNull;
    }

    private ScheduleBoardView getRequiredScheduleBoard()
    {
        ScheduleBoardView? scheduleBoardOrNull =
            this.FindControl<ScheduleBoardView>("ScheduleBoard");
        if (scheduleBoardOrNull == null)
        {
            throw new InvalidOperationException(
                "The schedule board export surface could not be prepared.");
        }

        return scheduleBoardOrNull;
    }

    private Canvas getRequiredPngExportHost()
    {
        Canvas? pngExportHostOrNull =
            this.FindControl<Canvas>("PngExportHost");
        if (pngExportHostOrNull == null)
        {
            throw new InvalidOperationException(
                "The PNG export host could not be prepared.");
        }

        return pngExportHostOrNull;
    }

    private FilePickerSaveOptions createPngSaveOptions(PlanName planName)
    {
        ArgumentNullException.ThrowIfNull(planName);
        FilePickerSaveOptions options = new FilePickerSaveOptions();
        options.Title = "시간표를 PNG 이미지로 저장";
        options.DefaultExtension = "png";
        options.ShowOverwritePrompt = true;
        options.SuggestedFileName = SchedulePngFileNameFactory.Create(planName);
        FilePickerFileType pngFileType = new FilePickerFileType("PNG 이미지");
        pngFileType.Patterns = new string[] { "*.png" };
        pngFileType.MimeTypes = new string[] { "image/png" };
        pngFileType.AppleUniformTypeIdentifiers = new string[] { "public.png" };
        options.FileTypeChoices = new FilePickerFileType[] { pngFileType };
        options.SuggestedFileType = pngFileType;
        return options;
    }

    private static FolderPickerOpenOptions createPngBatchFolderPickerOptions()
    {
        FolderPickerOpenOptions options = new FolderPickerOpenOptions();
        options.Title = "모든 후보 PNG 저장 위치 선택";
        options.AllowMultiple = false;
        return options;
    }

    private static void disposeUnselectedFolders(
        System.Collections.Generic.IReadOnlyList<IStorageFolder> folders)
    {
        for (int folderIndex = 1;
            folderIndex < folders.Count;
            ++folderIndex)
        {
            folders[folderIndex].Dispose();
        }
    }

    private void showPngExportFailure(Exception exception)
    {
        showExportFailure(
            exception,
            "PNG 이미지를 저장하지 못했습니다. 다시 시도해 주세요.");
    }
}
