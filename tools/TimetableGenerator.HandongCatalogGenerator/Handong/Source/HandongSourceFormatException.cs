using System;

namespace TimetableGenerator.HandongCatalogGenerator.Handong.Source;

internal sealed class HandongSourceFormatException : Exception
{
    public HandongSourceFormatException(string message)
        : base(message)
    {
    }

    public HandongSourceFormatException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
