# AVIS – Konzept

Stand: 13.11.2026 · Status: Entwurf, Technik noch nicht entschieden

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
| Kunde | eigene Aufträge erstellen, eigene Aufträge und deren Stand sehen, Rückfragen beantworten |
| Bearbeiter | Aufträge erfassen, alle Aufträge sehen (außer gesperrte Gruppen), übernehmen, weiterleiten, abschließen, sich selbst Gruppen zuordnen |
| Administrator | Gruppen, Kategorien, Standorte, Kunden und Geräte pflegen, Mitglieder gesperrter Gruppen zuordnen, Rollen vergeben |

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
| Erfassen | Kunde, Bearbeiter, E-Mail-Eingang | Auftrag entsteht in einer Gruppe, Standard ist die IT-Hotline |
| Übernehmen | jeder Bearbeiter, der den Auftrag sehen darf | Bearbeiter wird gesetzt, auch wenn schon ein anderer eingetragen ist |
| Weiterleiten | Bearbeiter | neue Gruppe wird gesetzt, Bearbeiter wird geleert, optional bevorzugter Bearbeiter |
| Interner Kommentar | Bearbeiter | Eintrag im Verlauf, für den Kunden unsichtbar |
| Nachricht an den Kunden | Bearbeiter | Eintrag im Verlauf, der Kunde sieht ihn und erhält eine Mail |
| Antwort des Kunden | Kunde | Eintrag im Verlauf |
| Ändern | Bearbeiter | Felder anpassen, Änderung steht im Verlauf |
| Abschließen | zuständiger Bearbeiter | Auftrag ist erledigt, mit Abschlusstext für den Kunden |
| Wieder öffnen | Bearbeiter | Auftrag liegt wieder offen in seiner letzten Gruppe |

Aufträge aus dem Kundenformular und aus dem E-Mail-Eingang gehen immer an die
IT-Hotline und werden dort zuerst bearbeitet. Ein Bearbeiter, den ein Kunde
direkt anspricht, kann den Auftrag selbst erfassen und sofort übernehmen. Der
Weg über die Hotline ist der Normalfall, aber kein Zwang.

Weitergeleitet wird nur an Gruppen, nie an Personen. Der bevorzugte Bearbeiter
ist ein Hinweis: Der Auftrag ist in der Gruppenliste markiert und erscheint bei
der genannten Person hervorgehoben, übernehmen kann ihn trotzdem jeder.

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
  Gruppe sehen nur ihre Mitglieder, der Kunde und der Erfasser.

### Verlauf

Jede Aktion erzeugt einen unveränderlichen Verlaufseintrag mit Zeitpunkt,
Person und Art. Weil jeder jeden Auftrag übernehmen darf, ersetzt der Verlauf
ein feines Berechtigungssystem: Es ist immer nachvollziehbar, wer was getan hat.

## 4. Felder eines Auftrags

| Feld | Art | Anmerkung |
|---|---|---|
| Nummer | automatisch | fortlaufend |
| Betreff | Text | Pflicht |
| Beschreibung | Text | Pflicht |
| Kunde | Verweis auf Kundentabelle | nicht zwingend der Erfasser |
| Erfasst von | Person | automatisch |
| Eingang | Web, E-Mail, Telefon | automatisch bzw. Vorgabe |
| Kategorie | Auswahlliste | vom Administrator gepflegt |
| Standort | Auswahlliste | vom Administrator gepflegt |
| Gerät | Freitext | immer möglich |
| Gerät (verknüpft) | Verweis auf Gerätetabelle | optional, siehe Abschnitt 5 |
| Dringlichkeit | 3 Stufen | siehe Matrix |
| Wichtigkeit | 3 Stufen | siehe Matrix |
| Termin | Datum | optional |
| Gruppe | Gruppe | aktuelle Zuordnung |
| Bearbeiter | Person | leer, solange der Auftrag offen ist |
| Bevorzugter Bearbeiter | Person | optional, wird beim Weiterleiten gesetzt |
| Anhänge | Dateien | am Auftrag oder an einem Verlaufseintrag |
| Verlauf | Einträge | automatisch |

Das Kundenformular zeigt nur Betreff, Beschreibung, Standort, Gerät und
Anhänge. Alles andere ergänzt die Hotline.

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
- Die Hotline kann am Telefon einen Kunden suchen oder neu anlegen.
- Am Kunden sind seine bisherigen Aufträge sichtbar.

### Gerätetabelle

- Inventarnummer, Bezeichnung, Typ, Standort, optional der Kunde, der das Gerät
  nutzt
