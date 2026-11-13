# Release Notes

## Version 1.1.1

### Änderungen
- **Pause-Feld entfernt:** Im Formular für Kundenbuchungen gibt es kein Feld „Pause (Minuten)“ mehr. Pausen trägst du im Bereich „Pause (ohne Kunde)“ mit Beginn und Ende ein. Beim Bearbeiten einer Buchung bleibt die vorhandene Pause erhalten.
- **Feiertagsabruf begrenzt:** Die Antwort von date.nager.at wird auf 1 MiB begrenzt. Größere Antworten werden abgelehnt.
- **Logbuch ohne Datenpfad:** Das Startlogbuch enthält keinen Datenordner mehr.
- **Hilfetext angepasst:** Die Anleitung beschreibt die Pausen im neuen Bereich.

### Technik
- Tests: 66 bestanden (3 neu für die Größenbegrenzung).
- Build: `dotnet publish src/Zeiterfassung/Zeiterfassung.csproj -c Release -r win-x64 --self-contained true -o publish-neu`

## Version 1.1.0

### Neu
- **Hilfe-Menü** neben „Einstellungen“ mit Anleitung und Versionsnummer.
- **Monat-Ansicht überarbeitet:** Zusammenfassung in Klartext, Saldo groß dargestellt, Tageszeilen mit Kundenstunden im Hinweis.
- **Abwesenheiten neu geregelt:**
  - Pro Tag ist nur eine Abwesenheit erlaubt.
  - Ausnahme: halber Urlaub und halber Freizeitausgleich am selben Tag.
  - Halber Urlaub und halber Krankentag dürfen mit Arbeitszeit kombiniert werden, Freizeitausgleich ebenfalls.
  - Ganzer Urlaub oder ganzer Krankentag sperrt Buchungen und Pausen an diesem Tag.
- **Kompakte Zeitfelder:** Start, Ende und Pause-Beginn/-Ende nehmen nur Ziffern und einen Doppelpunkt an (max. 5 Zeichen).

### Behoben
- Button-Text bei „Speichern“, „Pause eintragen“ und „Eintragen“ war auf hellem Hintergrund nicht lesbar.
- Hilfe-Menü war im Dunkelmodus nicht lesbar.
- Versionsnummer im Hilfe-Dialog war nicht lesbar.

## Version 1.0.0

Erstversion mit Heute, Monat, Erfassung, Abwesenheit, Einstellungen und Datensicherung.
