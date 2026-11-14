// Zeigt Treffer der Kundensuche schon während des Tippens.
// Ohne dieses Skript funktioniert die Suche über die Schaltfläche "Suchen".
(function () {
    const feld = document.getElementById('kundensuche');
    const treffer = document.getElementById('kundentreffer');
    if (!feld || !treffer) return;

    let wartend;
    let laufend = 0;

    // Beim Erfassen: Ein Kunde muss gewählt sein, und wer einen neuen anlegt, muss dessen
    // Namen nennen. Das prüft der Browser vor dem Absenden, damit das Formular nicht
    // mit einem Fehler zurückkommt und dabei die gewählten Anhänge verliert.
    const neuerName = document.querySelector('.neukunde input[name$=".KundeName"]');
    function pflicht() {
        if (!neuerName) return;
        const neu = treffer.querySelector('#kunde-neu');
        neuerName.required = !!neu && neu.checked;
        feld.setCustomValidity(treffer.querySelector('input[name="kundeId"]:checked')
            ? ''
            : 'Bitte einen Kunden suchen und auswählen. Ist er nicht dabei, lässt er sich nach der Suche neu anlegen.');
    }
    treffer.addEventListener('change', ereignis => {
        pflicht();
        // Wer "neuen Kunden anlegen" wählt, hat den Namen meist gerade gesucht.
        if (neuerName && ereignis.target.id === 'kunde-neu' && ereignis.target.checked) {
            const gesucht = feld.value.trim();
            if (!neuerName.value && gesucht && !/[@\d]/.test(gesucht)) neuerName.value = gesucht;
            neuerName.focus();
        }
    });
    pflicht();

    function suchen() {
        const gewaehlt = treffer.querySelector('input[name="kundeId"]:checked');
        const url = new URL(treffer.dataset.url, location.href);
        url.searchParams.set('q', feld.value);
        if (gewaehlt && gewaehlt.value) url.searchParams.set('gewaehlt', gewaehlt.value);
        if (gewaehlt && !gewaehlt.value) url.searchParams.set('neu', 'true');

        const nummer = ++laufend;
        fetch(url, { credentials: 'same-origin' })
            .then(antwort => antwort.ok ? antwort.text() : Promise.reject())
            .then(html => {
                // Nur die Antwort auf die letzte Eingabe zählt.
                if (nummer !== laufend) return;
                treffer.innerHTML = html;
                pflicht();
            })
            .catch(() => { });
    }

    feld.addEventListener('input', () => {
        clearTimeout(wartend);
        wartend = setTimeout(suchen, 250);
    });

    feld.addEventListener('keydown', ereignis => {
        if (ereignis.key !== 'Enter') return;
        ereignis.preventDefault();
        clearTimeout(wartend);
        suchen();
    });
})();
