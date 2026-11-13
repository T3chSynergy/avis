# AVIS

Auftrags-, Verfolgungs- und Informationssystem: das Ticketsystem der IT.

Was AVIS können soll und warum, steht im [Konzept](docs/konzept.md).

## Stand

Die Ausbaustufen 1 bis 3 sind umgesetzt. Vorhanden sind:

- Datenmodell mit Aufträgen, Verlauf, Gruppen, Benutzern, Kunden, Kategorien und
  Standorten
- Aufträge erfassen, übernehmen, weiterleiten, intern kommentieren, Nachricht
  an den Kunden, abschließen und wieder öffnen
- Angaben und Kunde eines Auftrags nachträglich ändern, mit den bisherigen
  Werten im Verlauf
- Kunden suchen, anlegen und pflegen, mit den Aufträgen eines Kunden; beim
  Erfassen erscheinen Treffer schon während des Tippens
- Listen: Meine Aufträge, offene Aufträge meiner Gruppen, alle Aufträge
- Meine Gruppen: selbst beitreten und austreten
- Verwaltung für Administratoren: Gruppen mit Mitgliedern, Benutzer und Rollen,
  Kategorien, Standorte
- Seiten für Kunden: eigenen Auftrag stellen, eigene Aufträge mit Stand und
  Nachrichten sehen, der IT antworten
- Mails über SMTP: an den Kunden bei Erfassung, Nachricht und Abschluss, an
  Bearbeiter bei einer Antwort des Kunden und als bevorzugter Bearbeiter
- Gesperrte Gruppen, deren Aufträge nur ihre Mitglieder sehen
- Suche über alle Aufträge mit Filtern nach Status, Gruppe und Bearbeiter
- Gerätetabelle, mit der Aufträge über die Inventarnummer verknüpft werden
- Anhänge an Aufträgen, Kommentaren, Nachrichten und Antworten
- Testanmeldung für die Entwicklung, Windows-Anmeldung für den Betrieb

Als Nächstes folgt Ausbaustufe 4 mit dem E-Mail-Eingang über Exchange.

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

Mails werden in der Entwicklung nicht versendet, sondern als Dateien unter
`src/Avis.Web/App_Data/Mails` abgelegt. Ein Mailprogramm öffnet sie.

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
| `src/Avis.Web/Anmeldung` | Anmeldung, Rollen, Verzeichnisabfrage, Zuordnung von Konto und Kunde |
| `src/Avis.Web/Mail` | Mailtexte und Versand |
| `src/Avis.Web/Anhaenge` | Ablage hochgeladener Dateien |
| `src/Avis.Web/Pages` | Seiten |
| `src/Avis.Web/wwwroot/css/avis.css` | Stylesheet |
| `src/Avis.Web/wwwroot/js` | Kleine Skripte, die Seiten bequemer machen; ohne sie funktioniert alles |
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
| `Verzeichnis:Art` | `ActiveDirectory` liest Name, E-Mail, Telefon und Abteilung neuer Konten aus dem Active Directory, `Keines` lässt das aus |
| `Mail:Art` | `Smtp` versendet, `Ordner` legt Mails als Dateien ab, `Aus` sendet nichts |
| `Mail:Server`, `Mail:Port` | Mailserver, Standardport 25 |
| `Mail:Verschluesselung` | `Auto`, `Keine`, `StartTls` oder `Ssl` |
| `Mail:Benutzer`, `Mail:Kennwort` | Nur wenn der Mailserver eine Anmeldung verlangt |
| `Mail:Absender`, `Mail:Absendername` | Absenderadresse und angezeigter Name |
| `Mail:BasisUrl` | Adresse von AVIS für die Links in den Mails |
| `Anhaenge:Ordner` | Ordner für Anhänge, relativ zum Ordner der Anwendung, Standard `App_Data/Anhaenge` |
| `Anhaenge:HoechstgroesseMb` | Höchstgröße einer Datei, Standard 10 |
| `Anhaenge:Hoechstzahl` | Höchstzahl der Dateien je Upload, Standard 10 |

Die Testanmeldung und die Testdaten lassen sich nur in der
Entwicklungsumgebung einschalten. Im Betrieb bricht der Start sonst ab.

Ein Kennwort für den Mailserver gehört nicht in die Datei im Repository. Auf
dem Server lässt es sich als Umgebungsvariable `Mail__Kennwort` setzen.

## Mails

Jede Mail wird zusammen mit der Aktion vorgemerkt und im Hintergrund
versendet, alle 15 Sekunden. Ist der Mailserver nicht erreichbar, gelingt die
Aktion trotzdem. Die Mail wird dann nach 1, 5, 15, 60 und 240 Minuten erneut
versucht und gilt nach sechs Versuchen als fehlgeschlagen. Unter Verwaltung →
Mails steht, was gesendet wurde, was wartet und warum etwas entfallen ist.
Dort lässt sich eine fehlgeschlagene Mail erneut anstoßen.

Die Texte aller Mails stehen in `src/Avis.Web/Mail/Mailtexte.cs`.

## Anhänge

Die Dateien liegen im Dateisystem unter `Anhaenge:Ordner`, sortiert nach Jahr
und Monat und mit einem von AVIS vergebenen Namen. In der Datenbank stehen
Dateiname, Größe und Ablageort. Zur Datensicherung gehören deshalb die
Datenbank und dieser Ordner.

Anhänge werden immer als Download ausgeliefert und nie im Browser angezeigt.
Wer einen Anhang bekommt, entscheidet der Verlaufseintrag, an dem er hängt.

Ein Upload darf insgesamt höchstens so groß sein, wie der Webserver zulässt.
Bei IIS sind das in der Voreinstellung rund 30 MB je Anfrage.

## Betrieb unter IIS

Dieser Teil ist noch nicht auf einem echten Server erprobt.

- Auf dem Server wird das ASP.NET Core Hosting Bundle für .NET 10 benötigt.
- In der IIS-Site ist die Windows-Authentifizierung einzuschalten und die
  anonyme Authentifizierung auszuschalten.
- Das Konto des Anwendungspools braucht Schreibrechte auf den Ordner
  `App_Data`. Dort liegen die Datenbank und die Anhänge.
- Der erste Administrator wird über `Anmeldung:Administratoren` festgelegt.
- Der Hintergrundversand läuft nur, solange die Anwendung läuft. Damit IIS sie
  nicht nach Leerlauf beendet, den Anwendungspool auf „AlwaysRunning" stellen
  und das Leerlauftimeout abschalten. Sonst gehen wartende Mails erst beim
  nächsten Aufruf hinaus.
- Die Abfrage des Active Directory nutzt die Domäne, in der der Server Mitglied
  ist. Schlägt sie fehl, arbeitet AVIS mit dem Anmeldenamen weiter und schreibt
  eine Warnung ins Protokoll.
