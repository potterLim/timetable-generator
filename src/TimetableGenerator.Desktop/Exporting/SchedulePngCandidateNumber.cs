using System;

namespace TimetableGenerator.Desktop.Exporting;

internal readonly record struct SchedulePngCandidateNumber
{
    public int Value { get; }

    public int Total { get; }

    public SchedulePngCandidateNumber(int value, int total)
    {
        if (total <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(total), total, "A PNG export batch must contain at least one candidate.");
        }

        if (value <= 0 || value > total)
        {
            throw new ArgumentOutOfRangeException(nameof(value), value, "The PNG candidate number must be within the batch range.");
        }

        Value = value;
        Total = total;
    }
}
