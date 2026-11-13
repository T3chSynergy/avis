// Komfort auf der Auftragsseite. Ohne dieses Skript funktioniert alles genauso.
(function () {
    // Geht ein Formular auf, steht die Schreibmarke gleich im ersten Feld.
    function fokus(aktion) {
        const panel = document.querySelector('.panel[data-aktion="' + aktion + '"]');
        const feld = panel && panel.querySelector('select, textarea');
        if (feld) feld.focus();
    }

    document.querySelectorAll('.aktionen input[name="aktion"]').forEach(wahl => {
        wahl.addEventListener('change', () => { if (wahl.checked && wahl.value) fokus(wahl.value); });
    });

    // Nach einem Fehler ist das Formular mit dem Entwurf schon offen.
    const offen = document.querySelector('.aktionen .leiste input[name="aktion"]:checked');
    if (offen) fokus(offen.value);

    // Strg+Eingabe sendet das Formular ab, in dem man gerade schreibt.
    document.querySelectorAll('.panel textarea').forEach(feld => {
        feld.addEventListener('keydown', ereignis => {
            if (ereignis.key === 'Enter' && (ereignis.ctrlKey || ereignis.metaKey)) {
                ereignis.preventDefault();
                feld.form.requestSubmit();
            }
        });
    });
})();
