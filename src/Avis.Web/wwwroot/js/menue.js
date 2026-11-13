// Schließt das Kontomenü bei einem Klick daneben und mit der Escape-Taste.
// Ohne dieses Skript öffnet und schließt es sich über den Namen.
(function () {
    const menue = document.querySelector('details.konto');
    if (!menue) return;

    document.addEventListener('click', ereignis => {
        if (!menue.contains(ereignis.target)) menue.open = false;
    });
    document.addEventListener('keydown', ereignis => {
        if (ereignis.key === 'Escape' && menue.open) {
            menue.open = false;
            menue.querySelector('summary').focus();
        }
    });
})();
