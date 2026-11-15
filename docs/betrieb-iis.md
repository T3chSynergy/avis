# AVIS unter Windows Server mit IIS

Wie AVIS auf einem Windows Server eingerichtet und später aktualisiert wird.
Was die einzelnen Einstellungen bedeuten und wie sich die Anbindungen
verhalten, steht in [Einstellungen und Anbindungen](einstellungen.md).

**Diese Anleitung ist erst in Teilen erprobt.** Die Einrichtung ist im
Probelauf unter Windows Server 2022 gelungen; das Update und die Testfassung
sind noch nicht ausprobiert. Wo sie nicht stimmt, bitte ein Issue stellen.

## Was auf dem Server liegt

Ein Update ersetzt das Paket. Alles, was zu einer Installation gehört, liegt
deshalb in Dateien, die das Paket nicht enthält.

| Was | Woher | Bei einem Update |
|---|---|---|
| Programmdateien, `wwwroot`, `Hilfe`, `LICENSE`, `THIRD-PARTY-NOTICES.md` | Paket | werden ersetzt |
| `appsettings.json` mit den Voreinstellungen | Paket | wird ersetzt, deshalb nicht ändern |
| `web.config` | Paket | wird ersetzt |
| `appsettings.Production.json` mit den Einstellungen dieser Installation | selbst angelegt | bleibt |
| `App_Data` mit Datenbank, Anhängen und dem eigenen Hilfetext | legt AVIS an | bleibt |

## Voraussetzungen

- Windows Server mit IIS. Dazu der Rollendienst **Windows-Authentifizierung**
  (Windows Authentication) und, damit die Hintergrunddienste ohne Aufruf
  starten, die **Anwendungsinitialisierung** (Application Initialization).
  Wie man beide nachinstalliert, steht im nächsten Abschnitt.
- Das **ASP.NET Core Hosting Bundle für .NET 10**. Nach der Installation IIS
  neu starten (`iisreset`).
- Der Server ist Mitglied der Domäne. Die Windows-Anmeldung und die Abgleiche
  mit dem Active Directory nutzen die Domäne des Servers; eine Adresse dafür
  wird nirgends eingestellt.
- Ein **Domänenkonto für den Anwendungspool**. Es muss im Verzeichnis lesen
  dürfen und, je nach Anbindung, den Mailserver, das Postfach und Snipe-IT
  erreichen.

Die Bezeichnungen der englischen Oberfläche von Windows Server und IIS stehen
in dieser Anleitung in Klammern.

## Rollendienste von IIS nachinstallieren

Beide Rollendienste gehören nicht zur Grundinstallation von IIS. In einer
PowerShell als Administrator:

```powershell
Install-WindowsFeature Web-Windows-Auth, Web-AppInit
```

Oder im Server-Manager über „Rollen und Features hinzufügen“ (Manage → Add
Roles and Features), unter Webserver (IIS) → Webserver:

| Rollendienst | Wo |
|---|---|
| Windows-Authentifizierung (Windows Authentication) | Sicherheit (Security) |
| Anwendungsinitialisierung (Application Initialization) | Anwendungsentwicklung (Application Development) |

Die Installation allein schaltet noch nichts ein. Das geschieht in den
Schritten 3 bis 5 unter „Einrichten“.

## Paket holen

