// Zugaben für die Textfelder mit Markdown.
//
// Vorschau: Unter jedem Textfeld steht eine Schaltfläche, die den Text so
// zeigt, wie er später im Auftrag steht. Die Umwandlung macht der Server,
// damit Vorschau und Ergebnis übereinstimmen.
//
// Bilder in Textfelder einfügen: Ein Bild aus der Zwischenablage (Strg+V) und
// hineingezogene Dateien werden Anhänge dieses Eintrags. Unter dem Dateifeld
// steht, was mitgeschickt wird. Wer nacheinander mehrmals Dateien wählt,
// behält die schon gewählten. Zu große, leere und zu viele Dateien werden
// gleich abgelehnt: Käme das Formular deswegen mit einem Fehler zurück,
// wären alle gewählten Dateien weg.
// Ohne dieses Skript wählt man Dateien über das Dateifeld, alle auf einmal,
// und es gibt keine Vorschau.
(function () {
    const vorschauadresse = document.body.dataset.vorschau;

    document.querySelectorAll('textarea[data-markdown]').forEach(feld => {
        const dateifeld = feld.form && feld.form.querySelector('input[type="file"][name="dateien"]');
        if (dateifeld) bilder(feld, dateifeld);
        if (vorschauadresse && feld.form && !feld.readOnly) vorschau(feld);
    });

    function vorschau(feld) {
        const flaeche = document.createElement('div');
        flaeche.className = 'vorschau';
        flaeche.hidden = true;
        flaeche.setAttribute('role', 'region');
        flaeche.setAttribute('aria-label', 'Vorschau');
        flaeche.setAttribute('aria-live', 'polite');
        const knopf = document.createElement('button');
        knopf.type = 'button';
        knopf.className = 'vorschauknopf';
        knopf.textContent = 'Vorschau';
        feld.after(flaeche, knopf);

        function hinweis(text) {
            const absatz = document.createElement('p');
            absatz.className = 'erklaerung';
            absatz.textContent = text;
            flaeche.replaceChildren(absatz);
        }

        function schreiben() {
            if (feld.hidden) {
                feld.hidden = false;
                flaeche.hidden = true;
                knopf.textContent = 'Vorschau';
            }
        }

        function zeigen() {
            // Die Vorschau nimmt den Platz des Textfelds ein und ist mindestens so hoch.
            flaeche.style.minHeight = feld.offsetHeight + 'px';
            feld.hidden = true;
            flaeche.hidden = false;
            knopf.textContent = 'Weiter schreiben';

            if (feld.value.trim() === '') {
                hinweis('Noch kein Text.');
                return;
            }

            const text = feld.value;
            const daten = new URLSearchParams();
            daten.set('text', text);
            const schutz = feld.form.querySelector('input[name="__RequestVerificationToken"]');
            if (schutz) daten.set('__RequestVerificationToken', schutz.value);
            hinweis('Die Vorschau wird geladen …');
            fetch(vorschauadresse, { method: 'POST', body: daten, credentials: 'same-origin' })
                .then(antwort => {
                    // Eine Umleitung führt zur Anmeldung: Deren Seite gehört nicht in die Vorschau.
                    if (!antwort.ok || antwort.redirected) throw new Error('Status ' + antwort.status);
                    return antwort.text();
                })
                .then(html => {
                    // Inzwischen zurückgeschaltet oder weitergeschrieben: Diese Antwort ist überholt.
                    if (flaeche.hidden || feld.value !== text) return;
                    flaeche.replaceChildren(...new DOMParser().parseFromString(html, 'text/html').body.childNodes);
                    // Ein Link öffnet sich in einem neuen Fenster: Sonst wäre mit dem Formular der Text weg.
                    flaeche.querySelectorAll('a').forEach(verweis => { verweis.target = '_blank'; });
                })
                .catch(() => {
                    if (!flaeche.hidden) hinweis('Die Vorschau lässt sich gerade nicht laden. Der Text ist unverändert.');
                });
        }

        knopf.addEventListener('click', () => {
            if (feld.hidden) {
                schreiben();
                feld.focus();
            } else {
                zeigen();
            }
        });

        // Fehlt beim Absenden ein Pflichttext, muss der Browser das Feld zeigen können.
        feld.addEventListener('invalid', schreiben);
    }

    function bilder(feld, dateifeld) {
        const hinweis = document.createElement('p');
        hinweis.className = 'erklaerung';
        hinweis.textContent = 'Bilder lassen sich mit Strg+V in das Textfeld einfügen, Dateien auch hineinziehen.';
        const vorschau = document.createElement('ul');
        vorschau.className = 'dateivorschau';
        vorschau.setAttribute('aria-live', 'polite');
        const meldung = document.createElement('p');
        meldung.className = 'feldfehler';
        meldung.setAttribute('role', 'alert');
        dateifeld.after(hinweis, meldung, vorschau);

        // Dieselben Grenzen prüft der Server noch einmal.
        const hoechstgroesse = Number(dateifeld.dataset.hoechstgroesse) || Infinity;
        const hoechstzahl = Number(dateifeld.dataset.hoechstzahl) || Infinity;

        // Was mitgeschickt wird. Der Browser ersetzt die Auswahl im Dateifeld
        // bei jedem Öffnen des Dialogs, deshalb steht sie hier noch einmal.
        let bestand = Array.from(dateifeld.files);

        function setzen(dateien) {
            bestand = dateien;
            const sammlung = new DataTransfer();
            dateien.forEach(datei => sammlung.items.add(datei));
            dateifeld.files = sammlung.files;
            zeigen();
        }

        // Dieselbe Datei ein zweites Mal zu wählen, hängt sie nicht doppelt an.
        function hinzufuegen(neue) {
            const liste = bestand.slice();
            const abgelehnt = [];
            const zuViele = [];
            neue.forEach(datei => {
                if (liste.some(vorhanden => gleich(vorhanden, datei))) return;
                if (datei.size === 0) abgelehnt.push(datei.name + ' ist leer');
                else if (datei.size > hoechstgroesse) abgelehnt.push(datei.name + ' ist größer als ' + groesse(hoechstgroesse));
                else if (liste.length >= hoechstzahl) zuViele.push(datei.name);
                else liste.push(datei);
            });
            if (zuViele.length) abgelehnt.push(zuViele.join(', ') + ' – es gehen höchstens ' + hoechstzahl + ' Dateien auf einmal');
            meldung.textContent = abgelehnt.length ? 'Nicht angehängt: ' + abgelehnt.join('; ') + '.' : '';
            setzen(liste);
        }

        function gleich(a, b) {
            return a.name === b.name && a.size === b.size && a.lastModified === b.lastModified;
        }

        function zeigen() {
            vorschau.replaceChildren();
            Array.from(dateifeld.files).forEach((datei, nummer) => {
                const eintrag = document.createElement('li');
                if (datei.type.startsWith('image/')) {
                    const bild = document.createElement('img');
                    bild.src = URL.createObjectURL(datei);
                    bild.alt = '';
                    bild.addEventListener('load', () => URL.revokeObjectURL(bild.src));
                    eintrag.appendChild(bild);
                }
                const name = document.createElement('span');
                name.textContent = datei.name + ' (' + groesse(datei.size) + ')';
                const weg = document.createElement('button');
                weg.type = 'button';
                weg.textContent = 'Entfernen';
                weg.setAttribute('aria-label', datei.name + ' entfernen');
                weg.addEventListener('click', () => setzen(bestand.filter((_, i) => i !== nummer)));
                eintrag.append(name, weg);
                vorschau.appendChild(eintrag);
            });
        }

        dateifeld.addEventListener('change', () => hinzufuegen(Array.from(dateifeld.files)));

        feld.addEventListener('paste', ereignis => {
            const dateien = Array.from(ereignis.clipboardData ? ereignis.clipboardData.files : []);
            if (dateien.length === 0) return; // gewöhnlicher Text
            ereignis.preventDefault();
            hinzufuegen(dateien.map(benannt));
        });

        feld.addEventListener('dragover', ereignis => {
            if (!ereignis.dataTransfer || !Array.from(ereignis.dataTransfer.types).includes('Files')) return;
            ereignis.preventDefault();
            feld.classList.add('ablegen');
        });
        feld.addEventListener('dragleave', () => feld.classList.remove('ablegen'));
        feld.addEventListener('drop', ereignis => {
            feld.classList.remove('ablegen');
            const dateien = Array.from(ereignis.dataTransfer ? ereignis.dataTransfer.files : []);
            if (dateien.length === 0) return;
            ereignis.preventDefault();
            hinzufuegen(dateien);
        });
    }

    // Ein Bild aus der Zwischenablage heißt immer "image.png". Es bekommt einen sprechenden Namen.
    function benannt(datei) {
        if (!/^image\.\w+$/i.test(datei.name)) return datei;
        const jetzt = new Date();
        const zwei = zahl => String(zahl).padStart(2, '0');
        const stempel = jetzt.getFullYear() + '-' + zwei(jetzt.getMonth() + 1) + '-' + zwei(jetzt.getDate())
            + ' ' + zwei(jetzt.getHours()) + '.' + zwei(jetzt.getMinutes()) + '.' + zwei(jetzt.getSeconds());
        const endung = datei.name.slice(datei.name.lastIndexOf('.'));
        return new File([datei], 'Bildschirmfoto ' + stempel + endung, { type: datei.type });
    }

    function groesse(bytes) {
        if (bytes < 1024) return bytes + ' Byte';
        if (bytes < 1024 * 1024) return Math.round(bytes / 1024) + ' KB';
        const mb = bytes / (1024 * 1024);
        return (Number.isInteger(mb) ? String(mb) : mb.toFixed(1).replace('.', ',')) + ' MB';
    }
})();
