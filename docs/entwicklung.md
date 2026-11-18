# Entwicklung

Für alle, die an AVIS mitarbeiten oder den Code lesen wollen. Wie eine
Änderung hineinkommt und worauf es im Projekt ankommt, steht in
[CONTRIBUTING.md](../CONTRIBUTING.md), das Fachliche im
[Konzept](konzept.md).

## Voraussetzungen

- .NET 10 SDK, unter Windows oder Linux

## Starten

```bash
dotnet run --project src/Avis.Web
```

Die Anwendung läuft dann unter http://localhost:5274. In der
Entwicklungsumgebung legt sie beim ersten Start eine SQLite-Datenbank unter
`src/Avis.Web/App_Data` mit Testdaten an und bietet eine Testanmeldung, bei der
man den Benutzer auswählt. Frische Testdaten gibt es auf der Seite der
Testanmeldung über „Datenbank neu anlegen": Das leert die Datenbank, entfernt
die Anhänge und beginnt die Zählung von vorn.

Zu den Testdaten gehören Kunden, die für Konten aus dem Active Directory
stehen (Hanna Verzeichnis, Igor Abgleich und die inaktive Jana Ehemals), und
ein von Hand stillgelegter Kunde (Karl Ruhestand). An ihnen lässt sich ohne
Verzeichnis ansehen, wie abgeglichene und inaktive Kunden aussehen. Ebenso
stehen drei Geräte für Assets aus Snipe-IT (TB-3001, BE-3002 und das
ausgemusterte TB-2990) und ein Bearbeiter für ein Konto aus dem Verzeichnis
(Finn Verzeichnis).

Mails werden in der Entwicklung nicht versendet, sondern als Dateien unter
`src/Avis.Web/App_Data/Mails` abgelegt. Ein Mailprogramm öffnet sie. Den
Ordner nennt auch die Seite der Testanmeldung.

## Testen

```bash
dotnet test
```

Die Tests prüfen die Regeln des Auftrags (`tests/Avis.Tests/AuftragTests.cs`)
und die Seiten gegen eine laufende Anwendung mit eigener Datenbank
(`tests/Avis.Tests/SeitenTests.cs`).

Ein Test ruft ein echtes Postfach über IMAP ab. Er läuft nur, wenn die
Umgebungsvariable `AVIS_TEST_IMAP` in der Form `host:port:benutzer:kennwort`
gesetzt ist. Er verarbeitet die ungelesenen Mails dieses Postfachs und
markiert sie als gelesen, gehört also nur an ein Testpostfach.

## Code prüfen

Der Build prüft den Code mit der Codeanalyse von .NET und zeigt, was sie
findet, als Warnung. `dotnet format` bringt die Formatierung in Ordnung:

```bash
dotnet format
```

Bei jedem Pull Request laufen Build, Formatprüfung und Tests auf GitHub,
unter Windows und Linux (`.github/workflows/pruefung.yml`). Dort lässt jede
Warnung den Build scheitern. Vor dem Hochladen prüft man dasselbe so:

```bash
dotnet build -warnaserror
dotnet format --verify-no-changes
dotnet test
```

Die Regeln stehen in `.editorconfig`, zusammen mit den Regeln, die
abgeschaltet sind, und dem Grund dafür.

## Aufbau

| Ort | Inhalt |
|---|---|
| `src/Avis.Web/Domain` | Auftrag, Verlauf und Stammdaten mit ihren Regeln |
| `src/Avis.Web/Data` | Datenbankzugriff, Migrationen, Start- und Testdaten |
| `src/Avis.Web/Anmeldung` | Anmeldung, Rollen, Verzeichnisabfrage, Zuordnung von Konto und Kunde |
| `src/Avis.Web/Mail` | Mailtexte und Versand |
| `src/Avis.Web/Posteingang` | Abruf des Hotline-Postfachs und Verarbeitung eingehender Mails |
| `src/Avis.Web/Anhaenge` | Ablage hochgeladener Dateien |
| `src/Avis.Web/Pages` | Seiten |
| `src/Avis.Web/wwwroot/css/avis.css` | Stylesheet |
| `src/Avis.Web/wwwroot/js` | Kleine Skripte, die Seiten bequemer machen (Suche beim Tippen, Bilder einfügen, Tastenkürzel); ohne sie funktioniert alles |
| `tests/Avis.Tests` | Tests |
| `werkzeuge/Avis.Kurzvideos` | Nimmt die Kurzvideos auf und baut die Kurzanleitung für Bearbeiter; gehört nicht zu `Avis.slnx` (siehe „Kurzvideos und Kurzanleitung“) |
| `docs` | Konzept, Betrieb, Einstellungen, Stand und diese Datei |
| `.github/workflows` | Prüfung bei jedem Pull Request (`pruefung.yml`), Paket für den Betrieb bei jedem Release (`paket.yml`), Kurzvideos und Kurzanleitung auf GitHub Pages auf Knopfdruck (`kurzvideos.yml`) |
| `LICENSE` | Lizenz von AVIS (GNU AGPL, Version 3); wird beim Veröffentlichen in den Ausgabeordner kopiert |
| `THIRD-PARTY-NOTICES.md` | Lizenzvermerke der mitgelieferten Komponenten; wird beim Veröffentlichen in den Ausgabeordner kopiert und ist anzupassen, wenn ein Paket hinzukommt oder wegfällt |

