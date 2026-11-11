using FluentAssertions;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;
using Zeiterfassung.Services;
using Zeiterfassung.Tests.Support;

namespace Zeiterfassung.Tests.Services;

public class TimeTrackingServiceTests : IDisposable
{
    private readonly TestDatabase _database = new();
    private readonly FixedTimeProvider _time = new(new DateTimeOffset(2026, 10, 28, 8, 0, 0, TimeSpan.FromHours(1)));
    private readonly TimeTrackingService _tracking;
    private readonly CustomerService _customers;
    private readonly int _customerId;

    public TimeTrackingServiceTests()
    {
        _tracking = new TimeTrackingService(_database.Factory, _time);
        _customers = new CustomerService(_database.Factory, _time);
        _customerId = _customers.AddCustomer("Müller GmbH").Id;
    }

    public void Dispose() => _database.Dispose();

    [Fact]
    public void Timer_Stoppen_erzeugt_Buchung_mit_Netto_Zeit()
    {
        _tracking.StartTimer(_customerId);
        _time.Advance(TimeSpan.FromHours(2));

        var entry = _tracking.StopTimer();

        entry.Should().NotBeNull();
        entry!.Start.Should().Be(new TimeOnly(8, 0));
        entry.End.Should().Be(new TimeOnly(10, 0));
        entry.NetMinutes.Should().Be(120);
        entry.FromTimer.Should().BeTrue();
        _tracking.GetActiveTimer().Should().BeNull();
    }

    [Fact]
    public void Pausen_werden_von_der_Netto_Zeit_abgezogen()
    {
        _tracking.StartTimer(_customerId);
        _time.Advance(TimeSpan.FromMinutes(60));
        _tracking.PauseTimer();
        _time.Advance(TimeSpan.FromMinutes(30));
        _tracking.ResumeTimer();
        _time.Advance(TimeSpan.FromMinutes(60));

        var entry = _tracking.StopTimer();

        entry!.BreakMinutes.Should().Be(30);
        entry.NetMinutes.Should().Be(120);
    }

    [Fact]
    public void Laufende_Pause_beim_Stoppen_zaehlt_als_Pause()
    {
        _tracking.StartTimer(_customerId);
        _time.Advance(TimeSpan.FromMinutes(60));
        _tracking.PauseTimer();
        _time.Advance(TimeSpan.FromMinutes(15));

        var entry = _tracking.StopTimer();

        entry!.BreakMinutes.Should().Be(15);
        entry.NetMinutes.Should().Be(60);
    }

    [Fact]
    public void Wechsel_des_Kunden_stoppt_den_laufenden_Timer_und_bucht_ihn()
    {
        var otherCustomer = _customers.AddCustomer("Schmidt AG").Id;
        _tracking.StartTimer(_customerId);
        _time.Advance(TimeSpan.FromHours(1));

        _tracking.StartTimer(otherCustomer);

        var entries = _tracking.GetEntries(new DateOnly(2026, 10, 28));
        entries.Should().ContainSingle(e => e.CustomerId == _customerId && e.NetMinutes == 60);
        _tracking.GetActiveTimer()!.CustomerId.Should().Be(otherCustomer);
    }

    [Fact]
    public void Timer_fuer_archivierten_Kunden_wird_abgelehnt()
    {
        _customers.SetArchived(_customerId, true);

        var act = () => _tracking.StartTimer(_customerId);

        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void Ueberlappende_manuelle_Buchung_wird_abgelehnt()
    {
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = _customerId,
            Date = new DateOnly(2026, 10, 28),
            Start = new TimeOnly(9, 0),
            End = new TimeOnly(11, 0)
        });

        var act = () => _tracking.AddEntry(new TimeEntry
        {
            CustomerId = _customerId,
            Date = new DateOnly(2026, 10, 28),
            Start = new TimeOnly(10, 0),
            End = new TimeOnly(12, 0)
        });

        act.Should().Throw<DomainException>().WithMessage("*überschneidet*");
    }

    [Fact]
    public void Timer_der_ueber_Mitternacht_laeuft_wird_nicht_gebucht()
    {
        _tracking.StartTimer(_customerId);
        _time.Now = new DateTimeOffset(2026, 10, 29, 0, 30, 0, TimeSpan.FromHours(1));

        var act = () => _tracking.StopTimer();

        act.Should().Throw<DomainException>().WithMessage("*Mitternacht*");
    }

    [Fact]
    public void Kunde_mit_Buchungen_kann_nicht_geloescht_werden()
    {
        _tracking.AddEntry(new TimeEntry
        {
            CustomerId = _customerId,
            Date = new DateOnly(2026, 10, 28),
            Start = new TimeOnly(9, 0),
            End = new TimeOnly(10, 0)
        });

        var act = () => _customers.DeleteCustomer(_customerId);

        act.Should().Throw<DomainException>().WithMessage("*archivieren*");
    }
}
