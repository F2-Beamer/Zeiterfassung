# Zeiterfassung

Lokale Windows-Anwendung zur Zeiterfassung (WPF, .NET 10, SQLite). Die Anwendung läuft als portable Single-File-EXE ohne Installation.

## Start

1. `Zeiterfassung.exe` in einen eigenen Ordner kopieren, z. B. `C:\Tools\Zeiterfassung\`.
2. Beim ersten Start legt die App die Datenbank `zeiterfassung.db` im selben Ordner an. Ist dieser Ordner nicht beschreibbar (z. B. unter `Program Files`), nutzt sie `%AppData%\Zeiterfassung`.
3. Windows SmartScreen zeigt beim ersten Start eine Warnung, weil die EXE nicht signiert ist. Mit „Weitere Informationen“ und „Trotzdem ausführen“ startet sie.

## Funktionen

- **Heute**: Live-Timer per Klick auf einen Kunden. Pause-Button, „Stoppen & buchen“. Tagesbuchungen, Ist, Soll, Saldo und Überstundenstand.
- **Monat**: Tagesliste mit Ist, Soll und Saldo, Monatssummen und Überstundenstand am Monatsende.
- **Erfassung**: Manuelle Buchungen mit Datum, Kunde, Start, Ende, Pause und Notiz. Überlappungen und Buchungen über Mitternacht werden abgelehnt.
- **Abwesenheit**: Urlaub, Krank und Freizeitausgleich, ganz oder halb. Anzeige des Urlaubskontos je Jahr.
- **Einstellungen**: Kunden (archivieren statt löschen), Soll-Arbeitszeit mit Gültigkeitsdatum, Urlaubsanspruch und Übertrag, Überstunden-Startsaldo, Bundesland, Feiertage (Abruf über date.nager.at), Datensicherung.

## Datensicherung

- Beim Start wird automatisch eine Sicherung im Ordner `backups` angelegt. Es bleiben die letzten 10 Sicherungen.
- Unter Einstellungen lässt sich manuell sichern und eine Sicherung wiederherstellen. Vor dem Wiederherstellen wird der aktuelle Stand gesichert. Danach startet die App neu.

## Entwicklung

```bash
dotnet build Zeiterfassung.slnx
dotnet test Zeiterfassung.slnx
```

## Portable Single-File-EXE bauen

```bash
dotnet publish src/Zeiterfassung/Zeiterfassung.csproj -c Release -o publish
```

Ergebnis: `publish\Zeiterfassung.exe` (ca. 70 MB, eigenständig, ohne installierte .NET-Runtime lauffähig).

Die Konfiguration steht in `src/Zeiterfassung/Zeiterfassung.csproj` (`PublishSingleFile`, `SelfContained`, `IncludeNativeLibrariesForSelfExtract`).
