using System;
using Message.Avalonia;

namespace Financisto.Desktop.Helpers
{
    public interface IToastNotifierWrapper
    {
        void ShowMessage(string message);
        void ShowWarning(string message);
    }

    public class ToastNotifierWrapper : IToastNotifierWrapper
    {
        public ToastNotifierWrapper()
        {
        }
        public void ShowMessage(string message)
        {
            MessageManager.Default.ShowInformationMessage(message, new Message.Avalonia.Models.MessageOptions { Duration = TimeSpan.FromSeconds(5) });
        }
        public void ShowWarning(string message)
        {
            MessageManager.Default.ShowWarningMessage(message, new Message.Avalonia.Models.MessageOptions { Duration = TimeSpan.FromSeconds(5) });
        }
    }
}
