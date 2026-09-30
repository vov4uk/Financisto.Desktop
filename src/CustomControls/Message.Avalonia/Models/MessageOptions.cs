using System;

namespace Message.Avalonia.Models;

public struct MessageOptions
{
    /// <summary>
    /// Automatically close the message after a certain duration.
    /// </summary>
    public TimeSpan? Duration { get; set; }
}
