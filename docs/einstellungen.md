# Einstellungen und Anbindungen

Was sich an AVIS einstellen lässt und wie sich die Anbindungen verhalten:
Mailversand, E-Mail-Eingang und die Abgleiche mit Active Directory und
Snipe-IT. Diese Datei ist für alle, die AVIS betreiben. Wie man es auf einem
Server einrichtet, steht in [Betrieb unter IIS](betrieb-iis.md).

## Einstellungen

Die Voreinstellungen stehen in `appsettings.json` im Ordner der Anwendung (im
Repository `src/Avis.Web/appsettings.json`). Die Werte einer Installation
stehen nicht dort, sondern in einer `appsettings.Production.json` daneben: Die
überlebt ein Update, siehe [Betrieb unter IIS](betrieb-iis.md). Jede
Einstellung lässt sich auch als Umgebungsvariable setzen, mit zwei
Unterstrichen statt des Doppelpunkts (`Mail__Server`).

| Einstellung | Bedeutung |
|---|---|
| `ConnectionStrings:Avis` | Ort der SQLite-Datei, relativ zum Ordner der Anwendung |
| `Anmeldung:Art` | `Windows` für den Betrieb, `Entwicklung` für die Testanmeldung |
| `Anmeldung:Administratoren` | Anmeldenamen (`domäne\\name`), die bei der Anmeldung Administrator werden |
| `Zeitzone` | Zeitzone für die Anzeige, Zeitpunkte werden in UTC gespeichert |
| `Auftraege:ErsteNummer` | Nummer, die der nächste Auftrag mindestens bekommt, zum Beispiel `200000`, um an eine vorhandene Zählung anzuschließen. Wird beim Start angewendet und wirkt nur nach oben. Standard 0: Die Zählung beginnt bei 1 |
| `Verzeichnis:Art` | `ActiveDirectory` liest Name, E-Mail, Telefon, Abteilung und Standort (Feld „Büro“) neuer Konten aus dem Active Directory, `Keines` lässt das aus |
| `Telefonbuch:Adresse` | Adresse der Seite einer Person in einem Telefonbuch des Hauses. An die Stelle von `{kennung}` tritt die Kennung ohne Domäne; fehlt der Platzhalter, wird sie angehängt. Leer: kein Verweis |
| `Telefonbuch:Bezeichnung` | Wie der Verweis in der Oberfläche heißt, Standard `Telefonbuch` |
| `Bearbeiterabgleich:Art` | `ActiveDirectory` gleicht ab, wer Bearbeiter ist, `Aus` lässt das aus |
| `Bearbeiterabgleich:Filter` | LDAP-Filter, der bestimmt, wer Bearbeiter ist, zum Beispiel `(memberOf=CN=AVIS-Bearbeiter,OU=Gruppen,DC=example,DC=org)` |
| `Bearbeiterabgleich:Suchbasis` | Wo im Verzeichnis gesucht wird. Leer: in der ganzen Domäne |
| `Bearbeiterabgleich:Uhrzeit` | Wann der Abgleich täglich läuft, in der eingestellten Zeitzone, Standard `01:30` |
| `Kundenabgleich:Art` | `ActiveDirectory` gleicht die Kunden mit dem Active Directory ab, `Aus` lässt das aus |
| `Kundenabgleich:Filter` | LDAP-Filter, der bestimmt, wer Kunde ist, zum Beispiel `(department=*)` |
| `Kundenabgleich:Suchbasis` | Wo im Verzeichnis gesucht wird, zum Beispiel `OU=Mitarbeiter,DC=example,DC=org`. Leer: in der ganzen Domäne |
| `Kundenabgleich:Uhrzeit` | Wann der Abgleich täglich läuft, in der eingestellten Zeitzone, Standard `02:00` |
| `Geraeteabgleich:Art` | `SnipeIt` gleicht die Geräte mit Snipe-IT ab, `Aus` lässt das aus |
| `Geraeteabgleich:Adresse` | Adresse der Installation von Snipe-IT, zum Beispiel `https://inventar.example.org` |
| `Geraeteabgleich:Schluessel` | Zugangsschlüssel (API-Token) eines Benutzers in Snipe-IT |
| `Geraeteabgleich:Uhrzeit` | Wann der Abgleich täglich läuft, in der eingestellten Zeitzone, Standard `02:30`. Nach dem Kundenabgleich, weil der Nutzer eines Geräts ein Kunde ist |
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
| `Posteingang:NurBekannteAbsender` | `true`: Ein neuer Auftrag entsteht nur aus der Mail eines aktiven Kunden oder eines aktiven Kontos. Standard `false` |
| `Posteingang:BilderImTextAbKb` | Ein Bild im Text einer Mail, das als Datei kleiner ist und höchstens 300 × 120 Bildpunkte misst, gilt als Logo und wird nicht übernommen. Standard 20; mit 0 wird jedes Bild übernommen |
| `Hilfe:EigenerText` | Markdown-Datei mit eigenem Text des Hauses, der über der Hilfe steht, relativ zum Ordner der Anwendung. Standard `App_Data/Hilfe.md`. Fehlt die Datei, steht dort nichts |
| `Anhaenge:Ordner` | Ordner für Anhänge, relativ zum Ordner der Anwendung, Standard `App_Data/Anhaenge` |
| `Anhaenge:HoechstgroesseMb` | Höchstgröße einer Datei, Standard 10 |
| `Anhaenge:Hoechstzahl` | Höchstzahl der Dateien je Upload, Standard 10 |

