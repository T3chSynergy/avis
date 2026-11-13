// Formatierleiste für Textfelder mit Markdown und Einfügen von Bildern.
// Ohne dieses Skript tippt man Markdown von Hand und wählt Dateien über das Dateifeld.
(function () {
    const KNOEPFE = [
        { text: 'Fett', titel: 'Fett (Strg+B)', aktion: feld => umschliessen(feld, '**', '**', 'fetter Text') },
        { text: 'Kursiv', titel: 'Kursiv (Strg+I)', aktion: feld => umschliessen(feld, '*', '*', 'kursiver Text') },
        { text: 'Liste', titel: 'Aufzählung', aktion: feld => zeilenanfang(feld, () => '- ') },
        { text: '1. Liste', titel: 'Nummerierte Liste', aktion: feld => zeilenanfang(feld, i => (i + 1) + '. ') },
        { text: 'Zitat', titel: 'Zitat', aktion: feld => zeilenanfang(feld, () => '> ') },
        { text: 'Code', titel: 'Code oder Fehlermeldung', aktion: code },
        { text: 'Link', titel: 'Link', aktion: link },
    ];

    document.querySelectorAll('textarea[data-markdown]').forEach(feld => {
        leiste(feld);
        feld.addEventListener('keydown', ereignis => {
            if (!(ereignis.ctrlKey || ereignis.metaKey) || ereignis.altKey) return;
            const taste = ereignis.key.toLowerCase();
            if (taste === 'b') { ereignis.preventDefault(); KNOEPFE[0].aktion(feld); }
            if (taste === 'i') { ereignis.preventDefault(); KNOEPFE[1].aktion(feld); }
        });

        const dateifeld = feld.form && feld.form.querySelector('input[type="file"][name="dateien"]');
        if (dateifeld) bilder(feld, dateifeld);
    });

    function leiste(feld) {
        const zeile = document.createElement('div');
        zeile.className = 'editorleiste';
        zeile.setAttribute('role', 'toolbar');
        zeile.setAttribute('aria-label', 'Formatierung');
        KNOEPFE.forEach(k => {
            const knopf = document.createElement('button');
            knopf.type = 'button';
            knopf.textContent = k.text;
            knopf.title = k.titel;
            knopf.addEventListener('click', () => { k.aktion(feld); feld.focus(); });
            zeile.appendChild(knopf);
        });
        feld.parentNode.insertBefore(zeile, feld);
    }

    // Ersetzt die Auswahl und lässt sich mit Strg+Z zurücknehmen.
    function ersetzen(feld, start, ende, text, auswahlStart, auswahlEnde) {
        feld.focus();
        feld.setSelectionRange(start, ende);
        if (!document.execCommand || !document.execCommand('insertText', false, text)) {
            feld.setRangeText(text, start, ende, 'end');
            feld.dispatchEvent(new Event('input', { bubbles: true }));
        }
        feld.setSelectionRange(start + auswahlStart, start + auswahlEnde);
    }

    function umschliessen(feld, davor, danach, platzhalter) {
        const start = feld.selectionStart, ende = feld.selectionEnd;
        const auswahl = feld.value.slice(start, ende) || platzhalter;
        ersetzen(feld, start, ende, davor + auswahl + danach, davor.length, davor.length + auswahl.length);
    }

    function zeilenanfang(feld, praefix) {
        const wert = feld.value;
        const start = wert.lastIndexOf('\n', feld.selectionStart - 1) + 1;
        let ende = wert.indexOf('\n', feld.selectionEnd);
        if (ende < 0) ende = wert.length;
        const neu = wert.slice(start, ende).split('\n').map((zeile, i) => praefix(i) + zeile).join('\n');
        ersetzen(feld, start, ende, neu, 0, neu.length);
    }

    function code(feld) {
        const auswahl = feld.value.slice(feld.selectionStart, feld.selectionEnd);
        if (auswahl.includes('\n')) {
            const start = feld.selectionStart, ende = feld.selectionEnd;
            const vorher = start > 0 && feld.value[start - 1] !== '\n' ? '\n' : '';
            ersetzen(feld, start, ende, vorher + '```\n' + auswahl + '\n```\n', vorher.length + 4, vorher.length + 4 + auswahl.length);
        } else {
            umschliessen(feld, '`', '`', 'Code');
        }
    }

    function link(feld) {
        const start = feld.selectionStart, ende = feld.selectionEnd;
        const auswahl = feld.value.slice(start, ende);
        if (/^https?:\/\//i.test(auswahl)) {
            ersetzen(feld, start, ende, '[Linktext](' + auswahl + ')', 1, 9);
        } else {
            const text = auswahl || 'Linktext';
            ersetzen(feld, start, ende, '[' + text + '](https://)', text.length + 3, text.length + 11);
        }
    }

    // Bilder aus der Zwischenablage und hineingezogene Dateien werden Anhänge
    // dieses Eintrags. Unter dem Dateifeld steht, was mitgeschickt wird.
    function bilder(feld, dateifeld) {
        const hinweis = document.createElement('p');
        hinweis.className = 'erklaerung';
        hinweis.textContent = 'Bilder lassen sich mit Strg+V in das Textfeld einfügen, Dateien auch hineinziehen.';
        const vorschau = document.createElement('ul');
        vorschau.className = 'dateivorschau';
        vorschau.setAttribute('aria-live', 'polite');
        dateifeld.after(hinweis, vorschau);

        function setzen(dateien) {
            const sammlung = new DataTransfer();
            dateien.forEach(datei => sammlung.items.add(datei));
            dateifeld.files = sammlung.files;
            zeigen();
        }

        function hinzufuegen(neue) {
            setzen(Array.from(dateifeld.files).concat(neue));
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
                weg.addEventListener('click', () => setzen(Array.from(dateifeld.files).filter((_, i) => i !== nummer)));
                eintrag.append(name, weg);
                vorschau.appendChild(eintrag);
            });
        }

        dateifeld.addEventListener('change', zeigen);

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
        return (bytes / (1024 * 1024)).toFixed(1).replace('.', ',') + ' MB';
    }
})();
