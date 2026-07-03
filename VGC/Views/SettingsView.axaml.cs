using System.Reactive.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using VGC.ViewModels;
using VGC.Views.Controls;

namespace VGC.Views;

public partial class SettingsView : UserControl
{
    public SettingsView()
    {
        InitializeComponent();
    }

    private void OnSettingsViewSizeChanged(object? sender, SizeChangedEventArgs e)
    {
        var width = e.NewSize.Width;
        var nav = this.FindControl<Border>("SettingsNavPanel");
        var content = this.FindControl<Border>("SettingsContentPanel");
        var editor = this.FindControl<Border>("CommLinkEditorDialog");
        var delete = this.FindControl<Border>("DeleteCommLinkDialog");
        if (nav is not null)
        {
            nav.Width = ScreenLayout.Clamp(width * 0.22, 180, 260);
        }

        if (content is not null)
        {
            content.MinWidth = ScreenLayout.Clamp(width * 0.52, 460, 840);
        }

        if (editor is not null)
        {
            editor.Width = ScreenLayout.Clamp(width * 0.42, 360, 520);
        }

        if (delete is not null)
        {
            delete.Width = ScreenLayout.Clamp(width * 0.32, 320, 420);
        }
    }

    private void OnEditCommLink(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LinkConfigurationItemViewModel link } && DataContext is SettingsViewModel vm)
        {
            vm.SelectedCommLink = link;
            vm.EditSelectedCommLinkCommand.Execute().Subscribe();
        }
    }

    private void OnDeleteCommLink(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LinkConfigurationItemViewModel link } && DataContext is SettingsViewModel vm)
        {
            vm.SelectedCommLink = link;
            vm.DeleteSelectedCommLinkCommand.Execute().Subscribe();
        }
    }

    private void OnToggleCommLinkConnection(object? sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: LinkConfigurationItemViewModel link } && DataContext is SettingsViewModel vm)
        {
            vm.SelectedCommLink = link;
            if (vm.SelectedCommLinkIsConnected)
            {
                vm.DisconnectSelectedCommLinkCommand.Execute().Subscribe();
            }
            else
            {
                vm.ConnectSelectedCommLinkCommand.Execute().Subscribe();
            }
        }
    }
}
