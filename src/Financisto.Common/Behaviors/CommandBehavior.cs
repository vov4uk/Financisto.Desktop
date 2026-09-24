using System;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;

namespace Financisto.Common.Behaviors
{
    /// <summary>
    /// Attached property to invoke a bound ICommand on DoubleTapped, keeping event wiring in XAML/MVVM
    /// instead of code-behind (Avalonia has no WPF-style MouseBinding/InputBindings).
    /// </summary>
    public static class CommandBehavior
    {
        public static readonly AttachedProperty<ICommand> DoubleTappedCommandProperty =
            AvaloniaProperty.RegisterAttached<Control, ICommand>("DoubleTappedCommand", typeof(CommandBehavior));

        public static readonly AttachedProperty<object> DoubleTappedCommandParameterProperty =
            AvaloniaProperty.RegisterAttached<Control, object>("DoubleTappedCommandParameter", typeof(CommandBehavior));

        static CommandBehavior()
        {
            DoubleTappedCommandProperty.Changed.AddClassHandler<Control>(OnDoubleTappedCommandChanged);
        }

        public static void SetDoubleTappedCommand(Control element, ICommand value) =>
            element.SetValue(DoubleTappedCommandProperty, value);

        public static ICommand GetDoubleTappedCommand(Control element) =>
            element.GetValue(DoubleTappedCommandProperty);

        public static void SetDoubleTappedCommandParameter(Control element, object value) =>
            element.SetValue(DoubleTappedCommandParameterProperty, value);

        public static object GetDoubleTappedCommandParameter(Control element) =>
            element.GetValue(DoubleTappedCommandParameterProperty);

        private static void OnDoubleTappedCommandChanged(Control control, AvaloniaPropertyChangedEventArgs e)
        {
            control.DoubleTapped -= OnDoubleTapped;
            if (e.NewValue is ICommand)
            {
                control.DoubleTapped += OnDoubleTapped;
            }
        }

        private static void OnDoubleTapped(object sender, TappedEventArgs e)
        {
            if (sender is not Control control)
            {
                return;
            }

            var command = GetDoubleTappedCommand(control);
            var parameter = GetDoubleTappedCommandParameter(control);
            if (command?.CanExecute(parameter) == true)
            {
                command.Execute(parameter);
            }

            // DoubleTapped is a routed (bubbling) event. Without marking it handled, a double tap on a
            // nested item (e.g. TreeViewItem) would bubble up and re-trigger the command on every ancestor
            // that also has this attached property set (e.g. parent TreeViewItems), causing the command
            // to execute multiple times.
            e.Handled = true;
        }
    }
}
