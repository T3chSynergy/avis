// Schlägt beim Tippen einer Inventarnummer passende Geräte vor.
// Ohne dieses Skript trägt man die Inventarnummer von Hand ein.
(function () {
    const liste = document.getElementById('geraetevorschlaege');
    const feld = document.querySelector('input[list="geraetevorschlaege"]');
    if (!feld || !liste) return;

    let wartend;
    let laufend = 0;

    feld.addEventListener('input', () => {
        clearTimeout(wartend);
        wartend = setTimeout(() => {
            const url = new URL(liste.dataset.url, location.href);
            url.searchParams.set('q', feld.value);

            const nummer = ++laufend;
            fetch(url, { credentials: 'same-origin' })
                .then(antwort => antwort.ok ? antwort.text() : Promise.reject())
                .then(html => {
                    if (nummer === laufend) liste.innerHTML = html;
                })
                .catch(() => { });
        }, 200);
    });
})();
