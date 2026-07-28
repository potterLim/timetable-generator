using System;

using Avalonia;

using TimetableGenerator.Desktop.Presentation.Windowing;

using Xunit;

namespace TimetableGenerator.Desktop.Tests.Presentation.Windowing;

public sealed class InitialWindowPlacementPolicyTests
{
    [Theory]
    [InlineData(1.0)]
    [InlineData(1.25)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void CreatePlacementKeepsWindowInsideFullHdWorkingArea(double scaleValue)
    {
        PixelRect bounds = new PixelRect(0, 0, 1_920, 1_020);
        WindowWorkingArea workingArea = new WindowWorkingArea(bounds, new DisplayScale(scaleValue));

        InitialWindowPlacement placement = InitialWindowPlacementPolicy.CreatePlacement(workingArea);

        assertPlacementInvariants(placement, workingArea);
    }

    [Fact]
    public void CreatePlacementReducesEffectiveMinimumOnSmallDisplay()
    {
        PixelRect bounds = new PixelRect(0, 0, 800, 450);
        WindowWorkingArea workingArea = new WindowWorkingArea(bounds, new DisplayScale(2.0));

        InitialWindowPlacement placement = InitialWindowPlacementPolicy.CreatePlacement(workingArea);

        Assert.True(placement.EffectiveMinimumSize.Width < 900.0);
        Assert.True(placement.EffectiveMinimumSize.Height < 640.0);
        Assert.Equal(placement.EffectiveMinimumSize, placement.InitialSize);
        assertPlacementInvariants(placement, workingArea);
    }

    [Fact]
    public void CreatePlacementCentersWindowInOffsetWorkingArea()
    {
        PixelRect bounds = new PixelRect(-2_560, 120, 2_560, 1_400);
        WindowWorkingArea workingArea = new WindowWorkingArea(bounds, new DisplayScale(1.5));

        InitialWindowPlacement placement = InitialWindowPlacementPolicy.CreatePlacement(workingArea);

        assertPlacementInvariants(placement, workingArea);
    }

    private static void assertPlacementInvariants(InitialWindowPlacement placement, WindowWorkingArea workingArea)
    {
        Assert.True(placement.InitialSize.Width >= placement.EffectiveMinimumSize.Width);
        Assert.True(placement.InitialSize.Height >= placement.EffectiveMinimumSize.Height);

        int initialPixelWidth = (int)Math.Ceiling(placement.InitialSize.Width * workingArea.Scale.Value);
        int initialPixelHeight = (int)Math.Ceiling(placement.InitialSize.Height * workingArea.Scale.Value);
        int right = placement.Position.X + initialPixelWidth;
        int bottom = placement.Position.Y + initialPixelHeight;

        Assert.True(placement.Position.X >= workingArea.Bounds.X);
        Assert.True(placement.Position.Y >= workingArea.Bounds.Y);
        Assert.True(right <= workingArea.Bounds.Right);
        Assert.True(bottom <= workingArea.Bounds.Bottom);

        int leftSpace = placement.Position.X - workingArea.Bounds.X;
        int rightSpace = workingArea.Bounds.Right - right;
        int topSpace = placement.Position.Y - workingArea.Bounds.Y;
        int bottomSpace = workingArea.Bounds.Bottom - bottom;
        Assert.InRange(Math.Abs(leftSpace - rightSpace), 0, 1);
        Assert.InRange(Math.Abs(topSpace - bottomSpace), 0, 1);
    }
}
