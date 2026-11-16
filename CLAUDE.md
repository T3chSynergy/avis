# AVIS – Hinweise für Claude

AVIS ist ein schlankes Ticketsystem für die IT (ASP.NET Core Razor Pages auf .NET 10, EF Core, SQLite). `README.md`
ist die kurze Übersicht; unter `docs`: `konzept.md` (Fachliches), `stand.md` (öffentlicher Stand, mitpflegen),
`einstellungen.md`, `betrieb-iis.md`, `entwicklung.md`. Hier steht nur, was sich daraus nicht ablesen lässt.

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
- **Pull Requests** nach `.github/pull_request_template.md`. Was nicht geprüft werden konnte, steht drin.
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
  sich aus vorschlagen. Abläufe höchstens als feste Funktion (Vorlage für mehrere Aufträge, feste Regeln,
  feste Schrittfolge); eine Genehmigung nur bei sehr großem Nutzen (neuer Stand, neue Rolle, eigene Mails);
  eine Engine mit einstellbaren Ständen ist ausgeschlossen. So vom Nutzer entschieden, siehe `docs/stand.md`.
- **Begriffe:** Kunde (der Anwender, für den gearbeitet wird), Auftrag, Gruppe, Bearbeiter.
- **Übernehmen ist Voraussetzung** für Weiterleiten, Kommentieren, Nachricht an den Kunden, Ändern und
  Abschließen. Die Regel sitzt im Domänenmodell (`Auftrag.NurZustaendig`), nicht nur in der Oberfläche.
- **Kunden** dürfen abgeschlossene Aufträge wieder öffnen.
- **Verlauf:** Einträge werden nie gelöscht. Ändern kann nur der Verfasser den Text interner Einträge
  (interner Kommentar, Hinweis beim Weiterleiten), und nur solange er den Auftrag bearbeitet. Der alte
  Text wird ersetzt, der Eintrag trägt „geändert“. Was ein Kunde gesehen oder per Mail bekommen hat,
  bleibt unveränderlich. So vom Nutzer entschieden.
- **Texte:** Markdown ohne Formatierleiste (vom Nutzer verworfen: wirkt überladen), kein fremder Editor wie
  EasyMDE. Dafür eine Schaltfläche „Vorschau“, die der Server rendert. Bilder einfügen mit Strg+V bleibt.
- **HTML-Mails:** Nur eindeutige Auszeichnungen (fett, kursiv, Überschrift, Link, Listen) und kleine,
  einfache Tabellen werden zu Markdown; alles andere wird schlichter Text.
- **Oberfläche:** Blau nur für Anklickbares, damit Schaltflächen sich vom Schmuck unterscheiden. Hell und
  dunkel: Farben nur als Variablen am Anfang von `avis.css`, die dunkle Palette zweimal (ein Test wacht darüber).
- **Persönliche Einstellungen:** Seite `/Einstellungen` für alle, am `Benutzer` gespeichert; weitere kommen dorthin.
- **JavaScript** nur als Zugabe, ohne Framework und ohne Node-Werkzeuge; deshalb auch kein ESLint.
- **E-Mail-Eingang** über IMAP (EWS wäre im Haus möglich, wurde nicht gewählt). Wer Aufträge stellen darf,
  begrenzt `Posteingang:NurBekannteAbsender`; ob ein Absender echt ist, kann nur der Mailserver prüfen.
- **Kunden und Bearbeiter aus dem AD:** AVIS zieht selbst, je ein LDAP-Filter (dynamische Verteilergruppen
  führen keine Mitglieder). Das AD gewinnt; fehlende Kunden werden inaktiv, Bearbeiter verlieren die Rolle.
- **Geräte aus Snipe-IT:** nur lesend, sonst wie die Kunden. Nutzer ist der Kunde mit derselben Mailadresse.
- **Abgleiche laufen einmal täglich** zur festen Uhrzeit, nie beim Start: Snipe-IT ist langsam.
- **Standort** (etwa der Raum „A 105“) ist freier Text an Kunde und Gerät. Der Auftrag hat keinen eigenen.
- **Geräte am Auftrag** nur über „INV <Nummer>“ in seinen Texten (`Geraetebezug`), kein Feld dafür.
- **Hilfe:** Markdown unter `src/Avis.Web/Hilfe`, je eine Datei für Bearbeiter, Kunden und Administratoren.
  **Bei jeder Änderung an der Oberfläche die Hilfe mitpflegen.** Hauseigenes steht nie darin (lokale Datei).