Das Paket hängt als `avis-<Version>-win-x64.zip` an jedem
[Release](https://github.com/T3chSynergy/avis/releases), daneben die Datei
`.sha256` mit der Prüfsumme. Vor dem Entpacken vergleichen:

```powershell
(Get-FileHash .\avis-1.2.3-win-x64.zip -Algorithm SHA256).Hash.ToLower()
Get-Content .\avis-1.2.3-win-x64.zip.sha256
```

Ein Paket von einem Stand ohne Release baut der Workflow „Paket“ auf
Knopfdruck (Actions → Paket → Run workflow). Es steht dann als Download am
Lauf und trägt die Version 0.0.0 mit dem Stand des Commits.

## Einrichten

1. **Ordner anlegen und entpacken**, zum Beispiel nach `C:\inetpub\avis`.
   Darin den Ordner `App_Data` anlegen.
2. **Rechte vergeben:** Das Konto des Anwendungspools braucht Lesen auf dem
   Ordner und Ändern auf `App_Data`.
3. **Anwendungspool anlegen** (Application Pools → Add Application Pool) und
   in seinen erweiterten Einstellungen (Advanced Settings) setzen:
   - .NET CLR-Version (.NET CLR version): „Kein verwalteter Code“ (No Managed
     Code)
   - Identität (Identity): das Domänenkonto
   - Startmodus (Start Mode): `AlwaysRunning`
   - Leerlauftimeout in Minuten (Idle Time-out (minutes)): `0`
4. **Website oder Anwendung anlegen** (Sites → Add Website), die auf den
   Ordner zeigt und diesen Anwendungspool nutzt. Eine Bindung mit HTTPS und
   Zertifikat einrichten. In den erweiterten Einstellungen der Website
   „Vorabladen aktiviert“ (Preload Enabled) auf `True` setzen; das wirkt nur,
   wenn die Anwendungsinitialisierung installiert ist.
5. **Authentifizierung** (Authentication) der Website: Windows-Authentifizierung
   (Windows Authentication) aktivieren, anonyme Authentifizierung (Anonymous
   Authentication) deaktivieren.
6. **Einstellungen anlegen:** die Datei `appsettings.Production.json` selbst
   erstellen, siehe den nächsten Abschnitt.
7. **Aufrufen.** Beim ersten Start legt AVIS die Datenbank in `App_Data` an.
   Die Seite öffnet sich ohne Anmeldedialog, oben rechts steht der eigene
   Name.

Startmodus, Leerlauftimeout und Vorabladen sind nötig, weil der Mailversand,
der Abruf des Postfachs und die nächtlichen Abgleiche nur laufen, solange die
Anwendung läuft. Beendet IIS sie nach Leerlauf, gehen wartende Mails erst beim
nächsten Aufruf hinaus, und die Abgleiche laufen erst morgens beim ersten
Aufruf statt in der Nacht.

## Einstellungen dieser Installation

Die Datei `appsettings.Production.json` **legt man selbst an**, im Ordner der
Anwendung neben `appsettings.json`. Sie ist nicht im Paket, und AVIS erzeugt
sie auch nicht: Nur so überlebt sie ein Update. Fehlt sie, gelten die
Voreinstellungen aus `appsettings.json`.

Sie enthält nur, was von den Voreinstellungen abweicht. Ein Beispiel für den
Anfang, mit erfundenen Namen:

```json
{
  "Anmeldung": {
    "Administratoren": [ "beispiel\\anna" ]
  },
  "Mail": {
    "Art": "Ordner",
    "BasisUrl": "https://avis.example.org"
  }
}
```

- **Der erste Administrator** steht in `Anmeldung:Administratoren`, in der
  Form `domäne\name`. In der Datei wird der Schrägstrich verdoppelt.
- **Anbindungen einzeln einschalten.** Mailversand, E-Mail-Eingang und die
  drei Abgleiche sind in der Voreinstellung aus oder harmlos. Es lohnt, sie
  nacheinander einzuschalten und jede zu prüfen, bevor die nächste kommt.
- **Mit `Mail:Art = Ordner` beginnen.** Die Mails landen dann als Dateien in
  `App_Data\Mails` und gehen an niemanden. Sobald der Kundenabgleich echte
  Adressen geholt hat, bekäme mit `Smtp` jeder Kunde eines Testauftrags eine
  Mail.
- **Den E-Mail-Eingang zuerst an einem Testpostfach prüfen.** AVIS markiert
  abgeholte Mails als gelesen oder verschiebt sie.

Nach einer Änderung an der Datei den Anwendungspool neu starten. Ob die Datei
gültig ist, zeigt PowerShell; bei einem Tippfehler nennt es die Stelle:

```powershell
Get-Content .\appsettings.Production.json -Raw | ConvertFrom-Json
```

### Kennwörter und Schlüssel

Das Kennwort des Mailservers, das des Postfachs und der Schlüssel für
Snipe-IT stehen nicht im Paket. Es gibt zwei Wege:

- **In `appsettings.Production.json`**, als `Mail:Kennwort`,
  `Posteingang:Kennwort` und `Geraeteabgleich:Schluessel`. Die Rechte der
  Datei dann so setzen, dass nur Administratoren und das Konto des
  Anwendungspools sie lesen.
- **Als Umgebungsvariablen des Anwendungspools:** `Mail__Kennwort`,
  `Posteingang__Kennwort`, `Geraeteabgleich__Schluessel`, mit zwei
  Unterstrichen. Sie lassen sich im Konfigurations-Editor (Configuration
  Editor) von IIS am Anwendungspool eintragen und stehen dann in der
  Konfiguration von IIS, nicht im Ordner der Anwendung.

Umgebungsvariablen in der `web.config` überleben kein Update.

## Testfassung mit Testanmeldung und Testdaten

Zum Ansehen und Ausprobieren lässt sich AVIS auch auf dem Server so starten
wie in der Entwicklung: mit erfundenen Testdaten und einer Anmeldung, bei der
man einen Testbenutzer auswählt. Mit Active Directory, Mailserver und
Snipe-IT hat diese Fassung nichts zu tun.

**Jeder, der die Seite erreicht, kann sich ohne Kennwort als Administrator
anmelden.** Sie gehört deshalb nur dorthin, wo allein Befugte hinkommen, und
nie in denselben Ordner wie eine echte Installation: „Datenbank neu anlegen“
auf der Anmeldeseite leert die Datenbank vollständig.

1. **Eigenen Ordner, eigenen Anwendungspool und eigene Website anlegen**, wie
   unter „Einrichten“, Schritte 1 bis 4.
2. **Authentifizierung** der Website: anonyme Authentifizierung (Anonymous
   Authentication) aktivieren. Die Windows-Authentifizierung wird hier nicht
   gebraucht.
3. **Die Datei `appsettings.Development.json` anlegen**, im Ordner der
   Anwendung neben `appsettings.json`, mit genau diesem Inhalt. Bei `BasisUrl`
   steht die Adresse, unter der die Testseite erreichbar ist.

   ```json
   {
     "Anmeldung": {
       "Art": "Entwicklung"
     },
     "Testdaten": {
       "Anlegen": true
     },
     "Verzeichnis": {
       "Art": "Keines"
     },
     "Mail": {
       "Art": "Ordner",
       "Absender": "it-hotline@example.org",
       "BasisUrl": "https://avis-test.example.org"
     }
   }
   ```

4. **Die Umgebung auf „Development“ stellen.** Dazu den Inhalt der Datei
   `web.config` im Ordner der Anwendung durch diesen ersetzen. Er
   unterscheidet sich von der ausgelieferten Datei nur durch den Block
   `environmentVariables`.

   ```xml
   <?xml version="1.0" encoding="utf-8"?>
   <configuration>
     <location path="." inheritInChildApplications="false">
       <system.webServer>
         <handlers>
           <add name="aspNetCore" path="*" verb="*" modules="AspNetCoreModuleV2" resourceType="Unspecified" />
         </handlers>
         <aspNetCore processPath=".\Avis.Web.exe" stdoutLogEnabled="false" stdoutLogFile=".\logs\stdout" hostingModel="inprocess">
           <environmentVariables>
             <environmentVariable name="ASPNETCORE_ENVIRONMENT" value="Development" />
           </environmentVariables>
         </aspNetCore>
       </system.webServer>
     </location>
   </configuration>
   ```

5. **Anwendungspool neu starten und die Seite aufrufen.** Es erscheint die
   Anmeldeseite mit den Testbenutzern.

Zur Sicherheit vor Tippfehlern lässt sich die Datei mit den Einstellungen
prüfen; bei einem Fehler nennt PowerShell die Stelle:

```powershell
Get-Content .\appsettings.Development.json -Raw | ConvertFrom-Json
```

- Ohne Schritt 4 bricht AVIS den Start ab: Die Testanmeldung und die
  Testdaten sind nur in der Umgebung „Development“ erlaubt.
- In dieser Umgebung liest AVIS `appsettings.Development.json` und nicht
  `appsettings.Production.json`.
- Mails gehen an niemanden; sie liegen als Dateien in `App_Data\Mails`.
- Bei einem Fehler zeigt AVIS in dieser Umgebung die technischen Einzelheiten
  im Browser.
- Ein Update ersetzt die `web.config`. Schritt 4 ist danach zu wiederholen;
  `appsettings.Development.json` und `App_Data` bleiben.

## Update

1. **Paket holen und Prüfsumme vergleichen**, wie oben.
2. **Anwendung anhalten:** den Anwendungspool beenden.
3. **`App_Data` sichern.** Der Ordner enthält die Datenbank und die Anhänge.
   Die Kopie ist nur bei angehaltener Anwendung sicher vollständig.
4. **Alte Programmdateien entfernen:** alles im Ordner löschen außer
   `App_Data` und `appsettings.Production.json`. So bleiben keine Dateien
   liegen, die es in der neuen Version nicht mehr gibt.
5. **Neues Paket in den Ordner entpacken.**
6. **Anwendungspool starten und die Seite aufrufen.** Beim Start bringt AVIS
   die Datenbank selbst auf den neuen Stand.
7. **Prüfen:** Am Ende der Hilfe steht „Installiert ist Version …“.

**Zurück zur alten Version:** Die Datenbank lässt sich nicht zurückstufen.
Deshalb das Paket der alten Version entpacken und die Sicherung von
`App_Data` zurückspielen. Was seit der Sicherung erfasst wurde, geht dabei
verloren.

## Wenn etwas nicht geht

- **Die Seite zeigt einen Fehler 500.19 oder 500.31.** Meist fehlt das
  Hosting Bundle, oder IIS wurde danach nicht neu gestartet.
- **Die Seite zeigt einen Fehler 500.30: AVIS startet nicht.** Der Grund
  steht in der Ereignisanzeige (Event Viewer) von Windows unter „Anwendung“
  (Windows Logs → Application). AVIS bricht den Start mit einer Meldung ab,
  wenn eine Einstellung nicht stimmt, zum Beispiel ein Abgleich ohne Filter,
  oder wenn es in `App_Data` nicht schreiben darf.
- **Warnungen zu Migrationen beim ersten Start.** Beim Anlegen der Datenbank
  meldet das Protokoll mehrmals, ein Schritt laufe nicht in einer Transaktion.
  Das ist bekannt und bei einer neuen Datenbank ohne Folgen.
- **Mehr Ausgabe beim Start:** in der `web.config` `stdoutLogEnabled` auf
  `true` setzen und den Ordner `logs` anlegen, auf den das Konto des
  Anwendungspools schreiben darf. Danach wieder ausschalten; das nächste
  Update setzt die Datei ohnehin zurück.
- **Es erscheint ein Anmeldedialog.** Die Windows-Anmeldung ohne Dialog
  setzt voraus, dass der Browser die Adresse zum Intranet zählt.
- **„Kein Zugriff“.** Das Konto ist in AVIS deaktiviert, oder die Seite ist
  nur für Bearbeiter oder Administratoren.
- **Ein Abgleich, eine Mail oder eine eingegangene Mail scheitert.** Die
  Meldung steht in der Verwaltung auf der Seite des Abgleichs, unter „Mails“
  oder unter „Posteingang“.
- **Ein Upload wird abgewiesen.** IIS nimmt in der Voreinstellung rund 30 MB
  je Anfrage an.
- **Namen und Adressen neuer Konten fehlen.** Die Abfrage des Verzeichnisses
  ist gescheitert; AVIS arbeitet dann mit dem Anmeldenamen weiter und
  schreibt eine Warnung ins Protokoll. Meist darf das Konto des
  Anwendungspools im Verzeichnis nicht lesen.

Am Exchange-Server muss IMAP für das Postfach eingeschaltet sein, damit der
E-Mail-Eingang es abrufen kann.
