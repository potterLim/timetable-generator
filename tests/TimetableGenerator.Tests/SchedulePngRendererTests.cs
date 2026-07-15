using System;
using System.Drawing;
using System.IO;
using System.Threading;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using TimetableGenerator.Core.Domain;
using TimetableGenerator.Infrastructure.Exporting;
using TimetableGenerator.Presentation.Schedules;

namespace TimetableGeneratorCore.Tests;

[TestClass]
public sealed class SchedulePngRendererTests
{
    [TestMethod]
    public void RenderProducesAReadableDeterministicPngAtTheProductCanvasSize()
    {
        ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
            "자료구조",
            EDay.Monday,
            1);
        SchedulePngRenderer renderer = new SchedulePngRenderer();

        RenderedSchedulePng firstPng = renderer.Render(scheduleGrid);
        RenderedSchedulePng secondPng = renderer.Render(scheduleGrid);
        byte[] firstContent = firstPng.GetContentCopy();
        byte[] secondContent = secondPng.GetContentCopy();

        Assert.AreEqual(1_800, firstPng.PixelSize.Width);
        Assert.AreEqual(1_484, firstPng.PixelSize.Height);
        Assert.IsGreaterThan(10_000, firstPng.ContentLength);
        CollectionAssert.AreEqual(firstContent, secondContent);
        assertPngSignature(firstContent);

        using (MemoryStream imageStream = new MemoryStream(firstContent, false))
        {
            using (Image image = Image.FromStream(imageStream))
            {
                Assert.AreEqual(firstPng.PixelSize.Width, image.Width);
                Assert.AreEqual(firstPng.PixelSize.Height, image.Height);
            }
        }
    }

    [TestMethod]
    public void RenderExpandsTheCanvasForLaterPeriods()
    {
        ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
            "야간 세미나",
            EDay.Friday,
            10);
        SchedulePngRenderer renderer = new SchedulePngRenderer();

        RenderedSchedulePng renderedPng = renderer.Render(scheduleGrid);

        Assert.AreEqual(1_740, renderedPng.PixelSize.Height);
    }

    [TestMethod]
    public void RenderReturnsDefensiveContentCopies()
    {
        ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
            "컴퓨터 구조",
            EDay.Wednesday,
            2);
        SchedulePngRenderer renderer = new SchedulePngRenderer();
        RenderedSchedulePng renderedPng = renderer.Render(scheduleGrid);

        byte[] firstCopy = renderedPng.GetContentCopy();
        byte originalFirstByte = firstCopy[0];
        firstCopy[0] = 0;
        byte[] secondCopy = renderedPng.GetContentCopy();

        Assert.AreEqual(originalFirstByte, secondCopy[0]);
        Assert.AreNotSame(firstCopy, secondCopy);
    }

    [TestMethod]
    public void RenderHonorsAPreCanceledToken()
    {
        ScheduleGridViewModel scheduleGrid = SchedulePngTestData.createScheduleGrid(
            "운영체제",
            EDay.Tuesday,
            2);
        SchedulePngRenderer renderer = new SchedulePngRenderer();
        using (CancellationTokenSource cancellationTokenSource = new CancellationTokenSource())
        {
            cancellationTokenSource.Cancel();

            Assert.ThrowsExactly<OperationCanceledException>(
                () => renderer.Render(scheduleGrid, cancellationTokenSource.Token));
        }
    }

    private static void assertPngSignature(byte[] content)
    {
        byte[] expectedSignature = new byte[]
        {
            0x89,
            0x50,
            0x4E,
            0x47,
            0x0D,
            0x0A,
            0x1A,
            0x0A,
        };

        for (int byteIndex = 0; byteIndex < expectedSignature.Length; ++byteIndex)
        {
            Assert.AreEqual(expectedSignature[byteIndex], content[byteIndex]);
        }
    }
}
