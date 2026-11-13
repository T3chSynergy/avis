# AVIS

Auftrags-, Verfolgungs- und Informationssystem: das Ticketsystem der IT.

Was AVIS können soll und warum, steht im [Konzept](docs/konzept.md).

## Stand

Ausbaustufe 1 ist begonnen. Vorhanden sind:

- Datenmodell mit Aufträgen, Verlauf, Gruppen, Benutzern, Kunden, Kategorien und
  Standorten
- Aufträge erfassen, übernehmen, weiterleiten, intern kommentieren, Nachricht
  an den Kunden, abschließen und wieder öffnen
- Listen: Meine Aufträge, offene Aufträge meiner Gruppen, alle Aufträge
- Meine Gruppen: selbst beitreten und austreten
- Verwaltung für Administratoren: Gruppen mit Mitgliedern, Benutzer und Rollen,
  Kategorien, Standorte
- Testanmeldung für die Entwicklung, Windows-Anmeldung für den Betrieb

Noch offen in Ausbaustufe 1:

- Kunden suchen beim Erfassen (bisher Zuordnung über die E-Mail-Adresse)
- Felder eines Auftrags nachträglich ändern

## Voraussetzungen

- .NET 10 SDK

## Starten

```bash
dotnet run --project src/Avis.Web
```

Die Anwendung läuft dann unter http://localhost:5274. In der
Entwicklungsumgebung legt sie beim ersten Start eine SQLite-Datenbank unter
`src/Avis.Web/App_Data` mit Testdaten an und bietet eine Testanmeldung, bei der
man den Benutzer auswählt. Wer die Datenbank löscht, bekommt beim nächsten
Start frische Testdaten.

## Testen

```bash
dotnet test
```

Die Tests prüfen die Regeln des Auftrags (`tests/Avis.Tests/AuftragTests.cs`)
und die Seiten gegen eine laufende Anwendung mit eigener Datenbank
(`tests/Avis.Tests/SeitenTests.cs`).

## Aufbau

| Ort | Inhalt |
|---|---|
| `src/Avis.Web/Domain` | Auftrag, Verlauf und Stammdaten mit ihren Regeln |
| `src/Avis.Web/Data` | Datenbankzugriff, Migrationen, Start- und Testdaten |
| `src/Avis.Web/Anmeldung` | Anmeldung und Rollen |
| `src/Avis.Web/Pages` | Seiten |
| `src/Avis.Web/wwwroot/css/avis.css` | Stylesheet |
| `tests/Avis.Tests` | Tests |

Bezeichner im Code sind deutsch und folgen den Begriffen des Konzepts, Umlaute
sind ausgeschrieben (`Auftraege`, `Geraet`).

## Datenbank ändern

Nach einer Änderung am Datenmodell wird eine Migration erzeugt:

```bash
dotnet tool restore
```

```bash
dotnet ef migrations add NameDerAenderung --project src/Avis.Web --output-dir Data/Migrations
```

Beim Start bringt die Anwendung die Datenbank selbst auf den aktuellen Stand.

## Einstellungen

Die Einstellungen stehen in `src/Avis.Web/appsettings.json`.

| Einstellung | Bedeutung |
|---|---|
| `ConnectionStrings:Avis` | Ort der SQLite-Datei, relativ zum Ordner der Anwendung |
| `Anmeldung:Art` | `Windows` für den Betrieb, `Entwicklung` für die Testanmeldung |
| `Anmeldung:Administratoren` | Anmeldenamen (`domäne\\name`), die bei der Anmeldung Administrator werden |
| `Zeitzone` | Zeitzone für die Anzeige, Zeitpunkte werden in UTC gespeichert |

Die Testanmeldung und die Testdaten lassen sich nur in der
Entwicklungsumgebung einschalten. Im Betrieb bricht der Start sonst ab.

## Betrieb unter IIS

Dieser Teil ist noch nicht auf einem echten Server erprobt.

- Auf dem Server wird das ASP.NET Core Hosting Bundle für .NET 10 benötigt.
- In der IIS-Site ist die Windows-Authentifizierung einzuschalten und die
  anonyme Authentifizierung auszuschalten.
- Das Konto des Anwendungspools braucht Schreibrechte auf den Ordner
  `App_Data`.
- Der erste Administrator wird über `Anmeldung:Administratoren` festgelegt.
