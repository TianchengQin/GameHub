using System.IO;
using System.Windows;
using GameHub.App.ViewModels;
using Wpf.Ui.Controls;

namespace GameHub.App.Views;

public partial class GameDetailWindow : FluentWindow
{
    private readonly GameDetailViewModel _viewModel;

    public GameDetailWindow(GameDetailViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        await _viewModel.SaveCommand.ExecuteAsync(null);
        DialogResult = true;
        Close();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }

    private async void OnSearchCoverOnlineClick(object sender, RoutedEventArgs e)
    {
        var apiKey = _viewModel.SettingsService.Current.SteamGridDbApiKey;
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            System.Windows.MessageBox.Show(this,
                "在线搜索封面需要 SteamGridDB 的 API Key。\n请先在 steamgriddb.com 注册账号并在个人偏好设置里生成 Key，然后填到本软件的\"设置\"页中。",
                "需要 API Key", System.Windows.MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        try
        {
            using var client = new GameHub.Core.Covers.SteamGridDbClient(apiKey);
            var searchVm = new CoverSearchViewModel(client, _viewModel.Name);
            var window = new CoverSearchWindow(searchVm) { Owner = this };
            if (window.ShowDialog() != true) return;

            var bytes = await searchVm.DownloadSelectedAsync();
            var extension = Path.GetExtension(new Uri(searchVm.SelectedResult!.ImageUrl).AbsolutePath);
            var destination = Path.Combine(GameHub.Core.AppPaths.CoversFolder, _viewModel.Id + (string.IsNullOrEmpty(extension) ? ".png" : extension));
            await File.WriteAllBytesAsync(destination, bytes);
            _viewModel.CoverImagePath = destination;
        }
        catch (Exception ex)
        {
            System.Windows.MessageBox.Show(this, $"下载封面失败：{ex.Message}", "在线搜索封面",
                System.Windows.MessageBoxButton.OK, MessageBoxImage.Warning);
        }
    }
}
