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

## Nicht vorgesehen

Was AVIS mit Absicht nicht bekommt, steht im
[Konzept](konzept.md#bewusst-nicht-enthalten).