Die Testanmeldung und die Testdaten lassen sich nur in der
Entwicklungsumgebung einschalten. Im Betrieb bricht der Start sonst ab.

Kennwörter und Schlüssel (`Mail:Kennwort`, `Posteingang:Kennwort`,
`Geraeteabgleich:Schluessel`) gehören nie in eine Datei im Repository. Wo sie
auf dem Server stehen, beschreibt [Betrieb unter IIS](betrieb-iis.md).

## Verweis ins Telefonbuch

Gibt es im Haus ein Telefonbuch oder Verzeichnis, das zu jeder Person eine
eigene Seite hat, etwa mit Vertretung und Anwesenheit, kann AVIS dorthin
verweisen. AVIS baut nur die Adresse und liest dort nichts.

```json
"Telefonbuch": {
  "Adresse": "https://telefonbuch.example.org/person/{kennung}",
  "Bezeichnung": "Telefonbuch"
}
```

- An die Stelle von `{kennung}` tritt der Anmeldename ohne die Domäne: aus
  `beispiel\musteruser` wird `musteruser`. Fehlt der Platzhalter, wird die
  Kennung an die Adresse angehängt.
- Der Verweis steht auf der Seite eines Kunden und am Auftrag: beim Kunden
  und am Namen des Bearbeiters und des bevorzugten Bearbeiters.
- Ein Kunde ohne Anmeldename, etwa einer, der nur per Mail geschrieben hat,
  bekommt keinen Verweis. Kunden selbst sehen ihn nicht.

## Mails

Jede Mail wird zusammen mit der Aktion vorgemerkt und im Hintergrund
versendet, alle 15 Sekunden. Ist der Mailserver nicht erreichbar, gelingt die
Aktion trotzdem. Die Mail wird dann nach 1, 5, 15, 60 und 240 Minuten erneut
versucht und gilt nach sechs Versuchen als fehlgeschlagen. Unter Verwaltung →
Mails steht, was gesendet wurde, was wartet und warum etwas entfallen ist.
Dort lässt sich eine fehlgeschlagene Mail erneut anstoßen.

## E-Mail-Eingang

AVIS holt ungelesene Mails aus dem Hotline-Postfach und markiert sie danach
als gelesen oder verschiebt sie in den Erledigtordner. Was mit jeder Mail
geschah, steht unter Verwaltung → Posteingang.

