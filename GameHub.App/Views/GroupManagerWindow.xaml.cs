using System.Windows;
using System.Windows.Input;
using GameHub.App.ViewModels;
using Wpf.Ui.Controls;

namespace GameHub.App.Views;

public partial class GroupManagerWindow : FluentWindow
{
    private const string GroupDragFormat = "GameHub.GroupRow";

    private readonly GroupManagerViewModel _viewModel;

    public GroupManagerWindow(GroupManagerViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();

    private void OnGripMouseMove(object sender, MouseEventArgs e)
    {
        if (e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement { DataContext: GroupRowViewModel row } grip) return;

        DragDrop.DoDragDrop(grip, new DataObject(GroupDragFormat, row), DragDropEffects.Move);
    }

    private void OnRowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(GroupDragFormat) ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnRowDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(GroupDragFormat) is not GroupRowViewModel dragged) return;
        if (sender is not FrameworkElement { DataContext: GroupRowViewModel target }) return;

        e.Handled = true;
        await _viewModel.MoveGroupAsync(dragged, target);
    }
}
