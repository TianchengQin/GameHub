using System.Collections.Specialized;
using System.Windows;
using System.Windows.Input;
using GameHub.App.ViewModels;

namespace GameHub.App.Views;

public partial class FloatingFavoritesWindow : Window
{
    public FloatingFavoritesWindow(MainWindowViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;

        if (viewModel.FavoritesView is INotifyCollectionChanged changes)
            changes.CollectionChanged += (_, _) => UpdateEmptyHint(viewModel);

        UpdateEmptyHint(viewModel);
    }

    private void UpdateEmptyHint(MainWindowViewModel viewModel)
        => EmptyHint.Visibility = viewModel.FavoritesView.IsEmpty ? Visibility.Visible : Visibility.Collapsed;

    private void OnDragMove(object sender, MouseButtonEventArgs e)
    {
        if (e.ButtonState == MouseButtonState.Pressed)
            DragMove();
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) => Close();
}
