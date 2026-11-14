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
- Listen: Meine Aufträge, offene Aufträge (meiner Gruppen und für mich vorgemerkte), alle Aufträge
- Meine Gruppen: selbst beitreten und austreten, je Gruppe eine Mail bei
  neuen Aufträgen ein- und ausschalten
- Verwaltung für Administratoren: Gruppen mit Mitgliedern, Benutzer und Rollen,
  Kategorien, Standorte
- Seiten für Kunden: eigenen Auftrag stellen, eigene Aufträge mit Stand und
  Nachrichten sehen, der IT antworten
- Mails über SMTP: an den Kunden bei Erfassung, Nachricht und Abschluss, an
  Bearbeiter bei einer Antwort des Kunden, als bevorzugter Bearbeiter und, wer
  es für eine Gruppe einschaltet, bei jedem neuen Auftrag in der Gruppe
- Gesperrte Gruppen, deren Aufträge nur ihre Mitglieder sehen
- Suche über alle Aufträge mit Filtern nach Status, Gruppe und Bearbeiter
- Gerätetabelle, mit der Aufträge über die Inventarnummer verknüpft werden
- Abgleich der Kunden mit dem Active Directory und der Geräte mit Snipe-IT
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
man den Benutzer auswählt. Frische Testdaten gibt es auf der Seite der
Testanmeldung über „Datenbank neu anlegen": Das leert die Datenbank, entfernt
die Anhänge und beginnt die Zählung von vorn.

Zu den Testdaten gehören Kunden, die für Konten aus dem Active Directory
stehen (Hanna Verzeichnis, Igor Abgleich und die inaktive Jana Ehemals), und
ein von Hand stillgelegter Kunde (Karl Ruhestand). An ihnen lässt sich ohne
Verzeichnis ansehen, wie abgeglichene und inaktive Kunden aussehen. Ebenso
stehen drei Geräte für Assets aus Snipe-IT (TB-3001, BE-3002 und das
ausgemusterte TB-2990).

Mails werden in der Entwicklung nicht versendet, sondern als Dateien unter
`src/Avis.Web/App_Data/Mails` abgelegt. Ein Mailprogramm öffnet sie. Den
Ordner nennt auch die Seite der Testanmeldung.

## Bedienung

- Alt+N öffnet auf jeder Seite „Auftrag erfassen", für Kunden „Neuer Auftrag".
- Alt+S springt in das Suchfeld im Kopf.
- Strg+Eingabe sendet auf der Auftragsseite das Formular ab, in dem man gerade
  schreibt.
- Strg+V fügt ein Bild aus der Zwischenablage als Anhang ein. Dateien lassen
  sich auch auf das Textfeld ziehen.
- Wer nacheinander mehrmals Dateien auswählt, behält die schon gewählten. Unter
  dem Dateifeld steht, was mitgeschickt wird; dort lässt sich jede Datei wieder
  entfernen.
- Fehlende Pflichtangaben und zu große, leere oder zu viele Dateien meldet schon
  der Browser, bevor das Formular abgeschickt wird. Kommt es trotzdem mit einem
  Fehler zurück, sind die gewählten Dateien nicht mehr im Formular; die Meldung
  nennt sie dann, damit man sie noch einmal anhängt.

## Texte mit Markdown

Beschreibung, Kommentare, Nachrichten und Antworten verstehen Markdown: fett,
kursiv, Listen, Zitate, Code, Links und Tabellen. Wer Markdown kennt, tippt es einfach;
eine Formatierleiste gibt es bewusst nicht. Gespeichert wird der reine Text,
erst die Anzeige formatiert ihn. Suche und Mails arbeiten deshalb mit dem reinen Text,
in Mails stehen die Markdown-Zeichen so, wie sie getippt wurden.

