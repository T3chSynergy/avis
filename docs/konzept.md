# AVIS – Konzept

Stand: 13.11.2026 · Status: Entwurf

AVIS (Auftrags-, Verfolgungs- und Informationssystem) ist das Ticketsystem der
IT. Es ersetzt die Jira-Lösung und knüpft an das frühere, in Classic ASP
entwickelte AVIS an.

## 1. Ziel

Ein kleines, schnelles System für formale IT-Aufträge: erfassen, an eine Gruppe
geben, übernehmen, erledigen. Größenordnung: rund 50 Bearbeiter, bis zu
40 Aufträge am Tag.

Leitsatz: Was das alte AVIS nicht brauchte, braucht das neue auch nicht.

### Bewusst nicht enthalten

- konfigurierbare Workflows und Statusmodelle
- frei definierbare Zusatzfelder
- Pflichtverknüpfungen mit Inventar oder anderen Systemen
- SLA-Uhren, Eskalationsregeln, Zeiterfassung
- Auswertungen pro Bearbeiter
- Altdatenübernahme aus Jira oder dem alten AVIS

## 2. Rollen

| Rolle | Darf |
|---|---|
| Kunde | eigene Aufträge erstellen, eigene Aufträge und deren Stand sehen, Rückfragen beantworten. Jedes Anmeldekonto ohne Rolle ist Kunde, auch Bearbeiter können eigene Aufträge stellen |
| Bearbeiter | Aufträge erfassen, alle Aufträge sehen (außer gesperrte Gruppen), übernehmen, weiterleiten, abschließen, sich selbst Gruppen zuordnen, Kunden anlegen und pflegen |
| Administrator | Gruppen, Kategorien und Geräte pflegen, Mitglieder gesperrter Gruppen zuordnen, Rollen vergeben |

Wer Bearbeiter ist, legt ein Administrator fest oder, wenn der
Bearbeiterabgleich eingeschaltet ist, das Active Directory: Ein LDAP-Filter,
in der Regel die Mitgliedschaft in einer Gruppe, bestimmt die Bearbeiter. Wen
das Verzeichnis nicht mehr nennt, der verliert die Rolle; seine Aufträge gehen
zurück in ihre Gruppen, sein Konto bleibt. Von Hand ernannte Bearbeiter und
Administratoren bleiben unberührt.

„Kunde" ist der Anwender, für den ein Auftrag bearbeitet wird.

## 3. Kernmodell

### Wo ein Auftrag liegt

Ein Auftrag gehört immer zu genau einer Gruppe und hat höchstens einen
Bearbeiter. Daraus ergibt sich der Status, es gibt kein eigenes Statusfeld zum
Auswählen:

| Gruppe | Bearbeiter | abgeschlossen | Status |
|---|---|---|---|
| gesetzt | leer | nein | offen (liegt in der Gruppe) |
| gesetzt | gesetzt | nein | in Bearbeitung |
| gesetzt | beliebig | ja | abgeschlossen |

Die Gruppe bleibt beim Übernehmen am Auftrag stehen. Der Auftrag verschwindet
nur aus der Liste der offenen Aufträge der Gruppe. Das ist nötig, damit
Aufträge gesperrter Gruppen auch während der Bearbeitung geschützt bleiben.

### Aktionen

