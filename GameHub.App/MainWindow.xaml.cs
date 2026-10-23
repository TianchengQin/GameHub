using System.Windows;
using GameHub.App.ViewModels;
using Wpf.Ui.Controls;

namespace GameHub.App;

public partial class MainWindow : FluentWindow
{
    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
        Loaded += async (_, _) => await viewModel.LoadCommand.ExecuteAsync(null);
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;
        if (DataContext is not MainWindowViewModel vm) return;

        foreach (var path in paths)
        {
            if (path.EndsWith(".exe", System.StringComparison.OrdinalIgnoreCase))
                await vm.AddGameFromPathAsync(path);
        }
    }
}