Eingegebenes HTML wird als Text gezeigt. Links führen nur zu `http`, `https`
und `mailto`. Bilder von fremden Adressen werden nicht geladen, Bilder gehören
als Anhang an den Eintrag. Ein umgekehrter Schrägstrich ist immer er selbst,
damit Pfade wie `\\server\freigabe` stimmen; ein Zeichen maskiert man dafür
nicht mit `\`, sondern als Zeichencode wie `&#42;`. Die Umsetzung steht in
`src/Avis.Web/Markdowntext.cs`.

Ein Verweis der Form `![Name](anhang:…)` setzt ein Bild aus den Anhängen
desselben Eintrags an diese Stelle im Text. So stehen Bilder aus Mails an
ihrem Platz. Sie erscheinen dann nicht noch einmal in der Liste der Anhänge.

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

## Code prüfen

Der Build prüft den Code mit der Codeanalyse von .NET und zeigt, was sie
findet, als Warnung. `dotnet format` bringt die Formatierung in Ordnung:

```bash
dotnet format
```

Bei jedem Pull Request laufen Build, Formatprüfung und Tests auf GitHub,
unter Windows und Linux (`.github/workflows/pruefung.yml`). Dort lässt jede
Warnung den Build scheitern. Vor dem Hochladen prüft man dasselbe so:

```bash
dotnet build -warnaserror
```

```bash
dotnet format --verify-no-changes
```

Die Regeln stehen in `.editorconfig`, zusammen mit den Regeln, die
abgeschaltet sind, und dem Grund dafür.

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
| `THIRD-PARTY-NOTICES.md` | Lizenzvermerke der mitgelieferten Komponenten; wird beim Veröffentlichen in den Ausgabeordner kopiert und ist anzupassen, wenn ein Paket hinzukommt oder wegfällt |

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
| `Auftraege:ErsteNummer` | Nummer, die der nächste Auftrag mindestens bekommt, zum Beispiel `200000`, um an eine vorhandene Zählung anzuschließen. Wird beim Start angewendet und wirkt nur nach oben. Standard 0: Die Zählung beginnt bei 1 |
| `Verzeichnis:Art` | `ActiveDirectory` liest Name, E-Mail, Telefon und Abteilung neuer Konten aus dem Active Directory, `Keines` lässt das aus |
| `Kundenabgleich:Art` | `ActiveDirectory` gleicht die Kunden mit dem Active Directory ab, `Aus` lässt das aus |
| `Kundenabgleich:Filter` | LDAP-Filter, der bestimmt, wer Kunde ist, zum Beispiel `(department=*)` |
| `Kundenabgleich:Suchbasis` | Wo im Verzeichnis gesucht wird, zum Beispiel `OU=Mitarbeiter,DC=example,DC=org`. Leer: in der ganzen Domäne |
| `Kundenabgleich:AbstandMinuten` | Abstand zwischen zwei Abgleichen, Standard 60, mindestens 5 |
| `Geraeteabgleich:Art` | `SnipeIt` gleicht die Geräte mit Snipe-IT ab, `Aus` lässt das aus |
| `Geraeteabgleich:Adresse` | Adresse der Installation von Snipe-IT, zum Beispiel `https://inventar.example.org` |
| `Geraeteabgleich:Schluessel` | Zugangsschlüssel (API-Token) eines Benutzers in Snipe-IT |
| `Geraeteabgleich:AbstandMinuten` | Abstand zwischen zwei Abgleichen, Standard 60, mindestens 5 |
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

Kennwörter und Schlüssel gehören nicht in die Datei im Repository. Auf dem
Server lassen sie sich als Umgebungsvariablen `Mail__Kennwort`,
`Posteingang__Kennwort` und `Geraeteabgleich__Schluessel` setzen.

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
- Kommt eine solche Mail von dem Bearbeiter, der den Auftrag bearbeitet, wird
  sie ein interner Kommentar.
- Kommt sie von jemand anderem, wird sie ein neuer Auftrag mit einem Hinweis.
  Niemand kann per Mail in den Auftrag eines anderen schreiben.
- Abwesenheitsnotizen, Zustellberichte, Rundmails und Mails von der eigenen
  Absenderadresse werden übergangen.
