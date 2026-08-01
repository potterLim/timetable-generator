using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;

using Avalonia;
using Avalonia.Media.Imaging;
using Avalonia.Platform;

using TimetableGenerator.Desktop.Presentation.Models;
using TimetableGenerator.Domain.Catalogs;
using TimetableGenerator.Domain.Planning;
using TimetableGenerator.Domain.Scheduling;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Exporting;

public sealed partial class SchedulePngBatchWriterTests
{
    private static string createTemporaryDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "TimetableGeneratorTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static ScheduleBoardPresentation createCandidatePresentation(
        PlanName planName,
        string title,
        EDay day,
        ScheduleTime start,
        ScheduleTime end)
    {
        DailyTimeRange dailyTimeRange = new DailyTimeRange(start, end);
        WeeklyTimeRange weeklyTimeRange = new WeeklyTimeRange(day, dailyTimeRange);
        PersonalSchedule personalSchedule = new PersonalSchedule(PersonalScheduleId.CreateNew(), new PersonalScheduleTitle(title), new WeeklyTimeRange[] { weeklyTimeRange }, PersonalScheduleDetails.CreateEmpty());
        PersonalScheduleEntry entry = new PersonalScheduleEntry(personalSchedule, weeklyTimeRange);
        return new ScheduleBoardPresentation(new ScheduleRecommendation(new ScheduleEntry[] { entry }), planName, new InstitutionName("한동대학교"), AcademicTerm.Parse("2026-2"));
    }

    private static void assertPngContainsRenderedBoard(string filePath)
    {
        using (FileStream stream = File.OpenRead(filePath))
        using (Bitmap bitmap = new Bitmap(stream))
        using (WriteableBitmap pixelCopy = new WriteableBitmap(bitmap.PixelSize, new Vector(96.0, 96.0), PixelFormat.Bgra8888, AlphaFormat.Premul))
        using (ILockedFramebuffer framebuffer = pixelCopy.Lock())
        {
            bitmap.CopyPixels(framebuffer);
            HashSet<int> sampledColors = new HashSet<int>();
            int horizontalStep = Math.Max(1, bitmap.PixelSize.Width / 96);
            int verticalStep = Math.Max(1, bitmap.PixelSize.Height / 96);
            for (int y = 0; y < bitmap.PixelSize.Height; y += verticalStep)
            {
                for (int x = 0; x < bitmap.PixelSize.Width; x += horizontalStep)
                {
                    int pixelOffset = (y * framebuffer.RowBytes) + (x * 4);
                    sampledColors.Add(Marshal.ReadInt32(framebuffer.Address, pixelOffset));
                }
            }

            Assert.True(sampledColors.Count >= 4, "The exported PNG contained only a flat background: " + filePath);
        }
    }
}