- **Datenbank:** SQLite; ein späterer Wechsel zu SQL Server soll möglich bleiben. Die Suche läuft deshalb
  über eigene `Suchtext`-Spalten in Kleinbuchstaben, nicht über Funktionen einer bestimmten Datenbank.
  Ausnahmen in `Startdaten`: `Auftraege:ErsteNummer` setzt den Zähler von SQLite direkt
  (`AuftragsnummerAnhebenAsync`), und das Neuanlegen der Testdaten leert die Tabellen mit Anweisungen
  von SQLite (`NeuAnlegenAsync`). Bei einem Wechsel sind beide Stellen neu zu schreiben.
- **.NET 10 (LTS) bleibt.** Pakete von .NET stehen auf `10.0.*`; Dependabot schlägt deren nächste
  Hauptversion nicht vor. Der Umstieg ist eine eigene Entscheidung.
- **`global.json` nicht anheben.** Die Version dort ist eine Mindestversion; eine Anhebung sperrt Rechner
  mit älterem SDK der Reihe 10.0 aus (auch die Cloud-Umgebung). Dependabot beobachtet sie deshalb nicht.
- **Codeanalyse:** Abgeschaltete Regeln stehen mit Grund in `.editorconfig`. In den Tests ist CA1305 aus,
  weil dort nur ganze Zahlen als Formularwerte geschrieben werden.
- **Fremde Komponenten:** Bei jedem neuen oder entfernten Paket `THIRD-PARTY-NOTICES.md` anpassen, Lizenztexte
  aus den Repos der Hersteller, nicht aus dem Gedächtnis. Das Favicon ist selbst gezeichnet; fremde Grafiken nicht.
- **Lizenz:** AGPL-3.0-or-later, ohne Inhabernamen. Verweis am Ende der Hilfe (`Lizenz.cs`) bleibt; Pakete müssen passen.
- **`SECURITY.md`** sagt bewusst keine Antwortfrist zu.
- **Releases** heißen `v1.2.3`; nur der Nutzer legt sie an. Der Workflow „Paket“ hängt das ZIP für IIS an.
  Einstellungen einer Installation stehen in `appsettings.Production.json`, nie im Paket.

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
- **Analysemeldungen** zeigt `dotnet build -warnaserror` nur für Projekte, die es neu übersetzt. Nach einem
  Fehlversuch vor dem Hochladen mit `--no-incremental` bauen, sonst fällt eine Meldung erst im Workflow auf.
- **Migrationen:** Eine neue Spalte ist harmlos. Ein Fremdschlüssel oder eine geänderte Spalte an einer
  vorhandenen Tabelle baut in SQLite die ganze Tabelle um, und das läuft nicht in einer Transaktion (EF
  warnt beim Start). Deshalb ist `Benachrichtigung.EmpfaengerId` nur eine Nummer ohne Verknüpfung. Jede
  neue Migration lesen, bevor sie eingecheckt wird.

## Nicht geprüft

In der Entwicklungsumgebung nicht prüfbar; nur der Nutzer kann es in der Zielumgebung ausprobieren.

- Im Probelauf des Nutzers haben funktioniert: Start unter IIS, Windows-Anmeldung, Nachschlagen neuer Konten,
  Bearbeiter- und Kundenabgleich (AD), Geräteabgleich (Snipe-IT, rund 30 000 Geräte), Mailversand, E-Mail-Eingang
- Noch offen: Dauerbetrieb mit nächtlichen Läufen, Update nach der Anleitung, HTML-Mails in ihrer Breite

## Offene Schritte

1. Probelauf in der Zielumgebung (siehe „Nicht geprüft“): Was der Nutzer meldet, beheben, Anleitung berichtigen.
2. Code-Scanning-Warnung „DOM text reinterpreted as HTML“ in `wwwroot/js/editor.js` (Bildvorschau über
   `URL.createObjectURL` in einem `img`): Fehlalarm, der Nutzer schließt sie als „False positive“.
3. Angedacht, nicht gebaut (`docs/stand.md`): Kundenanmeldung an den Abgleich binden; Beteiligte am Auftrag;
   Bezug zwischen Aufträgen über `AVIS 123` im Text, ohne Teilaufträge; Link von Gerät und Kunde ins Quellsystem.
4. Bekannte Lücken stehen in `docs/stand.md`.