| Aktion | Wer | Wirkung |
|---|---|---|
| Erfassen | Kunde, Bearbeiter, E-Mail-Eingang | Auftrag entsteht in einer Gruppe, Standard ist die IT-Hotline. Ein Bearbeiter kann dabei einen bevorzugten Bearbeiter nennen |
| Übernehmen | jeder Bearbeiter, der den Auftrag sehen darf | Bearbeiter wird gesetzt, auch wenn schon ein anderer eingetragen ist |
| Weiterleiten | wer den Auftrag bearbeitet | neue Gruppe wird gesetzt, Bearbeiter wird geleert, optional bevorzugter Bearbeiter |
| Interner Kommentar | wer den Auftrag bearbeitet | Eintrag im Verlauf, für den Kunden unsichtbar |
| Eintrag ändern | wer den Auftrag bearbeitet und den Eintrag geschrieben hat | Der Text eines internen Kommentars oder des Hinweises beim Weiterleiten wird ersetzt. Am Eintrag steht, wann er geändert wurde; der bisherige Text wird nicht aufbewahrt |
| Nachricht an den Kunden | wer den Auftrag bearbeitet | Eintrag im Verlauf, der Kunde sieht ihn und erhält eine Mail |
| Antwort des Kunden | Kunde | Eintrag im Verlauf |
| Ändern | wer den Auftrag bearbeitet | Angaben und Kunde anpassen, Änderung steht im Verlauf |
| Abschließen | wer den Auftrag bearbeitet | Auftrag ist erledigt, mit Abschlusstext für den Kunden |
| Wieder öffnen | jeder Bearbeiter, der den Auftrag sehen darf | Auftrag liegt wieder offen in seiner letzten Gruppe |
| Wieder öffnen | Kunde | wie oben, mit Begründung. Wer den Auftrag zuletzt bearbeitet hat, wird als bevorzugter Bearbeiter genannt und erhält eine Mail |

Übernehmen ist die Voraussetzung für alles Weitere: Nur wer den Auftrag
bearbeitet, leitet ihn weiter, kommentiert ihn, schreibt dem Kunden, ändert
Angaben oder schließt ihn ab. Ein offener Auftrag in einer Gruppe lässt sich
nur ansehen und übernehmen. Wer einem anderen den Auftrag abnimmt, übernimmt
ihn ebenfalls zuerst. Scheidet ein Bearbeiter aus, gibt die Verwaltung seine
Aufträge an ihre Gruppen zurück.

Aufträge aus dem Kundenformular und aus dem E-Mail-Eingang gehen immer an die
IT-Hotline und werden dort zuerst bearbeitet. Ein Bearbeiter, den ein Kunde
direkt anspricht, kann den Auftrag selbst erfassen und sofort übernehmen. Der
Weg über die Hotline ist der Normalfall, aber kein Zwang.

Weitergeleitet wird nur an Gruppen, nie an Personen. Der bevorzugte Bearbeiter
ist ein Hinweis: Der Auftrag ist in der Gruppenliste markiert und erscheint bei
der genannten Person hervorgehoben, übernehmen kann ihn trotzdem jeder. Das
gilt genauso, wenn ein Bearbeiter ihn schon beim Erfassen nennt. Kunden und
der E-Mail-Eingang nennen keinen.

### Interne Kommentare

Interne Kommentare sind für sicherheitsrelevante oder sehr technische
Informationen gedacht, die der Kunde nicht sehen soll. Damit nichts
versehentlich nach außen geht, sind „Interner Kommentar" und „Nachricht an den
Kunden" zwei getrennte Aktionen und kein Ankreuzfeld. Interne Einträge sind im
Verlauf deutlich abgesetzt und erscheinen weder in der Kundenansicht noch in
Mails an den Kunden.

### Gruppen

- Gruppen stehen für Aufgabenbereiche (Netzwerk, Systemverwaltung, Beschaffung,
  Anwenderbetreuung, IT-Hotline).
- Angelegt werden sie vom Administrator.
- Offene Gruppe: Jeder Bearbeiter trägt sich selbst ein und aus. Das regelt
  auch Vertretungen.
- Gesperrte Gruppe: Nur der Administrator ordnet Mitglieder zu. Aufträge dieser
  Gruppe sehen nur ihre Mitglieder, der Kunde, der Erfasser und wer den Auftrag
  gerade bearbeitet. Auch Administratoren sehen sie nur als Mitglied.
- Wird ein Auftrag aus einer gesperrten Gruppe weitergeleitet, sehen ihn danach
  wieder alle Bearbeiter, mit seinem ganzen Verlauf.
- Die Eingangsgruppe kann nicht gesperrt sein.

### Verlauf

