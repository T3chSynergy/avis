<!--
Die Kurzanleitung für Bearbeiter. Das Werkzeug macht daraus eine Webseite und ein PDF und nimmt die Bilder dazu
selbst auf (Bildschirmfotos.cs); im Repo liegen sie nicht. Besonderheiten dieser Datei:

- {{stand}} und {{datum}}: Stand von AVIS und Tag der Aufnahme
- {{inhalt}}: das Inhaltsverzeichnis aus den Überschriften der zweiten Ebene
- {{videos}}: die Liste der Kurzvideos; {{video:3}}: Verweis auf das dritte Video
- Ein Absatz nur aus Bildern wird eine Abbildung mit Nummer; ihr Text ist der Alternativtext des ersten Bildes.
  {{abb:auftrag.png}} nennt die Nummer der Abbildung mit diesem Bild.
- {.neueseite} an einer Überschrift beginnt im PDF eine neue Seite, {.schmal} und {.mittel} machen ein Bild kleiner.
-->

# AVIS – Kurzanleitung für Bearbeiter

Grundprinzip, Bedienung und Funktionen · AVIS {{stand}} · {{datum}}
{.untertitel}

{{inhalt}}

## 1 Worum es geht

AVIS (Auftrags-, Verfolgungs- und Informationssystem) ist ein schlankes Ticketsystem für die IT: Aufträge erfassen,
an eine Gruppe geben, übernehmen, erledigen. Die Anwender, für die gearbeitet wird, heißen in AVIS **Kunden**. Sie
stellen und verfolgen ihre Aufträge im Browser oder per E-Mail.

Diese Unterlage erklärt das Grundprinzip und die Bedienung aus Sicht der Bearbeiter, also für alle, die Aufträge
annehmen und erledigen: Hotline, Anwenderbetreuung, Systemverwaltung, Netzwerk, Beschaffung und andere. Die Bilder
zeigen erfundene Testdaten.

AVIS ist vollständig bedienbar, im Alltag aber noch wenig erprobt. Solange die Versionsnummer mit 0 beginnt, kann
sich noch Grundlegendes ändern.

> **Am besten ausprobieren.** Spiel Fälle aus deinem Alltag durch; Abschnitt 13 nennt einige zum Einstieg. Was nicht
> funktioniert, unklar ist oder fehlt, ist eine wertvolle Rückmeldung.

### Kurzvideos

Zu den wichtigsten Handgriffen gibt es kurze Videos, jedes zu einem Thema und meist unter einer Minute lang. An den
passenden Stellen dieser Anleitung steht ein Verweis darauf. Die Videos sind durch KI vertont und bearbeitet.

{{videos}}

## 2 Das Grundprinzip

### Vier Begriffe

| Begriff | Bedeutung |
|---|---|
| **Kunde** | Der Anwender, für den gearbeitet wird. |
| **Auftrag** | Eine Störung, eine Frage oder ein Wunsch. Was anderswo „Ticket“ heißt. |
| **Gruppe** | Ein Team mit einem Aufgabengebiet, in den Testdaten zum Beispiel IT-Hotline, Netzwerk, Systemverwaltung, Beschaffung und Anwenderbetreuung. Eine Gruppe ist die Eingangsgruppe: Dort landet, was Kunden selbst stellen. |
| **Bearbeiter** | Wer Aufträge bearbeitet. Jeder Bearbeiter trägt sich selbst in die Gruppen ein, deren Aufträge er bearbeitet oder vertritt. |

### Wo ein Auftrag liegt

Ein Auftrag gehört immer zu **genau einer Gruppe** und hat **höchstens einen Bearbeiter**. Daraus ergibt sich sein
Stand. Es gibt kein Feld, in dem man den Stand auswählt, und es gibt nur diese drei:

![Die drei Stände eines Auftrags und die Aktionen, die von einem zum anderen führen.](bilder/ablauf.svg){.ohnerand}

### Fünf Regeln, aus denen sich alles Weitere ergibt

1. **Erst übernehmen, dann arbeiten.** Einen offenen Auftrag kann man nur ansehen und übernehmen. Erst wer ihn
   übernommen hat, kann kommentieren, dem Kunden schreiben, weiterleiten, Angaben ändern und abschließen. So ist
   immer klar, wer zuständig ist.
