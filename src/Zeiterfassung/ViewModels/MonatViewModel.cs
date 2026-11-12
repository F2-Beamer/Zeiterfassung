using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zeiterfassung.Services;

namespace Zeiterfassung.ViewModels;

public sealed record MonthRow(
    DateOnly Date,
    string DateText,
    string Note,
    string Actual,
    string Target,
    string Balance,
    Tone BalanceTone,
    bool IsToday,
    bool IsFuture);

public partial class MonatViewModel(BalanceService balance, TimeProvider time, IDayDialog dayDialog)
    : ObservableObject, IRefreshable
{
    // Überstunden am Monatsende: bis 50 h grün, bis 80 h gelb, darüber rot.
    private const int OvertimeGreenMaxMinutes = 50 * 60;
    private const int OvertimeWarningMaxMinutes = 80 * 60;

    private int _year;
    private int _month;

    public ObservableCollection<MonthRow> Days { get; } = [];

    [ObservableProperty]
    private string _monthTitle = string.Empty;

    [ObservableProperty]
    private string _actualSum = "0:00";

    [ObservableProperty]
    private string _targetSum = "0:00";

    [ObservableProperty]
    private string _balanceSum = "0:00";

    [ObservableProperty]
    private string _overtimeEnd = "0:00";

    [ObservableProperty]
    private string _vacationMonthText = "0 Tage";

    [ObservableProperty]
    private string _vacationRemainingText = "0 Tage";

    /// <summary>Ein Satz über den Monat, z. B. „Im November hast du 25:00 Stunden gearbeitet. Bis zum Soll fehlen noch 15:00.“</summary>
    [ObservableProperty]
    private string _summaryText = string.Empty;

    /// <summary>Überstunden, Urlaub und Resturlaub als eine Zeile unter dem Saldo.</summary>
    [ObservableProperty]
    private string _detailLine = string.Empty;

    [ObservableProperty]
    private Tone _balanceSumTone;

    [ObservableProperty]
    private Tone _overtimeEndTone;

    public void Refresh()
    {
        var today = DateOnly.FromDateTime(time.GetLocalNow().DateTime);
        if (_year == 0)
        {
            _year = today.Year;
            _month = today.Month;
        }

        Load(today);
    }

    [RelayCommand]
    private void OpenDay(MonthRow row) => dayDialog.Show(row.Date, Refresh);

    [RelayCommand]
    private void PreviousMonth()
    {
        var first = new DateOnly(_year, _month, 1).AddMonths(-1);
        _year = first.Year;
        _month = first.Month;
        Refresh();
    }

    [RelayCommand]
    private void NextMonth()
    {
        var first = new DateOnly(_year, _month, 1).AddMonths(1);
        _year = first.Year;
        _month = first.Month;
        Refresh();
    }

    private void Load(DateOnly today)
    {
        var first = new DateOnly(_year, _month, 1);
        var last = first.AddMonths(1).AddDays(-1);

        MonthTitle = first.ToDateTime(TimeOnly.MinValue).ToString("MMMM yyyy", TimeFormat.German);

        var days = balance.GetRange(first, last);
        Days.Clear();

        var actual = 0;
        var target = 0;
        var balanceTotal = 0;
        foreach (var day in days)
        {
            var isFuture = day.Date > today;
            var notes = new List<string>();
            if (day.HolidayName is not null)
            {
                notes.Add(day.HolidayName);
            }

            notes.AddRange(day.Absences.Select(a =>
                (a.Type switch
                {
                    Models.AbsenceType.Vacation => "Urlaub",
                    Models.AbsenceType.Sick => "Krank",
                    _ => "Freizeitausgleich"
                }) + (a.Portion == 1m ? string.Empty : " (halb)")));

            if (!isFuture)
            {
                actual += day.ActualMinutes;
                target += day.TargetMinutes;
                balanceTotal += day.BalanceMinutes;
            }

            // Pro Kunde die Netto-Stunden des Tages, z. B. "Müller GmbH 7:30, Huber AG 2:00".
            var customerSums = day.Entries
                .GroupBy(e => e.Customer?.Name ?? "Ohne Kunde")
                .Select(g => $"{g.Key} {TimeFormat.Hm(g.Sum(e => e.NetMinutes))}");

            var hint = string.Join("; ", new[]
            {
                string.Join(", ", notes),
                string.Join(", ", customerSums)
            }.Where(part => part.Length > 0));

            Days.Add(new MonthRow(
                day.Date,
                day.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd dd.MM.", TimeFormat.German),
                hint,
                TimeFormat.Hm(day.ActualMinutes),
                TimeFormat.Hm(day.TargetMinutes),
                isFuture ? string.Empty : TimeFormat.Signed(day.BalanceMinutes),
                isFuture ? Tone.Neutral : BalanceToneOf(day.BalanceMinutes),
                day.Date == today,
                isFuture));
        }

        ActualSum = TimeFormat.Hm(actual);
        TargetSum = TimeFormat.Hm(target);
        BalanceSum = TimeFormat.Signed(balanceTotal);
        BalanceSumTone = BalanceToneOf(balanceTotal);

        var overtime = balance.GetOvertimeBalance(last);
        OvertimeEnd = TimeFormat.Signed(overtime);
        OvertimeEndTone = OvertimeToneOf(overtime);

        // Urlaub im Monat: alle Tage des Monats, auch geplante. Rest: Konto des angezeigten Jahres.
        VacationMonthText = TimeFormat.Days(days.Sum(d => d.VacationDaysUsed));
        VacationRemainingText = TimeFormat.Days(balance.GetVacation(first.Year).RemainingDays);

        DetailLine = $"Überstunden am Monatsende {OvertimeEnd}, Urlaub in diesem Monat {VacationMonthText}, Resturlaub {VacationRemainingText}";
        SummaryText = BuildSummary(first, actual, balanceTotal);
    }

    private static string BuildSummary(DateOnly first, int actualMinutes, int balanceMinutes)
    {
        var month = first.ToDateTime(TimeOnly.MinValue).ToString("MMMM", TimeFormat.German);
        var worked = $"Im {month} hast du {TimeFormat.Hm(actualMinutes)} Stunden gearbeitet.";

        var rest = balanceMinutes switch
        {
            < 0 => $"Bis zum Soll fehlen noch {TimeFormat.Hm(-balanceMinutes)}.",
            > 0 => $"Du hast {TimeFormat.Hm(balanceMinutes)} über dem Soll.",
            _ => "Du bist genau im Soll."
        };

        return worked + " " + rest;
    }

    private static Tone BalanceToneOf(int minutes) => minutes switch
    {
        > 0 => Tone.Positive,
        < 0 => Tone.Negative,
        _ => Tone.Neutral
    };

    private static Tone OvertimeToneOf(int minutes) => minutes switch
    {
        < 0 => Tone.Negative,
        <= OvertimeGreenMaxMinutes => Tone.Positive,
        <= OvertimeWarningMaxMinutes => Tone.Warning,
        _ => Tone.Negative
    };
}