- Hat die Mail einen HTML-Teil, bleibt ihre Formatierung erhalten, soweit sie
  eindeutig ist: fett, kursiv, Überschriften, Links, Listen, Zitate und kleine,
  einfache Tabellen (höchstens 6 Spalten und 30 Zeilen, keine verbundenen
  Zellen, keine Tabelle in der Tabelle). Alles andere wird gewöhnlicher Text:
  Schriftarten, Farben, Layout-Tabellen wie in Signaturen. Die Umwandlung steht
  in `src/Avis.Web/Posteingang/HtmlZuMarkdown.cs`.
- Ein Bild im Text der Mail, etwa ein eingefügtes Bildschirmfoto, steht im
  Auftrag an derselben Stelle im Text.
- Anhänge werden übernommen. Zu große Dateien und kleine Bilder im Text (Logos
  in Signaturen) bleiben weg, zu große werden im Auftrag genannt.
- Bei einer Antwort werden Bilder aus dem zitierten Schriftwechsel nicht noch
  einmal angehängt.
- Jede Mail wird über ihre Message-ID nur einmal verarbeitet.
- Lässt sich eine Mail dreimal nicht verarbeiten, bleibt sie gelesen und
  markiert im Postfach liegen, damit jemand sie sich ansieht.

Damit Antworten ankommen, muss `Mail:Absender` die Adresse des Postfachs sein,
das AVIS abruft.

Der Abruf ist gegen einen IMAP-Server (Dovecot) erprobt, nicht gegen
Exchange. Die Umwandlung von HTML ist mit nachgebauten Mails aus Outlook,
Gmail und Thunderbird geprüft, nicht mit echten Mails aus dem Haus. Der dazugehörige Test läuft nur, wenn die Umgebungsvariable
`AVIS_TEST_IMAP` in der Form `host:port:benutzer:kennwort` gesetzt ist. Er
verarbeitet die ungelesenen Mails dieses Postfachs und markiert sie als
gelesen, gehört also nur an ein Testpostfach.

## Kundenabgleich

Mit `Kundenabgleich:Art = ActiveDirectory` sind die Benutzerkonten des Active
Directory, auf die der LDAP-Filter aus `Kundenabgleich:Filter` passt, die
Kunden. AVIS liest sie eine Minute nach dem Start und dann im eingestellten
Abstand; unter Verwaltung → Kundenabgleich steht, was der letzte Lauf getan
hat, und er lässt sich dort sofort anstoßen.

Der Filter bestimmt, wer Kunde ist. Dass nur Benutzerkonten von Personen
zählen und deaktivierte Konten nicht, ergänzt AVIS selbst. Beispiele:

| Filter | Kunden sind |
|---|---|
| `(department=*)` | alle Konten, bei denen eine Abteilung eingetragen ist |
| `(mail=*@example.org)` | alle Konten mit einer Adresse dieser Domäne |
| `(memberOf=CN=AVIS-Kunden,OU=Gruppen,DC=example,DC=org)` | die Mitglieder dieser Gruppe |
| `(memberOf:1.2.840.113556.1.4.1941:=CN=AVIS-Kunden,OU=Gruppen,DC=example,DC=org)` | ihre Mitglieder samt verschachtelter Gruppen |

Mit `Kundenabgleich:Suchbasis` lässt sich die Suche auf einen Teil des
Verzeichnisses beschränken, zum Beispiel auf eine Organisationseinheit.

- Das Verzeichnis gewinnt: Name, E-Mail, Telefon, Organisationseinheit und
  Raum werden überschrieben und lassen sich in AVIS nicht ändern. Der Raum
  ist das Attribut `physicalDeliveryOfficeName` (in der Verwaltung des
  Verzeichnisses „Büro“), zum Beispiel `A 105`.
- Wer schon in AVIS steht, wird über Anmeldename oder Mailadresse
  wiedererkannt und übernommen.