Jede Aktion erzeugt einen unveränderlichen Verlaufseintrag mit Zeitpunkt,
Person und Art. Weil jeder jeden Auftrag übernehmen darf, ersetzt der Verlauf
ein feines Berechtigungssystem: Es ist immer nachvollziehbar, wer was getan hat.

## 4. Felder eines Auftrags

| Feld | Art | Anmerkung |
|---|---|---|
| Nummer | automatisch | fortlaufend. Die erste Nummer ist einstellbar, um an eine vorhandene Zählung anzuschließen |
| Betreff | Text | Pflicht |
| Beschreibung | Text | Pflicht. Wie alle längeren Texte mit Markdown formatierbar |
| Kunde | Verweis auf Kundentabelle | nicht zwingend der Erfasser |
| Erfasst von | Person | automatisch |
| Eingang | Web, E-Mail, Telefon | automatisch bzw. Vorgabe |
| Kategorie | Auswahlliste | vom Administrator gepflegt |
| Geräte | Verweise auf die Gerätetabelle | alle, die im Auftrag mit „INV“ und Inventarnummer genannt sind, siehe Abschnitt 5 |
| Dringlichkeit | 3 Stufen | siehe Matrix |
| Wichtigkeit | 3 Stufen | siehe Matrix |
| Termin | Datum | optional |
| Gruppe | Gruppe | aktuelle Zuordnung |
| Bearbeiter | Person | leer, solange der Auftrag offen ist |
| Bevorzugter Bearbeiter | Person | optional, wird beim Erfassen durch einen Bearbeiter oder beim Weiterleiten gesetzt |
| Anhänge | Dateien | am Auftrag, an Kommentaren, Nachrichten und Antworten. Anhänge an internen Kommentaren sieht der Kunde nicht. Bilder erscheinen als Vorschau und lassen sich mit Strg+V einfügen |
| Verlauf | Einträge | automatisch |

Das Kundenformular zeigt nur Betreff, Beschreibung und Anhänge. Alles andere
ergänzt die Hotline. Einen eigenen Standort hat ein Auftrag nicht: An ihm
steht der Standort des Kunden.

### Priorität

Die Priorität wird nicht ausgewählt, sondern ergibt sich aus Dringlichkeit und
Wichtigkeit. Sie bestimmt die Sortierung der Listen.

| | Wichtigkeit niedrig | Wichtigkeit mittel | Wichtigkeit hoch |
|---|---|---|---|
| **Dringlichkeit hoch** | 3 | 2 | 1 |
| **Dringlichkeit mittel** | 4 | 3 | 2 |
| **Dringlichkeit niedrig** | 5 | 4 | 3 |

## 5. Kunden und Geräte

AVIS führt zwei eigene Stammdatentabellen, mit denen Aufträge verknüpft werden.
Beide sind so angelegt, dass sie später aus externen Systemen gefüllt werden
können, ohne dass sich am Auftrag etwas ändert.

### Kundentabelle

- Name, E-Mail, Telefon, Standort, Organisationseinheit, Anmeldename
- Jeder Auftrag verweist auf einen Kunden.
- Meldet sich ein Kunde an oder schreibt eine Mail, wird er über Anmeldename
  bzw. Mailadresse zugeordnet. Gibt es ihn noch nicht, wird er angelegt.
- Die Mailadresse eines Anmeldekontos stammt aus dem Active Directory oder vom
  Administrator, nie von der Person selbst. So kann sich niemand die Aufträge
  eines anderen zuordnen.
- Die Hotline sucht am Telefon den Kunden und wählt ihn aus. Gesucht wird
  über Name, E-Mail, Telefon, Organisationseinheit und Standort. Ein neuer
  Kunde ist die Ausnahme: Er lässt sich erst anlegen, nachdem gesucht wurde.
- Eine E-Mail-Adresse gehört zu genau einem Kunden.
- Am Kunden sind seine bisherigen Aufträge sichtbar.
- Ein Kunde lässt sich als inaktiv markieren. Er bleibt mit seinen Aufträgen
  erhalten, steht aber nicht mehr zur Wahl, wenn ein Auftrag erfasst wird.