2. **Weitergeleitet wird an Gruppen, nie an Personen.** Man kann einen bevorzugten Bearbeiter nennen. Das ist ein
   Hinweis an die Gruppe und keine Zuweisung: Übernehmen kann den Auftrag trotzdem jeder.
3. **Intern und für den Kunden sind getrennt.** Ein interner Kommentar erreicht den Kunden nie, auch nicht in einer
   Mail. Eine Nachricht an den Kunden sieht er im Browser und bekommt sie als Mail.
4. **Der Verlauf vergisst nichts.** Jede Aktion steht mit Name und Zeit am Auftrag. Einträge werden nie gelöscht.
5. **Die Priorität ergibt sich.** Man wählt Dringlichkeit und Wichtigkeit, AVIS macht daraus P1 bis P5 und sortiert
   die Listen danach.

## 3 Die Oberfläche im Überblick

{{video:1}}

AVIS läuft im Browser. Ist die Windows-Anmeldung eingerichtet, bist du ohne Kennworteingabe angemeldet. Oben auf
jeder Seite stehen dieselben beiden Zeilen:

![Der Kopf jeder Seite.](bilder/kopf.png)

| Element | Wozu |
|---|---|
| **Suchfeld** | Sucht in allen Aufträgen. Eine Auftragsnummer führt direkt zum Auftrag. Tastenkürzel: Alt+S. |
| **+ Auftrag erfassen** | Legt einen neuen Auftrag an, siehe Abschnitt 5. Tastenkürzel: Alt+N. |
| **Hilfe** | Die Hilfe in der Anwendung. Der Verweis führt gleich zum Abschnitt, der zur aktuellen Seite gehört. |
| **Dein Name** | Öffnet ein Menü mit „Meine Gruppen“, „Eigene Aufträge als Kunde“ und „Einstellungen“. |
| **Meine, Offene und Alle Aufträge** | Die drei Listen, siehe Abschnitt 4. Die Zahlen zeigen, wie viel in den ersten beiden liegt. |
| **Kunden, Geräte** | Nachschlagen, mit den Aufträgen des Kunden oder des Geräts. |

Blau ist in AVIS nur, was sich anklicken lässt.

### Der erste Schritt: Meine Gruppen

{{video:2}}

Im Menü unter deinem Namen steht **Meine Gruppen**. Dort trägst du dich in die Gruppen ein, deren Aufträge du
bearbeitest oder vertrittst, und auch wieder aus. Das ist der erste Schritt, denn „Offene Aufträge“ zeigt nur, was in
deinen Gruppen liegt.

![Meine Gruppen: beitreten, austreten und je Gruppe die Mail bei neuen Aufträgen ein- oder ausschalten.](bilder/gruppen.png){.mittel}

- Je Gruppe kannst du eine **Mail bei neuen Aufträgen** einschalten. Sie kommt, wenn dort ein Auftrag erfasst oder
  dorthin weitergeleitet wird. Voreingestellt ist sie aus.
- In eine **gesperrte Gruppe** trägt dich nur ein Administrator ein. Sie ist für Vertrauliches gedacht: Ihre
  Aufträge sehen nur die Mitglieder, der Kunde, wer den Auftrag erfasst hat und wer ihn gerade bearbeitet.

## 4 Die Listen

### Offene Aufträge

Hier steht, was noch niemand übernommen hat: die offenen Aufträge deiner Gruppen und oben, als eigener Abschnitt, was
dich als bevorzugten Bearbeiter nennt. Die Ansicht lässt sich umschalten: je Gruppe eine Liste, alles zusammen nach
Priorität oder nach Liegezeit, die ältesten zuerst.

![Offene Aufträge, Ansicht „Nach Gruppe“. Oben stehen die Aufträge, die Bernd Muster als bevorzugten Bearbeiter nennen.](bilder/offene.png)

### Meine Aufträge

Die Startseite zeigt, was du in Bearbeitung hast, sortiert nach Priorität.

![Meine Aufträge. Beim ersten Auftrag hat der Kunde geantwortet, und noch niemand hat reagiert.](bilder/meine.png)

### Zeichen in den Listen

| Zeichen | Bedeutung |
|---|---|
| **P1 bis P5** | Die Priorität. Danach sind die Listen sortiert. |
| **Kunde wartet** | Der Kunde hat geantwortet oder den Auftrag wieder geöffnet, und noch niemand hat reagiert. |
| **für …** | Der bevorzugte Bearbeiter. Steht dort dein Name, ist die Marke hervorgehoben. |
| **gesperrt** | Der Auftrag liegt in einer gesperrten Gruppe. |
| **überfällig, heute** | Der Termin des Auftrags ist überschritten oder heute. |

