// Bilder in Textfelder einfügen: Ein Bild aus der Zwischenablage (Strg+V) und
// hineingezogene Dateien werden Anhänge dieses Eintrags. Unter dem Dateifeld
// steht, was mitgeschickt wird.
// Ohne dieses Skript wählt man Dateien über das Dateifeld.
(function () {
    document.querySelectorAll('textarea[data-markdown]').forEach(feld => {
        const dateifeld = feld.form && feld.form.querySelector('input[type="file"][name="dateien"]');
        if (dateifeld) bilder(feld, dateifeld);
    });

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
