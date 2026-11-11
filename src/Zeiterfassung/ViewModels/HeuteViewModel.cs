using System.Collections.ObjectModel;
using System.Windows.Threading;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;

namespace Zeiterfassung.ViewModels;

public sealed record CustomerChip(int Id, string Name, bool IsActive);

public sealed record DayEntryRow(int Id, string Start, string End, string Customer, string Break, string Duration, string Note);

public partial class HeuteViewModel : ObservableObject, IRefreshable
{
    private readonly TimeTrackingService _tracking;
    private readonly CustomerService _customers;
    private readonly BalanceService _balance;
    private readonly TimeProvider _time;
    private readonly DispatcherTimer _clock = new() { Interval = TimeSpan.FromSeconds(1) };
    private ActiveTimer? _activeTimer;

    public HeuteViewModel(
        TimeTrackingService tracking,
        CustomerService customers,
        BalanceService balance,
        TimeProvider time,
        ErfassungViewModel erfassung)
    {
        _tracking = tracking;
        _customers = customers;
        _balance = balance;
        _time = time;
        Erfassung = erfassung;
        _clock.Tick += (_, _) => UpdateClock();
        // Manuelle Buchungen aktualisieren die Kennzahlen dieser Seite.
        erfassung.EntriesChanged += Refresh;
    }

    public ObservableCollection<CustomerChip> Customers { get; } = [];

    /// <summary>Manuelle Erfassung mit Formular und Buchungen des gewählten Tages, auf dieser Seite eingebettet.</summary>
    public ErfassungViewModel Erfassung { get; }

    [ObservableProperty]
    private string _dateTitle = string.Empty;

    [ObservableProperty]
    private string _timerCustomer = "Kein Timer aktiv";

    [ObservableProperty]
    private string _timerClock = "00:00:00";

    [ObservableProperty]
    private bool _isRunning;

    [ObservableProperty]
    private bool _isPaused;

    [ObservableProperty]
    private string _pauseButtonText = "Pause";

    [ObservableProperty]
    private string _actualText = "0:00";

    [ObservableProperty]
    private string _targetText = "0:00";

    [ObservableProperty]
    private string _breakText = "0:00";

    [ObservableProperty]
    private string _balanceText = "0:00";

    [ObservableProperty]
    private bool _balanceIsNegative;

    [ObservableProperty]
    private string _overtimeText = "0:00";

    [ObservableProperty]
    private bool _overtimeIsNegative;

    [ObservableProperty]
    private double _actualPercent;

    [ObservableProperty]
    private string _holidayNote = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public void Refresh()
    {
        var today = DateOnly.FromDateTime(_time.GetLocalNow().DateTime);
        DateTitle = today.ToDateTime(TimeOnly.MinValue).ToString("dddd, dd.MM.yyyy", TimeFormat.German);

        var customers = _customers.GetCustomers(includeArchived: false);
        _activeTimer = _tracking.GetActiveTimer();
        IsRunning = _activeTimer is not null;
        IsPaused = _activeTimer?.PausedAt is not null;
        PauseButtonText = IsPaused ? "Weiter" : "Pause";

        var running = _activeTimer is null ? null : customers.FirstOrDefault(c => c.Id == _activeTimer.CustomerId);
        TimerCustomer = _activeTimer is null
            ? "Kein Timer aktiv"
            : running?.Name ?? _customers.GetCustomers(includeArchived: true).First(c => c.Id == _activeTimer.CustomerId).Name;

        Customers.Clear();
        foreach (var customer in customers)
        {
            Customers.Add(new CustomerChip(customer.Id, customer.Name, customer.Id == _activeTimer?.CustomerId));
        }

        var day = _balance.GetDay(today);

        ActualText = TimeFormat.Hm(day.ActualMinutes);
        TargetText = TimeFormat.Hm(day.TargetMinutes);
        BreakText = TimeFormat.Hm(day.Entries.Sum(e => e.BreakMinutes));
        BalanceText = TimeFormat.Signed(day.BalanceMinutes);
        BalanceIsNegative = day.BalanceMinutes < 0;
        ActualPercent = day.TargetMinutes > 0 ? Math.Min(100, day.ActualMinutes * 100.0 / day.TargetMinutes) : 0;

        var overtime = _balance.GetOvertimeBalance(today);
        OvertimeText = TimeFormat.Signed(overtime);
        OvertimeIsNegative = overtime < 0;

        HolidayNote = day.HolidayName is null ? string.Empty : $"Feiertag: {day.HolidayName}";

        Erfassung.Refresh();
        UpdateClock();
        _clock.Start();
    }

    [RelayCommand]
    private void StartCustomer(int customerId)
    {
        Run(() => _tracking.StartTimer(customerId));
    }

    [RelayCommand]
    private void TogglePause()
    {
        Run(() =>
        {
            if (IsPaused)
            {
                _tracking.ResumeTimer();
            }
            else
            {
                _tracking.PauseTimer();
            }
        });
    }

    [RelayCommand]
    private void StopAndBook()
    {
        Run(() =>
        {
            if (_tracking.StopTimer() is null && IsRunning)
            {
                ErrorMessage = "Der Timer lief weniger als eine Minute und wurde nicht gebucht.";
            }
        });
    }

    private void Run(Action action)
    {
        ErrorMessage = null;
        try
        {
            action();
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
        }

        Refresh();
    }

    private void UpdateClock()
    {
        if (_activeTimer is null)
        {
            TimerClock = "00:00:00";
            return;
        }

        var now = _time.GetLocalNow().DateTime;
        var pauseSpan = TimeSpan.FromSeconds(_activeTimer.AccumulatedBreakSeconds);
        if (_activeTimer.PausedAt is { } pausedAt)
        {
            pauseSpan += now - pausedAt;
        }

        var net = now - _activeTimer.StartedAt - pauseSpan;
        if (net < TimeSpan.Zero)
        {
            net = TimeSpan.Zero;
        }

        TimerClock = $"{(int)net.TotalHours:00}:{net.Minutes:00}:{net.Seconds:00}";
    }
}
