# Umsetzungsplan: Zeiterfassung (Windows, portable)

Stand: 2026-10-28. Grundlage ist das Ergebnis des Interviews (Grill-Me). Offene Punkte stehen in Abschnitt 7.

---

## 0. Festgelegte Entscheidungen

| Thema | Entscheidung |
|---|---|
| UI-Technologie | WPF auf .NET 10 LTS |
| Datenhaltung | SQLite über Entity Framework Core |
| Speicherort DB | Neben der EXE. Fallback: `%AppData%\Zeiterfassung`, wenn der Ordner nicht beschreibbar ist |
| Soll-Arbeitszeit | Wochenstunden, verteilt auf Arbeitstage (Standard Mo–Fr). Pro Wochentag überschreibbar. Zeitlich versioniert (wirkt ab Gültigkeitsdatum) |
| Überstundenkonto | Startsaldo mit Stichtag, danach fortlaufend berechnet (Ist − Soll, kumuliert) |
| Urlaubskonto | Jahresanspruch in Tagen. Resturlaub des Vorjahres als manueller Startwert. Halbe Tage (0,5) buchbar. Kein automatischer Verfall |
| Live-Timer | Nur ein aktiver Timer. Wechsel per Klick stoppt den laufenden. Startzeit wird persistiert (überlebt Neustart/Absturz) |
| Pausen | Manueller Pause-Button am Timer. Pausen werden als Summe gespeichert. Keine automatische Pausenregel |
| Abwesenheiten | Ganz- oder halbtägig. Urlaub und Krank wirken neutral auf den Saldo (Soll des Tages entfällt). Freizeitausgleich baut den Saldo ab (Soll des Tages wird vom Überstundenkonto abgezogen) |
| Feiertage | Online-Abruf über `date.nager.at` (kein Schlüssel). Lokaler Cache in der DB. Manuelle Aktualisierung pro Jahr. Bundesland in den Einstellungen wählbar |
| Auswertungen | Tagesansicht und Monatsübersicht. Jahresübersicht ist eine spätere Phase |
| Zeitvalidierung | Überlappende Buchungen werden abgelehnt. Buchungen über Mitternacht nicht erlaubt (aufteilen) |
| Kunden | Anlegen, Bearbeiten, Archivieren. Löschen nur ohne Buchungen, sonst Archivieren |
| Backup | Manueller Button (SQLite-Backup-API, Zeitstempel). Automatisches Backup beim Start aktiv, begrenzt auf die letzten 10 Kopien. Wiederherstellen per Dateiauswahl, vorher automatische Sicherung des aktuellen Stands |
| Jahreswechsel Überstunden | Keine Aktion nötig. Der Saldo läuft fortlaufend weiter |
| Jahreswechsel Urlaub | Rest des Vorjahres (Anspruch − genommen) wird automatisch als Übertrag übernommen, unbegrenzt und ohne Verfall. Manuelles Korrekturfeld für Sonderfälle |
| Signierung | Unsigniert. Hinweis zur SmartScreen-Warnung in der Doku |
| Zielplattform | Windows x64 |

---

## 1. Architekturübersicht

**Muster:** MVVM mit klarer Schichtung.

```
Views (XAML)  ──binden──▶  ViewModels  ──nutzen──▶  Services  ──nutzen──▶  Repositories / DbContext
                                                        │
                                                        └──▶  HolidayApiClient (HttpClient)
```

- **Views:** nur XAML und minimaler Code-Behind. Keine Geschäftslogik.
- **ViewModels:** Zustand und Befehle. Nutzen `CommunityToolkit.Mvvm` (`ObservableObject`, `RelayCommand`, `[ObservableProperty]`).
- **Services (Geschäftslogik):**
  - `TimeTrackingService`: Buchungen, Timer, Validierung (Überlappung, Mitternacht)
  - `TargetTimeService`: Soll-Berechnung je Tag inkl. Versionierung, Feiertage, Abwesenheiten
  - `BalanceService`: Überstundensaldo und Urlaubskonto (Startwerte + Berechnung)
  - `HolidayService`: Abruf, Cache, Jahresaktualisierung
  - `AbsenceService`: Urlaub, Krank, Freizeitausgleich
  - `ReportService`: Tages- und Monatsauswertung
  - `BackupService`: Kopie und Rotation
  - `SettingsService`: Einstellungen inkl. Pfadauflösung der DB
