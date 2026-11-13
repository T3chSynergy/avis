# AVIS

Auftrags-, Verfolgungs- und Informationssystem: das Ticketsystem der IT.

Was AVIS können soll und warum, steht im [Konzept](docs/konzept.md).

## Stand

Alle vier Ausbaustufen des Konzepts sind umgesetzt. Vorhanden sind:

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
- E-Mail-Eingang über IMAP: Mails an die Hotline werden Aufträge, Antworten
  mit Auftragsnummer im Betreff landen im Auftrag
- Kunden öffnen abgeschlossene Aufträge wieder, im Browser oder per Mail
- Testanmeldung für die Entwicklung, Windows-Anmeldung für den Betrieb

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

## Bedienung

- Alt+N öffnet auf jeder Seite „Auftrag erfassen", für Kunden „Neuer Auftrag".
- Alt+S springt in das Suchfeld im Kopf.
- Strg+Eingabe sendet auf der Auftragsseite das Formular ab, in dem man gerade
  schreibt.
- Strg+V fügt ein Bild aus der Zwischenablage als Anhang ein. Dateien lassen
  sich auch auf das Textfeld ziehen.

## Texte mit Markdown

Beschreibung, Kommentare, Nachrichten und Antworten verstehen Markdown: fett,
kursiv, Listen, Zitate, Code und Links. Wer Markdown kennt, tippt es einfach;
eine Formatierleiste gibt es bewusst nicht. Gespeichert wird der reine Text,
erst die Anzeige formatiert ihn. Suche und Mails arbeiten deshalb mit dem reinen Text,
in Mails stehen die Markdown-Zeichen so, wie sie getippt wurden.

Eingegebenes HTML wird als Text gezeigt. Links führen nur zu `http`, `https`
und `mailto`. Bilder von fremden Adressen werden nicht geladen, Bilder gehören
als Anhang an den Eintrag. Die Umsetzung steht in
`src/Avis.Web/Markdowntext.cs`.

Die Tastenkürzel brauchen JavaScript. Ohne funktioniert alles andere genauso.

Blau steht in der Oberfläche für alles, was man anklicken kann: Schaltflächen
und Links. Der Kopf und Markierungen nutzen es nicht.

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
| `src/Avis.Web/Posteingang` | Abruf des Hotline-Postfachs und Verarbeitung eingehender Mails |
| `src/Avis.Web/Anhaenge` | Ablage hochgeladener Dateien |
| `src/Avis.Web/Pages` | Seiten |
| `src/Avis.Web/wwwroot/css/avis.css` | Stylesheet |
| `src/Avis.Web/wwwroot/js` | Kleine Skripte, die Seiten bequemer machen (Suche beim Tippen, Bilder einfügen, Tastenkürzel); ohne sie funktioniert alles |
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
| `Posteingang:Art` | `Imap` ruft das Hotline-Postfach ab, `Aus` nicht |
| `Posteingang:Server`, `Posteingang:Port` | IMAP-Server, Standardport 993 |
| `Posteingang:Verschluesselung` | `Auto`, `Keine`, `StartTls` oder `Ssl` |
| `Posteingang:Benutzer`, `Posteingang:Kennwort` | Anmeldung am Postfach |
| `Posteingang:Ordner` | Ordner, aus dem ungelesene Mails geholt werden, Standard `INBOX` |
| `Posteingang:Erledigtordner` | Ordner, in den verarbeitete Mails verschoben werden. Leer: Sie bleiben als gelesen im Eingang |
| `Posteingang:AbstandSekunden` | Abstand zwischen zwei Abrufen, Standard 60 |
| `Posteingang:BilderImTextAbKb` | Kleinere Bilder im Text einer Mail gelten als Logo und werden nicht übernommen, Standard 20 |
| `Anhaenge:Ordner` | Ordner für Anhänge, relativ zum Ordner der Anwendung, Standard `App_Data/Anhaenge` |
| `Anhaenge:HoechstgroesseMb` | Höchstgröße einer Datei, Standard 10 |
| `Anhaenge:Hoechstzahl` | Höchstzahl der Dateien je Upload, Standard 10 |

Die Testanmeldung und die Testdaten lassen sich nur in der
Entwicklungsumgebung einschalten. Im Betrieb bricht der Start sonst ab.

Kennwörter gehören nicht in die Datei im Repository. Auf dem Server lassen sie
sich als Umgebungsvariablen `Mail__Kennwort` und `Posteingang__Kennwort`
setzen.

