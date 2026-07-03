using Avalonia.Controls;
using Avalonia.Interactivity;
using VGC.ViewModels;
using VGC.Views.Controls;

namespace VGC.Views;

public partial class ParameterView : UserControl
{
    public ParameterView()
    {
        InitializeComponent();
    }

    private void OnParameterViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var groupPanel = this.FindControl<Border>("ParameterGroupPanel");
        var detailPanel = this.FindControl<Border>("ParameterDetailPanel");
        var editorDialog = this.FindControl<Border>("ParameterEditorDialog");
        if (groupPanel is not null)
        {
            groupPanel.Width = ScreenLayout.Clamp(width * 0.22, 180, 260);
        }

        if (detailPanel is not null)
        {
            detailPanel.MinWidth = ScreenLayout.Clamp(width * 0.28, 260, 360);
            detailPanel.MaxWidth = ScreenLayout.Clamp(width * 0.36, 320, 420);
        }

        if (editorDialog is not null)
        {
            editorDialog.Width = ScreenLayout.Clamp(width * 0.42, 360, 520);
        }
    }

    private void OnClearSearch(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.ClearSearch();
        }
    }

    private void OnShowFullList(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.ShowFullList();
        }
    }

    private void OnShowModifiedList(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.ShowModifiedList();
        }
    }

    private void OnShowFavoritesList(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.ShowFavoritesList();
        }
    }

    private void OnToggleToolsMenu(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.ToggleToolsMenu();
        }
    }

    private void OnRefreshParameters(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.RefreshParameters();
        }
    }

    private void OnClearFavorites(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.ClearFavorites();
        }
    }

    private void OnRebootVehicle(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.RebootVehicle();
        }
    }

    private void OnToggleSelectedFavorite(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.ToggleSelectedFavorite();
        }
    }

    private void OnOpenEditor(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.OpenEditor();
        }
    }

    private void OnCancelEditor(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.CancelEditor();
        }
    }

    private void OnSaveEditor(object? sender, RoutedEventArgs e)
    {
        if (DataContext is ParameterViewModel vm)
        {
            vm.SaveEditor();
        }
    }
}