- **Persistenz:** `AppDbContext` (EF Core). Repositories nur, wo Abfragen wiederverwendet werden. Ansonsten direkt über Services.
- **Dependency Injection:** `Microsoft.Extensions.DependencyInjection`, Start in `App.xaml.cs`.
- **Navigation:** einfacher `NavigationService` oder ein Hauptfenster mit `ContentControl` und ViewModel-Wechsel.

**Rechenregeln (zentral in den Services, testbar):**

- Netto-Ist eines Tages = Summe(Ende − Start) − Summe(Pausen)
- Soll eines Tages = Wochenstunden-Profil (gültig am Datum) / Arbeitstage, außer Feiertag oder Urlaub/Krank (dann 0)
- Saldo-Tag = Ist − Soll, bei Freizeitausgleich zusätzlich − Soll
- Überstundenstand = Startsaldo + Σ(Saldo-Tage seit Stichtag)
- Urlaubsrest = Übertrag + Anspruch + Korrektur − Σ(genommene Urlaubstage, halbe Tage als 0,5)
- Übertrag eines Jahres = Urlaubsrest des Vorjahres (rekursiv berechnet, Startjahr nutzt manuellen Startwert)

---

## 2. Datenbank-Schema

SQLite-Datei `zeiterfassung.db`. Alle Zeitstempel lokal als `DateTime` (Datum und Uhrzeit getrennt, siehe unten).

| Entität | Felder (Auswahl) | Hinweise |
|---|---|---|
| **Customer** | `Id`, `Name`, `IsArchived`, `CreatedAt` | Archivieren statt Löschen bei vorhandenen Buchungen |
| **TimeEntry** | `Id`, `CustomerId` (FK), `Date`, `StartTime`, `EndTime`, `BreakMinutes`, `Note`, `Source` (Manuell/Timer) | Index auf `Date`. Check: `EndTime > StartTime`, kein Mitternachtsübergang |
| **ActiveTimer** | `Id`, `CustomerId`, `StartedAt`, `PausedAt?`, `AccumulatedBreakSeconds` | Höchstens eine Zeile. Persistiert laufenden Timer |
| **WorkTimeProfile** | `Id`, `ValidFrom`, `WeeklyHours`, `MonWorkdays`/`MonHours` … (je Wochentag optional) | Versioniert. Gültig ab `ValidFrom` bis zum nächsten Eintrag |
| **Absence** | `Id`, `Date`, `Type` (Urlaub/Krank/Freizeitausgleich), `Portion` (Ganz/Halb), `Note` | Index auf `Date` |
| **Holiday** | `Id`, `Date`, `Name`, `State` (z. B. `DE-BY`), `Year`, `FetchedAt` | Cache, pro Jahr und Bundesland eindeutig |
| **VacationEntitlement** | `Id`, `Year`, `Days`, `CorrectionDays` | Anspruch je Jahr. Übertrag wird berechnet (Rest des Vorjahres), `CorrectionDays` ist ein optionales manuelles Korrekturfeld |
| **OvertimeOpening** | `Id`, `AsOfDate`, `Hours` | Startsaldo mit Stichtag (genau ein aktiver Eintrag) |
| **AppSetting** | `Key`, `Value` | Bundesland, Auto-Backup an/aus, Backup-Pfad |

**Hinweise zum Schema:**
- Stunden als `decimal` oder Minuten als `int` speichern. Keine Fließkommafehler (Minuten-Basis bevorzugt).
- `Date` als `DateOnly`, Zeiten als `TimeOnly` (EF Core 8+ unterstützt beides mit SQLite über Converter bzw. Standardmapping, bei Bedarf Converter ergänzen).
- Migrationen über `dotnet ef migrations`. Initialmigration beim ersten Start automatisch anwenden.

---

## 3. Projektstruktur & Abhängigkeiten

**Struktur (Solution):**

```
Zeiterfassung/
├── Zeiterfassung.sln
├── src/
│   └── Zeiterfassung/
│       ├── App.xaml / App.xaml.cs          # DI-Start, Pfadauflösung DB
│       ├── Models/                         # EF-Entitäten
│       ├── Data/                           # AppDbContext, Migrations
│       ├── Services/                       # Geschäftslogik (Abschnitt 1)
│       ├── Integration/                    # HolidayApiClient
│       ├── ViewModels/
│       ├── Views/                          # XAML
│       ├── Converters/                     # WPF-Value-Converter
│       └── Resources/                      # Styles, Themes
└── tests/
    └── Zeiterfassung.Tests/                # xUnit, Fokus auf Services
```

**NuGet-Pakete (vorläufig):**

