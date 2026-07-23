using Avalonia.Platform.Storage;

using TimetableGenerator.Desktop.Views;
using TimetableGenerator.Domain.Planning;

using Xunit;

namespace TimetableGenerator.Desktop.Tests;

public sealed class PngSaveFilePickerOptionsTests
{
    [Fact]
    public void MacOSOptionsAvoidTheNativeFileTypeAccessoryView()
    {
        FilePickerSaveOptions options = ScheduleWorkspaceView.createPngSaveOptions(
            new PlanName("PNG 저장 테스트"),
            true);

        Assert.Equal("png", options.DefaultExtension);
        Assert.EndsWith(".png", options.SuggestedFileName);
        Assert.Null(options.FileTypeChoices);
        Assert.Null(options.SuggestedFileType);
    }

    [Fact]
    public void OtherPlatformsKeepTheExplicitPngFileType()
    {
        FilePickerSaveOptions options = ScheduleWorkspaceView.createPngSaveOptions(
            new PlanName("PNG 저장 테스트"),
            false);

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
    public void DestinationFileNameMustKeepThePngExtension(
        string fileName,
        bool expectedResult)
    {
        Assert.Equal(
            expectedResult,
            ScheduleWorkspaceView.hasPngFileNameExtension(fileName));
    }
}
