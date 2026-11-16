# Stand und Ausblick

Wie weit AVIS ist, was erprobt ist und was nicht, und was als Nächstes ansteht.
Diese Datei wird mit dem Projekt fortgeschrieben. Was AVIS kann, steht im
[README](../README.md), warum es so gebaut ist, im [Konzept](konzept.md).

## Versionen

Die erste Version ist 0.1.0. Alle Versionen mit ihren Paketen stehen unter
[Releases](https://github.com/T3chSynergy/avis/releases). Solange die
Versionsnummer mit 0 beginnt, kann sich von einer Version zur nächsten noch
Grundlegendes ändern.

## Umgesetzt

Alle fünf Ausbaustufen des [Konzepts](konzept.md#9-ausbaustufen) sind gebaut:

1. Kern: Anmeldung, Gruppen, Kunden, Aufträge mit Verlauf und internen
   Kommentaren
2. Seiten für Kunden und Mails an den Kunden
3. Anhänge, Suche, gesperrte Gruppen, Geräte
4. E-Mail-Eingang
5. Abgleich der Bearbeiter und Kunden mit dem Active Directory und der Geräte
   mit Snipe-IT

Dazu kamen die Hilfe in der Anwendung, die Vorschau an den Textfeldern, die
dunkle Darstellung mit den persönlichen Einstellungen und das Paket für den
Betrieb unter Windows Server.

## Wie es geprüft ist

- Automatische Tests für die Regeln des Auftrags und für die Seiten, bei
  jedem Pull Request unter Windows und Linux, dazu eine Codeanalyse.
- Die Oberfläche ist beim Bauen in einem Browser (Chromium) angesehen worden,
  nicht in Firefox oder Edge.

## Im Probelauf

Ein Probelauf in einer echten Umgebung läuft: Windows Server mit IIS, Active
Directory, Snipe-IT und ein Mailserver. Dort hat bisher funktioniert:

- der Start unter IIS und die Windows-Anmeldung
- das Nachschlagen neuer Konten im Active Directory
- der Abgleich der Bearbeiter und der Kunden mit dem Active Directory
- der Abgleich der Geräte mit Snipe-IT, mit rund 30 000 Geräten
- der Mailversand und der E-Mail-Eingang, auch mit einer Antwort im
  HTML-Format mit eingefügtem Bild

Dabei gefundene Fehler sind behoben: der fehlende Standort neuer Konten und
kleine eingefügte Bilder, die als Logo verworfen wurden.

## Noch nicht erprobt

- der Betrieb über längere Zeit: die nächtlichen Läufe der Abgleiche, der
  Mailversand und der Abruf des Postfachs im Dauerbetrieb
- das Update nach der Anleitung für den [Betrieb unter IIS](betrieb-iis.md)
- HTML-Mails in ihrer Breite: Tabellen, Listen und Signaturen aus echten
  Mailprogrammen
- der E-Mail-Eingang nur für bekannte Absender
  (`Posteingang:NurBekannteAbsender`)
- der Alltag mit mehreren Bearbeitern

## Bekannte Lücken

- Doppelte Kunden lassen sich nicht zusammenführen.
- Anhänge lassen sich nicht löschen.
- Im Browser eingefügte Bilder stehen unter dem Text, nicht mittendrin.
- Inventarnummern mit Leerzeichen oder Schrägstrichen erkennt AVIS hinter
  „INV“ nicht.

## Als Nächstes

1. **Probelauf abschließen.** Was dabei auffällt, wird behoben, und die
   Anleitung wird berichtigt.
2. **Das erste Update erproben:** von 0.1.0 auf die nächste Version, nach der
   Anleitung für den Betrieb unter IIS.

## Angedacht, nicht zugesagt

- Die Anmeldung der Kunden an den Kundenabgleich binden: Wer dort nicht
  genannt ist, käme nicht mehr hinein.
- Geräte tagsüber nachziehen: nur das holen, was sich in Snipe-IT seit dem
  letzten Lauf geändert hat, statt einmal täglich alles.
- Wechsel der Datenbank zu SQL Server. Er ist im Aufbau berücksichtigt, siehe
  [Konzept](konzept.md#späterer-wechsel-zu-sql-server).
- Weitere Personen am Auftrag. Der Auftrag behält genau einen Kunden als
  Auftraggeber; dazu ließen sich weitere Betroffene verknüpfen, über die
  Kundensuche und nur intern, und Bearbeiter als beteiligt nennen. Offen ist
  vor allem, ob ein beteiligter Bearbeiter intern kommentieren darf, ohne den
  Auftrag zu übernehmen. Das sollen zuerst die künftigen Bearbeiter
  besprechen.
- Bezug zwischen Aufträgen. Jeder Auftrag steht für sich; Teilaufträge und
  Abhängigkeiten zwischen Aufträgen soll es nicht geben. Wer einen anderen
  Auftrag im Text nennt, zum Beispiel `AVIS 123`, stellt damit den Bezug her,
  so wie `INV 4711` ein Gerät verknüpft: Die Nennung wird ein Verweis, und am
  genannten Auftrag steht, wo er erwähnt ist. Für Arbeit, die sich auf mehrere
  Gruppen verteilt, reicht dann ein zweiter Auftrag mit einem solchen Bezug.
- Verweis in das Quellsystem. Ein Gerät aus Snipe-IT bekäme auf seiner Seite
  einen Link zu seiner Detailseite dort. Dasselbe wäre bei Kunden möglich:
  ein Link zum Eintrag der Person in einem Verzeichnis des Hauses, etwa einem
  Telefonbuch, über eine einstellbare Adresse mit Platzhalter.
- Kleine, feste Abläufe. Realistisch sind drei Formen, jede als feste
  Funktion und ohne Änderung am Kern:
  - eine Vorlage, die mehrere eigenständige Aufträge in verschiedenen Gruppen
    anlegt, etwa für einen neuen Mitarbeiter;
  - feste Regeln, zum Beispiel dass die Kategorie die Gruppe bestimmt oder
    dass nach einigen Tagen ohne Antwort eine Erinnerung kommt;
  - eine feste Schrittfolge, bei der ein Auftrag nach dem Erledigen von selbst
    an die nächste Gruppe geht.

  Dazu passt ein Formular für Kunden je Art von Auftrag: eine Eingabemaske,
  aus der ein Auftrag mit einer bestimmten Kategorie in einer bestimmten
  Gruppe entsteht, mit eigenem Titel und einem vorgegebenen Text, der sagt,
  welche Angaben gebraucht werden. Eigene Felder je Formular gehören nicht
  dazu, siehe „Nicht vorgesehen“.

  Eine Genehmigung durch jemanden, der kein Bearbeiter ist, käme nur in Frage,
  wenn der Nutzen sehr groß ist: Sie braucht einen neuen Stand, eine neue
  Rolle und eigene Mails. Eine Engine mit frei einstellbaren Ständen und
  Übergängen ist ausgeschlossen, siehe „Nicht vorgesehen“.

## Nicht vorgesehen

Was AVIS mit Absicht nicht bekommt, steht im
[Konzept](konzept.md#bewusst-nicht-enthalten).
