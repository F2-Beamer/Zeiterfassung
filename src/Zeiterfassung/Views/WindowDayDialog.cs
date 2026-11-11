using System.Windows;
using Zeiterfassung.Services;
using Zeiterfassung.ViewModels;

namespace Zeiterfassung.Views;

/// <summary>Zeigt das Tagesfenster als modalen Dialog über dem Hauptfenster.</summary>
public sealed class WindowDayDialog(BalanceService balance, AbsenceService absences, TimeTrackingService tracking) : IDayDialog
{
    public void Show(DateOnly date, Action changed)
    {
        var viewModel = new DayDetailViewModel(date, balance, absences, tracking, changed);
        var window = new DayDetailWindow
        {
            DataContext = viewModel,
            Owner = Application.Current.MainWindow
        };
        window.ShowDialog();
    }
}
