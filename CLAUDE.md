# AVIS – Hinweise für Claude

AVIS ist ein schlankes Ticketsystem für die IT (ASP.NET Core Razor Pages auf .NET 10, EF Core, SQLite).
Was es kann und wie man es startet, steht in `README.md`, das Fachliche in `docs/konzept.md`.
Hier steht nur, was sich daraus und aus dem Code nicht ablesen lässt.

Diese Datei kurz halten (Ziel: höchstens 120 Zeilen) und keine Versionsgeschichte führen – die steht in
Git und in den Pull Requests. Nach einer neuen Entscheidung oder Erkenntnis ergänzen, Erledigtes streichen.

## Arbeitsweise

- **Sprache:** Deutsch – mit dem Nutzer (Du), in Doku, Commits und Pull Requests. Bezeichner im Code sind
  deutsch, Umlaute umschrieben (`Auftraege`, `Geraet`). Die Oberfläche duzt Bearbeiter und spricht Kunden
  neutral an.
- **Ablauf:** Arbeitszweig `claude/…` vom aktuellen `main` → Pull Request gegen `main` → Prüfungen abwarten →
  **Merge erst, wenn der Nutzer es sagt** („pull request und merge“), als Merge-Commit. Nur mit grünen
  Prüfungen mergen. Danach den Arbeitszweig auf `main` nachziehen.
- **Vor dem Hochladen:** `dotnet build -warnaserror`, `dotnet format --verify-no-changes`, `dotnet test`.
  Dasselbe prüft der Workflow `.github/workflows/pruefung.yml` unter Windows und Linux.
- **Pull Requests** gliedern: Worum es geht / Änderungen / Geprüft / Noch nicht geprüft. Was nicht geprüft
  werden konnte, steht ausdrücklich drin.
- **Das Repository ist öffentlich.** Keine echten Namen, Adressen, Servernamen oder Zugangsdaten einchecken –
  nur erfundene Beispiele (`example.org`, `avis.test`).
- **Rechtliche und organisatorische Rahmenbedingungen eines Einsatzorts gehören nicht ins Repo** – weder in
  Doku noch in Code, Commits oder Pull Requests. Vorgabe des Nutzers.
- **Nur der Nutzer kann:** Repo-Einstellungen ändern (Beschreibung, Standardzweig, Schalter unter „Security“)
  und Code-Scanning-Warnungen einsehen oder schließen. Claude sieht nur das CodeQL-Ergebnis am einzelnen
  Pull Request; für eine Warnung auf `main` den Nutzer um Regel, Datei und Zeile bitten.
- **Cloud-Sitzung:** Umgebung `avis`; ihr Skript installiert das .NET-SDK aus dem Ubuntu-Paket (Reihe
  10.0.1xx). Bauen und Testen geht dort, die Zielumgebung (siehe „Nicht geprüft“) nicht.

## Entscheidungen

- **Klein und einfach.** Leitsatz des Nutzers: „Was das alte AVIS nicht brauchte, braucht das neue auch
  nicht.“ Keine einstellbaren Abläufe, keine Pflichtkopplung an das Inventar. Solche Ausbauten nicht von
  sich aus vorschlagen.
- **Begriffe:** Kunde (der Anwender, für den gearbeitet wird), Auftrag, Gruppe, Bearbeiter.
- **Übernehmen ist Voraussetzung** für Weiterleiten, Kommentieren, Nachricht an den Kunden, Ändern und
  Abschließen. Die Regel sitzt im Domänenmodell (`Auftrag.NurZustaendig`), nicht nur in der Oberfläche.
- **Kunden** dürfen abgeschlossene Aufträge wieder öffnen.
- **Texte:** Markdown ohne Formatierleiste (vom Nutzer verworfen: lenkt ab, wird kaum benutzt). Bilder
  einfügen mit Strg+V bleibt.
- **HTML-Mails:** Nur eindeutige Auszeichnungen (fett, kursiv, Überschrift, Link, Listen) und kleine,
  einfache Tabellen werden zu Markdown; alles andere wird schlichter Text.
- **Oberfläche:** Blau nur für Anklickbares, damit Schaltflächen sich vom Schmuck unterscheiden.
- **JavaScript** nur als Zugabe, ohne Framework und ohne Node-Werkzeuge; deshalb auch kein ESLint.
- **E-Mail-Eingang** über IMAP (EWS wäre im Haus möglich, wurde nicht gewählt).
- **Datenbank:** SQLite; ein späterer Wechsel zu SQL Server soll möglich bleiben. Die Suche läuft deshalb
  über eigene `Suchtext`-Spalten in Kleinbuchstaben, nicht über Funktionen einer bestimmten Datenbank.
