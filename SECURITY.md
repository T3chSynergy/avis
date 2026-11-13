# Sicherheit

## Unterstützte Versionen

AVIS hat keine nummerierten Versionen. Sicherheitskorrekturen gibt es nur für
den aktuellen Stand des Zweigs `main`. Wer AVIS betreibt, sollte deshalb
regelmäßig auf den aktuellen Stand gehen.

## Eine Sicherheitslücke melden

Sicherheitslücken bitte **nicht** als öffentliches Issue, in einem Pull
Request oder in einer Diskussion melden. Der Weg ist die vertrauliche Meldung
auf GitHub:

1. Im Repository den Reiter **Security** öffnen.
2. **Report a vulnerability** wählen.
3. Das Formular ausfüllen und absenden.

Die Meldung sehen nur die Betreuer des Repositorys.

Hilfreich sind diese Angaben:

- was die Lücke ist und was ein Angreifer damit erreichen kann
- die Schritte, mit denen sie sich nachstellen lässt
- der Stand von AVIS (Datum oder Commit) und die Art der Anmeldung
  (`Anmeldung:Art`)
- wenn vorhanden: ein Vorschlag für die Korrektur

Bitte keine echten Daten mitschicken – keine Namen, Mailadressen,
Auftragstexte, Servernamen oder Zugangsdaten. Erfundene Beispiele reichen.

## Was danach passiert

AVIS wird in der Freizeit entwickelt. Eine feste Frist für die Antwort gibt es
deshalb nicht; jede Meldung wird aber gelesen und beantwortet.

- Die Meldung wird geprüft, bei Rückfragen über das Formular auf GitHub.
- Bestätigt sich die Lücke, wird sie auf `main` behoben.
- Danach wird sie als Sicherheitshinweis (Security Advisory) veröffentlicht.
  Wer sie gemeldet hat, wird dort genannt, wenn er oder sie das möchte.

Bitte die Lücke nicht öffentlich machen, bevor die Korrektur auf `main` ist.

## Was hierher gehört und was nicht

Hierher gehören Lücken im Code von AVIS, zum Beispiel:

- Zugriff auf Aufträge, Anhänge oder Seiten ohne die nötige Berechtigung
- Einschleusen von Skripten oder Befehlen über Texte, Mails oder Anhänge
- Umgehen der Anmeldung

Nicht hierher gehören:

- Lücken in den verwendeten Komponenten (siehe `THIRD-PARTY-NOTICES.md`).
  Sie werden beim jeweiligen Projekt gemeldet; AVIS übernimmt deren
  Korrekturen über die Aktualisierung der Pakete.
- Fehler in der Einrichtung einer einzelnen Installation, zum Beispiel ein
  falsch eingestellter Server
- Fehler ohne Bezug zur Sicherheit; dafür gibt es das Formular für Meldungen
  unter **Issues**
