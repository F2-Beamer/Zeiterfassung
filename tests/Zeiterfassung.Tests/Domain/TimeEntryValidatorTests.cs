using FluentAssertions;
using Zeiterfassung.Domain;
using Zeiterfassung.Models;

namespace Zeiterfassung.Tests.Domain;

public class TimeEntryValidatorTests
{
    private static TimeEntry Entry(int id, string start, string end, int breakMinutes = 0) => new()
    {
        Id = id,
        CustomerId = 1,
        Date = new DateOnly(2026, 10, 28),
        Start = TimeOnly.Parse(start),
        End = TimeOnly.Parse(end),
        BreakMinutes = breakMinutes
    };

    [Fact]
    public void Gueltige_Buchung_ohne_Ueberlappung_ist_ok()
    {
        var errors = TimeEntryValidator.Validate(
            Entry(0, "08:00", "12:00"),
            [Entry(1, "13:00", "17:00")]);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Ueberlappung_wird_abgelehnt()
    {
        var errors = TimeEntryValidator.Validate(
            Entry(0, "11:00", "13:00"),
            [Entry(1, "12:00", "14:00")]);

        errors.Should().ContainSingle().Which.Should().Contain("überschneidet");
    }

    [Fact]
    public void Buchung_schliesst_direkt_an_eine_andere_an_ohne_Ueberlappung()
    {
        var errors = TimeEntryValidator.Validate(
            Entry(0, "12:00", "13:00"),
            [Entry(1, "08:00", "12:00")]);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Eigene_Buchung_wird_beim_Bearbeiten_nicht_als_Ueberlappung_gewertet()
    {
        var errors = TimeEntryValidator.Validate(
            Entry(1, "08:00", "12:00"),
            [Entry(1, "08:00", "12:00")]);

        errors.Should().BeEmpty();
    }

    [Fact]
    public void Ende_vor_Start_wird_abgelehnt()
    {
        var errors = TimeEntryValidator.Validate(Entry(0, "14:00", "13:00"), []);

        errors.Should().ContainSingle();
    }

    [Fact]
    public void Pause_laenger_als_die_Dauer_wird_abgelehnt()
    {
        var errors = TimeEntryValidator.Validate(Entry(0, "08:00", "09:00", breakMinutes: 60), []);

        errors.Should().ContainSingle().Which.Should().Contain("Pausenzeit");
    }

    [Fact]
    public void Netto_Minuten_ziehen_die_Pause_ab()
    {
        Entry(0, "08:00", "12:30", breakMinutes: 30).NetMinutes.Should().Be(240);
    }
}
