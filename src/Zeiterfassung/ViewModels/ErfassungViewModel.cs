using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;

namespace Zeiterfassung.ViewModels;

public sealed record CustomerPick(int Id, string Name, bool IsSelected);

public sealed record PauseRow(int Id, string Start, string End, string Duration);

public partial class ErfassungViewModel(
    TimeTrackingService tracking,
    CustomerService customers) : ObservableObject, IRefreshable
{
    private int? _editingId;

    /// <summary>Wird nach jeder gespeicherten oder gelöschten Buchung ausgelöst.</summary>
    public event Action? EntriesChanged;

    public ObservableCollection<Customer> ActiveCustomers { get; } = [];

    public ObservableCollection<CustomerPick> CustomerPicks { get; } = [];

    public ObservableCollection<DayEntryRow> Entries { get; } = [];

    public ObservableCollection<PauseRow> Pauses { get; } = [];

    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    [ObservableProperty]
    private Customer? _selectedCustomer;

    partial void OnSelectedCustomerChanged(Customer? value) => UpdateCustomerPicks();

    [ObservableProperty]
    private string _startText = "08:00";

    [ObservableProperty]
    private string _endText = "16:00";

    [ObservableProperty]
    private string _breakText = "0";

    [ObservableProperty]
    private string _note = string.Empty;

    [ObservableProperty]
    private string _formTitle = "Neue Buchung";

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _pauseStartText = "12:00";

    [ObservableProperty]
    private string _pauseEndText = "12:30";

    [ObservableProperty]
    private string? _pauseError;

    [RelayCommand]
    private void AddPause()
    {
        try
        {
            if (!TimeFormat.TryParseClock(PauseStartText, out var start) || !TimeFormat.TryParseClock(PauseEndText, out var end))
            {
                throw new DomainException("Pausenbeginn und -ende bitte im Format HH:mm angeben.");
            }

            tracking.AddPause(DateOnly.FromDateTime(SelectedDate), start, end);
            PauseError = null;
            PauseStartText = PauseEndText;
        }
        catch (DomainException ex)
        {
            PauseError = ex.Message;
            return;
        }

        LoadEntries();
        EntriesChanged?.Invoke();
    }

    [RelayCommand]
    private void DeletePause(PauseRow row)
    {
        tracking.DeletePause(row.Id);
        PauseError = null;
        LoadEntries();
        EntriesChanged?.Invoke();
    }

    partial void OnSelectedDateChanged(DateTime value) => LoadEntries();

    public void Refresh()
    {
        ActiveCustomers.Clear();
        foreach (var customer in customers.GetCustomers(includeArchived: false))
        {
            ActiveCustomers.Add(customer);
        }

        if (SelectedCustomer is null && ActiveCustomers.Count > 0)
        {
            SelectedCustomer = ActiveCustomers[0];
        }

        UpdateCustomerPicks();
        LoadEntries();
    }

    [RelayCommand]
    private void SelectCustomer(int customerId)
    {
        SelectedCustomer = ActiveCustomers.FirstOrDefault(c => c.Id == customerId);
    }

    private void UpdateCustomerPicks()
    {
        CustomerPicks.Clear();
        foreach (var customer in ActiveCustomers)
        {
            CustomerPicks.Add(new CustomerPick(customer.Id, customer.Name, customer.Id == SelectedCustomer?.Id));
        }
    }

    [RelayCommand]
    private void Save()
    {
        try
        {
            if (SelectedCustomer is null)
            {
                throw new DomainException("Bitte einen Kunden auswählen.");
            }

            if (!TimeFormat.TryParseClock(StartText, out var start) || !TimeFormat.TryParseClock(EndText, out var end))
            {
                throw new DomainException("Start und Ende bitte im Format HH:mm angeben.");
            }

            if (!int.TryParse(BreakText, out var breakMinutes))
            {
                throw new DomainException("Die Pause bitte in Minuten angeben.");
            }

            var entry = new TimeEntry
            {
                Id = _editingId ?? 0,
                CustomerId = SelectedCustomer.Id,
                Date = DateOnly.FromDateTime(SelectedDate),
                Start = start,
                End = end,
                BreakMinutes = breakMinutes,
                Note = string.IsNullOrWhiteSpace(Note) ? null : Note.Trim()
            };

            if (_editingId is null)
            {
                tracking.AddEntry(entry);
            }
            else
            {
                tracking.UpdateEntry(entry);
            }

            ErrorMessage = null;
            ResetForm();
            LoadEntries();
            EntriesChanged?.Invoke();
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
            LoadEntries();
        }
    }

    [RelayCommand]
    private void Edit(DayEntryRow row)
    {
        var entry = tracking.GetEntries(DateOnly.FromDateTime(SelectedDate)).First(e => e.Id == row.Id);
        _editingId = entry.Id;
        SelectedCustomer = ActiveCustomers.FirstOrDefault(c => c.Id == entry.CustomerId) ?? entry.Customer;
        StartText = entry.Start.ToString("HH:mm");
        EndText = entry.End.ToString("HH:mm");
        BreakText = entry.BreakMinutes.ToString();
        Note = entry.Note ?? string.Empty;
        FormTitle = "Buchung bearbeiten";
        ErrorMessage = null;
    }

    [RelayCommand]
    private void Delete(DayEntryRow row)
    {
        tracking.DeleteEntry(row.Id);
        if (_editingId == row.Id)
        {
            ResetForm();
        }

        LoadEntries();
        EntriesChanged?.Invoke();
    }

    [RelayCommand]
    private void NewEntry()
    {
        ResetForm();
        ErrorMessage = null;
    }

    private void ResetForm()
    {
        _editingId = null;
        FormTitle = "Neue Buchung";
        StartText = "08:00";
        EndText = "16:00";
        BreakText = "0";
        Note = string.Empty;
    }

    private void LoadEntries()
    {
        Pauses.Clear();
        foreach (var pause in tracking.GetPauses(DateOnly.FromDateTime(SelectedDate)))
        {
            Pauses.Add(new PauseRow(
                pause.Id,
                pause.Start.ToString("HH:mm"),
                pause.End.ToString("HH:mm"),
                TimeFormat.Hm(pause.Minutes)));
        }

        Entries.Clear();
        foreach (var entry in tracking.GetEntries(DateOnly.FromDateTime(SelectedDate)))
        {
            Entries.Add(new DayEntryRow(
                entry.Id,
                entry.Start.ToString("HH:mm"),
                entry.End.ToString("HH:mm"),
                entry.Customer?.Name ?? string.Empty,
                TimeFormat.Hm(entry.BreakMinutes),
                TimeFormat.Hm(entry.NetMinutes),
                entry.Note ?? string.Empty));
        }
    }
}
