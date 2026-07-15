using System;

namespace TimetableGenerator.UI.Product;

internal sealed class MessageStateContent
{
    internal EMessageStateKind Kind { get; }

    internal MessageStateTitle Title { get; }

    internal MessageStateDescription Description { get; }

    internal MessageStateDetail Detail { get; }

    internal MessageStateActionText PrimaryActionText { get; }

    internal MessageStateContent(
        EMessageStateKind kind,
        MessageStateTitle title,
        MessageStateDescription description,
        MessageStateDetail detail,
        MessageStateActionText primaryActionText)
    {
        if (Enum.IsDefined(kind) == false)
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (title == null)
        {
            throw new ArgumentNullException(nameof(title));
        }

        if (description == null)
        {
            throw new ArgumentNullException(nameof(description));
        }

        if (detail == null)
        {
            throw new ArgumentNullException(nameof(detail));
        }

        if (primaryActionText == null)
        {
            throw new ArgumentNullException(nameof(primaryActionText));
        }

        Kind = kind;
        Title = title;
        Description = description;
        Detail = detail;
        PrimaryActionText = primaryActionText;
    }
}
