using System;
using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Metadata;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Threading;
using Message.Avalonia.Models;

namespace Message.Avalonia.Controls;

[PseudoClasses(PC_Information, PC_Warning)]
internal class MessageItem : TemplatedControl
{
    private const string PC_Information = ":information";
    private const string PC_Warning = ":warning";

    public static readonly StyledProperty<string> MessageProperty =
        AvaloniaProperty.Register<MessageItem, string>(nameof(Message), string.Empty);

    public static readonly StyledProperty<MessageType> TypeProperty =
        AvaloniaProperty.Register<MessageItem, MessageType>(nameof(Type));

    public static readonly StyledProperty<TimeSpan> DurationProperty =
        AvaloniaProperty.Register<MessageItem, TimeSpan>(nameof(Duration), TimeSpan.MaxValue);

    public static readonly StyledProperty<bool> ExpandedProperty =
        AvaloniaProperty.Register<MessageItem, bool>(nameof(Expanded));

    public static readonly StyledProperty<bool> IsClosingProperty =
        AvaloniaProperty.Register<MessageItem, bool>(nameof(IsClosing));

    public static readonly StyledProperty<bool> IsClosedProperty =
        AvaloniaProperty.Register<MessageItem, bool>(nameof(IsClosed));

    private readonly Stopwatch _durationStopwatch = new();

    private readonly DispatcherTimer _durationTimer = new() { Interval = TimeSpan.FromMilliseconds(300) };

    public MessageItem()
    {
        _durationTimer.Tick += DurationTimerOnTick;
    }

    internal event EventHandler? MessageClosed;

    public string Message
    {
        get => GetValue(MessageProperty);
        set => SetValue(MessageProperty, value);
    }

    public MessageType Type
    {
        get => GetValue(TypeProperty);
        set => SetValue(TypeProperty, value);
    }

    /// <summary>
    /// Time after which the message closes itself; it never closes by itself when it is <see cref="TimeSpan.MaxValue"/>.
    /// </summary>
    public TimeSpan Duration
    {
        get => GetValue(DurationProperty);
        set => SetValue(DurationProperty, value);
    }

    /// <summary>
    /// Whether the expanding animation is skipped (it is only needed when other messages are shown).
    /// </summary>
    public bool Expanded
    {
        get => GetValue(ExpandedProperty);
        set => SetValue(ExpandedProperty, value);
    }

    public bool IsClosing
    {
        get => GetValue(IsClosingProperty);
        set => SetValue(IsClosingProperty, value);
    }

    public bool IsClosed
    {
        get => GetValue(IsClosedProperty);
        set => SetValue(IsClosedProperty, value);
    }

    public void Close()
    {
        if (IsClosing || IsClosed)
        {
            return;
        }

        IsClosing = true;
        StopDurationTimer();
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        if (e.NameScope.Find<Button>("PART_CloseButton") is { } closeButton)
        {
            closeButton.Click += (_, ee) =>
            {
                ee.Handled = true;
                Close();
            };
        }

        UpdateMessageType();
        StartDurationTimer();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TypeProperty)
        {
            UpdateMessageType();
        }
        else if (change.Property == IsClosedProperty && IsClosed)
        {
            MessageClosed?.Invoke(this, EventArgs.Empty);
        }
    }

    protected override void OnPointerEntered(PointerEventArgs e)
    {
        base.OnPointerEntered(e);
        StopDurationTimer();
    }

    protected override void OnPointerExited(PointerEventArgs e)
    {
        base.OnPointerExited(e);
        StartDurationTimer();
    }

    private void UpdateMessageType()
    {
        PseudoClasses.Set(PC_Information, Type == MessageType.Information);
        PseudoClasses.Set(PC_Warning, Type == MessageType.Warning);
    }

    private void StartDurationTimer()
    {
        if (IsClosing || IsClosed)
        {
            return;
        }

        _durationStopwatch.Restart();
        _durationTimer.Start();
    }

    private void StopDurationTimer()
    {
        _durationStopwatch.Stop();
        _durationTimer.Stop();
    }

    private void DurationTimerOnTick(object? sender, EventArgs e)
    {
        if (IsPointerOver || _durationStopwatch.Elapsed < Duration)
        {
            return;
        }

        Close();
    }
}