### Abgleich der Kunden mit dem Active Directory

- AVIS holt die Kunden selbst aus dem Active Directory, einmal täglich im
  Hintergrund und auf Wunsch aus der Verwaltung. Es gibt dafür keine
  Schnittstelle nach außen.
- Ein LDAP-Filter in den Einstellungen bestimmt, wer Kunde ist. Er kann eine
  Gruppe nennen, muss es aber nicht. Deaktivierte Konten zählen nie.
- Das Verzeichnis gewinnt: Name, E-Mail, Telefon, Organisationseinheit und
  Standort werden überschrieben und lassen sich in AVIS nicht ändern.
- Wer schon in AVIS steht, wird über Anmeldename oder Mailadresse
  wiedererkannt und übernommen, nicht verdoppelt.
- Auf wen der Filter nicht mehr passt, der wird nicht gelöscht, sondern
  inaktiv, und wieder aktiv, sobald der Filter wieder passt.
- Kunden, die nur in AVIS angelegt wurden, fasst der Abgleich nicht an. Sie
  werden von Hand als inaktiv markiert.
- Liefert das Verzeichnis nichts oder ist es nicht erreichbar, ändert sich
  nichts.
- Der Abgleich selbst kennt die Quelle nicht. Eine andere Quelle könnte
  dieselbe Liste liefern.

### Gerätetabelle

- Inventarnummer, Bezeichnung, Typ, Standort, optional der Kunde, der das
  Gerät nutzt
- Ein Auftrag ist mit den Geräten verknüpft, die in ihm genannt sind: „INV“
  und die Inventarnummer, etwa „INV 4532436“, im Betreff, in der Beschreibung
  oder in einem späteren Text. Es gibt dafür kein Feld, und es können
  mehrere Geräte sein. Eine unbekannte Nummer bleibt Text.
- Es zählt jeder Text am Auftrag: interne Kommentare, Hinweise beim
  Weiterleiten, Nachrichten, Antworten des Kunden und eingehende Mails. Groß-
  und Kleinschreibung spielen keine Rolle, ebenso wenig, ob zwischen „INV“
  und der Nummer ein Leerzeichen, ein Doppelpunkt, ein Bindestrich oder nichts
  steht.
- Wird eine Nennung gestrichen, ist die Verknüpfung gelöst. Was in einer
  Nachricht an den Kunden steht, bleibt.
- Kunden sehen die Liste der Geräte am Auftrag nicht.
- Ausgemusterte Geräte bleiben an ihren Aufträgen, werden aber nicht mehr
  neu verknüpft.
- Am Gerät sind seine bisherigen Aufträge sichtbar.

### Abgleich der Geräte mit Snipe-IT

- AVIS holt die Geräte selbst aus Snipe-IT und schreibt dort nichts,
  einmal täglich im Hintergrund und auf Wunsch aus der Verwaltung.
- Snipe-IT gewinnt: Inventarnummer, Bezeichnung, Typ, Standort und Nutzer
  werden überschrieben. Ein Gerät aus Snipe-IT lässt sich in AVIS nicht
  ändern.
- Der Nutzer ist der Kunde mit der Mailadresse des Benutzers, an den das
  Gerät ausgegeben ist. Gibt es ihn in AVIS nicht, bleibt der Nutzer leer;
  Kunden kommen nur aus dem Kundenabgleich.
- Ein Gerät, das schon mit derselben Inventarnummer in AVIS steht, wird
  übernommen, nicht verdoppelt.
- Was die Quelle nicht mehr liefert, wird nicht gelöscht, sondern
  ausgemustert, und wieder in Betrieb genommen, sobald es wieder geliefert
  wird.
- Geräte, die nur in AVIS angelegt wurden, fasst der Abgleich nicht an.
- Liefert die Quelle nichts oder ist sie nicht erreichbar, ändert sich nichts.
- Der Abgleich selbst kennt die Quelle nicht. Eine andere Quelle könnte
  dieselbe Liste liefern.

### Standort