- Auf wen der Filter nicht mehr passt oder wessen Konto deaktiviert ist, der
  wird als inaktiv markiert. Er bleibt mit seinen Aufträgen erhalten und steht
  nur nicht mehr zur Wahl, wenn ein Auftrag erfasst wird.
- Kunden, die nur in AVIS angelegt wurden, bleiben unberührt. Ein Bearbeiter
  markiert sie auf der Seite des Kunden als inaktiv.
- Liefert das Verzeichnis keinen einzigen Kunden oder ist es nicht
  erreichbar, ändert sich nichts.

Gelesen wird mit dem Konto, unter dem AVIS läuft, aus der Domäne des Servers.
Ein fehlerhafter Filter lässt den Abgleich scheitern, ohne dass sich etwas
ändert; die Meldung steht in der Verwaltung. Der Abgleich ist noch nicht an
einem echten Verzeichnis erprobt.

## Geräteabgleich

Mit `Geraeteabgleich:Art = SnipeIt` kommen die Geräte aus Snipe-IT. AVIS liest
nur: Es fragt die Assets über die Schnittstelle von Snipe-IT ab und schreibt
dort nichts. Gelesen wird zwei Minuten nach dem Start und dann im
eingestellten Abstand; unter Verwaltung → Geräteabgleich steht, was der letzte
Lauf getan hat, und er lässt sich dort sofort anstoßen.

| Snipe-IT | AVIS |
|---|---|
| Asset Tag | Inventarnummer |
| Name des Assets, sonst der Name des Modells | Bezeichnung |
| Kategorie | Typ |
| Standort, sonst der Standardstandort | Raum |
| Mailadresse des Benutzers, an den das Asset ausgegeben ist | Nutzer: der Kunde mit dieser Adresse |

- Snipe-IT gewinnt: Diese Angaben werden überschrieben und lassen sich in AVIS
  nicht ändern. Nur der Standort eines Geräts gehört weiter AVIS.
- Ein Gerät, das schon mit derselben Inventarnummer in AVIS steht, wird
  übernommen, nicht verdoppelt.
- Der Nutzer wird nur gesetzt, wenn es in AVIS einen Kunden mit der
  Mailadresse gibt. Kunden legt der Geräteabgleich nicht an. Ist ein Asset an
  einen Standort oder ein anderes Asset ausgegeben, bleibt der Nutzer leer.
- Was Snipe-IT nicht mehr liefert, etwa archivierte oder gelöschte Assets,
  wird ausgemustert. Es bleibt mit seinen Aufträgen erhalten und lässt sich
  nur nicht mehr neu verknüpfen.
- Geräte, die nur in AVIS angelegt wurden, bleiben unberührt.
- Assets ohne Asset Tag werden übergangen. Kommt eine Inventarnummer mehrfach
  vor oder gehört sie in AVIS schon einem anderen Gerät der Quelle, wird das
  Asset ausgelassen und in der Verwaltung genannt.
- Liefert Snipe-IT kein einziges Gerät, lehnt es den Schlüssel ab oder ist es
  nicht erreichbar, ändert sich nichts.

Der Schlüssel gehört zu einem Benutzer in Snipe-IT, der Assets ansehen darf.
Darf er keine Benutzer ansehen, liefert Snipe-IT keine Mailadressen, und die
Geräte bleiben ohne Nutzer. `Geraeteabgleich:Adresse` ist die Adresse, unter
der Snipe-IT im Browser erreichbar ist, ohne `/api/v1`.

Die Abfrage ist nach dem Quelltext von Snipe-IT geschrieben und mit
nachgestellten Antworten geprüft, aber noch nicht an einer echten Installation
erprobt.

## Raum

Kunden und Geräte tragen einen Raum, zum Beispiel `A 105`. Bei abgeglichenen
Einträgen kommt er aus der Quelle, bei den übrigen wird er von Hand
eingetragen. Er steht in den Listen, am Auftrag beim Kunden und wird von der
Suche nach Kunden und Geräten gefunden. Der Standort bleibt daneben die
Auswahlliste, die der Administrator pflegt.

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
