using System;

using Avalonia.Platform.Storage;

using TimetableGenerator.Desktop.Exporting;
using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PngSaveFilePickerOptionsTests
{
    [Fact]
    public void MacOSOptionsAvoidTheNativeFileTypeAccessoryView()
    {
        FilePickerSaveOptions options = ScheduleWorkspaceView.createPngSaveOptions(new PlanName("PNG 저장 테스트"), true);

        Assert.Equal("png", options.DefaultExtension);
        Assert.EndsWith(".png", options.SuggestedFileName);
        Assert.Null(options.FileTypeChoices);
        Assert.Null(options.SuggestedFileType);
    }

    [Fact]
    public void OtherPlatformsKeepTheExplicitPngFileType()
    {
        FilePickerSaveOptions options = ScheduleWorkspaceView.createPngSaveOptions(new PlanName("PNG 저장 테스트"), false);

        Assert.NotNull(options.FileTypeChoices);
        FilePickerFileType fileType = Assert.Single(options.FileTypeChoices);
        Assert.Equal("PNG 이미지", fileType.Name);
        Assert.Equal(new string[] { "*.png" }, fileType.Patterns);
        Assert.Equal(new string[] { "image/png" }, fileType.MimeTypes);
        Assert.Equal(new string[] { "public.png" }, fileType.AppleUniformTypeIdentifiers);
        Assert.Same(fileType, options.SuggestedFileType);
    }

    [Theory]
    [InlineData("시간표.png", true)]
    [InlineData("시간표.PNG", true)]
    [InlineData("시간표", false)]
    [InlineData("시간표.jpg", false)]
    public void DestinationFileNameMustKeepThePngExtension(string fileName, bool expectedResult)
    {
        Assert.Equal(expectedResult, ScheduleWorkspaceView.hasPngFileNameExtension(fileName));
    }

    [Fact]
    public void BatchFailureMessageReportsSuccessAndFailureCounts()
    {
        SchedulePngBatchExportException exception = new SchedulePngBatchExportException(
            2,
            1,
            new Exception[]
            {
                new InvalidOperationException("synthetic failure"),
            });

        Assert.Equal("가능한 시간표 2개 저장에 성공하고 1개 저장에 실패했습니다. " + "완성된 폴더는 만들지 않았습니다. 다시 시도해 주세요.", ScheduleWorkspaceView.formatPngBatchFailureMessage(exception));
    }
}
