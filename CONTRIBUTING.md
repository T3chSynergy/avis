# Mitarbeiten an AVIS

Fehlerberichte, Wünsche und Pull Requests sind willkommen. Diese Seite sagt,
worauf es im Projekt ankommt und wie eine Änderung hineinkommt.

**Sicherheitslücken** bitte nicht als Issue oder Pull Request melden, sondern
vertraulich: siehe [SECURITY.md](SECURITY.md).

## Grundregeln

- **Keine echten Daten.** Das Repository ist öffentlich. Namen, Mailadressen,
  Servernamen, Auftragstexte und Zugangsdaten gehören nirgends hinein: nicht
  in Code, Tests, Doku, Issues oder Bildschirmfotos. Beispiele sind erfunden
  und nutzen Adressen wie `example.org` und `avis.test`.
- **Nichts, was nur für ein Haus gilt.** AVIS ist für jede Installation
  gleich. Was ein einzelner Einsatzort braucht, steht in dessen Einstellungen
  oder in dessen eigenem Hilfetext, nicht im Repository.
- **Klein und einfach.** AVIS ist ein schlankes Ticketsystem und soll es
  bleiben: keine einstellbaren Abläufe, und es läuft auch ohne Verzeichnis und
  Inventarsystem. Eine größere Erweiterung bitte zuerst als Issue vorschlagen.
- **Deutsch** in Oberfläche, Hilfe, Dokumentation, Commits und Pull Requests.
  Auch die Bezeichner im Code sind deutsch, Umlaute umschrieben (`Auftraege`,
  `Geraet`). Die Oberfläche duzt Bearbeiter und spricht Kunden neutral an.
- **JavaScript nur als Zugabe.** Jede Seite funktioniert ohne. Es gibt kein
  Framework und keine Node-Werkzeuge.

## Entwicklungsumgebung

Nötig ist nur das **.NET 10 SDK**, unter Windows oder Linux.

```bash
git clone https://github.com/T3chSynergy/avis.git
cd avis
dotnet run --project src/Avis.Web
```

Die Anwendung läuft dann unter http://localhost:5274 mit einer Testanmeldung
und Testdaten; Mails landen als Dateien in einem Ordner. Mehr dazu steht im
[README](README.md) unter „Starten“, der Aufbau des Codes unter „Aufbau“ und
das Fachliche im [Konzept](docs/konzept.md).

## Vor dem Pull Request

Diese drei Befehle müssen ohne Meldung durchlaufen. Dasselbe prüft GitHub bei
jedem Pull Request unter Windows und Linux.

```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

## Ablauf für Änderungen

1. Bei größeren Änderungen zuerst ein **Issue** eröffnen und die Idee
   abstimmen.
2. Einen Zweig vom aktuellen `main` anlegen.
3. Die Änderung umsetzen und **Tests ergänzen**.
4. Mitpflegen, was dazugehört:
   - **Hilfe:** Ändert sich die Oberfläche, auch die Hilfetexte unter
     `src/Avis.Web/Hilfe` anpassen. Ein Test prüft, dass jede Seite einen
     Abschnitt in der Hilfe hat.
   - **Dokumentation:** `README.md` für Einstellungen und Betrieb,
     `docs/konzept.md` für das Fachliche.
   - **Pakete:** Kommt eines hinzu oder fällt eines weg,
     `THIRD-PARTY-NOTICES.md` anpassen. Seine Lizenz muss zur AGPL passen.
5. Einen **Pull Request gegen `main`** stellen, gegliedert in: Worum es geht,
   Änderungen, Geprüft, Noch nicht geprüft. Was sich nicht prüfen ließ, steht
   ausdrücklich drin.
6. Gemergt wird nur mit grünen Prüfungen.

Ein Release bekommt einen Namen der Form `v1.2.3`. Der Workflow „Paket“ baut
daraus das Paket für Windows Server und hängt es an das Release.

## Worauf zu achten ist

- **Windows und Linux.** Die Tests laufen unter beiden. Zahlen und
  Datumsangaben deshalb immer mit fester Kultur schreiben und lesen, und in
  Tests Texte unabhängig von den Zeilenenden des Rechners vergleichen.
- **Migrationen.** Eine neue Spalte ist harmlos. Ein Fremdschlüssel oder eine
  geänderte Spalte an einer vorhandenen Tabelle baut in SQLite die ganze
  Tabelle um. Jede neue Migration lesen, bevor sie eingecheckt wird; siehe
  „Datenbank ändern“ im README.
- **Datenbank.** Abfragen sollen nicht von SQLite abhängen: Ein späterer
  Wechsel der Datenbank soll möglich bleiben. Die Suche läuft deshalb über
  eigene Spalten in Kleinbuchstaben.
- **Was sich ohne die Zielumgebung nicht prüfen lässt:** Windows-Anmeldung
  unter IIS, die Abgleiche mit Active Directory und Snipe-IT, SMTP und IMAP
  gegen Exchange. Wer daran etwas ändert und es nicht dort ausprobieren kann,
  schreibt das in den Pull Request.

## Lizenz

AVIS steht unter der **GNU AGPL-3.0-or-later** (siehe [LICENSE](LICENSE)). Mit
einem Pull Request erklärst du dich einverstanden, dass dein Beitrag unter
derselben Lizenz veröffentlicht wird.
