using FluentAssertions;
using Zeiterfassung.Integration;

namespace Zeiterfassung.Tests.Integration;

public class HolidayParserTests
{
    private const string Json = """
        [
          { "date": "2026-01-01", "localName": "Neujahr", "name": "New Year's Day", "counties": null },
          { "date": "2026-01-06", "localName": "Heilige Drei Könige", "name": "Epiphany", "counties": ["DE-BW", "DE-BY", "DE-ST"] },
          { "date": "2026-10-03", "localName": "Tag der Deutschen Einheit", "name": "German Unity Day", "counties": null }
        ]
        """;

    [Fact]
    public void Bayern_bekommt_bundesweite_und_bayerische_Feiertage()
    {
        var holidays = HolidayParser.Parse(Json, "DE-BY", DateTime.UtcNow);

        holidays.Select(h => h.Name).Should().BeEquivalentTo(
            ["Neujahr", "Heilige Drei Könige", "Tag der Deutschen Einheit"]);
        holidays.Should().OnlyContain(h => h.State == "DE-BY");
    }

    [Fact]
    public void Hamburg_bekommt_keine_Landesfeiertage_von_Bayern()
    {
        var holidays = HolidayParser.Parse(Json, "DE-HH", DateTime.UtcNow);

        holidays.Select(h => h.Name).Should().BeEquivalentTo(
            ["Neujahr", "Tag der Deutschen Einheit"]);
    }
}