Kunden und Geräte tragen einen Standort als freien Text, zum Beispiel den
Raum „A 105“. Eine Auswahlliste gibt es dafür nicht. Bei abgeglichenen Kunden
und Geräten liefert ihn die Quelle, sonst wird er von Hand eingetragen. Ein
Auftrag hat keinen eigenen Standort; an ihm steht der des Kunden.

### Externe Quellen

Kunden und Geräte tragen je eine externe Kennung und die Angabe ihrer Quelle.
Damit lassen sich die Kunden aus dem Active Directory und die Geräte aus
Snipe-IT abgleichen (siehe oben).

Die Verknüpfung ist nie Pflicht: Ein Auftrag lässt sich immer auch ohne
passenden Geräteeintrag erfassen. Das Gerät steht dann nur im Text.

## 6. Seiten

**Kunde**

- Neuer Auftrag
- Meine Aufträge (Stand, Nachrichten, Abschluss)
- Hilfe für Kunden
- Einstellungen: persönlich, am Konto gespeichert; bisher die Darstellung
  (hell, dunkel oder wie das Gerät). Für Bearbeiter dieselbe Seite

**Bearbeiter**

- Meine Aufträge: alles, was ich gerade in Bearbeitung habe
- Offene Aufträge: was noch niemand übernommen hat – die offenen Aufträge
  meiner Gruppen und, oben als eigener Abschnitt, was mich als bevorzugten
  Bearbeiter nennt, auch aus Gruppen, in denen ich kein Mitglied bin.
  Umschaltbar: je Gruppe eine Liste, oder alles zusammen nach Priorität oder
  nach Liegezeit (die ältesten zuerst)
- Alle Aufträge: Suche in Betreff, Beschreibung, Gerät, Kunde und Verlauf,
  Filter nach Status, Gruppe und Bearbeiter, über alles, was ich sehen darf.
  Eine Auftragsnummer führt direkt zum Auftrag
- Auftrag erfassen: für einen Kunden, z. B. am Telefon
- Auftrag: Felder, Verlauf, Anhänge und die Aktionen aus Abschnitt 3
- Kunden und Geräte: suchen, ansehen, zugehörige Aufträge
- Meine Gruppen: beitreten und austreten, je Gruppe die Mail bei einem neuen
  Auftrag ein- und ausschalten
- Hilfe: eine Anleitung für Bearbeiter und eine für Kunden. „Hilfe“ im Kopf
  führt von jeder Seite zum passenden Abschnitt. Eigener Text des Hauses
  steht in einer Datei auf dem Server, nicht in der Anwendung

**Administrator**

- Gruppen, Kategorien, Geräte, Rollen
- Hilfe für Administratoren: eine Anleitung zur Verwaltung

## 7. E-Mail

Mailsystem ist Exchange.

- Eingang: AVIS ruft das Hotline-Postfach über IMAP ab. Eine Mail erzeugt
  einen Auftrag in der Gruppe IT-Hotline. Der Absender wird über die
  Kundentabelle zugeordnet, Betreff und Text werden übernommen, Anhänge
  angehängt.
- Antworten mit Auftragsnummer im Betreff landen als Antwort des Kunden im
  Verlauf des bestehenden Auftrags, wenn sie vom Kunden dieses Auftrags
  kommen. Ein abgeschlossener Auftrag wird dadurch wieder geöffnet.
- Die Mail des Bearbeiters, der den Auftrag bearbeitet, wird ein interner
  Kommentar.
- Nennt jemand anderes eine Auftragsnummer, entsteht ein neuer Auftrag. Niemand
  kann per Mail in den Auftrag eines anderen schreiben.
- Automatische Mails wie Abwesenheitsnotizen werden übergangen.
- Auf Wunsch entsteht ein neuer Auftrag nur aus der Mail eines bekannten
  Absenders: eines aktiven Kunden oder eines aktiven Kontos. Alles andere
  wird ohne Antwort übergangen. Ob ein Absender echt ist, kann nur der
  Mailserver prüfen; das Postfach sollte deshalb dort geschützt sein.
