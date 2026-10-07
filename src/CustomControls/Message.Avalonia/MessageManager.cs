using System;
using Avalonia.Threading;
using Message.Avalonia.Controls;
using Message.Avalonia.Models;

namespace Message.Avalonia;

public class MessageManager
{
    /// <summary>
    /// Default duration for the message.
    /// </summary>
    public TimeSpan Duration { get; set; } = TimeSpan.MaxValue;

    /// <summary>
    /// Default instance of the <see cref="MessageManager"/>.
    /// </summary>
    public static MessageManager Default { get; } = new();

    public void ShowInformationMessage(string message, MessageOptions options = default) =>
        Show(MessageType.Information, message, options);

    public void ShowWarningMessage(string message, MessageOptions options = default) =>
        Show(MessageType.Warning, message, options);

    private void Show(MessageType type, string message, MessageOptions options)
    {
        Dispatcher.UIThread.Invoke(() =>
        {
            var item = new MessageItem
            {
                Type = type,
                Message = message,
                Duration = options.Duration ?? Duration,
            };

            MessageHost.Current?.AddMessage(item);
        });
    }
}