Bezeichner im Code sind deutsch und folgen den Begriffen des Konzepts, Umlaute
sind ausgeschrieben (`Auftraege`, `Geraet`).

Wo einzelne Regeln stehen:

| Was | Wo |
|---|---|
| Regeln des Auftrags (wer darf was) | `src/Avis.Web/Domain/Auftrag.cs` |
| Geräte am Auftrag über „INV“ und Inventarnummer | `src/Avis.Web/Domain/Geraetebezug.cs`; angewendet bei jedem Speichern in `AvisDb` |
| Texte aller Mails | `src/Avis.Web/Mail/Mailtexte.cs` |
| Umwandlung von HTML-Mails in Markdown | `src/Avis.Web/Posteingang/HtmlZuMarkdown.cs` |
| Anzeige von Markdown | `src/Avis.Web/Markdowntext.cs` |
| Abgleiche mit Active Directory und Snipe-IT | `src/Avis.Web/Abgleich` |
| Version und Lizenzhinweis | `src/Avis.Web/Programmversion.cs`, `src/Avis.Web/Lizenz.cs` |

## Datenbank ändern

Nach einer Änderung am Datenmodell wird eine Migration erzeugt:

```bash
dotnet tool restore
dotnet ef migrations add NameDerAenderung --project src/Avis.Web --output-dir Data/Migrations
```

Beim Start bringt die Anwendung die Datenbank selbst auf den aktuellen Stand.

Eine neue Spalte ist harmlos. Ein Fremdschlüssel oder eine geänderte Spalte an
einer vorhandenen Tabelle baut in SQLite die ganze Tabelle um, und das läuft
nicht in einer Transaktion. Jede neue Migration deshalb lesen, bevor sie
eingecheckt wird.

## Texte mit Markdown

Beschreibung, Kommentare, Nachrichten und Antworten verstehen Markdown: fett,
kursiv, Listen, Zitate, Code, Links und Tabellen. Wer Markdown kennt, tippt es
einfach; eine Formatierleiste gibt es bewusst nicht. Gespeichert wird der
reine Text, erst die Anzeige formatiert ihn. Suche und Mails arbeiten deshalb
mit dem reinen Text, in Mails stehen die Markdown-Zeichen so, wie sie getippt
wurden.

Eingegebenes HTML wird als Text gezeigt. Links führen nur zu `http`, `https`
und `mailto`. Bilder von fremden Adressen werden nicht geladen, Bilder gehören
als Anhang an den Eintrag. Ein umgekehrter Schrägstrich ist immer er selbst,
damit Pfade wie `\\server\freigabe` stimmen; ein Zeichen maskiert man dafür
nicht mit `\`, sondern als Zeichencode wie `&#42;`. Die Umsetzung steht in
`src/Avis.Web/Markdowntext.cs`.

Ein Verweis der Form `![Name](anhang:…)` setzt ein Bild aus den Anhängen
desselben Eintrags an diese Stelle im Text. So stehen Bilder aus Mails an
ihrem Platz. Sie erscheinen dann nicht noch einmal in der Liste der Anhänge.

Unter jedem dieser Textfelder steht die Schaltfläche „Vorschau“. Sie zeigt
den Text an der Stelle des Felds so, wie er später im Auftrag steht, und
schaltet mit „Weiter schreiben“ zurück. Die Umwandlung macht der Server über
die Seite `/Vorschau` mit derselben Umsetzung wie bei der Anzeige; gespeichert
wird dabei nichts. Bilder, die noch nicht hochgeladen sind, zeigt die
Vorschau nicht, ein Verweis `anhang:…` erscheint als „[Bild: Name]“.

