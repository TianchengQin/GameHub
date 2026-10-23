using System.Windows;
using GameHub.App.ViewModels;
using Wpf.Ui.Controls;

namespace GameHub.App.Views;

public partial class GroupManagerWindow : FluentWindow
{
    public GroupManagerWindow(GroupManagerViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
