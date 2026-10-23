using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using GameHub.App.Tray;
using GameHub.App.ViewModels;
using Wpf.Ui.Controls;
using Button = System.Windows.Controls.Button;
using ListBoxItem = System.Windows.Controls.ListBoxItem;

namespace GameHub.App;

public partial class MainWindow : FluentWindow
{
    private const string GameDragFormat = "GameHub.GameCard";

    private readonly MainWindowViewModel _viewModel;
    private readonly TrayIconService _tray;
    private Point _cardDragStart;
    private bool _cardDragCandidate;

    public MainWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        DataContext = viewModel;

        _tray = new TrayIconService(this);
        viewModel.MinimizeToTrayRequested += (_, _) => _tray.MinimizeToTray();

        Loaded += async (_, _) =>
        {
            await viewModel.LoadCommand.ExecuteAsync(null);
            viewModel.RestoreFloatingWindowIfNeeded();
        };

        Closing += (_, _) => viewModel.PrepareForShutdown();
        Closed += (_, _) => _tray.Dispose();
    }

    private void OnWindowDragOver(object sender, DragEventArgs e)
    {
        if (e.Data.GetDataPresent(GameDragFormat)) return;

        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnWindowDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] paths) return;

        foreach (var path in paths)
        {
            if (path.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                await _viewModel.AddGameFromPathAsync(path);
        }
    }

    private void OnGameCardPreviewMouseDown(object sender, MouseButtonEventArgs e)
    {
        _cardDragCandidate = e.OriginalSource is DependencyObject source && FindAncestor<Button>(source) is null;
        _cardDragStart = e.GetPosition(null);
    }

    private void OnGameCardPreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (!_cardDragCandidate || e.LeftButton != MouseButtonState.Pressed) return;
        if (sender is not FrameworkElement { DataContext: GameCardViewModel card } element) return;

        var position = e.GetPosition(null);
        if (Math.Abs(position.X - _cardDragStart.X) < SystemParameters.MinimumHorizontalDragDistance &&
            Math.Abs(position.Y - _cardDragStart.Y) < SystemParameters.MinimumVerticalDragDistance)
            return;

        _cardDragCandidate = false;
        DragDrop.DoDragDrop(element, new DataObject(GameDragFormat, card.Id), DragDropEffects.Move);
    }

    private void OnNavItemDragOver(object sender, DragEventArgs e)
    {
        var canDrop = e.Data.GetDataPresent(GameDragFormat) &&
                      sender is ListBoxItem { DataContext: NavItem { Kind: LibraryFilterKind.Group } };
        e.Effects = canDrop ? DragDropEffects.Move : DragDropEffects.None;
        e.Handled = true;
    }

    private async void OnNavItemDrop(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(GameDragFormat)) return;
        if (sender is not ListBoxItem { DataContext: NavItem { Kind: LibraryFilterKind.Group, GroupId: { } groupId } }) return;
        if (e.Data.GetData(GameDragFormat) is not Guid gameId) return;

        e.Handled = true;
        await _viewModel.AssignGameToGroupAsync(gameId, groupId);
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null)
        {
            if (current is T match) return match;
            current = current is Visual or System.Windows.Media.Media3D.Visual3D
                ? VisualTreeHelper.GetParent(current)
                : LogicalTreeHelper.GetParent(current);
        }

        return null;
    }
}