- Eine Mail ohne Auftragsnummer wird ein neuer Auftrag in der Eingangsgruppe.
  Den Kunden erkennt AVIS an der Absenderadresse, einen unbekannten Absender
  legt es als Kunden an. Das lässt sich einschränken, siehe
  [Wer per Mail einen Auftrag stellen kann](#wer-per-mail-einen-auftrag-stellen-kann).
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
  Schriftarten, Farben, Layout-Tabellen wie in Signaturen. Listen und Zitate
  werden höchstens zehn Stufen tief eingerückt. Ein HTML-Teil mit mehr als
  einer Million Zeichen wird nicht umgewandelt; dann gilt der Nur-Text-Teil
  der Mail oder, wenn sie keinen hat, der Text ohne Formatierung.
- Ein Bild im Text der Mail, etwa ein eingefügtes Bildschirmfoto, steht im
  Auftrag an derselben Stelle im Text.
- Anhänge werden übernommen, auch Dateien, die das Mailprogramm nicht
  ausdrücklich als Anhang kennzeichnet (manche schicken ein PDF als Teil des
  Textes, Scanner oft ganz ohne Angabe). Zu große Dateien bleiben weg und
  werden im Auftrag genannt.
- Logos in Signaturen bleiben weg. Als Logo gilt ein Bild im Text, das als
  Datei klein ist (`Posteingang:BilderImTextAbKb`) und höchstens 300 × 120
  Bildpunkte misst. Ein eingefügtes Bildschirmfoto ist fast immer breiter
  oder höher und bleibt deshalb erhalten, auch wenn die Datei klein ist. Wurde
  ein Bild ausgelassen, steht das unter Verwaltung → Posteingang in der Spalte
  „Hinweis“.
- Bei einer Antwort werden Bilder aus dem zitierten Schriftwechsel nicht noch
  einmal angehängt.
- Jede Mail wird über ihre Message-ID nur einmal verarbeitet.

### Wer per Mail einen Auftrag stellen kann

In der Voreinstellung wird jede Mail ein Auftrag, die im Postfach ankommt und
nicht automatisch erzeugt ist. Der Absender wird als Kunde angelegt und
bekommt die Bestätigung. Erreicht Werbung das Postfach, entstehen also
Aufträge und Kunden, und AVIS antwortet an Adressen, die gefälscht sein
können. Dagegen helfen zwei Dinge, am besten beide:

1. **Das Postfach am Mailserver schützen.** Das Postfach der Hotline sollte
   nur Mails von angemeldeten Absendern des eigenen Hauses annehmen. Ob ein
   Absender echt ist, kann nur der Mailserver prüfen; AVIS sieht nur, was in
   der Mail steht.
2. **`Posteingang:NurBekannteAbsender` auf `true` setzen.** Ein neuer Auftrag
   entsteht dann nur, wenn die Absenderadresse zu einem aktiven Kunden gehört
   oder zu einem aktiven Konto. Mit dem Kundenabgleich sind das die Personen
   aus dem Verzeichnis.

Was mit der Einstellung für Mails anderer Absender gilt:

- AVIS übergeht sie. Es entsteht kein Auftrag und kein Kunde, Anhänge werden
  nicht gespeichert, und es geht keine Antwort hinaus.
- Unter Verwaltung → Posteingang steht die Mail mit dem Hinweis „Absender
  unbekannt“. Im Postfach bleibt sie wie jede verarbeitete Mail erhalten.
- Wer von außen Aufträge stellen soll, etwa ein Dienstleister, wird vorher
  unter „Kunden“ mit seiner Mailadresse angelegt.
- Die Antwort auf einen eigenen Auftrag kommt immer an, auch wenn der Kunde
  inzwischen inaktiv ist.
- Lässt sich eine Mail dreimal nicht verarbeiten, bleibt sie gelesen und
  markiert im Postfach liegen, damit jemand sie sich ansieht.

Damit Antworten ankommen, muss `Mail:Absender` die Adresse des Postfachs sein,
das AVIS abruft.

Abruf und Versand sind im Probelauf gegen einen echten Mailserver gelaufen.
Die Umwandlung von HTML ist vor allem mit nachgebauten Mails aus Outlook,
Gmail und Thunderbird geprüft.

## Bearbeiterabgleich

Mit `Bearbeiterabgleich:Art = ActiveDirectory` bestimmt das Active Directory,
wer Bearbeiter ist: die Benutzerkonten, auf die der LDAP-Filter aus
`Bearbeiterabgleich:Filter` passt. In der Regel nennt der Filter eine Gruppe:

| Filter | Bearbeiter sind |
|---|---|
| `(memberOf=CN=AVIS-Bearbeiter,OU=Gruppen,DC=example,DC=org)` | die Mitglieder dieser Gruppe |
| `(memberOf:1.2.840.113556.1.4.1941:=CN=AVIS-Bearbeiter,OU=Gruppen,DC=example,DC=org)` | ihre Mitglieder samt verschachtelter Gruppen |

Zum Herauskopieren in die `appsettings.Production.json`: alle Mitglieder
einer Gruppe, auch die aus Gruppen, die ihrerseits Mitglied sind. Die lange
Zahl ist fest und sorgt für die Verschachtelung; zu ersetzen ist nur der Name
der Gruppe ab `CN=`.

```json
"Bearbeiterabgleich": {
  "Art": "ActiveDirectory",
  "Filter": "(memberOf:1.2.840.113556.1.4.1941:=CN=AVIS-Bearbeiter,OU=Gruppen,DC=example,DC=org)"
}
```

Der vollständige Name der Gruppe (Distinguished Name) muss genau stimmen.
PowerShell nennt ihn auf einem Rechner mit den Verwaltungswerkzeugen für das
Active Directory:

```powershell
(Get-ADGroup "AVIS-Bearbeiter").DistinguishedName
```

Dass nur Benutzerkonten von Personen zählen und deaktivierte nicht, ergänzt
AVIS selbst. Unter Verwaltung → Bearbeiterabgleich steht, was der letzte Lauf
getan hat, und er lässt sich dort sofort anstoßen.

- Wen das Verzeichnis nennt, der ist Bearbeiter, mit Name und E-Mail von
  dort. Gibt es sein Konto in AVIS noch nicht, wird es angelegt: Er steht
  dann schon zur Wahl, bevor er sich das erste Mal anmeldet.
- Wer schon in AVIS steht, wird über den Anmeldenamen wiedererkannt und
  übernommen.
- Wen das Verzeichnis nicht mehr nennt, der verliert die Rolle Bearbeiter.
  Seine Aufträge in Bearbeitung gehen zurück in ihre Gruppen, und er wird aus
  allen Gruppen ausgetragen. Das Konto bleibt; als Kunde kommt er weiter an
  seine eigenen Aufträge.
- Bearbeiter, die nur in AVIS dazu gemacht wurden, bleiben unberührt.
  Administratoren bleiben immer Bearbeiter.
- Bei einem Konto aus dem Verzeichnis lassen sich Name, E-Mail und die Rolle
  Bearbeiter in AVIS nicht ändern. Zum Administrator macht man jemanden
  weiter unter Verwaltung → Benutzer oder über `Anmeldung:Administratoren`.
- In welchen Gruppen von AVIS jemand ist, regelt der Abgleich nicht: Das
  trägt sich jeder selbst ein.
- Liefert das Verzeichnis keinen einzigen Bearbeiter oder ist es nicht
  erreichbar, ändert sich nichts.

## Kundenabgleich

Mit `Kundenabgleich:Art = ActiveDirectory` sind die Benutzerkonten des Active
Directory, auf die der LDAP-Filter aus `Kundenabgleich:Filter` passt, die
Kunden. AVIS liest sie einmal täglich zur eingestellten Uhrzeit (siehe
[Wann die Abgleiche laufen](#wann-die-abgleiche-laufen)); unter Verwaltung →
Kundenabgleich steht, was der letzte Lauf getan hat, und er lässt sich dort
sofort anstoßen.

Der Filter bestimmt, wer Kunde ist. Dass nur Benutzerkonten von Personen
zählen und deaktivierte Konten nicht, ergänzt AVIS selbst. Beispiele:

| Filter | Kunden sind |
|---|---|
| `(department=*)` | alle Konten, bei denen eine Abteilung eingetragen ist |
| `(mail=*@example.org)` | alle Konten mit einer Adresse dieser Domäne |
| `(memberOf=CN=AVIS-Kunden,OU=Gruppen,DC=example,DC=org)` | die Mitglieder dieser Gruppe |
| `(memberOf:1.2.840.113556.1.4.1941:=CN=AVIS-Kunden,OU=Gruppen,DC=example,DC=org)` | ihre Mitglieder samt verschachtelter Gruppen |
| `(&(objectCategory=person)(telephoneNumber=*)(physicalDeliveryOfficeName=*)(mail=*)(department=*))` | alle Personen, bei denen Telefon, Büro, E-Mail und Abteilung eingetragen sind |

Zum Herauskopieren in die `appsettings.Production.json`: alle Personen, bei
denen Telefonnummer, Büro, E-Mail-Adresse und Abteilung ausgefüllt sind.
Funktionskonten und technische Konten fallen damit meist von selbst heraus.
`(&` am Anfang verlangt, dass alle Bedingungen zugleich gelten; `=*` heißt
„ist ausgefüllt“.

```json
"Kundenabgleich": {
  "Art": "ActiveDirectory",
  "Filter": "(&(objectCategory=person)(telephoneNumber=*)(physicalDeliveryOfficeName=*)(mail=*)(department=*))"
}
```

Die Bedingung `(objectCategory=person)` ergänzt AVIS zwar selbst. Mit ihr
trifft der Filter aber auch außerhalb von AVIS dasselbe, zum Beispiel wenn
man ihn vorher ausprobiert. Dieser Befehl zählt die Konten, die er trifft;
deaktivierte zählt er mit, AVIS lässt sie später weg:

```powershell
(Get-ADUser -LDAPFilter "(&(objectCategory=person)(telephoneNumber=*)(physicalDeliveryOfficeName=*)(mail=*)(department=*))").Count
```

Mit `Kundenabgleich:Suchbasis` lässt sich die Suche auf einen Teil des
Verzeichnisses beschränken, zum Beispiel auf eine Organisationseinheit.

- Das Verzeichnis gewinnt: Name, E-Mail, Telefon, Organisationseinheit und
  Standort werden überschrieben und lassen sich in AVIS nicht ändern. Der
  Standort ist das Attribut `physicalDeliveryOfficeName` (in der Verwaltung
  des Verzeichnisses „Büro“), zum Beispiel `A 105`.
- Wer schon in AVIS steht, wird über Anmeldename oder Mailadresse
  wiedererkannt und übernommen. Das gilt auch für ein Konto, das im
  Verzeichnis gelöscht und mit demselben Anmeldenamen neu angelegt wurde: Es
  bleibt derselbe Kunde mit seinen Aufträgen.
- Auf wen der Filter nicht mehr passt oder wessen Konto deaktiviert ist, der
  wird als inaktiv markiert. Er bleibt mit seinen Aufträgen erhalten und steht
  nur nicht mehr zur Wahl, wenn ein Auftrag erfasst wird.
- Kunden, die nur in AVIS angelegt wurden, bleiben unberührt. Ein Bearbeiter
  markiert sie auf der Seite des Kunden als inaktiv.
- Liefert das Verzeichnis keinen einzigen Kunden oder ist es nicht
  erreichbar, ändert sich nichts.

Gelesen wird mit dem Konto, unter dem AVIS läuft, aus der Domäne des Servers.
Ein fehlerhafter Filter lässt den Abgleich scheitern, ohne dass sich etwas
ändert; die Meldung steht in der Verwaltung.

## Geräteabgleich

Mit `Geraeteabgleich:Art = SnipeIt` kommen die Geräte aus Snipe-IT. AVIS liest
nur: Es fragt die Assets über die Schnittstelle von Snipe-IT ab und schreibt
dort nichts. Gelesen wird einmal täglich zur eingestellten Uhrzeit (siehe
[Wann die Abgleiche laufen](#wann-die-abgleiche-laufen)); unter Verwaltung →
Geräteabgleich steht, was der letzte Lauf getan hat, und er lässt sich dort
sofort anstoßen.

| Snipe-IT | AVIS |
|---|---|
| Asset Tag | Inventarnummer |
| Name des Assets, sonst der Name des Modells | Bezeichnung |
| Kategorie | Typ |
| Standort, sonst der Standardstandort | Standort |
| Mailadresse des Benutzers, an den das Asset ausgegeben ist | Nutzer: der Kunde mit dieser Adresse |

- Snipe-IT gewinnt: Diese Angaben werden überschrieben. Ein Gerät aus
  Snipe-IT lässt sich in AVIS nicht ändern.
- Ein Gerät, das schon mit derselben Inventarnummer in AVIS steht, wird
  übernommen, nicht verdoppelt.
- Die Seite eines solchen Geräts führt mit „In Snipe-IT ansehen“ zu seiner
  Seite dort: `Geraeteabgleich:Adresse`, dann `/hardware/` und die Kennung des
  Assets.
- Der Nutzer wird nur gesetzt, wenn es in AVIS einen Kunden mit der
  Mailadresse gibt. Kunden legt der Geräteabgleich nicht an. Ist ein Asset an
  einen Standort oder ein anderes Asset ausgegeben, bleibt der Nutzer leer.
- Was Snipe-IT nicht mehr liefert, etwa archivierte oder gelöschte Assets,
  wird ausgemustert. Es bleibt mit seinen Aufträgen erhalten und lässt sich
  nur nicht mehr neu verknüpfen.
- Geräte, die nur in AVIS angelegt wurden, bleiben unberührt.
- Assets ohne Asset Tag werden übergangen. Kommt eine Inventarnummer mehrfach
  vor oder gehört sie in AVIS schon einem anderen Gerät, das die Quelle noch
  liefert, wird das Asset ausgelassen und in der Verwaltung genannt.
- Wurde ein Asset in Snipe-IT gelöscht und mit demselben Asset Tag neu
  angelegt, bleibt es in AVIS dasselbe Gerät mit seinen Aufträgen.
- Liefert Snipe-IT kein einziges Gerät, lehnt es den Schlüssel ab oder ist es
  nicht erreichbar, ändert sich nichts.

Der Schlüssel gehört zu einem Benutzer in Snipe-IT, der Assets ansehen darf.
Darf er keine Benutzer ansehen, liefert Snipe-IT keine Mailadressen, und die
Geräte bleiben ohne Nutzer. `Geraeteabgleich:Adresse` ist die Adresse, unter
der Snipe-IT im Browser erreichbar ist, ohne `/api/v1`.

Antwortet Snipe-IT auf einen Abruf nicht innerhalb von 60 Sekunden, gilt der
Lauf als gescheitert; die Meldung nennt die Stelle. Im Probelauf hat der
Abgleich rund 30 000 Geräte in kurzer Zeit gelesen.

## Wann die Abgleiche laufen

Die Abgleiche der Bearbeiter, Kunden und Geräte lesen ihre Quelle jedes Mal
ganz. Sie laufen deshalb nur einmal am Tag, zur Uhrzeit aus
`Bearbeiterabgleich:Uhrzeit`, `Kundenabgleich:Uhrzeit` und
`Geraeteabgleich:Uhrzeit`, und nicht bei jedem Start der Anwendung.

- AVIS sieht alle fünf Minuten nach, ob ein Lauf ansteht. Der letzte Lauf
  steht in der Datenbank; die Verwaltung zeigt ihn auch nach einem Neustart.
- Lief AVIS zur eingestellten Uhrzeit nicht, wird der Lauf nachgeholt, sobald
  es wieder läuft. Das gilt auch für den allerersten Lauf nach dem
  Einschalten eines Abgleichs.
- Scheitert ein Lauf, etwa weil die Quelle nicht erreichbar ist, versucht AVIS
  es nach einer Stunde wieder, bis er gelingt.
- „Jetzt abgleichen“ in der Verwaltung geht jederzeit und zählt als Lauf des
  Tages. Läuft derselbe Abgleich gerade, startet kein zweiter.

Tagsüber ändert sich an Kunden und Geräten also nichts von selbst. Ein Gerät,
das AVIS noch nicht kennt, steht am Auftrag einfach im Text.

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

## Eigener Text in der Hilfe

Die Hilfetexte der Anwendung sind für jede Installation gleich. Eigener Text
des Hauses, etwa Telefonnummer und Zeiten der Hotline, steht in der Datei aus
`Hilfe:EigenerText`, in der Voreinstellung `App_Data/Hilfe.md` auf dem Server.
Ihr Inhalt steht in einem eigenen Kasten über der Hilfe, bei Kunden wie bei
Bearbeitern. Eine Änderung ist ohne Neustart zu sehen. Ein Beispiel:

```markdown
## So erreichen Sie die IT-Hotline

Telefon **1234**, montags bis freitags von 8 bis 16 Uhr.
```

Es gilt dasselbe Markdown wie in den Aufträgen; eingegebenes HTML wird als
Text gezeigt.
