using FluentAssertions;
using Zeiterfassung.Models;
using Zeiterfassung.Services;
using Zeiterfassung.Tests.Support;
using Zeiterfassung.ViewModels;

namespace Zeiterfassung.Tests.ViewModels;

public class DayDetailViewModelTests : IDisposable
{
    // 16. Januar 2027 ist ein Samstag: Dort soll die Abwesenheit trotzdem eingetragen werden.
    private static readonly DateOnly Saturday = new(2027, 1, 16);

    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.FromHours(1)));
    private readonly BalanceService _balance;
    private readonly AbsenceService _absences;
    private readonly TimeTrackingService _tracking;
    private readonly CustomerService _customers;
    private int _changedCount;

    public DayDetailViewModelTests()
    {
        var settings = new SettingsService(_database.Factory);
        _balance = new BalanceService(_database.Factory, settings, _time);
        _absences = new AbsenceService(_database.Factory);
        _tracking = new TimeTrackingService(_database.Factory, _time);
        _customers = new CustomerService(_database.Factory, _time);
    }

    public void Dispose() => _database.Dispose();

    private DayDetailViewModel CreateViewModel(DateOnly date) =>
        new(date, _balance, _absences, _tracking, () => _changedCount++);

    [Fact]
    public void Abwesenheit_am_Wochenende_wird_fuer_den_gewaehlten_Tag_eingetragen()
    {
        var viewModel = CreateViewModel(Saturday);

        viewModel.AddAbsenceCommand.Execute(null);

        viewModel.Absences.Should().ContainSingle();
        viewModel.Absences[0].Type.Should().Be("Urlaub");
        viewModel.Absences[0].Portion.Should().Be("ganz");
        viewModel.ErrorMessage.Should().BeNull();
        viewModel.NoAbsences.Should().BeFalse();
        _changedCount.Should().Be(1);
    }

    [Fact]
    public void Halber_Tag_wird_mit_Art_und_Umfang_gespeichert()
    {
        var viewModel = CreateViewModel(Saturday);
        viewModel.NewType = viewModel.TypeOptions.Single(o => o.Type == AbsenceType.Sick);
        viewModel.NewPortion = 0.5m;

        viewModel.AddAbsenceCommand.Execute(null);

        viewModel.Absences.Should().ContainSingle();
        viewModel.Absences[0].Type.Should().Be("Krank");
        viewModel.Absences[0].Portion.Should().Be("halb");
    }

    [Fact]
    public void Doppelte_Abwesenheit_desselben_Typs_zeigt_Fehler_und_speichert_nicht()
    {
        var viewModel = CreateViewModel(Saturday);
        viewModel.AddAbsenceCommand.Execute(null);

        viewModel.AddAbsenceCommand.Execute(null);

        viewModel.Absences.Should().ContainSingle();
        viewModel.ErrorMessage.Should().Contain("bereits eine Abwesenheit");
        _changedCount.Should().Be(1);
    }

    [Fact]
    public void Abwesenheit_loeschen_entfernt_sie_und_meldet_die_Aenderung()
    {
        var viewModel = CreateViewModel(Saturday);
        viewModel.AddAbsenceCommand.Execute(null);

        viewModel.DeleteAbsenceCommand.Execute(viewModel.Absences[0]);

        viewModel.Absences.Should().BeEmpty();
        viewModel.NoAbsences.Should().BeTrue();
        _changedCount.Should().Be(2);
    }

    [Fact]
    public void Buchungen_des_Tages_zeigen_Kunde_Zeit_Pause_und_Netto()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Saturday,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0),
            BreakMinutes = 30
        });

        var viewModel = CreateViewModel(Saturday);

        viewModel.Bookings.Should().ContainSingle();
        var row = viewModel.Bookings[0];
        row.Time.Should().Be("08:00 – 16:00");
        row.Customer.Should().Be("Müller GmbH");
        row.Break.Should().Be("30 min");
        row.Net.Should().Be("7:30");
        viewModel.NoBookings.Should().BeFalse();
        viewModel.Summary.Should().Contain("Ist 7:30");
    }
}