- **.NET 10 (LTS) bleibt.** Pakete von .NET stehen auf `10.0.*`; Dependabot schlägt deren nächste
  Hauptversion nicht vor. Der Umstieg ist eine eigene Entscheidung.
- **`global.json` nicht anheben.** Die Version dort ist eine Mindestversion; eine Anhebung sperrt Rechner
  mit älterem SDK der Reihe 10.0 aus (auch die Cloud-Umgebung). Dependabot beobachtet sie deshalb nicht.
- **Codeanalyse:** Abgeschaltete Regeln stehen mit Grund in `.editorconfig`. In den Tests ist CA1305 aus,
  weil dort nur ganze Zahlen als Formularwerte geschrieben werden.
- **Fremde Komponenten:** Bei jedem neuen oder entfernten Paket `THIRD-PARTY-NOTICES.md` anpassen. Das
  Favicon ist selbst gezeichnet (Flächen, keine Schrift); fremde Grafiken nicht übernehmen.
- **`SECURITY.md`** sagt bewusst keine Antwortfrist zu.
- **Lizenz:** noch nicht entschieden, es gibt keine `LICENSE`.

## Erkenntnisse

- **Sprache des Rechners:** Anfragen laufen mit `de-DE` (`UseRequestLocalization`), die Hintergrunddienste
  für Mailversand und Posteingang aber mit der Einstellung des Servers. Zahlen und Datumsangaben deshalb
  immer mit fester Kultur schreiben und lesen. Zweimal ging das schief: beim Ablageordner der Anhänge
  (`yyyy/MM` wurde zu `yyyy.MM`) und bei der Dateigröße.
- **Zeilenenden in Mails:** MimeKit schreibt und liest den Text eines Mailteils mit den Zeilenenden des
  Rechners (unter Windows CRLF). Die Anwendung vereinheitlicht eingehende Texte. Tests wandeln Mails mit
  `NewLineFormat.Dos` in die Rohform und vergleichen Mailtexte mit `ReplaceLineEndings("\n")`.
- **Windows und Linux:** Die Tests müssen unter beiden laufen. `.gitattributes` legt LF fest, sonst
  enthielten mehrzeilige Texte im Code unter Windows andere Zeichen.
- **Namensvergleich in der Verwaltung** (`ToLower()` in der Abfrage) läuft in der Datenbank und ist von den
  Kultur-Regeln ausgenommen. SQLite wandelt dabei nur ASCII um: „Ärger“ und „ärger“ gelten als verschieden.
- **Dependabot-Pull-Requests:** Ihre Prüfungen liefen gegen den Stand von `main` beim Anlegen – vor dem
  Mergen auf dem aktuellen `main` nachprüfen. Bei neuen Hauptversionen der Testwerkzeuge im Log nachsehen,
  dass wirklich alle Tests liefen: Ein Testläufer, der keine Tests findet, meldet ebenfalls Erfolg.
- **Lizenztexte** für `THIRD-PARTY-NOTICES.md` stammen aus den Repos der Hersteller, nicht aus dem
  Gedächtnis.

## Nicht geprüft

In der Entwicklungsumgebung nicht prüfbar; muss in der Zielumgebung ausprobiert werden:

- Windows-Anmeldung (`Anmeldung:Art = Windows`) unter IIS
- Abgleich mit Active Directory (`ActiveDirectoryVerzeichnis`)
- SMTP und IMAP gegen Exchange – IMAP lief bisher nur gegen einen lokalen Dovecot-Server
  (Test mit der Umgebungsvariable `AVIS_TEST_IMAP`)
- HTML-Mails aus dem echten Outlook

## Offene Schritte

1. Probelauf in der Zielumgebung (siehe „Nicht geprüft“).
2. Testumgebung: Der Nutzer überlegt, automatisch auszuliefern. Offen ist, wofür sie dienen soll (Oberfläche
   zeigen oder Zielumgebung testen). Erster Schritt wäre ein Paket aus `dotnet publish` als Artefakt des
   Workflows.
3. Code-Scanning-Warnung „DOM text reinterpreted as HTML“ in `wwwroot/js/editor.js` (Bildvorschau über
   `URL.createObjectURL` in einem `img`): Fehlalarm, der Nutzer schließt sie als „False positive“.
4. Lizenz wählen.
5. Bekannte Lücken: doppelte Kunden lassen sich nicht zusammenführen oder stilllegen; Anhänge lassen sich
   nicht löschen; im Browser eingefügte Bilder stehen unter dem Text, nicht mittendrin; keine
   Markdown-Vorschau.
