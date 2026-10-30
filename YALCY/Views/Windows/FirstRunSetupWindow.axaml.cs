using Avalonia.Controls;
using Avalonia.Interactivity;
using YALCY.ViewModels;

namespace YALCY.Views.Windows;

public partial class FirstRunSetupWindow : Window
{
    public FirstRunSetupWindow()
    {
        InitializeComponent();
        Closing += (_, e) =>
        {
            if (DataContext is FirstRunSetupViewModel { IsBusy: true }) e.Cancel = true;
        };
    }

    private void OnSkip(object? sender, RoutedEventArgs e) => Close();

    private async void OnFinish(object? sender, RoutedEventArgs e)
    {
        if (DataContext is FirstRunSetupViewModel model && await model.CompleteAsync()) Close();
    }
}
