using System;

namespace TimetableGenerator.Desktop.Exporting;

public sealed class PngExportScale
{
    private const double MAXIMUM_MULTIPLIER = 4.0;
    private const double MINIMUM_MULTIPLIER = 1.0;
    private const double PRODUCT_QUALITY_MULTIPLIER = 2.0;

    public static readonly PngExportScale PRODUCT_QUALITY = new PngExportScale(PRODUCT_QUALITY_MULTIPLIER);

    public double Multiplier
    {
        get;
        private init;
    }

    private PngExportScale(double multiplier)
    {
        Multiplier = multiplier;
    }

    public static PngExportScale Create(double multiplier)
    {
        if (double.IsFinite(multiplier) == false ||
            multiplier < MINIMUM_MULTIPLIER ||
            multiplier > MAXIMUM_MULTIPLIER)
        {
            throw new ArgumentOutOfRangeException(nameof(multiplier), multiplier, $"PNG export scale must be between {MINIMUM_MULTIPLIER} and {MAXIMUM_MULTIPLIER}.");
        }

        return new PngExportScale(multiplier);
    }
}
