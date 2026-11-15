# Hilfe für Administratoren

Administratoren sind Bearbeiter mit einem zusätzlichen Bereich: der
**Verwaltung**. Diese Seite erklärt, was du dort einstellst und abliest. Wie
du mit Aufträgen arbeitest, steht in der Hilfe für Bearbeiter.

## Die Verwaltung

„Verwaltung“ ist der letzte Eintrag der Hauptnavigation und hat diese Bereiche:

| Bereich | Wofür |
|---|---|
| [Gruppen](#gruppen) | Gruppen anlegen, sperren, deaktivieren; Eingangsgruppe festlegen; Mitglieder zuordnen |
| [Benutzer](#benutzer-und-rollen) | Wer Bearbeiter oder Administrator ist; Konten deaktivieren |
| [Kategorien](#kategorien) | Die Kategorien, die am Auftrag zur Wahl stehen |
| [Mails](#mails) | Was AVIS versendet hat, was wartet und was nicht ankam |
| [Posteingang](#posteingang) | Welche Mails AVIS abgeholt und was es daraus gemacht hat |
| [Bearbeiterabgleich](#bearbeiterabgleich), [Kundenabgleich](#kundenabgleich), [Geräteabgleich](#geraeteabgleich) | Stand und Ergebnis der täglichen Abgleiche |

Außerhalb der Verwaltung kannst du als Administrator zusätzlich
[Geräte anlegen und ändern](#geraete-anlegen-und-aendern).

Nicht in der Verwaltung steht, womit AVIS verbunden ist: Mailserver,
Postfach, Verzeichnis, Inventarsystem. Das wird auf dem Server eingestellt,
siehe [Einstellungen auf dem Server](#einstellungen-auf-dem-server).

## Gruppen

Jeder Auftrag liegt in genau einer Gruppe. Die Liste zeigt je Gruppe die Zahl
der Mitglieder und der laufenden Aufträge. Unter der Liste legst du eine neue
Gruppe an; ein Name darf nur einmal vorkommen.

Ein Klick auf den Namen öffnet die Gruppe. Dort stellst du ein:

- **Eingangsgruppe.** Hier landen neue Aufträge: die, die Kunden selbst
  stellen, und die aus dem E-Mail-Eingang. Beim Erfassen durch Bearbeiter ist
  sie vorbelegt. Es gibt immer genau eine. Du nimmst einer Gruppe diese
  Aufgabe, indem du sie an einer anderen Gruppe ankreuzt.
- **Gesperrt.** Die Aufträge einer gesperrten Gruppe sehen nur ihre
  Mitglieder, außerdem der Kunde, wer den Auftrag erfasst hat und wer ihn
  gerade bearbeitet. Gedacht ist das für Vertrauliches. Weiterleiten kann
  jeder Bearbeiter dorthin; die Gruppe steht in der Auswahl mit dem Zusatz
  „(gesperrt)“. Die Eingangsgruppe kann nicht gesperrt sein.
- **Aktiv.** Eine deaktivierte Gruppe steht nicht mehr zur Wahl. Deaktivieren
  lässt sie sich nur, wenn sie keine laufenden Aufträge hat und nicht
  Eingangsgruppe ist. Gelöscht wird eine Gruppe nie, weil abgeschlossene
  Aufträge auf sie verweisen.

### Mitglieder

Bearbeiter tragen sich unter „Meine Gruppen“ selbst in Gruppen ein und
wieder aus. Auf der Seite der Gruppe kannst du nachhelfen: jemanden
hinzufügen oder entfernen.

Bei einer gesperrten Gruppe geht es nur so. Auch du selbst siehst ihre
Aufträge erst, wenn du Mitglied bist.

## Benutzer und Rollen

Jeder, der sich an AVIS anmeldet, bekommt beim ersten Besuch ein Konto, auch
jeder Kunde. Die Liste kann deshalb sehr lang werden und zeigt zuerst nur,
wer eine Rolle hat.

- Die Auswahl über der Liste schaltet um: **Mit Rolle**, **Administratoren**,
  **Ohne Rolle**, **Alle**. Die Zahlen dahinter gelten für die aktuelle Suche.
- Die Suche findet Name, Anmeldename und E-Mail. Angezeigt werden höchstens
  100 Konten; gib mehr ein, um einzugrenzen.

Es gibt drei Rollen:

| Rolle | Darf |
|---|---|
| keine | Eigene Aufträge stellen und verfolgen. Das ist jeder Kunde. |
| Bearbeiter | Aufträge aller Kunden erfassen und bearbeiten, Kunden und Geräte ansehen. |
| Administrator | Alles, was ein Bearbeiter darf, und die Verwaltung. |

Ein Klick auf den Namen öffnet das Konto. Dort änderst du Name, E-Mail, Rolle
und ob das Konto aktiv ist.

- An die **E-Mail-Adresse** gehen die Mails, die ein Bearbeiter von AVIS
  bekommt. Fehlt sie, entfallen diese Mails.
- **Verliert jemand die Rolle Bearbeiter**, gehen seine Aufträge in
  Bearbeitung zurück in ihre Gruppen und liegen dort wieder offen. Er wird
  aus allen Gruppen ausgetragen. Wie viele Aufträge das betrifft, steht vorher
  auf der Seite unter „Aufträge in Bearbeitung“ und nachher in der Meldung.
- Ein **deaktiviertes Konto** hat keinen Zugriff mehr, auch nicht als Kunde.
  Für Bearbeiter gilt dabei dasselbe wie beim Verlust der Rolle. Das Konto
  und alles, was die Person geschrieben hat, bleiben erhalten.
- **Dein eigenes Konto:** Rolle und Status kann nur ein anderer Administrator
  ändern. So bleibt immer mindestens einer übrig.

**Bearbeiter vorab anlegen.** Unter der Liste legst du ein Konto an, bevor
sich jemand zum ersten Mal anmeldet. Der Anmeldename muss so lauten, wie
Windows ihn meldet, mit Domäne: `domäne\name`.

**Konten mit der Marke „abgeglichen“** stammen aus dem
[Bearbeiterabgleich](#bearbeiterabgleich). Name, E-Mail und ob jemand
Bearbeiter ist, kommen aus der Quelle und lassen sich hier nicht ändern. Zum
Administrator machst du auch diese Konten hier.

Wer auf dem Server als Administrator eingetragen ist (Einstellung
`Anmeldung:Administratoren`), wird es bei jeder Anmeldung wieder. Die Rolle
lässt sich solchen Konten hier nicht dauerhaft nehmen.

## Kategorien

Kategorien ordnen Aufträge nach ihrer Art. Am Auftrag sind sie freiwillig.

- Name und „Aktiv“ änderst du direkt in der Zeile, dann **Speichern** in
  derselben Zeile.
- Ein geänderter Name gilt auch für die Aufträge, die die Kategorie schon
  tragen.
- Eine deaktivierte Kategorie steht nicht mehr zur Wahl, bleibt an
  bestehenden Aufträgen aber erhalten. Gelöscht wird nichts.

## Mails

Der Bereich zeigt die letzten 100 Benachrichtigungen und oben, wie der
Mailversand eingestellt ist: `Smtp` versendet, `Ordner` legt die Mails nur als
Dateien ab, `Aus` sendet nichts.

Mails werden im Hintergrund versendet. Eine Aktion am Auftrag gelingt auch
dann, wenn der Mailserver gerade nicht erreichbar ist.

| Status | Bedeutung |
|---|---|
| wartet | Die Mail ist vorgemerkt oder wird nach einem Fehler erneut versucht. |
| gesendet | Die Mail ist an den Mailserver übergeben. Bei `Ordner`: Die Datei ist abgelegt. |
| fehlgeschlagen | Sechs Versuche sind gescheitert. In der Spalte „Hinweis“ steht die Meldung des Mailservers. |
| entfallen | Es gab nichts zu senden. Der Grund steht in der Spalte „Hinweis“. |

- Nach einem Fehler versucht AVIS es nach 1, 5, 15, 60 und 240 Minuten
  wieder. Ist die Ursache behoben, stößt **Erneut versuchen** eine
  fehlgeschlagene Mail neu an.
- „Entfallen“ ist kein Fehler. Häufige Gründe: Für den Kunden oder den
  Bearbeiter ist keine E-Mail-Adresse hinterlegt, der Auftrag wurde inzwischen
  übernommen, oder der Mailversand ist ausgeschaltet.
- Steht hier viel auf „wartet“ mit steigenden Versuchen, ist meist der
  Mailserver nicht erreichbar oder lehnt die Anmeldung ab.

## Posteingang

Ist der E-Mail-Eingang eingerichtet, holt AVIS die ungelesenen Mails aus dem
Postfach der Hotline. Der Bereich zeigt die letzten 100 und was daraus
wurde. Oben steht die Einstellung: `Imap` ruft ab, `Aus` nicht.

| Ergebnis | Bedeutung |
|---|---|
| neuer Auftrag | Die Mail wurde ein Auftrag in der Eingangsgruppe. Einen unbekannten Absender legt AVIS als Kunden an. |
| Antwort des Kunden | Der Betreff nannte eine Auftragsnummer, und die Mail kam vom Kunden dieses Auftrags. |
| Auftrag wieder geöffnet | Wie eine Antwort, aber der Auftrag war schon abgeschlossen. |
| interner Kommentar | Die Mail kam von dem Bearbeiter, der den Auftrag gerade bearbeitet. |
| übergangen | AVIS hat nichts daraus gemacht. Der Grund steht in der Spalte „Hinweis“. |

- Übergangen werden Abwesenheitsnotizen, Zustell- und Leseberichte,
  Massenmails und Mails von der eigenen Absenderadresse.
- Nennt eine Mail einen Auftrag, kommt aber weder von dessen Kunden noch von
  dessen Bearbeiter, wird sie ein neuer Auftrag. Der Hinweis nennt dann den
  gemeinten Auftrag. Niemand kann per Mail in den Auftrag eines anderen
  schreiben.
- Lässt sich eine Mail dreimal nicht verarbeiten, bleibt sie gelesen und
  markiert im Postfach liegen, damit jemand sie sich ansieht.
- Vermisst ein Kunde seine Mail, sieh hier nach. Fehlt sie, obwohl sie erst
  vor Kurzem kam, hat AVIS sie nicht abgeholt. Dann liegt es am Postfach oder
  an der Verbindung dorthin.

## Abgleiche

Bearbeiter, Kunden und Geräte kann AVIS aus anderen Systemen übernehmen. Für
alle drei Abgleiche gilt:

- **Die Quelle gewinnt.** Was von dort kommt, lässt sich in AVIS nicht
  ändern; eine Änderung dort ist nach dem nächsten Abgleich hier zu sehen.
- **Nichts wird gelöscht.** Was in der Quelle fehlt, wird nur markiert: Ein
  Kunde wird inaktiv, ein Gerät ausgemustert, ein Bearbeiter verliert die
  Rolle. Taucht es wieder auf, ist es wieder da.
- **Was nur in AVIS angelegt wurde, bleibt unberührt.**
- **Einmal täglich.** Jeder Abgleich läuft zu der Uhrzeit, die auf seiner
  Seite steht. Lief AVIS zu dieser Zeit nicht, wird er nachgeholt. Tagsüber
  ändert sich also nichts von selbst.
- **Jetzt abgleichen** startet den Abgleich sofort, zum Beispiel wenn eine
  neue Kollegin nicht bis morgen warten soll. Bei vielen Einträgen kann das
  eine Weile dauern.

Die Seite zeigt den letzten Lauf: wann er war, wie viele Einträge gelesen,
neu angelegt und geändert wurden und was dabei auffiel („Hinweise“).

Ist ein Lauf gescheitert, steht dort stattdessen die Fehlermeldung. Geändert
wurde dann nichts, und AVIS versucht es nach einer Stunde wieder. Auch eine Quelle,
die gar nichts liefert, ändert nichts: So macht eine Störung nicht alle
Kunden inaktiv.

Steht auf der Seite, der Abgleich sei ausgeschaltet, ist er auf dem Server
nicht eingerichtet. Dann wird in AVIS von Hand gepflegt.

## Bearbeiterabgleich

Bestimmt, wer Bearbeiter ist: die Konten, die die Quelle nennt, in der Regel
die Mitglieder einer Gruppe im Verzeichnis.

- Wen die Quelle nennt, der ist Bearbeiter, mit Name und E-Mail von dort. Ein
  fehlendes Konto wird angelegt.
- Wen sie nicht mehr nennt, der verliert die Rolle, mit denselben Folgen wie
  von Hand: Seine Aufträge gehen zurück in ihre Gruppen, und er wird aus allen
  Gruppen ausgetragen. Das Konto bleibt.
- Bearbeiter, die du nur unter „Benutzer“ dazu gemacht hast, bleiben
  unberührt. Administratoren verlieren ihre Rolle durch den Abgleich nie.

## Kundenabgleich

Bestimmt die Kunden: die Konten des Verzeichnisses, auf die der eingestellte
Filter passt.

- Name, E-Mail, Telefon, Organisationseinheit und Standort kommen aus der
  Quelle und werden überschrieben.
- Wer dort fehlt, wird als inaktiv markiert. Er bleibt mit seinen Aufträgen
  erhalten, steht beim Erfassen aber nicht mehr zur Wahl.
- Hat ein Bearbeiter einen Kunden von Hand angelegt und kommt später ein
  Konto mit derselben E-Mail-Adresse aus der Quelle, wird daraus ein Kunde
  und kein zweiter. Deshalb lohnt es, beim Anlegen die E-Mail-Adresse
  einzutragen.

## Geräteabgleich

Holt die Geräte aus dem Inventarsystem. AVIS liest dort nur und schreibt nie
zurück.

- Inventarnummer, Bezeichnung, Typ, Standort und Nutzer kommen aus der Quelle
  und werden überschrieben.
- Der Nutzer eines Geräts ist der Kunde mit derselben E-Mail-Adresse. Deshalb
  läuft dieser Abgleich nach dem Kundenabgleich.
- Was dort fehlt, wird ausgemustert. Ein ausgemustertes Gerät bleibt an
  seinen Aufträgen, wird aber nicht mehr neu verknüpft.

## Geräte anlegen und ändern

Unter **Geräte** steht für Administratoren neben der Suche „Gerät anlegen“,
und auf der Seite eines Geräts „Bearbeiten“. Das gilt nur für Geräte, die in
AVIS selbst angelegt wurden. Geräte aus dem Geräteabgleich werden im
Inventarsystem gepflegt.

- Pflicht sind Inventarnummer und Bezeichnung. Über die Inventarnummer
  verknüpfen Bearbeiter das Gerät mit einem Auftrag, indem sie `INV` und die
  Nummer in den Text schreiben.
- Den Nutzer suchst du wie beim Erfassen eines Auftrags unter den Kunden.
- Ohne das Häkchen „In Betrieb“ ist das Gerät ausgemustert.

## Eigener Text in der Hilfe

Die Hilfetexte sind für jede Installation gleich. Was nur für euer Haus gilt,
etwa Telefonnummer und Zeiten der Hotline, steht in einer eigenen Datei auf
dem Server, in der Voreinstellung `App_Data/Hilfe.md` im Ordner der
Anwendung.

- Ihr Inhalt erscheint als Kasten über jeder Hilfe, bei Kunden wie bei
  Bearbeitern. Fehlt die Datei, steht dort nichts.
- Geschrieben wird sie wie die Texte in den Aufträgen, siehe „Texte
  formatieren“ in der Hilfe für Bearbeiter.
- Eine Änderung ist sofort zu sehen, ohne Neustart.
- Schreib so, dass auch Kunden den Text lesen können: Sie sehen ihn ebenfalls.

## Lizenz und Quelltext

AVIS ist freie Software unter der GNU Affero General Public License, Version
3 oder später. Am Ende jeder Hilfeseite steht deshalb der Verweis „Quelltext
und Lizenz“.

Wird AVIS im Haus verändert, muss der Verweis zum Quelltext der veränderten
Fassung führen: Die Lizenz gibt jedem Benutzer das Recht darauf. Die Adresse
steht im Quelltext in der Datei `Lizenz.cs`.

## Einstellungen auf dem Server

Was AVIS mit anderen Systemen verbindet, steht in der Konfiguration der
Anwendung auf dem Server und nicht in der Verwaltung:

- Mailversand und E-Mail-Eingang
- die drei Abgleiche mit Quelle, Filter und Uhrzeit
- wer von vornherein Administrator ist
- Höchstgröße und Ablageort der Anhänge
- die erste Auftragsnummer und die Zeitzone

Die Verwaltung zeigt an, was davon gilt, ändert es aber nicht. Alle
Einstellungen sind in der Dokumentation zur Installation beschrieben (Datei
`README.md` des Projekts, Abschnitt „Einstellungen“). Nach einer Änderung
dort ist die Anwendung neu zu starten.
