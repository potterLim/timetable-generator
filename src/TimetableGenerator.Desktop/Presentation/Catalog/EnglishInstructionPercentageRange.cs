using System;
using System.Collections.Generic;

using TimetableGenerator.CatalogJson;

namespace TimetableGenerator.Desktop.Presentation.Catalog;

internal readonly record struct EnglishInstructionPercentageRange
{
    public EnglishInstructionPercentage Minimum { get; }

    public EnglishInstructionPercentage Maximum { get; }

    public bool IsUniform
    {
        get
        {
            return Minimum == Maximum;
        }
    }

    public string DisplayText
    {
        get
        {
            if (IsUniform)
            {
                return "영어 " + Minimum + "%";
            }

            return "영어 " + Minimum + "–" + Maximum + "%";
        }
    }

    public string AccessibleText
    {
        get
        {
            if (IsUniform)
            {
                return "영어 강의 비율 " + Minimum + "%";
            }

            return "영어 강의 비율 " + Minimum + "%에서 " + Maximum + "%";
        }
    }

    public EnglishInstructionPercentageRange(
        EnglishInstructionPercentage minimum,
        EnglishInstructionPercentage maximum)
    {
        if (minimum.Value > maximum.Value)
        {
            throw new ArgumentException(
                "The minimum English instruction percentage cannot exceed the maximum.",
                nameof(minimum));
        }

        Minimum = minimum;
        Maximum = maximum;
    }

    public static EnglishInstructionPercentageRange Create(
        IEnumerable<EnglishInstructionPercentage> percentages)
    {
        if (percentages == null)
        {
            throw new ArgumentNullException(nameof(percentages));
        }

        using (IEnumerator<EnglishInstructionPercentage> enumerator = percentages.GetEnumerator())
        {
            if (enumerator.MoveNext() == false)
            {
                throw new ArgumentException(
                    "An English instruction percentage range requires at least one value.",
                    nameof(percentages));
            }

            EnglishInstructionPercentage minimum = enumerator.Current;
            EnglishInstructionPercentage maximum = enumerator.Current;
            while (enumerator.MoveNext())
            {
                EnglishInstructionPercentage percentage = enumerator.Current;
                if (percentage.Value < minimum.Value)
                {
                    minimum = percentage;
                }

                if (percentage.Value > maximum.Value)
                {
                    maximum = percentage;
                }
            }

            return new EnglishInstructionPercentageRange(minimum, maximum);
        }
    }

    public static EnglishInstructionPercentageRange CreateUniform(
        EnglishInstructionPercentage percentage)
    {
        return new EnglishInstructionPercentageRange(percentage, percentage);
    }
}
