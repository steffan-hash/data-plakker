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

## Tussentijdse update 2026-10-15 — van PowerShell naar .exe op de achtergrond
**Wat gedaan:** op verzoek van Steffan (puur performance) herschreven als
één C#-bestand `src/DataPlakker.cs`, gecompileerd met de `csc.exe` die in
Windows zit (C# 5, geen ontwikkelpakket). Draait onzichtbaar met icoon in
het systeemvak, luistert via `RegisterHotKey` naar Ctrl+Alt+P.
- Gemeten geheugen in rust: ~22 MB privé (PowerShell-achtergrond zou
  ~67 MB zijn, waarvan 56 MB PowerShell zelf).
- Zelfde logica als de PowerShell-versie (kolomgoten, 4x vergroten,
  spatieherstel, CF_HTML-klembord). Tests zitten in de .exe:
  `--selftest`, 16 controles, alle groen.
- OCR-koppeling: de standaard WinRT-bridge vraagt een SDK die hier niet
  staat; omweg = ruwe pixels via `SoftwareBitmap.CreateCopyFromBuffer` en
  zelf wachten op `IAsyncOperation` (ook sneller: geen PNG-tussenstap).
- `scripts/install.ps1`: stopt oude versie, compileert naar `bin/`, draait
  selftest, verwijdert de oude Startmenu-snelkoppeling, zet snelkoppeling
  in Opstarten en start de .exe.
- **Verwijderd:** `scripts/data-plakker.ps1`, `layout.ps1`,
  `test-layout.ps1`, `install-sneltoets.ps1` (vervangen; staan in git-historie).
- `PROJECT.md`: alleen Status (techkeuze) en commando's bijgewerkt, op
  basis van Steffans keuze voor de .exe.
**Valkuil (gebeurd):** Verkenner hield Ctrl+Alt+P vast van de verwijderde
oude snelkoppeling → .exe meldde "al in gebruik". Opgelost door Verkenner
te herstarten (met akkoord Steffan).
**Nog open:** selectiekader met de echte sneltoets nog door Steffan te
proberen; geheugen na de eerste OCR niet gemeten; eigen icoon (nu het
standaard Windows-programma-icoon).

## Tussentijdse update 2026-10-15 — selectiekader sneller in beeld
**Wat gedaan:** `data-plakker.ps1` gemeten van processtart tot kader in
beeld: ~0,5–0,65 s → ~0,37 s. Twee oorzaken weggehaald:
- DPI-instelling via Add-Type compileerde elke start C# (~0,2 s); nu een
  directe aanroep via reflection. Valkuil: zonder `PreserveSig` gaf die
  aanroep stil `False` en werkte de DPI-instelling niet — gecontroleerd
  dat hij nu wél slaagt (awareness 0 → 2).
- OCR-onderdelen (`layout.ps1`) pas laden ná het selecteren.
- Volledige flow nagebootst zonder muis (kader → OCR → klembord): werkt.
**Nog open:** de resterende ~0,37 s is vooral PowerShell zelf opstarten.
Bijna direct kan alleen met een proces dat op de achtergrond blijft
draaien en zelf naar de sneltoets luistert — niet gedaan, op Steffans
verzoek op te pakken.

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