| Paket | Zweck |
|---|---|
| `CommunityToolkit.Mvvm` | MVVM-Basis |
| `Microsoft.EntityFrameworkCore.Sqlite` | Datenzugriff |
| `Microsoft.EntityFrameworkCore.Design` | Migrationen (nur Entwicklung) |
| `Microsoft.Extensions.DependencyInjection` | DI |
| `Microsoft.Extensions.Http` | `HttpClient`-Factory für Feiertage |
| `Microsoft.Extensions.Logging` + Datei-Logger (z. B. `Serilog.Sinks.File`) | Fehlerprotokoll neben der DB |
| `xunit`, `FluentAssertions` (oder Shouldly) | Tests |

**Projekt-Einstellungen (wichtig für Deployment):**
- `TargetFramework`: `net10.0-windows`
- `UseWPF`: `true`
- `Nullable`: `enable`, `ImplicitUsings`: `enable`

---

## 4. Phaseneinteilung

Jede Phase endet mit einem lauffähigen Stand und, wo sinnvoll, mit Tests für die Services.

**Phase 0: Grundgerüst**
- Solution und Projekte anlegen, Pakete einbinden
- DI-Start, Hauptfenster mit Navigation (Platzhalter-Views)
- Pfadauflösung der DB: EXE-Ordner, Fallback `%AppData%`
- Datei-Logging

**Phase 1: Datenbank und Einstellungen**
- Entitäten, `AppDbContext`, Initialmigration
- `SettingsService`, Einstellungsseite (Bundesland, Auto-Backup)
- Tests: Pfadauflösung, Migration läuft durch

**Phase 2: Stammdaten**
- Kunden: anlegen, bearbeiten, archivieren, löschen (nur ohne Buchungen)
- Wochenstunden-Profil mit Gültigkeitsdatum (versioniert), optionale Tageswerte
- Urlaubsanspruch und Übertrag je Jahr
- Überstunden-Startsaldo mit Stichtag
- Tests: Versionierung (Profil gilt ab Datum), Kunden-Regel (Archivieren vs. Löschen)

**Phase 3: Feiertage**
- `HolidayApiClient` gegen `date.nager.at` (Endpunkt `PublicHolidays/{Jahr}/DE`, Filter auf Bundesland über `counties`)
- Cache in der DB, manueller Aktualisierungs-Button pro Jahr
- Verhalten ohne Internet: gespeicherte Daten bleiben nutzbar, Hinweis in der UI
- Tests: Parsing der API-Antwort (gespeicherte Beispiel-JSON), Bundesland-Filter

**Phase 4: Zeiterfassung manuell**
- Eingabe: Datum, Start, Ende, Pause, Kunde, Notiz
- Validierung: Ende > Start, keine Überlappung, kein Mitternachtsübergang
- Liste der Buchungen pro Tag, bearbeiten und löschen
- Tests: Validierung (Überlappung, Mitternacht, Grenzfälle)

**Phase 5: Live-Timer**
- Kunden-Buttons: Klick startet, Wechsel stoppt den laufenden Timer
- Pause-Button: pausieren und fortsetzen, Pausensumme wird mitgeführt
- Persistenz der `ActiveTimer`-Zeile, Wiederaufnahme-Dialog beim Start
- Beim Beenden entsteht eine `TimeEntry` mit Pausensumme
- Tests: Wechsel-Logik, Pause-Berechnung, Wiederaufnahme nach Neustart

**Phase 6: Abwesenheiten**
- Erfassung Urlaub, Krank, Freizeitausgleich (ganz oder halb)
- Auswirkung in `TargetTimeService` und `BalanceService` gemäß Abschnitt 1
- Tests: Soll-Reduktion, Freizeitausgleich-Abbau, Urlaubsrest mit halben Tagen

**Phase 7: Auswertung**
- Tagesansicht: Buchungen, Ist, Soll, Pausen, Tages-Saldo (Visualisierung mit Fortschrittsbalken o. Ä.)
- Monatsübersicht: Tage mit Ist, Soll, Saldo, Monatssumme, Überstundenstand am Monatsende
- Tests: Monatsberechnung mit Feiertagen und Abwesenheiten

**Phase 8: Backup**
- Manueller Backup-Button mit SQLite-Backup-API
- Automatisches Backup beim Start, Rotation auf die letzten 10 Kopien
- Wiederherstellen: Dateiauswahl, Prüfung auf gültige SQLite-Datei und Schema-Version, automatische Sicherung des aktuellen Stands, Ersetzen der DB, Neustart
- Tests: Rotation, Zeitstempel im Dateinamen, Wiederherstellung mit ungültiger Datei wird abgelehnt

