using System.Windows;
using GameHub.App.ViewModels;
using Wpf.Ui.Controls;

namespace GameHub.App.Views;

public partial class CoverSearchWindow : FluentWindow
{
    private readonly CoverSearchViewModel _viewModel;

    public CoverSearchWindow(CoverSearchViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.SearchCommand.ExecuteAsync(null);
    }

    private void OnConfirmClick(object sender, RoutedEventArgs e)
    {
        if (_viewModel.SelectedResult is null)
        {
            _viewModel.StatusMessage = "请先点击选择一张封面。";
            return;
        }

        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