## Mails

Jede Mail wird zusammen mit der Aktion vorgemerkt und im Hintergrund
versendet, alle 15 Sekunden. Ist der Mailserver nicht erreichbar, gelingt die
Aktion trotzdem. Die Mail wird dann nach 1, 5, 15, 60 und 240 Minuten erneut
versucht und gilt nach sechs Versuchen als fehlgeschlagen. Unter Verwaltung →
Mails steht, was gesendet wurde, was wartet und warum etwas entfallen ist.
Dort lässt sich eine fehlgeschlagene Mail erneut anstoßen.

Die Texte aller Mails stehen in `src/Avis.Web/Mail/Mailtexte.cs`.

## E-Mail-Eingang

AVIS holt ungelesene Mails aus dem Hotline-Postfach und markiert sie danach
als gelesen oder verschiebt sie in den Erledigtordner. Was mit jeder Mail
geschah, steht unter Verwaltung → Posteingang.

- Eine Mail ohne Auftragsnummer wird ein neuer Auftrag in der Eingangsgruppe.
  Den Kunden erkennt AVIS an der Absenderadresse, einen unbekannten Absender
  legt es als Kunden an.
- Steht im Betreff eine Auftragsnummer in der Form `[AVIS-123]` und kommt die
  Mail vom Kunden dieses Auftrags, ist sie seine Antwort. Der zitierte
  bisherige Schriftwechsel wird abgeschnitten. Ein abgeschlossener Auftrag
  wird dadurch wieder geöffnet.
- Kommt eine solche Mail von einem Bearbeiter, wird sie ein interner Kommentar.
- Kommt sie von jemand anderem, wird sie ein neuer Auftrag mit einem Hinweis.
  Niemand kann per Mail in den Auftrag eines anderen schreiben.
- Abwesenheitsnotizen, Zustellberichte, Rundmails und Mails von der eigenen
  Absenderadresse werden übergangen.
- Anhänge werden übernommen. Zu große Dateien und kleine Bilder im Text (Logos
  in Signaturen) bleiben weg, zu große werden im Auftrag genannt.
- Jede Mail wird über ihre Message-ID nur einmal verarbeitet.
- Lässt sich eine Mail dreimal nicht verarbeiten, bleibt sie gelesen und
  markiert im Postfach liegen, damit jemand sie sich ansieht.

Damit Antworten ankommen, muss `Mail:Absender` die Adresse des Postfachs sein,
das AVIS abruft.

Der Abruf ist gegen einen IMAP-Server (Dovecot) erprobt, nicht gegen
Exchange. Der dazugehörige Test läuft nur, wenn die Umgebungsvariable
`AVIS_TEST_IMAP` in der Form `host:port:benutzer:kennwort` gesetzt ist. Er
verarbeitet die ungelesenen Mails dieses Postfachs und markiert sie als
gelesen, gehört also nur an ein Testpostfach.

## Anhänge

Die Dateien liegen im Dateisystem unter `Anhaenge:Ordner`, sortiert nach Jahr
und Monat und mit einem von AVIS vergebenen Namen. In der Datenbank stehen
Dateiname, Größe und Ablageort. Zur Datensicherung gehören deshalb die
Datenbank und dieser Ordner.

Bilder (PNG, JPEG, GIF, WebP) erscheinen im Verlauf als Vorschau und lassen
sich in voller Größe öffnen. Ob eine Datei ein Bild ist, erkennt AVIS am
Inhalt, nicht am Namen. Alle anderen Anhänge werden nur als Download
ausgeliefert und nie im Browser angezeigt, auch SVG nicht.
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
- Der Hintergrundversand und der Abruf des Postfachs laufen nur, solange die
  Anwendung läuft. Damit IIS sie
  nicht nach Leerlauf beendet, den Anwendungspool auf „AlwaysRunning" stellen
  und das Leerlauftimeout abschalten. Sonst gehen wartende Mails erst beim
  nächsten Aufruf hinaus, und neue Mails werden erst dann abgeholt.
- Am Exchange-Server muss IMAP für das Hotline-Postfach eingeschaltet sein.
- Die Abfrage des Active Directory nutzt die Domäne, in der der Server Mitglied
  ist. Schlägt sie fehl, arbeitet AVIS mit dem Anmeldenamen weiter und schreibt
  eine Warnung ins Protokoll.