### Alle Aufträge und die Suche

{{video:8}}

Unter **Alle Aufträge** suchst du in Betreff, Beschreibung, Kunde, den verknüpften Geräten und allen Texten des
Verlaufs. Mehrere Wörter müssen alle vorkommen; Groß- und Kleinschreibung spielen keine Rolle. Dazu gibt es Filter
nach Status, Gruppe und Bearbeiter. Aufträge gesperrter Gruppen, in denen du kein Mitglied bist, findet die Suche
nicht.

![Alle Aufträge mit dem Suchwort „wlan“.](bilder/alle.png)

## 5 Auftrag erfassen

{{video:3}}

„Auftrag erfassen“ steht auf jeder Seite oben rechts. Gedacht ist das Formular vor allem für den Anruf: Kunde suchen,
Anliegen aufschreiben, fertig.

![Auftrag erfassen. Die Suche nach „müller“ zeigt zwei Kunden; gewählt ist Gabi Müller.](bilder/erfassen.png){.mittel}

1. **Kunde suchen.** Tippe den Namen, die Telefonnummer, die E-Mail-Adresse, die Organisationseinheit oder den
   Standort. Die Treffer erscheinen schon beim Tippen. Wähle den Kunden aus.
2. **Der Kunde ist nicht dabei?** Nach einer Suche steht unter den Treffern „Nicht dabei: neuen Kunden anlegen“. Trag
   dann möglichst auch die E-Mail-Adresse ein: An sie gehen die Mails zum Auftrag, und über sie wird der Kunde später
   wiedergefunden, sodass kein doppelter Eintrag entsteht.
3. **Betreff und Beschreibung.** Beides ist Pflicht. Geräte und andere Aufträge nennst du im Text, siehe Abschnitt 7.
4. **Dringlichkeit und Wichtigkeit** ergeben die Priorität. Kategorie und Termin sind freiwillig.
5. **Gruppe.** Vorbelegt ist die Eingangsgruppe. Du kannst den Auftrag gleich in die richtige Gruppe geben und einen
   bevorzugten Bearbeiter nennen.
6. **Auftrag erfassen** legt den Auftrag offen in die Gruppe. **Erfassen und selbst übernehmen** macht dich sofort
   zum Bearbeiter.

Der Kunde bekommt eine Mail mit der Auftragsnummer, wenn eine Adresse bekannt ist.

### Priorität

Die Priorität wählst du nicht aus. Sie ergibt sich aus Dringlichkeit und Wichtigkeit. **Dringlich** ist, was bald
erledigt sein muss. **Wichtig** ist, was viele betrifft oder großen Schaden anrichten kann. Beides lässt sich später
am Auftrag ändern.

| | Wichtigkeit niedrig | Wichtigkeit mittel | Wichtigkeit hoch |
|---|:-:|:-:|:-:|
| **Dringlichkeit hoch** | P3 | P2 | P1 |
| **Dringlichkeit mittel** | P4 | P3 | P2 |
| **Dringlichkeit niedrig** | P5 | P4 | P3 |

## 6 Am Auftrag arbeiten

### Übernehmen

{{video:4}}

Ein offener Auftrag zeigt nur eine Schaltfläche: **Übernehmen**. Damit bist du sein Bearbeiter, und der Auftrag ist
„in Bearbeitung“.

![Ein offener Auftrag. Außer „Übernehmen“ gibt es noch keine Aktion.](bilder/auftrag-offen.png)

Du kannst auch einen Auftrag übernehmen, den schon ein anderer bearbeitet, etwa als Vertretung. Die Schaltfläche
heißt dann „Von … übernehmen“. Der bisherige Bearbeiter sieht das im Verlauf. Hat er gerade einen Text geschrieben,
geht dieser nicht verloren: AVIS zeigt ihn ihm zum Kopieren an.

### Die Seite des Auftrags

Links stehen Beschreibung, Aktionen und Verlauf, rechts die Angaben: Kunde mit Telefon und Standort, Priorität,
Termin, Kategorie, Geräte und andere Aufträge, die mit diesem zu tun haben. Der **Verlauf** zeigt die neuesten
Einträge zuerst. Texte stehen als eigene Einträge da, reine Ereignisse wie „Übernommen“ als schmale Zeilen
dazwischen. Interne Einträge sind gelb abgesetzt.

