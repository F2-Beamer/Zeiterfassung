using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;

namespace Zeiterfassung.ViewModels;

public sealed record AbsenceOption(AbsenceType Type, string Label);

public sealed record DayBookingRow(string Time, string Customer, string Break, string Net, string Note)
{
    /// <summary>Notiz mit Trenner für die Kundenzeile, leer wenn keine Notiz vorhanden ist.</summary>
    public string NoteSuffix => string.IsNullOrEmpty(Note) ? string.Empty : " (" + Note + ")";
}

public sealed record DayAbsenceRow(int Id, string Type, string Portion, string Note);

/// <summary>Inhalt des Tagesfensters aus der Monatsansicht: Buchungen, Summen und Abwesenheiten eines Tages.</summary>
public partial class DayDetailViewModel : ObservableObject
{
    private static readonly IReadOnlyList<AbsenceOption> Types =
    [
        new(AbsenceType.Vacation, "Urlaub"),
        new(AbsenceType.Sick, "Krank"),
        new(AbsenceType.CompensatoryTime, "Freizeitausgleich")
    ];

    private static readonly IReadOnlyList<decimal> Portions = [1m, 0.5m];

    private readonly DateOnly _date;
    private readonly BalanceService _balance;
    private readonly AbsenceService _absences;
    private readonly TimeTrackingService _tracking;
    private readonly Action _changed;

    public DayDetailViewModel(
        DateOnly date,
        BalanceService balance,
        AbsenceService absences,
        TimeTrackingService tracking,
        Action changed)
    {
        _date = date;
        _balance = balance;
        _absences = absences;
        _tracking = tracking;
        _changed = changed;
        Title = date.ToDateTime(TimeOnly.MinValue).ToString("dddd, dd.MM.yyyy", TimeFormat.German);
        Load();
    }

    // Instanz-Eigenschaften, nicht static: WPF kann static Eigenschaften nicht über {Binding} lesen.
    public string Title { get; }

    public IReadOnlyList<AbsenceOption> TypeOptions => Types;

    public IReadOnlyList<decimal> PortionOptions => Portions;

    public ObservableCollection<DayBookingRow> Bookings { get; } = [];

    public ObservableCollection<DayAbsenceRow> Absences { get; } = [];

    [ObservableProperty]
    private string _summary = string.Empty;

    [ObservableProperty]
    private bool _noBookings;

    [ObservableProperty]
    private bool _noAbsences;

    [ObservableProperty]
    private AbsenceOption _newType = Types[0];

    [ObservableProperty]
    private decimal _newPortion = 1m;

    [ObservableProperty]
    private string _newNote = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    [RelayCommand]
    private void AddAbsence()
    {
        try
        {
            // Der Tag wurde bewusst ausgewählt, daher gilt er auch am Wochenende.
            _absences.AddRange(_date, _date, NewType.Type, NewPortion, NewNote, includeWeekends: true);
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
            return;
        }

        NewNote = string.Empty;
        ErrorMessage = null;
        Load();
        _changed();
    }

    [RelayCommand]
    private void DeleteAbsence(DayAbsenceRow row)
    {
        _absences.DeleteAbsence(row.Id);
        ErrorMessage = null;
        Load();
        _changed();
    }

    private void Load()
    {
        var day = _balance.GetDay(_date);

        // Buchungen und Pausen zusammen, nach Uhrzeit sortiert.
        var rows = day.Entries
            .Select(entry => (Start: entry.Start, Row: new DayBookingRow(
                $"{entry.Start:HH\\:mm} – {entry.End:HH\\:mm}",
                entry.Customer?.Name ?? "–",
                $"{entry.BreakMinutes} min",
                TimeFormat.Hm(entry.NetMinutes),
                entry.Note ?? string.Empty)))
            .Concat(_tracking.GetPauses(_date).Select(pause => (Start: pause.Start, Row: new DayBookingRow(
                $"{pause.Start:HH\\:mm} – {pause.End:HH\\:mm}",
                "Pause (ohne Kunde)",
                string.Empty,
                TimeFormat.Hm(pause.Minutes),
                string.Empty))))
            .OrderBy(item => item.Start);

        Bookings.Clear();
        foreach (var item in rows)
        {
            Bookings.Add(item.Row);
        }

        Absences.Clear();
        foreach (var absence in day.Absences)
        {
            Absences.Add(new DayAbsenceRow(
                absence.Id,
                Types.First(o => o.Type == absence.Type).Label,
                absence.Portion == 1m ? "ganz" : "halb",
                absence.Note ?? string.Empty));
        }

        var parts = new List<string>();
        if (day.HolidayName is not null)
        {
            parts.Add(day.HolidayName);
        }

        parts.Add($"Ist {TimeFormat.Hm(day.ActualMinutes)}");
        parts.Add($"Soll {TimeFormat.Hm(day.TargetMinutes)}");
        parts.Add($"Saldo {TimeFormat.Signed(day.BalanceMinutes)}");
        Summary = string.Join(", ", parts);

        NoBookings = Bookings.Count == 0;
        NoAbsences = Absences.Count == 0;
    }
}
