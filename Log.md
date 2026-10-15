# Log

Dit bestand wordt door Claude bijgehouden, niet handmatig door de product
owner. Nieuwste sessie bovenaan. Zie CLAUDE.md → "Sessie einde" voor het
format.

<!--
Voorbeeld van een entry:

## Sessie 2026-08-18
**Status:** [één regel: waar staat het project]
**Wat gedaan:** [wat er echt gebouwd/aangepast is, met bestandsnamen]
**Nog open:** [wat de product owner moet weten of beslissen]
**Volgende stap:** [wat er logisch als eerste opgepakt wordt]
-->

## Tussentijdse update 2026-10-15 — OCR nauwkeuriger, Slack-werkwijze
**Wat gedaan:**
- `layout.ps1`: selectie wordt nu tot 4x vergroot vóór de OCR (was 2x).
  Gemeten op een nagemaakte ERP-achtige tabel: 8pt van 10 naar 3 foute
  cellen, 9pt van 3 naar 0, 10pt van 1 naar 0.
- Nieuw `Repair-OcrSpacing`: herstelt alleen spaties in datums
  (`15- 10-2026`, `15-10-202611:14`); verandert nooit cijfers of letters.
  3 extra controles in `test-layout.ps1`, alles groen.
- Slack (web) getest door Steffan: Ctrl+V plakt de HTML-versie als één
  lange regel; **Ctrl+Shift+V** + Slacks vraag "Apply markdown" geeft een
  perfecte tabel als codeblok. Die vraag komt van Slack zelf en is niet
  vanuit data-plakker over te slaan (mogelijk via Slack-instelling
  "Format messages with markup", nog niet bevestigd). Echte Markdown-tabellen
  rendert Slack alleen in canvassen, niet in berichten.
**Nog open:** een gemiste `:` in tijden (`1031`) en losse leesfouten
(`IC31`) bij kleine tekst worden bewust niet geraden. Eventueel een aparte
chat-sneltoets (alleen tekst op klembord, zodat gewone Ctrl+V werkt).

## Sessie 2026-10-15
**Status:** Eerste werkende versie gebouwd en automatisch getest; nog niet
door Steffan op een echt scherm geprobeerd.

**Wat gedaan:**
- `PROJECT.md` ingevuld na gesprek met Steffan (akkoord): sneltoets →
  selectie → klembord, voor mail + chat, alles lokaal (werkdata), PoC alleen
  op de werklaptop, installer voor collega's later.
- Techkeuze: PowerShell + de OCR die in Windows zelf zit — niets te
  installeren, er gaat niets van de laptop af.
- `scripts/layout.ps1`: OCR (afbeelding 2x vergroot, via het geheugen, geen
  bestand op schijf) en tabelherkenning: rijen op hoogte, kolommen via
  verticale "goten" van minstens 1 letterhoogte zonder tekst. Klembord krijgt
  tegelijk een HTML-tabel (Gmail/Excel) en uitgelijnde tekst tussen ```
  (Teams/Slack/WhatsApp tonen dat in vaste-breedte-letters).
- `scripts/data-plakker.ps1`: bevriest het scherm, geel selectiekader,
  Esc/rechtermuisknop annuleert, geluidje als het klaar is.
- `scripts/install-sneltoets.ps1`: snelkoppeling in het Startmenu met
  Ctrl+Alt+P (geïnstalleerd op deze laptop).
- `scripts/test-layout.ps1`: 13 controles, waaronder echte OCR op een
  getekende tabel en tekst, en de byte-posities van de HTML op het klembord.
  Alles groen.

**Nog open:**
- Selectiekader/sneltoets niet automatisch getest (vraagt muisbediening) —
  Steffan test dit zelf, ook op een geschaald of tweede scherm.
- Bekende beperking: een kleine tabel (< 5 rijen) met een brede titelregel
  erboven wordt als gewone tekst gezien (`ponytail:`-notitie in
  `layout.ps1`).
- Na de sneltoets duurt het ±1 seconde voordat het kader verschijnt
  (PowerShell opstarten).

**Volgende stap:** Steffan probeert het op echte schermen (ERP, WMS,
Excel, mail); op basis daarvan de tabelherkenning bijstellen.
