using FluentAssertions;
using Zeiterfassung.Models;
using Zeiterfassung.Services;
using Zeiterfassung.Tests.Support;
using Zeiterfassung.ViewModels;

namespace Zeiterfassung.Tests.ViewModels;

public class MonatViewModelTests : IDisposable
{
    private static readonly DateOnly Day = new(2027, 1, 15);

    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.FromHours(1)));
    private readonly TimeTrackingService _tracking;
    private readonly CustomerService _customers;
    private readonly AbsenceService _absences;
    private readonly BalanceService _balance;

    public MonatViewModelTests()
    {
        var settings = new SettingsService(_database.Factory);
        _tracking = new TimeTrackingService(_database.Factory, _time);
        _customers = new CustomerService(_database.Factory, _time);
        _absences = new AbsenceService(_database.Factory);
        _balance = new BalanceService(_database.Factory, settings, _time);
    }

    public void Dispose() => _database.Dispose();

    private MonatViewModel CreateViewModel()
    {
        var viewModel = new MonatViewModel(_balance, _time, new NoopDayDialog());
        viewModel.Refresh();
        return viewModel;
    }

    private void AddBooking(int customerId, int startHour, int endHour, int breakMinutes = 0) =>
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(startHour, 0),
            End = new TimeOnly(endHour, 0),
            BreakMinutes = breakMinutes
        });

    [Fact]
    public void Hinweis_zeigt_pro_Kunde_die_Netto_Stunden_des_Tages()
    {
        var mueller = _customers.AddCustomer("Müller GmbH").Id;
        var huber = _customers.AddCustomer("Huber AG").Id;
        AddBooking(mueller, 8, 16, breakMinutes: 30);   // 7:30
        AddBooking(huber, 16, 18);                      // 2:00
        AddBooking(mueller, 18, 19);                    // +1:00 → Müller 8:30 gesamt

        var row = CreateViewModel().Days.Single(d => d.Date == Day);

        row.Note.Should().Be("Müller GmbH 8:30  ·  Huber AG 2:00");
    }

    [Fact]
    public void Hinweis_zeigt_Abwesenheit_ohne_Kundenstunden_an_dem_Tag()
    {
        _absences.AddRange(Day, Day, AbsenceType.Sick, 0.5m, null, includeWeekends: true);

        var row = CreateViewModel().Days.Single(d => d.Date == Day);

        row.Note.Should().Be("Krank (halb)");
    }

    [Fact]
    public void Tag_ohne_Buchung_hat_keinen_Kundenhinweis()
    {
        var row = CreateViewModel().Days.Single(d => d.Date == Day);

        row.Note.Should().BeEmpty();
    }

    private sealed class NoopDayDialog : IDayDialog
    {
        public void Show(DateOnly date, Action changed) { }
    }
}
