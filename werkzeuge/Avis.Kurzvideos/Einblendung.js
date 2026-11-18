// Läuft während der Aufnahme in jeder Seite: ein sichtbarer Mauszeiger, ein Ring bei jedem Klick und unten ein
// Band mit der Zeile zum Schritt. Zeiger und Zeile merkt sich die Seite, damit sie einen Seitenwechsel überstehen.
// Die Datei ist eine Funktion; das Werkzeug ruft sie mit der Höhe des Bandes auf. Mit 0 gibt es kein Band.
band => {
    if (window.top !== window) return;
    const bauen = () => {
        if (document.getElementById('film-zeiger')) return;
        const stil = document.createElement('style');
        const platz = band > 0 ? `
            html { scroll-padding-bottom: ${band + 24}px; }
            body { padding-bottom: ${band + 8}px !important; }` : `
            #film-text { display: none !important; }`;
        stil.textContent = platz + `
            #film-zeiger { position: fixed; z-index: 2147483647; left: 0; top: 0; width: 26px; height: 26px;
                pointer-events: none; transform: translate(-200px, -200px); }
            #film-ring { position: fixed; z-index: 2147483646; left: 0; top: 0; width: 44px; height: 44px;
                margin: -22px 0 0 -22px; border-radius: 50%; border: 3px solid #1560bd;
                background: rgba(21, 96, 189, .18); pointer-events: none; opacity: 0; }
            #film-ring.an { animation: film-klick .55s ease-out; }
            @keyframes film-klick { from { opacity: 1; transform: scale(.35); } to { opacity: 0; transform: scale(1.25); } }
            #film-text { position: fixed; z-index: 2147483645; left: 0; right: 0; bottom: 0; height: ${band}px;
                box-sizing: border-box; padding: 0 40px; background: #1c2530; border-top: 3px solid #1560bd;
                color: #fff; pointer-events: none; display: flex; flex-direction: column; justify-content: center;
                align-items: center; text-align: center; font: 600 22px/1.3 Inter, "Segoe UI", Arial, sans-serif; }
            #film-text small { display: block; font-weight: 400; font-size: 16px; opacity: .85; margin-top: 3px; }
            #film-text small:empty { display: none; }`;
        document.head.appendChild(stil);

        const zeiger = document.createElement('div');
        zeiger.id = 'film-zeiger';
        const bild = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        bild.setAttribute('viewBox', '0 0 26 26');
        bild.setAttribute('width', '26');
        bild.setAttribute('height', '26');
        const pfeil = document.createElementNS('http://www.w3.org/2000/svg', 'path');
        pfeil.setAttribute('d', 'M4 2 L4 21 L9.2 16.4 L12.6 24 L15.8 22.6 L12.4 15.2 L19.4 15.2 Z');
        pfeil.setAttribute('fill', '#111');
        pfeil.setAttribute('stroke', '#fff');
        pfeil.setAttribute('stroke-width', '1.6');
        pfeil.setAttribute('stroke-linejoin', 'round');
        bild.appendChild(pfeil);
        zeiger.appendChild(bild);

        const ring = document.createElement('div');
        ring.id = 'film-ring';
        const text = document.createElement('div');
        text.id = 'film-text';
        const haupt = document.createElement('span');
        const neben = document.createElement('small');
        text.append(haupt, neben);
        document.body.append(ring, text, zeiger);

        const zeigerSetzen = (x, y) => { zeiger.style.transform = `translate(${x - 4}px, ${y - 2}px)`; };
        const merken = (name, wert) => {
            try { sessionStorage.setItem(name, JSON.stringify(wert)); } catch { /* dann eben ohne Gedächtnis */ }
        };
        const gemerkt = name => {
            try { return JSON.parse(sessionStorage.getItem(name) || 'null'); } catch { return null; }
        };

        // Das Werkzeug setzt die Zeile über diese Funktion.
        window.filmText = (zeile, nebenzeile) => {
            haupt.textContent = zeile;
            neben.textContent = nebenzeile;
            merken('film-text', [zeile, nebenzeile]);
        };

        const ort = gemerkt('film-zeiger');
        if (ort) zeigerSetzen(ort.x, ort.y);
        const zeilen = gemerkt('film-text');
        if (zeilen) { haupt.textContent = zeilen[0]; neben.textContent = zeilen[1]; }

        document.addEventListener('mousemove', e => {
            zeigerSetzen(e.clientX, e.clientY);
            merken('film-zeiger', { x: e.clientX, y: e.clientY });
        }, true);
        document.addEventListener('mousedown', e => {
            ring.style.left = e.clientX + 'px';
            ring.style.top = e.clientY + 'px';
            ring.classList.remove('an');
            void ring.offsetWidth;
            ring.classList.add('an');
        }, true);
    };
    if (document.body) bauen(); else document.addEventListener('DOMContentLoaded', bauen);
}