![Ein Auftrag in Bearbeitung. Im Verlauf von oben: die Antwort des Kunden, die Nachricht an ihn und ein interner Kommentar.](bilder/auftrag.png)

### Die Aktionen

| Aktion | Was geschieht |
|---|---|
| **Interner Kommentar** | Ein Eintrag im Verlauf, den der Kunde nie sieht, auch nicht in einer Mail. Für Technisches und alles, was nicht nach außen soll. |
| **Nachricht an den Kunden** | Ein Eintrag im Verlauf, den der Kunde sieht. Er bekommt ihn außerdem als Mail. |
| **Weiterleiten** | Du gibst den Auftrag an eine andere Gruppe und bist danach nicht mehr sein Bearbeiter. Den Hinweis an die Gruppe sieht der Kunde nicht. |
| **Abschließen** | Der Auftrag ist erledigt. Der Abschlusstext ist Pflicht und geht als Mail an den Kunden. |
| **Wieder öffnen** | Ein abgeschlossener Auftrag liegt wieder offen in seiner letzten Gruppe. Das kann jeder Bearbeiter, der den Auftrag sehen darf, und auch der Kunde selbst. |

Ein Klick auf eine Aktion klappt darunter ihr Formular auf. Interner Kommentar und Nachricht an den Kunden sind mit
Absicht zwei getrennte Schaltflächen und unterschiedlich gefärbt, damit nichts versehentlich nach außen geht. Über
dem Textfeld steht jeweils, wer den Text sehen wird.

![Links der interne Kommentar auf gelbem Grund, rechts die Nachricht an den Kunden.](bilder/panel-intern.png)
![](bilder/panel-nachricht.png)

{{video:5}}

Beim **Weiterleiten** wählst du die Gruppe. Der bevorzugte Bearbeiter und der Hinweis an die Gruppe sind freiwillig.
Beim **Abschließen** schreibst du, was getan wurde; diesen Text bekommt der Kunde.

![Links Weiterleiten, rechts Abschließen.](bilder/panel-weiterleiten.png)
![](bilder/panel-abschliessen.png)

{{video:6}}

Antwortet der Kunde, bekommst du als Bearbeiter eine Mail, und der Auftrag trägt die Marke „Kunde wartet“, bis jemand
reagiert.

### Nachträglich ändern

- **Angaben bearbeiten** und **Kunde ändern** stehen rechts unter den Angaben, solange du den Auftrag bearbeitest.
  Was vorher galt, steht danach im Verlauf. „Kunde ändern“ ordnet den Auftrag einem anderen Kunden zu, zum Beispiel
  wenn er am Telefon für die falsche Person erfasst wurde.
- **Eigene interne Einträge** kannst du ändern, solange du den Auftrag bearbeitest: An deinen internen Kommentaren
  und Hinweisen beim Weiterleiten steht „Ändern“. Der alte Text wird ersetzt, am Eintrag steht danach „geändert“.
- **Was ein Kunde gesehen oder per Mail bekommen hat, lässt sich nicht ändern.** Nachrichten an den Kunden und
  Abschlusstexte bleiben, wie sie sind.

## 7 Geräte, andere Aufträge und Kunden

### Geräte am Auftrag nennen

{{video:7}}

Ein Gerät kommt an einen Auftrag, indem du es im Text nennst: das Wort `INV` und die Inventarnummer, zum Beispiel
`INV 4532436`. Ein eigenes Feld gibt es dafür nicht, und es dürfen beliebig viele Geräte sein.

- Es zählen Betreff, Beschreibung und jeder spätere Text, also auch interne Kommentare und Nachrichten. „INV 4711
  gegen INV 4712 getauscht“ in einem Kommentar verknüpft beide Geräte.
- Die Schreibweise ist großzügig: `inv 4711`, `INV:4711`, `INV-4711` und `INV4711` gelten alle.
- Kennt AVIS die Nummer nicht, bleibt sie einfach Text. Es gibt keine Fehlermeldung.

Die verknüpften Geräte stehen rechts bei den Angaben des Auftrags, mit Bezeichnung und Standort.

### Andere Aufträge nennen

