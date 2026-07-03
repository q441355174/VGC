using Avalonia.Controls;
using Avalonia.Interactivity;
using VGC.ViewModels;

namespace VGC.Views;

public partial class AnalyzeView : UserControl
{
    public AnalyzeView()
    {
        InitializeComponent();
    }

    private void OnSelectInspectorTab(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyzeViewModel vm)
            vm.SelectAnalyzeTab("inspector");
    }

    private void OnSelectConsoleTab(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyzeViewModel vm)
            vm.SelectAnalyzeTab("console");
    }

    private void OnSelectChartTab(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyzeViewModel vm)
            vm.SelectAnalyzeTab("chart");
    }

    private void OnSendConsoleCommand(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyzeViewModel vm)
            vm.SendConsoleCommand();
    }

    private void OnClearConsole(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyzeViewModel vm)
            vm.ClearConsole();
    }

    private void OnClearChart(object? sender, RoutedEventArgs e)
    {
        if (DataContext is AnalyzeViewModel vm)
            vm.ClearChart();
    }
}
