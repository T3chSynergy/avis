# Stand und Ausblick

Wie weit AVIS ist, was noch nicht erprobt ist und was als Nächstes ansteht.
Diese Datei wird mit dem Projekt fortgeschrieben. Was AVIS kann, steht im
[README](../README.md), warum es so gebaut ist, im [Konzept](konzept.md).

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

## Noch nicht erprobt

AVIS ist bisher nur in der Entwicklungsumgebung gelaufen. Gegen die echten
Systeme noch nie gelaufen sind:

- die Windows-Anmeldung unter IIS
- die Abgleiche mit dem Active Directory und das Nachschlagen neuer Konten
  dort
- der Abgleich mit Snipe-IT; er ist nach dessen Quelltext geschrieben und mit
  nachgestellten Antworten geprüft
- der Mailversand über SMTP und der Abruf über IMAP gegen Exchange; der Abruf
  lief bisher nur gegen einen anderen IMAP-Server
- die Umwandlung echter HTML-Mails aus Outlook; geprüft ist sie mit
  nachgebauten Mails
- die Anleitung für den [Betrieb unter IIS](betrieb-iis.md)

## Bekannte Lücken

- Doppelte Kunden lassen sich nicht zusammenführen.
- Anhänge lassen sich nicht löschen.
- Im Browser eingefügte Bilder stehen unter dem Text, nicht mittendrin.
- Inventarnummern mit Leerzeichen oder Schrägstrichen erkennt AVIS hinter
  „INV“ nicht.

## Als Nächstes

1. **Probelauf in einer echten Umgebung** mit Windows Server, IIS, Active
   Directory, Snipe-IT und Exchange. Was dabei auffällt, wird behoben, und die
   Anleitung wird berichtigt.
2. **Erstes Release** mit Versionsnummer und fertigem Paket, sobald der
   Probelauf trägt.

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
