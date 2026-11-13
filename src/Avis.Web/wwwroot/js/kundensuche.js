// Zeigt Treffer der Kundensuche schon während des Tippens.
// Ohne dieses Skript funktioniert die Suche über die Schaltfläche "Suchen".
(function () {
    const feld = document.getElementById('kundensuche');
    const treffer = document.getElementById('kundentreffer');
    if (!feld || !treffer) return;

    let wartend;
    let laufend = 0;

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
                if (nummer === laufend) treffer.innerHTML = html;
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