Die Vorschau, das Einfügen von Bildern mit Strg+V und die Tastenkürzel
brauchen JavaScript. Ohne funktioniert alles andere genauso.

## Darstellung

Blau steht in der Oberfläche für alles, was man anklicken kann: Schaltflächen
und Links. Der Kopf und Markierungen nutzen es nicht.

Die Oberfläche gibt es hell und dunkel. Ohne eigene Wahl folgt sie dem, was am
Gerät für helle oder dunkle Oberflächen eingestellt ist. Unter „Einstellungen“
im Menü unter dem eigenen Namen legt jeder Benutzer sie für sein Konto fest;
die Wahl gilt dann auf jedem Gerät. Alle Farben stehen als Variablen am Anfang
von `src/Avis.Web/wwwroot/css/avis.css`, einmal hell und zweimal dunkel (für
„wie das Gerät“ und für die ausdrückliche Wahl). Ein Test prüft, dass die
beiden dunklen Blöcke gleich sind und dass im übrigen Stylesheet keine festen
Farben stehen.

## Hilfe pflegen

Die Hilfe steht in der Anwendung unter „Hilfe“ im Kopf jeder Seite. Der
Eintrag führt zu dem Abschnitt, der zur gerade geöffneten Seite gehört.

- Die Texte sind Markdown-Dateien: `src/Avis.Web/Hilfe/bearbeiter.md`,
  `kunden.md` und `administratoren.md`. Wer sie ändert, braucht dafür nicht zu
  programmieren. Sie werden mit der Anwendung ausgeliefert.
- Jede Überschrift der zweiten Ebene (`## …`) ist ein Abschnitt mit einer
  Sprungmarke, die sich aus ihrem Text ergibt: aus „Geräte nennen“ wird
  `#geraete-nennen`. Welche Seite zu welchem Abschnitt führt, steht in
  `Hilfeverweise` (`src/Avis.Web/Hilfe/Hilfetexte.cs`). Ein Test prüft, dass
  es jeden dort genannten Abschnitt gibt und dass jede Seite einen hat. Wer
  eine Überschrift umbenennt, sieht das also am Test.