Gehört ein Auftrag zu einem anderen, nennst du diesen genauso im Text: das Wort `AVIS` und die Auftragsnummer, zum
Beispiel `AVIS 123`. Beide Aufträge zeigen einander dann rechts bei den Angaben unter „Aufträge“, mit Betreff und
Stand. Am genannten Auftrag steht dabei „nennt diesen“.

![Die Angaben eines Auftrags, in dessen Texten zwei Geräte und der Auftrag 3 genannt sind.](bilder/bezug.png){.schmal}

- Die Schreibweise ist auch hier großzügig: `avis 123`, `AVIS-123`, `AVIS#123` und `[AVIS-123]` aus dem Betreff einer
  Mail gelten alle. Gibt es die Nummer nicht, bleibt sie einfach Text.
- Streichst du die Nennung aus der Beschreibung oder aus deinem internen Kommentar, ist der Bezug gelöst.
- Einen Auftrag einer gesperrten Gruppe sieht dort nur, wer ihn auch sonst sehen darf. Kunden sehen die Bezüge nie.

Der Bezug ist nur ein Verweis. Jeder Auftrag steht für sich: mit eigener Gruppe, eigenem Bearbeiter und eigenem
Abschluss. Keiner wartet auf den anderen, und Teilaufträge gibt es nicht.

> **Wenn mehrere Gruppen etwas tun müssen:** Folgt eines auf das andere, leite den Auftrag weiter. Soll gleichzeitig
> gearbeitet werden, erfasse für den anderen Teil einen zweiten Auftrag in der anderen Gruppe und nenne darin den
> ersten.

### Kunden

{{video:10}}

Unter **Kunden** suchst du nach Name, E-Mail, Telefon, Organisationseinheit oder Standort. Die Seite eines Kunden
zeigt seine laufenden und abgeschlossenen Aufträge und seine Geräte. Von dort lässt sich auch gleich ein Auftrag für
ihn erfassen.

![Die Seite eines Kunden mit seinen Aufträgen und Geräten.](bilder/kunde.png)

- Kunden, die aus dem Verzeichnis (Active Directory) abgeglichen werden, lassen sich in AVIS nicht ändern. Ihre
  Angaben kommen jede Nacht von dort.
- Kunden, die nur in AVIS stehen, pflegst du selbst. Wer nicht mehr gebraucht wird, lässt sich als inaktiv markieren:
  Er bleibt mit seinen Aufträgen erhalten, steht aber beim Erfassen nicht mehr zur Wahl.
- Ist ein **Telefonbuch** des Hauses angebunden, führt ein Verweis auf der Seite des Kunden und am Auftrag zu seinem
  Eintrag dort. Am Auftrag führt auch der Name des Bearbeiters dorthin.

### Geräte

Unter **Geräte** suchst du nach Inventarnummer, Bezeichnung, Typ, Standort oder Nutzer. Die Seite eines Geräts zeigt
seine Angaben und alle seine Aufträge. Die Geräte kommen in der Regel aus dem Inventarsystem und werden jede Nacht
abgeglichen; ändern lassen sie sich nur dort. AVIS liest im Inventarsystem nur und schreibt nichts zurück. Bei einem
Gerät aus Snipe-IT führt **In Snipe-IT ansehen** zu seiner Seite dort.

![Die Seite eines Geräts mit seinen Aufträgen und dem Verweis in das Inventarsystem.](bilder/geraet.png)

## 8 Texte, Bilder und Anhänge

{{video:11}}

Alle längeren Texte lassen sich mit einfachen Zeichen formatieren (Markdown). Nötig ist das nicht; ein Zeilenumbruch
bleibt ein Zeilenumbruch. Eine Formatierleiste gibt es mit Absicht nicht.

| So geschrieben | So angezeigt |
|---|---|
| `**fett**` | **fett** |
| `*kursiv*` | *kursiv* |
| `- Punkt` am Zeilenanfang | eine Liste |
| `1. Schritt` am Zeilenanfang | eine nummerierte Liste |
| `` `Befehl` `` zwischen zwei Gravis-Zeichen | `Befehl` in fester Schrift |
| `> Zitat` am Zeilenanfang | ein eingerücktes Zitat |
| `https://example.org` | ein Link |

Unter jedem Textfeld steht **Vorschau**. Die Schaltfläche zeigt den Text so, wie er später im Auftrag steht. „Weiter
schreiben“ führt zurück zum Textfeld; der Text bleibt dabei erhalten.

