using System.Windows;
using Zeiterfassung.ViewModels;

namespace Zeiterfassung.Views;

public partial class MainWindow : Window
{
    public MainWindow(MainViewModel viewModel)
    {
        InitializeComponent();
        DataContext = viewModel;
    }

    private void Hilfe_Click(object sender, RoutedEventArgs e)
    {
        HilfePopup.IsOpen = !HilfePopup.IsOpen;
    }

    private void Anleitung_Click(object sender, RoutedEventArgs e)
    {
        HilfePopup.IsOpen = false;
        new HelpWindow { Owner = this }.ShowDialog();
    }

    private void Version_Click(object sender, RoutedEventArgs e)
    {
        HilfePopup.IsOpen = false;
        new AboutWindow { Owner = this }.ShowDialog();
    }
}
