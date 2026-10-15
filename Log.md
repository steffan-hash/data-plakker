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
