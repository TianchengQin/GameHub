using System.Windows;
using GameHub.App.ViewModels;
using Wpf.Ui.Controls;

namespace GameHub.App.Views;

public partial class ScanConfirmationWindow : FluentWindow
{
    public ScanConfirmationWindow(ScanConfirmationViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