**Phase 9: Politur und Deployment**
- Fehlerbehandlung und Nutzerhinweise
- Publish-Profil (siehe Abschnitt 5)
- Kurze Bedienungsanleitung inkl. SmartScreen-Hinweis

---

## 5. Deployment: portable Single-File-EXE

**Publish-Befehl (Beispiel):**

```bash
dotnet publish src/Zeiterfassung/Zeiterfassung.csproj ^
  -c Release ^
  -r win-x64 ^
  --self-contained true ^
  -p:PublishSingleFile=true ^
  -p:IncludeNativeLibrariesForSelfExtract=true ^
  -p:EnableCompressionInSingleFile=true ^
  -p:DebugType=None ^
  -p:PublishTrimmed=false
```

**Begründung:**
- `SelfContained=true`: keine Runtime-Installation nötig
- `PublishSingleFile=true`: eine einzige EXE
- `IncludeNativeLibrariesForSelfExtract=true`: die native SQLite-Bibliothek (`e_sqlite3`) wird mit eingebettet, sonst fehlt sie zur Laufzeit
- `PublishTrimmed=false`: Trimming bricht erfahrungsgemäß EF Core und WPF-Reflection, deshalb aus
- Größe: ca. 70–90 MB (Self-Contained), akzeptabel für eine portable App

**Ablauf:**
1. Publish-Befehl ausführen, Ausgabe in `publish/`
2. `Zeiterfassung.exe` in einen eigenen Ordner kopieren (z. B. `C:\Tools\Zeiterfassung\`)
3. Beim ersten Start entsteht `zeiterfassung.db` neben der EXE (oder in `%AppData%`, falls der Ordner gesperrt ist)
4. Der Nutzer muss beim ersten Start die SmartScreen-Warnung mit „Trotzdem ausführen“ bestätigen (siehe Phase 9)

**Hinweis zur Entwicklung:** Die Single-File-Konfiguration verursacht beim Debuggen keine Probleme, sie sollte aber in einer Pipeline oder einem Publish-Profil getestet werden, bevor du ihr vertraust.

---

## 6. Abnahmekriterien (Kurzfassung)

- Live-Timer überlebt einen Neustart der App.
- Überstundenstand stimmt nach Erfassung eines Monats mit manueller Nachrechnung überein.
- Urlaubsrest berücksichtigt halbe Tage und den manuellen Übertrag.
- Feiertage reduzieren das Soll, auch ohne Internetverbindung nach einmaligem Abruf.
- Überlappende oder über Mitternacht gehende Buchungen werden abgelehnt.
- Die EXE startet auf einem frischen Windows-Rechner ohne installierte .NET-Runtime.

---

## 7. Offene Punkte (vor Start der Implementierung bitte bestätigen)

1. **Auto-Backup beim Start**: standardmäßig aktiv (Annahme aus deiner Antwort „A“).
2. **Halbe Urlaubstage**: buchbar (Annahme aus deiner Antwort „A“).
3. **Persistenz des Timers**: ja (Annahme aus deiner Antwort „A“).
4. **Bundesland**: Bayern als Standard, in den Einstellungen änderbar (bestätigt).
5. **Sprache der Oberfläche**: nur Deutsch (Annahme, bestätigt).
6. **Testansatz**: xUnit auf der Service-Ebene, keine UI-Tests (Annahme, bestätigt).
7. **Feiertags-API**: Online-Abfrage ist akzeptabel (bestätigt).

**Design (entschieden):** Variante B1 „Dunkel, Fokus zentriert“ (`design/b1-fokus-zentriert.html`). Verworfen: A (klassisch hell), C (kompakt mit Zeitstrahl), B2 und B3.

Merkmale von B1, die in WPF umgesetzt werden:
- Dunkles Theme, Akzentfarbe Hellblau (`#38bdf8`), Hintergrund `#0f172a`, Karten `#1e293b`
- Oben Navigation als Pill-Tabs (Heute, Monat, Erfassung, Abwesenheit, Einstellungen)
- Timer mittig, groß (ca. 72 px, Gewicht „Light“), darunter Status und zwei Buttons (Pause, Stoppen & buchen)
- Kunden-Kacheln als Chips, der aktive Kunde mit Akzentrahmen
- Drei Kennzahlen (Ist, Soll, Überstundenstand) mit Fortschrittsbalken bei Ist
- Buchungsliste unten

---

**Nächster Schritt:** Bitte bestätige mit „Plan bestätigt, starte Phase 0“. Erst dann beginne ich mit der Implementierung.
