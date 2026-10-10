using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Financisto.Desktop.Views;

public partial class AccountsPageView : UserControl
{
    // Set by a right-click press: whether it landed on a row. Null for a menu opened from the keyboard.
    private bool? rightClickedRow;

    public AccountsPageView()
    {
        InitializeComponent();

        // A right-click doesn't select the row under the pointer, but the menu acts on the selected account.
        AccountsGrid.AddHandler(PointerPressedEvent, OnGridPointerPressed, RoutingStrategies.Tunnel);
    }

    private void OnGridPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        rightClickedRow = null;
        if (!e.GetCurrentPoint(AccountsGrid).Properties.IsRightButtonPressed)
        {
            return;
        }

        var row = (e.Source as Visual)?.FindAncestorOfType<DataGridRow>(includeSelf: true);
        rightClickedRow = row != null;
        if (row != null)
        {
            AccountsGrid.SelectedItem = row.DataContext;
        }
    }

    // The menu is for an account: not over the header or the empty area, and not without a selection.
    private void OnContextMenuOpening(object? sender, CancelEventArgs e)
    {
        e.Cancel = rightClickedRow == false || (rightClickedRow == null && AccountsGrid.SelectedItem == null);
        rightClickedRow = null;
    }
}
