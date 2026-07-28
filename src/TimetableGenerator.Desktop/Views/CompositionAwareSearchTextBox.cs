using System;
using System.Diagnostics.CodeAnalysis;

using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Presenters;
using Avalonia.Controls.Primitives;
using Avalonia.Data;
using Avalonia.Threading;

namespace TimetableGenerator.Desktop.Views;

internal sealed class CompositionAwareSearchTextBox : TextBox
{
    [SuppressMessage("Style", "IDE1006:Naming Styles", Justification = "Avalonia requires the {PropertyName}Property field convention.")]
    public static readonly StyledProperty<string> QueryTextProperty = AvaloniaProperty.Register<CompositionAwareSearchTextBox, string>(nameof(QueryText), string.Empty, defaultBindingMode: BindingMode.TwoWay);

    private TextPresenter? mTextPresenterOrNull;

    private bool mIsApplyingQueryText;

    private bool mIsPublishingQueryText;

    private bool mIsPresenterSubscriptionActive;

    private int mDeferredPublicationGeneration;

    protected override Type StyleKeyOverride
    {
        get
        {
            return typeof(TextBox);
        }
    }

    public string QueryText
    {
        get
        {
            return GetValue(QueryTextProperty);
        }
        set
        {
            SetValue(QueryTextProperty, normalizeNullableText(value));
        }
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs eventArguments)
    {
        unsubscribeFromTextPresenter();
        base.OnApplyTemplate(eventArguments);

        mTextPresenterOrNull = eventArguments.NameScope.Get<TextPresenter>("PART_TextPresenter");
        subscribeToTextPresenter();
        publishVisibleQueryText();
    }

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs eventArguments)
    {
        base.OnAttachedToVisualTree(eventArguments);
        subscribeToTextPresenter();
        publishVisibleQueryText();
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs eventArguments)
    {
        cancelDeferredQueryPublication();
        unsubscribeFromTextPresenter();
        base.OnDetachedFromVisualTree(eventArguments);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == QueryTextProperty)
        {
            applyQueryText(change.GetNewValue<string>());
            return;
        }

        if (change.Property == TextProperty)
        {
            cancelDeferredQueryPublication();
            publishCurrentQueryText(change.GetNewValue<string>());
            return;
        }

        if (change.Property == CaretIndexProperty && hasPreeditText())
        {
            publishVisibleQueryText();
        }
    }

    private void applyQueryText(string? queryTextOrNull)
    {
        if (mIsPublishingQueryText)
        {
            return;
        }

        cancelDeferredQueryPublication();
        string queryText = normalizeNullableText(queryTextOrNull);
        if (string.Equals(queryText, createVisibleQueryText(), StringComparison.Ordinal))
        {
            return;
        }

        mIsApplyingQueryText = true;
        try
        {
            SetCurrentValue(TextProperty, queryText);
            SetCurrentValue(CaretIndexProperty, queryText.Length);
            SetCurrentValue(SelectionStartProperty, queryText.Length);
            SetCurrentValue(SelectionEndProperty, queryText.Length);
        }
        finally
        {
            mIsApplyingQueryText = false;
        }
    }

    private string createVisibleQueryText()
    {
        return createVisibleQueryText(Text);
    }

    private string createVisibleQueryText(string? committedTextOrNull)
    {
        string committedText = normalizeNullableText(committedTextOrNull);
        string preeditText = normalizeNullableText(mTextPresenterOrNull?.PreeditText);
        if (preeditText.Length == 0)
        {
            return committedText;
        }

        int selectionStart = Math.Clamp(Math.Min(SelectionStart, SelectionEnd), 0, committedText.Length);
        int selectionEnd = Math.Clamp(Math.Max(SelectionStart, SelectionEnd), 0, committedText.Length);
        if (selectionStart != selectionEnd)
        {
            return committedText.Remove(selectionStart, selectionEnd - selectionStart).Insert(selectionStart, preeditText);
        }

        int caretIndex = Math.Clamp(CaretIndex, 0, committedText.Length);
        return committedText.Insert(caretIndex, preeditText);
    }

    private bool hasPreeditText()
    {
        return string.IsNullOrEmpty(mTextPresenterOrNull?.PreeditText) == false;
    }

    private void onTextPresenterPropertyChanged(object? senderOrNull, AvaloniaPropertyChangedEventArgs eventArguments)
    {
        if (eventArguments.Property == TextPresenter.PreeditTextProperty)
        {
            if (hasPreeditText())
            {
                cancelDeferredQueryPublication();
                publishVisibleQueryText();
                return;
            }

            deferCommittedQueryPublication();
        }
    }

    private void cancelDeferredQueryPublication()
    {
        ++mDeferredPublicationGeneration;
    }

    private void deferCommittedQueryPublication()
    {
        int publicationGeneration = ++mDeferredPublicationGeneration;
        Dispatcher.UIThread.Post(
            delegate
            {
                if (publicationGeneration != mDeferredPublicationGeneration)
                {
                    return;
                }

                publishCommittedQueryText();
            },
            DispatcherPriority.Input);
    }

    private void publishCommittedQueryText()
    {
        publishCommittedQueryText(Text);
    }

    private void publishCommittedQueryText(string? committedTextOrNull)
    {
        if (mIsApplyingQueryText)
        {
            return;
        }

        publishQueryText(normalizeNullableText(committedTextOrNull));
    }

    private void publishCurrentQueryText(string? committedTextOrNull)
    {
        if (hasPreeditText())
        {
            publishQueryText(createVisibleQueryText(committedTextOrNull));
            return;
        }

        publishCommittedQueryText(committedTextOrNull);
    }

    private void publishQueryText(string queryText)
    {
        if (string.Equals(QueryText, queryText, StringComparison.Ordinal))
        {
            return;
        }

        mIsPublishingQueryText = true;
        try
        {
            SetCurrentValue(QueryTextProperty, queryText);
        }
        finally
        {
            mIsPublishingQueryText = false;
        }
    }

    private void publishVisibleQueryText()
    {
        if (mIsApplyingQueryText)
        {
            return;
        }

        publishQueryText(createVisibleQueryText());
    }

    private void subscribeToTextPresenter()
    {
        if (mTextPresenterOrNull == null || mIsPresenterSubscriptionActive)
        {
            return;
        }

        mTextPresenterOrNull.PropertyChanged += onTextPresenterPropertyChanged;
        mIsPresenterSubscriptionActive = true;
    }

    private static string normalizeNullableText(string? valueOrNull)
    {
        return valueOrNull == null ? string.Empty : valueOrNull;
    }

    private void unsubscribeFromTextPresenter()
    {
        if (mTextPresenterOrNull == null || mIsPresenterSubscriptionActive == false)
        {
            return;
        }

        mTextPresenterOrNull.PropertyChanged -= onTextPresenterPropertyChanged;
        mIsPresenterSubscriptionActive = false;
    }
}