![Links der geschriebene Text, rechts dieselbe Stelle nach einem Klick auf „Vorschau“.](bilder/vorschau-1.png)
![](bilder/vorschau-2.png)

- **Bildschirmfotos:** Ein Bild aus der Zwischenablage fügst du mit **Strg+V** in das Textfeld ein.
- **Dateien** lassen sich in das Textfeld ziehen oder über das Dateifeld wählen.
- Was an einem internen Kommentar hängt, sieht der Kunde nicht.

## 9 E-Mails

### Was der Kunde bekommt

- eine Mail, wenn sein Auftrag erfasst wurde, mit der Auftragsnummer,
- jede Nachricht an ihn,
- den Abschlusstext.

Interne Kommentare, Weiterleitungen und Gruppen stehen in keiner Mail an den Kunden.

### Was du bekommst

AVIS schickt Bearbeitern nur dann eine Mail, wenn es sie betrifft:

- Der Kunde antwortet auf einen Auftrag, den du bearbeitest.
- Du wirst als bevorzugter Bearbeiter genannt, oder ein Kunde öffnet einen Auftrag wieder, den du zuletzt bearbeitet
  hast.
- Ein Auftrag liegt neu in einer Gruppe, für die du die Mail eingeschaltet hast (siehe „Meine Gruppen“).

Der Betreff jeder Mail beginnt mit der Auftragsnummer in der Form `[AVIS-123]`.

### E-Mail-Eingang

Ist der E-Mail-Eingang eingerichtet, liest AVIS das Postfach der Hotline:

- Eine Mail eines Kunden wird ein Auftrag in der Eingangsgruppe. Der Betreff der Mail wird der Betreff des Auftrags,
  Anhänge hängen am Auftrag.
- Nennt der Betreff eine Auftragsnummer und kommt die Mail vom Kunden dieses Auftrags, wird sie seine Antwort. Ein
  abgeschlossener Auftrag ist damit wieder offen.
- Antwortest du selbst per Mail auf einen Auftrag, den du gerade bearbeitest, wird deine Antwort ein interner
  Kommentar.
- Abwesenheitsnotizen und andere automatische Mails werden übergangen.
- Der Eingang lässt sich auf bekannte Absender beschränken. Dann wird nur zum Auftrag, was von der Adresse eines
  aktiven Kunden oder Kontos kommt.

## 10 Was der Kunde sieht

{{video:9}}

Kunden haben eine eigene, einfache Sicht: ihre Aufträge, den Stand und die Schaltfläche „Neuer Auftrag“. Ein Auftrag,
den ein Kunde im Browser stellt, landet in der Eingangsgruppe.

![Die Startseite eines Kunden: oben die laufenden, darunter die abgeschlossenen Aufträge.](bilder/kunde-meine.png){.mittel}

Am einzelnen Auftrag sieht der Kunde die Beschreibung, den Stand, den Namen des Bearbeiters und die Nachrichten der
IT. Er kann antworten, Bilder und Dateien nachreichen und einen abgeschlossenen Auftrag wieder öffnen. Interne
Kommentare, Weiterleitungen, Gruppe, Priorität und Bezüge zu anderen Aufträgen sieht er nicht.

![Derselbe Auftrag wie in Abb. {{abb:auftrag.png}}, aus Sicht des Kunden. Der interne Kommentar fehlt.](bilder/kunde-auftrag.png){.mittel}

Unter „Eigene Aufträge als Kunde“ im Menü unter deinem Namen kannst du diese Sicht selbst ausprobieren und Aufträge
für dich stellen.

## 11 Tastenkürzel, Einstellungen und Hilfe

| Tasten | Wirkung |
|---|---|
| Alt+N | Auftrag erfassen |
| Alt+S | in das Suchfeld im Kopf springen |
| Eingabetaste im Feld „Kunde suchen“ | sofort suchen |
| Strg+Eingabe am Auftrag | das Formular absenden, in dem du gerade schreibst |

Unter **Einstellungen** im Menü unter deinem Namen wählst du die Darstellung: hell, dunkel oder „wie das Gerät“. Die
Wahl gilt für dein Konto, auf jedem Gerät.

![Das Menü unter dem Namen und die Seite „Einstellungen“.](bilder/einstellungen.png){.mittel}

