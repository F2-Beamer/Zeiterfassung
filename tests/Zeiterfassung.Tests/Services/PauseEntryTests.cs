using FluentAssertions;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;
using Zeiterfassung.Tests.Support;

namespace Zeiterfassung.Tests.Services;

public class PauseEntryTests : IDisposable
{
    private static readonly DateOnly Day = new(2027, 1, 15);

    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2027, 1, 15, 12, 0, 0, TimeSpan.FromHours(1)));
    private readonly TimeTrackingService _tracking;
    private readonly BalanceService _balance;
    private readonly CustomerService _customers;

    public PauseEntryTests()
    {
        var settings = new SettingsService(_database.Factory);
        _tracking = new TimeTrackingService(_database.Factory, _time);
        _balance = new BalanceService(_database.Factory, settings, _time);
        _customers = new CustomerService(_database.Factory, _time);
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Pause_wird_von_der_Ist_Zeit_des_Tages_abgezogen()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(16, 0)
        });

        _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 30));

        _balance.GetDay(Day).ActualMinutes.Should().Be(7 * 60 + 30);
    }

    [Fact]
    public void Pause_in_Luecke_zwischen_Buchungen_mindert_die_Ist_Zeit_nicht()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(7, 0),
            End = new TimeOnly(12, 0)
        });
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(12, 30),
            End = new TimeOnly(16, 0)
        });

        _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 30));

        _balance.GetDay(Day).ActualMinutes.Should().Be(8 * 60 + 30);
    }

    [Fact]
    public void Pause_die_teilweise_in_Buchung_liegt_mindert_nur_den_Teil_in_der_Buchung()
    {
        var customerId = _customers.AddCustomer("Müller GmbH").Id;
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = customerId,
            Date = Day,
            Start = new TimeOnly(8, 0),
            End = new TimeOnly(12, 15)
        });

        _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 30));

        _balance.GetDay(Day).ActualMinutes.Should().Be(4 * 60 + 15 - 15);
    }

    [Fact]
    public void Pause_ohne_Kunde_ist_moeglich()
    {
        var pause = _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 45));

        pause.Minutes.Should().Be(45);
        _tracking.GetPauses(Day).Should().ContainSingle();
    }

    [Fact]
    public void Ueberschneidende_Pausen_werden_abgelehnt()
    {
        _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(13, 0));

        var act = () => _tracking.AddPause(Day, new TimeOnly(12, 30), new TimeOnly(13, 30));

        act.Should().Throw<DomainException>().WithMessage("*überschneidet*");
    }

    [Fact]
    public void Pausenende_vor_Pausenbeginn_wird_abgelehnt()
    {
        var act = () => _tracking.AddPause(Day, new TimeOnly(13, 0), new TimeOnly(12, 0));

        act.Should().Throw<DomainException>().WithMessage("*nach dem Pausenbeginn*");
    }

    [Fact]
    public void Pause_ohne_Buchung_ergibt_keine_negative_Ist_Zeit()
    {
        _tracking.AddPause(Day, new TimeOnly(12, 0), new TimeOnly(12, 30));

        _balance.GetDay(Day).ActualMinutes.Should().Be(0);
    }
}
