// Komfort im Kopf jeder Seite. Ohne dieses Skript funktioniert alles genauso,
// nur ohne Tastenkürzel.
(function () {
    // Das Kontomenü schließt bei einem Klick daneben und mit der Escape-Taste.
    const menue = document.querySelector('details.konto');
    if (menue) {
        document.addEventListener('click', ereignis => {
            if (!menue.contains(ereignis.target)) menue.open = false;
        });
        document.addEventListener('keydown', ereignis => {
            if (ereignis.key === 'Escape' && menue.open) {
                menue.open = false;
                menue.querySelector('summary').focus();
            }
        });
    }

    // Alt+N: neuer Auftrag. Alt+S: Suche. In jedem Browser dieselben Tasten.
    document.addEventListener('keydown', ereignis => {
        if (!ereignis.altKey || ereignis.ctrlKey || ereignis.metaKey || ereignis.shiftKey) return;

        if (ereignis.code === 'KeyN') {
            const neu = document.querySelector('.kopf a.neu');
            if (neu) { ereignis.preventDefault(); neu.click(); }
        } else if (ereignis.code === 'KeyS') {
            const suche = document.getElementById('schnellsuche');
            if (suche) { ereignis.preventDefault(); suche.focus(); suche.select(); }
        }
    });
})();
