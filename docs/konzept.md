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
| Administrator | Gruppen, Kategorien, Standorte und Geräte pflegen, Mitglieder gesperrter Gruppen zuordnen, Rollen vergeben |

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
| Anhänge | Dateien | am Auftrag, an Kommentaren, Nachrichten und Antworten. Anhänge an internen Kommentaren sieht der Kunde nicht |
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
- Die Mailadresse eines Anmeldekontos stammt aus dem Active Directory oder vom
  Administrator, nie von der Person selbst. So kann sich niemand die Aufträge
  eines anderen zuordnen.
- Die Hotline kann am Telefon einen Kunden suchen oder neu anlegen. Gesucht
  wird über Name, E-Mail, Telefon und Organisationseinheit.
- Eine E-Mail-Adresse gehört zu genau einem Kunden.
- Am Kunden sind seine bisherigen Aufträge sichtbar.

### Gerätetabelle

- Inventarnummer, Bezeichnung, Typ, Standort, optional der Kunde, der das Gerät
  nutzt
- Ein Auftrag kann über die Inventarnummer mit einem Gerät verknüpft werden,
  muss aber nicht. Das Freitextfeld bleibt daneben bestehen.
- Ausgemusterte Geräte bleiben an ihren Aufträgen, lassen sich aber nicht mehr
  neu verknüpfen.
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
- Alle Aufträge: Suche in Betreff, Beschreibung, Gerät, Kunde und Verlauf,
  Filter nach Status, Gruppe und Bearbeiter, über alles, was ich sehen darf.
  Eine Auftragsnummer führt direkt zum Auftrag
- Auftrag erfassen: für einen Kunden, z. B. am Telefon
- Auftrag: Felder, Verlauf, Anhänge und die Aktionen aus Abschnitt 3
- Kunden und Geräte: suchen, ansehen, zugehörige Aufträge
- Meine Gruppen: beitreten und austreten

**Administrator**

- Gruppen, Kategorien, Standorte, Geräte, Rollen

## 7. E-Mail

Mailsystem ist Exchange.

- Eingang: Eine Mail an das Hotline-Postfach erzeugt einen Auftrag in der
  Gruppe IT-Hotline. Der Absender wird über die Kundentabelle zugeordnet,
  Betreff und Text werden übernommen, Anhänge angehängt.
- Antworten mit Auftragsnummer im Betreff landen als Antwort des Kunden im
  Verlauf des bestehenden Auftrags.
- Ausgang: Versand über SMTP. Der Kunde erhält eine Mail bei Erfassung, bei
  Nachrichten an ihn und beim Abschluss. Interne Kommentare, Weiterleitungen
  und Gruppen stehen in keiner Mail an den Kunden.
- Bearbeiter erhalten keine Mails für Gruppenaufträge. Der zuständige
  Bearbeiter erhält eine Mail, wenn der Kunde antwortet, und wer als
  bevorzugter Bearbeiter genannt wird, erhält einen Hinweis.
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
| Mail | Exchange |

Grundsätze:

- Seiten werden auf dem Server erzeugt. Es gibt kein separates
  JavaScript-Frontend mit eigener Build-Kette und API-Schicht. Einzelne kleine
  Skripte machen Seiten bequemer, etwa die Kundensuche während des Tippens.
  Ohne sie funktioniert alles.
- AVIS bringt keine fremden Oberflächenbibliotheken mit, das Stylesheet ist
  selbst geschrieben.
- Anhänge liegen im Dateisystem, nicht in der Datenbank. Sie werden nur als
  Download ausgeliefert.
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

## 10. Offene Punkte

- Exchange: im Haus oder Exchange Online, und welcher Zugriff auf das
  Hotline-Postfach freigeschaltet ist
- Die Abfrage des Active Directory für Name, E-Mail, Telefon und Abteilung
  neuer Konten ist eingebaut, aber noch nicht an einem echten Verzeichnis
  erprobt.
- Darf ein Kunde auf einen abgeschlossenen Auftrag antworten und ihn damit
  wieder öffnen? Bisher kann er das nicht.