- Die Formatierung einer HTML-Mail bleibt erhalten, soweit sie eindeutig ist:
  fett, kursiv, Überschriften, Links, Listen und kleine, einfache Tabellen.
  Bilder im Text der Mail stehen im Auftrag an derselben Stelle.
- Ausgang: Versand über SMTP. Der Kunde erhält eine Mail bei Erfassung, bei
  Nachrichten an ihn und beim Abschluss. Interne Kommentare, Weiterleitungen
  und Gruppen stehen in keiner Mail an den Kunden.
- Bearbeiter erhalten von sich aus keine Mails für Gruppenaufträge. Wer es
  unter „Meine Gruppen“ für eine Gruppe einschaltet, erhält eine Mail, wenn
  dort ein Auftrag neu erfasst oder dorthin weitergeleitet wird. Der
  zuständige Bearbeiter erhält eine Mail, wenn der Kunde antwortet, und wer
  als bevorzugter Bearbeiter genannt wird, erhält einen Hinweis.
- Der Betreff jeder Mail beginnt mit der Auftragsnummer in der Form
  `[AVIS-123]`, damit Antworten später zugeordnet werden können.
- Ist der Mailserver nicht erreichbar, geht nichts verloren: Mails werden
  vorgemerkt und später erneut versucht.

## 8. Technik

| Baustein | Entscheidung |
|---|---|
| Anwendung | ASP.NET Core mit Razor Pages auf .NET 10 (Langzeit-Support) |
| Betrieb | Windows Server mit IIS |
| Anmeldung | Windows-Anmeldung über Active Directory, ohne Passworteingabe |
| Datenbank | SQLite, Zugriff über Entity Framework Core |
| Mail | Exchange im Haus: Versand über SMTP, Abruf über IMAP |

Grundsätze:

- Seiten werden auf dem Server erzeugt. Es gibt kein separates
  JavaScript-Frontend mit eigener Build-Kette und API-Schicht. Einzelne kleine
  Skripte machen Seiten bequemer, etwa die Kundensuche während des Tippens.
  Ohne sie funktioniert alles.
- AVIS bringt keine fremden Oberflächenbibliotheken mit, das Stylesheet ist
  selbst geschrieben.
- Anhänge liegen im Dateisystem, nicht in der Datenbank. Bilder werden
  angezeigt, alles andere wird nur als Download ausgeliefert.
- Texte werden als reiner Text gespeichert. Markdown formatiert nur die Anzeige.
- Die Rollen (Bearbeiter, Administrator) stehen in der Benutzertabelle von
  AVIS. Active Directory liefert nur, wer angemeldet ist.

### Späterer Wechsel zu SQL Server

Ein Wechsel ist möglich und vorbereitet:

- Das Datenmodell nutzt keine Besonderheiten von SQLite. Zeitpunkte liegen in
  UTC vor, alle Textfelder außer Beschreibung und Verlaufstext haben eine
  Höchstlänge, Fremdschlüssel löschen nichts mit.
- Für den Wechsel wird der Datenbankanbieter ausgetauscht und der Satz der
  Migrationen für SQL Server neu erzeugt. Die Daten werden einmalig übertragen.
- Die Suche verhält sich bei Groß- und Kleinschreibung je nach Datenbank
  unterschiedlich. Das ist beim Wechsel zu prüfen.

## 9. Ausbaustufen

1. Kern: Anmeldung, Gruppen, Kundentabelle, Aufträge erfassen, übernehmen,
   weiterleiten, abschließen, Verlauf mit internen Kommentaren
2. Kundenseite: eigenes Formular, Meine Aufträge, Mails an den Kunden
3. Anhänge, Suche, gesperrte Gruppen, Gerätetabelle
4. E-Mail-Eingang über Exchange
5. Anbindung externer Quellen für Kunden und Geräte

Alle fünf Stufen sind umgesetzt.

## 10. Offene Punkte

Was noch nicht erprobt ist, welche Lücken bekannt sind und was als Nächstes
ansteht, steht in [Stand und Ausblick](stand.md).
