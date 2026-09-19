using System;
using System.Collections;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Financisto.Common.Behaviors
{
    /// <summary>
    /// Attached property to invoke a bound ICommand with the DataGrid's SelectedItems on SelectionChanged,
    /// replacing WPF's i:EventTrigger/InvokeCommandAction which Avalonia does not support here.
    /// </summary>
    public static class DataGridSelectionBehavior
    {
        public static readonly AttachedProperty<ICommand> SelectionChangedCommandProperty =
            AvaloniaProperty.RegisterAttached<DataGrid, ICommand>("SelectionChangedCommand", typeof(DataGridSelectionBehavior));

        static DataGridSelectionBehavior()
        {
            SelectionChangedCommandProperty.Changed.AddClassHandler<DataGrid>(OnSelectionChangedCommandChanged);
        }

        public static void SetSelectionChangedCommand(DataGrid element, ICommand value) =>
            element.SetValue(SelectionChangedCommandProperty, value);

        public static ICommand GetSelectionChangedCommand(DataGrid element) =>
            element.GetValue(SelectionChangedCommandProperty);

        private static void OnSelectionChangedCommandChanged(DataGrid grid, AvaloniaPropertyChangedEventArgs e)
        {
            grid.SelectionChanged -= OnSelectionChanged;
            if (e.NewValue is ICommand)
            {
                grid.SelectionChanged += OnSelectionChanged;
            }
        }

        private static void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is not DataGrid grid)
            {
                return;
            }

            var command = GetSelectionChangedCommand(grid);
            IList selectedItems = grid.SelectedItems;
            if (command?.CanExecute(selectedItems) == true)
            {
                command.Execute(selectedItems);
            }
        }
    }
}