- Ein Auftrag kann mit einem Gerät verknüpft werden, muss aber nicht. Das
  Freitextfeld bleibt daneben bestehen.
- Am Gerät sind seine bisherigen Aufträge sichtbar.

### Vorbereitung für externe Quellen

Kunden und Geräte tragen je eine externe Kennung und die Angabe ihrer Quelle.
In der ersten Fassung werden beide Tabellen in AVIS gepflegt. Später können
Einträge aus externen Systemen abgeglichen oder die Verknüpfung auf externe
Objekte umgestellt werden.

Die Verknüpfung ist nie Pflicht: Ein Auftrag lässt sich immer auch ohne
passenden Geräteeintrag erfassen.

## 6. Seiten

**Kunde**

- Neuer Auftrag
- Meine Aufträge (Stand, Nachrichten, Abschluss)

**Bearbeiter**

- Meine Aufträge: alles, was ich gerade in Bearbeitung habe
- Gruppen: offene Aufträge meiner Gruppen, bevorzugt an mich gerichtete markiert
- Alle Aufträge: Suche und Filter über alles, was ich sehen darf
- Auftrag erfassen: für einen Kunden, z. B. am Telefon
- Auftrag: Felder, Verlauf, Anhänge und die Aktionen aus Abschnitt 3
- Kunden und Geräte: suchen, ansehen, zugehörige Aufträge
- Meine Gruppen: beitreten und austreten

**Administrator**

- Gruppen, Kategorien, Standorte, Kunden, Geräte, Rollen

## 7. E-Mail

Mailsystem ist Exchange.

- Eingang: Eine Mail an das Hotline-Postfach erzeugt einen Auftrag in der
  Gruppe IT-Hotline. Der Absender wird über die Kundentabelle zugeordnet,
  Betreff und Text werden übernommen, Anhänge angehängt.
- Antworten mit Auftragsnummer im Betreff landen als Antwort des Kunden im
  Verlauf des bestehenden Auftrags.
- Ausgang: Der Kunde erhält eine Mail bei Erfassung, bei Nachrichten an ihn und
  beim Abschluss. Interne Kommentare werden nie verschickt.
- Bearbeiter erhalten keine Mails für Gruppenaufträge, höchstens einen Hinweis,
  wenn sie als bevorzugter Bearbeiter genannt werden.

## 8. Technik

Noch nicht entschieden. Fest steht unabhängig von der Sprache:

- Seiten werden auf dem Server erzeugt. Kein separates JavaScript-Frontend mit
  eigener Build-Kette und API-Schicht, für Listen und Formulare ist das unnötig.
- Datenbankzugriff über eine Abstraktionsschicht, damit die Datenbank
  austauschbar bleibt.
- Anhänge liegen im Dateisystem, nicht in der Datenbank.

Zur Wahl stehen:

| | ASP.NET Core (Razor Pages) | Python (Django) |
|---|---|---|
| Betrieb auf Windows Server / IIS | nativ | möglich, aber umständlicher |
| Betrieb auf Linux | möglich | nativ |
| Anmeldung ohne Passwort über Active Directory | eingebaut | über vorgeschalteten Webserver oder LDAP |
| Verwaltungsoberfläche für Stammdaten | selbst zu bauen | mitgeliefert |
| Einstieg für gelegentliche Pflege | höher | niedriger |

Die Entscheidung hängt an drei Punkten: auf welchem Server AVIS läuft, wie sich
die Benutzer anmelden sollen und wer es künftig pflegt.

SQLite reicht für die Last aus. Ein Datenbankserver lohnt sich nur, wenn es
dafür ohnehin einen gesicherten Betrieb im Haus gibt.

## 9. Ausbaustufen

1. Kern: Anmeldung, Gruppen, Kundentabelle, Aufträge erfassen, übernehmen,
   weiterleiten, abschließen, Verlauf mit internen Kommentaren
2. Kundenseite: eigenes Formular, Meine Aufträge, Mails an den Kunden
3. Anhänge, Suche, gesperrte Gruppen, Gerätetabelle
4. E-Mail-Eingang über Exchange
5. Anbindung externer Quellen für Kunden und Geräte

## 10. Offene Punkte

- Server: Windows oder Linux
- Anmeldung: automatisch über Active Directory oder eigenes Login
- Exchange: im Haus oder Exchange Online, und welcher Zugriff auf das
  Hotline-Postfach freigeschaltet ist
- Technik gemäß Abschnitt 8
