// Zeigt Treffer der Kundensuche schon während des Tippens.
// Ohne dieses Skript funktioniert die Suche über die Schaltfläche "Suchen".
(function () {
    const feld = document.getElementById('kundensuche');
    const treffer = document.getElementById('kundentreffer');
    if (!feld || !treffer) return;

    let wartend;
    let laufend = 0;

    // Beim Erfassen: Wer keinen Kunden wählt, legt einen neuen an und muss dessen
    // Namen nennen. Das prüft der Browser vor dem Absenden, damit das Formular nicht
    // mit einem Fehler zurückkommt und dabei die gewählten Anhänge verliert.
    const neuerName = document.querySelector('.neukunde input[name$=".KundeName"]');
    function pflicht() {
        const neu = treffer.querySelector('#kunde-neu');
        if (neuerName) neuerName.required = !!neu && neu.checked;
    }
    treffer.addEventListener('change', pflicht);
    pflicht();

    function suchen() {
        const gewaehlt = treffer.querySelector('input[name="kundeId"]:checked');
        const url = new URL(treffer.dataset.url, location.href);
        url.searchParams.set('q', feld.value);
        if (gewaehlt && gewaehlt.value) url.searchParams.set('gewaehlt', gewaehlt.value);

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
