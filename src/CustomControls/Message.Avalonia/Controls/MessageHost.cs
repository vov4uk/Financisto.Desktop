using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;

namespace Message.Avalonia.Controls;

/// <summary>
/// Shows the messages in the bottom right corner. Place it once in the window; messages go to the newest host.
/// </summary>
public class MessageHost : TemplatedControl
{
    private static readonly List<MessageHost> HostList = [];
    private readonly ConcurrentQueue<MessageItem> _pendingItemsQueue = [];

    private Panel? _itemsPanel;

    public MessageHost()
    {
        HostList.Insert(0, this);
    }

    internal static MessageHost? Current => HostList.FirstOrDefault();

    internal void AddMessage(MessageItem msg)
    {
        if (_itemsPanel == null)
        {
            _pendingItemsQueue.Enqueue(msg);
            return;
        }

        msg.MessageClosed += (sender, _) => _itemsPanel?.Children.Remove((MessageItem)sender!);

        // Cancel the expanding animation when there is no previous message
        msg.Expanded = _itemsPanel.Children.Count == 0;

        _itemsPanel.Children.Add(msg);
    }

    protected override void OnApplyTemplate(TemplateAppliedEventArgs e)
    {
        base.OnApplyTemplate(e);

        // Insert back if the host be replaced
        if (!HostList.Contains(this))
        {
            HostList.Insert(0, this);
        }

        _itemsPanel = e.NameScope.Find<Panel>("PART_Items");

        while (_pendingItemsQueue.TryDequeue(out var msg))
        {
            AddMessage(msg);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);

        HostList.Remove(this);

        if (_itemsPanel != null)
        {
            foreach (var messageItem in _itemsPanel.Children.OfType<MessageItem>().ToList())
            {
                messageItem.Close();
            }
        }
    }
}