- Eigener Text eines Hauses gehört nicht in diese Dateien, siehe
  [Einstellungen](einstellungen.md#eigener-text-in-der-hilfe).

## Bild im README

`docs/bilder/auftrag.png` zeigt Auftrag 1 der Testdaten, übernommen und um
einen internen Kommentar und eine Nachricht an den Kunden ergänzt: helle
Darstellung, Fenster 1280 Punkte breit, doppelte Auflösung. Ändert sich die
Seite des Auftrags sichtbar, wird es neu aufgenommen. Es zeigt nur erfundene
Daten.

## Kurzvideos und Kurzanleitung

Unter https://t3chsynergy.github.io/avis/ stehen kurze Videos, die Bearbeitern
die wichtigsten Handgriffe zeigen, und die Kurzanleitung für Bearbeiter als
Webseite und als PDF. Niemand nimmt die Videos oder die Bilder von Hand auf:
Das Werkzeug `werkzeuge/Avis.Kurzvideos` bedient AVIS mit den erfundenen
Testdaten in einem Browser (Playwright für .NET).

- **Videos:** Es nimmt das Bild auf, lässt die Sätze von einer künstlichen
  Stimme sprechen und schneidet beides mit ffmpeg zusammen. Die Seite dazu
  sagt ausdrücklich, dass die Videos durch KI vertont und bearbeitet sind.
- **Kurzanleitung:** Der Text steht in `Kurzanleitung.md`. Das Werkzeug nimmt
  die Bildschirmfotos dazu auf, macht aus beidem eine Webseite und druckt sie
  als PDF. Im Repository liegen die Bilder nicht.

| Datei | Inhalt |
|---|---|
| `Drehbuch.cs` | Die Videos: Titel, gesprochene Sätze und der Ablauf im Browser |
| `Kamera.cs` | Aufnahme und die Handgriffe eines Videos (Maus, Klick, Tippen, Zeile im Band) |
| `Einblendung.js` | Was während der Aufnahme in der Seite mitläuft: Mauszeiger, Klickring, Band am unteren Rand |
| `Sprecher.cs` | Sprachausgabe und Schnitt |
| `Kurzanleitung.md` | Der Text der Kurzanleitung; am Anfang steht, welche Platzhalter er kennt |
| `Bildschirmfotos.cs` | Das Szenario, an dessen Stationen die Bilder der Kurzanleitung entstehen |
| `Anleitung.cs`, `Anleitung.css`, `Ablauf.svg` | Webseite und PDF der Kurzanleitung |
| `Seite.cs`, `Seite.css` | Die Seite mit den Videos, Kopf und Fuß beider Seiten |

Der Workflow `.github/workflows/kurzvideos.yml` baut alles neu und
veröffentlicht es auf GitHub Pages, und zwar nur auf Knopfdruck (Actions →
Kurzvideos → Run workflow, vom Zweig `main`). Bei einem Release läuft er
absichtlich nicht von selbst: Die meisten Releases ändern an der Oberfläche
nichts, und jeder Lauf kostet die Sprachausgabe. **Ein Lauf lohnt sich, wenn
sich die Oberfläche sichtbar geändert hat** – am besten gleich nach dem
Release, dann steht dessen Name auf den Seiten. Den Schlüssel für die
Sprachausgabe (OpenRouter) liest der Workflow aus dem Secret
`PAGES_OPENROUTER`. Ohne Schlüssel entstehen die Videos ohne Ton, mit
Untertiteln.

**Ändert sich die Oberfläche, gehören Drehbuch und Kurzanleitung
mitgepflegt:** Ein Ablauf kann hängen bleiben, und Text oder Bild der
Kurzanleitung können nicht mehr stimmen. Ob alles durchläuft, zeigt ein
Schnelldurchgang in gut einer Minute. Er baut die Kurzanleitung vollständig
und spielt die Videos ohne Ton und ohne Wartezeiten durch:

```bash
dotnet run --project src/Avis.Web          # in einem zweiten Fenster
dotnet run --project werkzeuge/Avis.Kurzvideos -- einrichten    # einmalig: lädt den Browser
dotnet run --project werkzeuge/Avis.Kurzvideos -- --schnell
```

**Achtung: Das Werkzeug legt die Testdaten der Entwicklungsumgebung neu
an**, so wie „Datenbank neu anlegen“ auf der Seite der Testanmeldung. Videos
und Bilder bauen aufeinander auf und brauchen jeweils denselben Anfang.

- Ohne `--schnell` entstehen auch die Videos (dafür muss `ffmpeg` installiert
  sein), mit `OPENROUTER_API_KEY` in der Umgebung auch der Ton.
- `--bis 3` hört nach dem dritten Video auf, `--bis 0` baut nur die
  Kurzanleitung. Einzelne Videos lassen sich nicht herausgreifen.
- `--ziel` nennt den Ordner für die Seiten. Scheitert ein Ablauf, liegt dort
  ein Bild der Stelle (`…-fehler.png`).
- Die Verweise ins Telefonbuch und nach Snipe-IT sind auf den Bildern nur zu
  sehen, wenn AVIS mit `Telefonbuch__Adresse` und `Geraeteabgleich__Adresse`
  in der Umgebung gestartet wurde; der Workflow setzt dafür erfundene Adressen.

Bei Pull Requests, die das Werkzeug ändern, läuft der Schnelldurchgang im
Workflow; die gebaute Kurzanleitung hängt dann zum Ansehen am Lauf.

Jeder Lauf lässt die Sätze neu sprechen; die Aussprache kann sich deshalb von
Lauf zu Lauf leicht unterscheiden. **Nach jedem Lauf die Videos einmal
durchspielen und die Kurzanleitung durchblättern:** Ob Bild, Ton und Text
stimmen, hört und sieht nur ein Mensch. Die gesprochenen Sätze meiden
Abkürzungen und Sonderzeichen, über die eine künstliche Stimme stolpert.

## Paket und Versionen

Der Workflow `.github/workflows/paket.yml` baut bei jedem Release die
veröffentlichte Anwendung für Windows Server und hängt sie als ZIP mit
Prüfsumme an das Release. Auf Knopfdruck baut er dasselbe Paket von jedem
Stand. Ein Release heißt `v1.2.3`; daraus wird die Version, die am Ende der
Hilfe steht. Alles ohne Release ist Version 0.0.0 mit dem Stand des Commits.
