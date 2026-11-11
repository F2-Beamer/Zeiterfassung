using System.IO;
using System.Net.Http;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;

namespace Zeiterfassung.ViewModels;

public sealed record CustomerRow(int Id, string Name, bool IsArchived)
{
    public string DisplayName => IsArchived ? $"{Name} (archiviert)" : Name;
}

public sealed record ProfileRow(int Id, string ValidFrom, string WeeklyHours);

public sealed record HolidayRow(string Date, string Name);

public sealed record FederalStateOption(string Code, string Name);

public partial class EinstellungenViewModel(
    CustomerService customers,
    MasterDataService masterData,
    BalanceService balance,
    SettingsService settings,
    HolidayService holidays,
    BackupService backups) : ObservableObject, IRefreshable
{
    private static readonly DayOfWeek[] OverrideDays =
    [
        DayOfWeek.Monday, DayOfWeek.Tuesday, DayOfWeek.Wednesday, DayOfWeek.Thursday,
        DayOfWeek.Friday, DayOfWeek.Saturday, DayOfWeek.Sunday
    ];

    private int? _editingProfileId;

    public ObservableCollection<CustomerRow> Customers { get; } = [];

    public ObservableCollection<ProfileRow> Profiles { get; } = [];

    public ObservableCollection<HolidayRow> HolidayRows { get; } = [];

    public ObservableCollection<string> BackupFiles { get; } = [];

    public IReadOnlyList<FederalStateOption> FederalStates { get; } =
        SettingsService.FederalStates.Select(s => new FederalStateOption(s.Code, s.Name)).ToList();

    // Kunden
    [ObservableProperty]
    private string _newCustomerName = string.Empty;

    [ObservableProperty]
    private CustomerRow? _selectedCustomer;

    [ObservableProperty]
    private string _renameText = string.Empty;

    // Soll-Arbeitszeit
    [ObservableProperty]
    private DateTime _profileValidFrom = new(DateTime.Today.Year, 1, 1);

    [ObservableProperty]
    private string _profileWeeklyHours = "40";

    [ObservableProperty]
    private string _overrideMonday = string.Empty;

    [ObservableProperty]
    private string _overrideTuesday = string.Empty;

    [ObservableProperty]
    private string _overrideWednesday = string.Empty;

    [ObservableProperty]
    private string _overrideThursday = string.Empty;

    [ObservableProperty]
    private string _overrideFriday = string.Empty;

    [ObservableProperty]
    private string _overrideSaturday = string.Empty;

    [ObservableProperty]
    private string _overrideSunday = string.Empty;

    [ObservableProperty]
    private ProfileRow? _selectedProfile;

    // Urlaub
    [ObservableProperty]
    private int _vacationYear = DateTime.Today.Year;

    [ObservableProperty]
    private string _vacationDays = "30";

    [ObservableProperty]
    private string _vacationCorrection = "0";

    [ObservableProperty]
    private string _vacationOpening = string.Empty;

    [ObservableProperty]
    private string _vacationStatusText = string.Empty;

    // Überstunden-Startsaldo
    [ObservableProperty]
    private DateTime _overtimeAsOf = new(DateTime.Today.Year - 1, 12, 31);

    [ObservableProperty]
    private string _overtimeHours = "0";

    [ObservableProperty]
    private string _overtimeStatusText = string.Empty;

    // Bundesland, Feiertage
    [ObservableProperty]
    private FederalStateOption? _selectedFederalState;

    [ObservableProperty]
    private int _holidayYear = DateTime.Today.Year;

    [ObservableProperty]
    private string _holidayStatusText = string.Empty;

    // Backup
    [ObservableProperty]
    private bool _autoBackupOnStart;

    [ObservableProperty]
    private string? _selectedBackupFile;

    [ObservableProperty]
    private string _backupStatusText = string.Empty;

    [ObservableProperty]
    private string? _errorMessage;

    public void Refresh()
    {
        LoadCustomers();
        LoadProfiles();
        LoadVacation();
        LoadOvertimeOpening();
        SelectedFederalState = FederalStates.First(s => s.Code == settings.GetFederalState());
        LoadHolidays();
        LoadBackups();
        AutoBackupOnStart = settings.IsAutoBackupEnabled();
    }

    // ---- Kunden ----

    [RelayCommand]
    private void AddCustomer()
    {
        Run(() => customers.AddCustomer(NewCustomerName), () => NewCustomerName = string.Empty);
    }

    [RelayCommand]
    private void RenameCustomer()
    {
        if (SelectedCustomer is null)
        {
            return;
        }

        Run(() => customers.RenameCustomer(SelectedCustomer.Id, RenameText));
    }

    [RelayCommand]
    private void ToggleArchive(CustomerRow row)
    {
        if (row is null)
        {
            return;
        }

        Run(() => customers.SetArchived(row.Id, !row.IsArchived));
    }

    [RelayCommand]
    private void DeleteCustomer(CustomerRow row)
    {
        if (row is null)
        {
            return;
        }

        Run(() => customers.DeleteCustomer(row.Id));
    }

    partial void OnSelectedCustomerChanged(CustomerRow? value)
    {
        RenameText = value?.Name ?? string.Empty;
    }

    private void LoadCustomers()
    {
        Customers.Clear();
        foreach (var customer in customers.GetCustomers(includeArchived: true))
        {
            Customers.Add(new CustomerRow(customer.Id, customer.Name, customer.IsArchived));
        }
    }

    // ---- Soll-Arbeitszeit ----

    [RelayCommand]
    private void SaveProfile()
    {
        Run(() =>
        {
            if (!TimeFormat.TryParseDecimal(ProfileWeeklyHours, out var weekly))
            {
                throw new DomainException("Die Wochenstunden bitte als Zahl angeben, z. B. 40 oder 38,5.");
            }

            var overrides = ReadOverrides();
            masterData.SaveProfile(new WorkTimeProfile
            {
                Id = _editingProfileId ?? 0,
                ValidFrom = DateOnly.FromDateTime(ProfileValidFrom),
                WeeklyMinutes = (int)Math.Round(weekly * 60m, MidpointRounding.AwayFromZero),
                Overrides = overrides
            });
        }, ResetProfileForm);
    }

    [RelayCommand]
    private void EditProfile(ProfileRow row)
    {
        var profile = masterData.GetProfiles().First(p => p.Id == row.Id);
        _editingProfileId = profile.Id;
        ProfileValidFrom = profile.ValidFrom.ToDateTime(TimeOnly.MinValue);
        ProfileWeeklyHours = TimeFormat.Hm(profile.WeeklyMinutes);
        WriteOverrides(profile.Overrides);
        ErrorMessage = null;
    }

    [RelayCommand]
    private void DeleteProfile(ProfileRow row)
    {
        Run(() => masterData.DeleteProfile(row.Id), ResetProfileForm);
    }

    [RelayCommand]
    private void NewProfile()
    {
        ResetProfileForm();
        ErrorMessage = null;
    }

    private void ResetProfileForm()
    {
        _editingProfileId = null;
        ProfileValidFrom = new DateTime(DateTime.Today.Year, 1, 1);
        ProfileWeeklyHours = "40";
        WriteOverrides([]);
    }

    private void LoadProfiles()
    {
        Profiles.Clear();
        foreach (var profile in masterData.GetProfiles())
        {
            Profiles.Add(new ProfileRow(
                profile.Id,
                profile.ValidFrom.ToDateTime(TimeOnly.MinValue).ToString("dd.MM.yyyy", TimeFormat.German),
                TimeFormat.Hm(profile.WeeklyMinutes)));
        }
    }

    private List<WorkTimeDayOverride> ReadOverrides()
    {
        var texts = new[]
        {
            OverrideMonday, OverrideTuesday, OverrideWednesday, OverrideThursday,
            OverrideFriday, OverrideSaturday, OverrideSunday
        };

        var result = new List<WorkTimeDayOverride>();
        for (var i = 0; i < texts.Length; i++)
        {
            if (string.IsNullOrWhiteSpace(texts[i]))
            {
                continue;
            }

            if (!TimeFormat.TryParseHours(texts[i], out var minutes))
            {
                throw new DomainException($"Die Stunden für {OverrideDays[i]} sind ungültig (z. B. 6 oder 6:30).");
            }

            result.Add(new WorkTimeDayOverride { Day = OverrideDays[i], Minutes = minutes });
        }

        return result;
    }

    private void WriteOverrides(IEnumerable<WorkTimeDayOverride> overrides)
    {
        var lookup = overrides.ToDictionary(o => o.Day, o => TimeFormat.Hm(o.Minutes));
        string Get(DayOfWeek day) => lookup.GetValueOrDefault(day, string.Empty);

        OverrideMonday = Get(DayOfWeek.Monday);
        OverrideTuesday = Get(DayOfWeek.Tuesday);
        OverrideWednesday = Get(DayOfWeek.Wednesday);
        OverrideThursday = Get(DayOfWeek.Thursday);
        OverrideFriday = Get(DayOfWeek.Friday);
        OverrideSaturday = Get(DayOfWeek.Saturday);
        OverrideSunday = Get(DayOfWeek.Sunday);
    }

    // ---- Urlaub ----

    [RelayCommand]
    private void SaveVacation()
    {
        Run(() =>
        {
            if (!TimeFormat.TryParseDecimal(VacationDays, out var days))
            {
                throw new DomainException("Der Urlaubsanspruch muss eine Zahl sein.");
            }

            if (!TimeFormat.TryParseDecimal(string.IsNullOrWhiteSpace(VacationCorrection) ? "0" : VacationCorrection, out var correction))
            {
                throw new DomainException("Die Korrektur muss eine Zahl sein.");
            }

            decimal? opening = null;
            if (!string.IsNullOrWhiteSpace(VacationOpening))
            {
                if (!TimeFormat.TryParseDecimal(VacationOpening, out var parsedOpening))
                {
                    throw new DomainException("Der Startwert muss eine Zahl sein.");
                }

                opening = parsedOpening;
            }

            masterData.SaveEntitlement(VacationYear, days, correction, opening);
        }, LoadVacation);
    }

    [RelayCommand]
    private void VacationYearChanged()
    {
        LoadVacation();
    }

    private void LoadVacation()
    {
        var entitlement = masterData.GetEntitlement(VacationYear);
        VacationDays = entitlement?.Days.ToString("0.##", TimeFormat.German) ?? "30";
        VacationCorrection = entitlement?.CorrectionDays.ToString("0.##", TimeFormat.German) ?? "0";
        VacationOpening = entitlement?.OpeningCarryOverDays?.ToString("0.##", TimeFormat.German) ?? string.Empty;

        var status = balance.GetVacation(VacationYear);
        VacationStatusText = $"Übertrag {TimeFormat.Days(status.CarryOverDays)}, " +
                             $"genommen {TimeFormat.Days(status.TakenDays)}, " +
                             $"Rest {TimeFormat.Days(status.RemainingDays)}";
    }

    // ---- Überstunden-Startsaldo ----

    [RelayCommand]
    private void SaveOvertimeOpening()
    {
        Run(() =>
        {
            if (!TimeFormat.TryParseHours(OvertimeHours, out var minutes))
            {
                throw new DomainException("Den Saldo bitte als Stunden angeben, z. B. 12:30 oder -3,5.");
            }

            masterData.SaveOvertimeOpening(DateOnly.FromDateTime(OvertimeAsOf), minutes);
        }, LoadOvertimeOpening);
    }

    private void LoadOvertimeOpening()
    {
        var opening = masterData.GetOvertimeOpening();
        if (opening is null)
        {
            OvertimeStatusText = "Noch kein Startsaldo hinterlegt.";
            return;
        }

        OvertimeAsOf = opening.AsOfDate.ToDateTime(TimeOnly.MinValue);
        OvertimeHours = TimeFormat.Signed(opening.Minutes);
        OvertimeStatusText = $"Gespeichert: {TimeFormat.Signed(opening.Minutes)} h zum {OvertimeAsOf:dd.MM.yyyy}";
    }

    // ---- Bundesland und Feiertage ----

    [RelayCommand]
    private void SaveFederalState()
    {
        if (SelectedFederalState is null)
        {
            return;
        }

        Run(() => settings.SetFederalState(SelectedFederalState.Code));
        LoadHolidays();
    }

    [RelayCommand]
    private async Task RefreshHolidaysAsync()
    {
        var state = SelectedFederalState?.Code ?? settings.GetFederalState();
        HolidayStatusText = "Feiertage werden abgerufen …";
        try
        {
            var count = await holidays.RefreshAsync(HolidayYear, state);
            HolidayStatusText = $"{count} Feiertage für {HolidayYear} gespeichert.";
            ErrorMessage = null;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            HolidayStatusText = "Abruf fehlgeschlagen. Der gespeicherte Stand bleibt erhalten.";
        }

        LoadHolidays();
    }

    [RelayCommand]
    private void HolidayYearChanged()
    {
        LoadHolidays();
    }

    private void LoadHolidays()
    {
        var state = SelectedFederalState?.Code ?? settings.GetFederalState();
        SelectedFederalState ??= FederalStates.First(s => s.Code == state);

        HolidayRows.Clear();
        foreach (var holiday in holidays.GetHolidays(HolidayYear, state))
        {
            HolidayRows.Add(new HolidayRow(
                holiday.Date.ToDateTime(TimeOnly.MinValue).ToString("ddd dd.MM.yyyy", TimeFormat.German),
                holiday.Name));
        }

        if (HolidayRows.Count == 0 && string.IsNullOrEmpty(HolidayStatusText))
        {
            HolidayStatusText = "Noch keine Feiertage gespeichert.";
        }
    }

    // ---- Backup ----

    partial void OnAutoBackupOnStartChanged(bool value)
    {
        settings.SetAutoBackupEnabled(value);
    }

    [RelayCommand]
    private void CreateBackup()
    {
        Run(() =>
        {
            var file = backups.CreateBackup();
            backups.Prune();
            BackupStatusText = $"Sicherung erstellt: {Path.GetFileName(file)}";
        }, LoadBackups);
    }

    [RelayCommand]
    private void RestoreBackup()
    {
        if (SelectedBackupFile is null)
        {
            return;
        }

        var confirm = MessageBox.Show(
            "Der aktuelle Datenbestand wird durch die gewählte Sicherung ersetzt.\n" +
            "Vorher wird automatisch eine Sicherung des aktuellen Stands angelegt.\n\n" +
            "Die App wird danach neu gestartet. Fortfahren?",
            "Backup wiederherstellen",
            MessageBoxButton.YesNo,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.Yes)
        {
            return;
        }

        try
        {
            backups.Restore(SelectedBackupFile);
            Process.Start(new ProcessStartInfo(Environment.ProcessPath!) { UseShellExecute = true });
            Application.Current.Shutdown();
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
        }
    }

    private void LoadBackups()
    {
        BackupFiles.Clear();
        foreach (var file in backups.ListBackups())
        {
            BackupFiles.Add(file);
        }
    }

    private void Run(Action action, Action? onSuccess = null)
    {
        try
        {
            action();
            ErrorMessage = null;
            onSuccess?.Invoke();
        }
        catch (DomainException ex)
        {
            ErrorMessage = ex.Message;
        }

        LoadCustomers();
        LoadProfiles();
    }
}