**Hilfe** im Kopf jeder Seite führt zur Hilfe in der Anwendung. Dort steht alles aus dieser Unterlage zum
Nachschlagen, jeweils passend zur Seite, auf der du gerade bist.

## 12 Was AVIS nicht hat

AVIS bleibt mit Absicht klein. Folgendes ist bewusst nicht enthalten:

- einstellbare Abläufe und weitere Stände neben offen, in Bearbeitung und abgeschlossen
- Teilaufträge und Aufträge, die aufeinander warten
- frei definierbare Zusatzfelder
- Fristen mit Uhr (SLA), Eskalationsregeln und Zeiterfassung
- Auswertungen pro Bearbeiter
- eine Pflicht, Aufträge mit dem Inventar zu verknüpfen

Das heißt nicht, dass nichts davon je kommen kann. Es heißt, dass jede Erweiterung gegen die Einfachheit abgewogen
wird. Für eine Rückmeldung ist es deshalb hilfreich zu wissen, **wofür** du etwas brauchst.

### Bekannte Lücken

- Doppelte Kunden lassen sich nicht zusammenführen.
- Anhänge lassen sich nicht löschen.
- Im Browser eingefügte Bilder stehen unter dem Text, nicht mittendrin.
- Inventarnummern mit Leerzeichen oder Schrägstrichen erkennt AVIS hinter „INV“ nicht.

## 13 Zum Ausprobieren {.neueseite}

Diese Fälle decken das Wichtigste ab. Am meisten zeigt sich, wenn zwei oder drei Kollegen zusammen testen: einer
erfasst, einer übernimmt, einer spielt den Kunden.

- [ ] Unter „Meine Gruppen“ in die eigenen Gruppen eintragen und für eine Gruppe die Mail bei neuen Aufträgen
  einschalten.
- [ ] Einen Auftrag für einen Kunden erfassen, wie am Telefon: Kunde suchen, Betreff, Beschreibung, Dringlichkeit und
  Wichtigkeit.
- [ ] Einen Auftrag mit einem Gerät erfassen (`INV` und Inventarnummer im Text) und prüfen, ob das Gerät rechts bei
  den Angaben steht.
- [ ] Einen offenen Auftrag aus der eigenen Gruppe übernehmen, intern kommentieren und dem Kunden eine Nachricht
  schreiben.
- [ ] In einem Kommentar einen anderen Auftrag nennen (`AVIS` und Nummer) und prüfen, ob beide Aufträge aufeinander
  verweisen.
- [ ] Ein Bildschirmfoto mit Strg+V in einen Kommentar einfügen und die Vorschau ansehen.
- [ ] Einen Auftrag an eine andere Gruppe weiterleiten, mit Hinweis und bevorzugtem Bearbeiter. Kommt die Mail beim
  Kollegen an?
- [ ] Einen Auftrag eines Kollegen übernehmen, als wäre er im Urlaub.
- [ ] Einen Auftrag abschließen und danach wieder öffnen.
- [ ] Als Kunde: unter „Eigene Aufträge als Kunde“ einen Auftrag stellen, auf eine Nachricht antworten, einen
  abgeschlossenen Auftrag wieder öffnen.
- [ ] Per Mail: einen Auftrag an die Adresse der Hotline schicken und auf eine Mail von AVIS antworten.
- [ ] Einen älteren Auftrag über die Suche wiederfinden, über ein Wort aus einem Kommentar, den Kunden oder das
  Gerät.
- [ ] Die Seite eines Kunden und eines Geräts ansehen: Stimmen die Angaben, stehen die Aufträge dabei? Führen die
  Verweise ins Telefonbuch und ins Inventarsystem an die richtige Stelle?

### Was bei einer Rückmeldung hilft

| Art | Hilfreiche Angaben |
|---|---|
| **Fehler** | Was hast du getan, was hast du erwartet, was ist geschehen? Dazu die Auftragsnummer, die Uhrzeit und möglichst ein Bildschirmfoto. |
| **Wunsch** | Beschreib die Situation, nicht nur die Lösung: Was willst du erreichen, wie oft kommt das vor, und wie löst du es heute? |
| **Unklares** | Auch „Ich habe nicht gefunden, wie …“ ist eine wertvolle Rückmeldung. Sie zeigt, wo die Oberfläche oder die Hilfe besser werden muss. |

Rückmeldungen gehen an die, die AVIS bei euch betreuen.
