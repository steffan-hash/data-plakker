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

## Tussentijdse update 2026-10-15 — gemiste woorden en spaties (lokaal, nog niet gepubliceerd)
**Aanleiding:** Steffans test op een nieuwe WMS-tabel: een lege
locatiecode-cel, "XXL" weg, "UV-C" → "UVC", "T5 40W" → "T540W". Op de
opgeslagen screenshot kwamen alleen de spaties terug; de rest hing af van
waar het kader viel. Nagebootst met 24 licht verschoven kaders: precies
dezelfde fouten, plus soms "UV-C lamp TL 30W" helemaal weg.
**Wat gedaan** (`src/DataPlakker.cs`, `Ocr`):
- `RecoverMissed`: inkt die buiten alle gevonden woorden valt (vlekken van
  letterformaat, per regel samengevoegd) wordt los gelezen; tot 3 rondes,
  kort woord desnoods op 8x. Lijnen vallen af op hun vorm.
- `RestoreSpaces`: een leeg stuk van ≥0,16 letterhoogte binnen een woord →
  rechterdeel los lezen; is dat precies het eind van het woord, dan een
  spatie. Nooit naast een "1" (die heeft vaak extra ruimte). Tekens
  veranderen nooit.
- Herlezen ook voor korte woorden in hoofdletters (`UVC`, `SSE`); was het
  woord hoofdletters, dan blijft het hoofdletters.
- `Isolate` vult de rand met de meest voorkomende kleur in het stuk (gaf
  letters terug op een oranje gemarkeerde rij).
- Twee nieuwe selftests (smalle spatie; gemist woord terugvinden). De
  selftest ving een te lage spatiedrempel bij 11pt-tekst ("Zwem bad").
**Gemeten** (lokaal): 96,0→96,4%, 95,1→96,7%, 96,3→97,0%; op de 24
verschoven kaders valt geen woord meer weg (was: locatiecode, XXL, halve
beschrijving). Tijd ongeveer gelijk (~2,5–3 s voor een grote tabel).
**Nog open:** "50N" blijft "SON" (alle lezingen zeggen SON); in sommige
kaders nog een spatie of streepje mis ("lampT5", "UVC").

## Tussentijdse update 2026-10-15 — kolom als woordenlijst: geprobeerd, niet ingebouwd
Idee: een woord dat 1x in de kolom staat en op ≤2 tekens na lijkt op een
woord dat er ≥2x staat, vervangen. Gemeten op drie WMS-screenshots: 1 cel
slechter, 2 gelijk. Eén goede reparatie (`LIV-C` → `UV-C`), maar ook foute:
`zwart` → `watt`, `panelen` → `paneel`, en haakjes/komma's verdwenen. De
OCR maakt dezelfde fout bovendien consequent (`muembad` 2x, net zo vaak als
`zwembad`), dus een leesfout is uit de tabel alleen niet van een echt ander
woord te onderscheiden. Teruggedraaid; geïnstalleerde versie ongewijzigd.

## Tussentijdse update 2026-10-15 — bij twijfel kiest de kolom (lokaal, nog niet gepubliceerd)
**Aanleiding:** drie korte locatiecodes bleven fout; debug liet een gelijkspel
zien (normale + 6x-lezing `16-24-116-00` tegen 3x + 8x `16-2-A-116-OO`).
**Wat gedaan:** herlezen woorden onthouden al hun lezingen; in
`RepairToColumnPattern` wint per cel de lezing die past op het patroon dat
het vaakst in de kolom voorkomt (daarna O→0 zoals voorheen). Alleen cellen
met één woord, alleen lezingen die de OCR zelf gaf. Nieuwe selftest.
**Gemeten** (drie WMS-screenshots, lokaal): 95,5→96,0%, 94,9→95,1%,
95,6→96,3%, zonder extra tijd. Ook geprobeerd: 10x-lezing erbij (iets
slechter) en alle woorden herlezen (+0,2–0,5 punt maar ~45% trager) — niet
gedaan.
**Status:** geïnstalleerd op Steffans laptop, lokaal gecommit, níet gepusht
en geen release — wacht op Steffans akkoord (zie PROJECT.md → Publiceren).

## Tussentijdse update 2026-10-15 — publiceren pas na akkoord
Op verzoek van Steffan (te veel versies): nieuwe versies worden eerst alleen
lokaal gebouwd, getest en geïnstalleerd en lokaal gecommit. Online zetten
(push + release) pas na zijn akkoord, via nieuw `scripts/publiceer.ps1`
(controleert ook op bedrijfsdata). Vastgelegd in `PROJECT.md` → Publiceren.

## Tussentijdse update 2026-10-15 — woorden met cijfers los herlezen (stemmen)
**Aanleiding:** Windows-OCR gokt codes mede op basis van de buren en las
korte locatiecodes structureel fout (`-A-` als `4`). Pixelanalyse: de cel
die fout ging en een cel die goed ging hebben exact dezelfde pixels voor dat
stuk; alleen de context verschilt.
**Wat gedaan:** elk woord mét een cijfer wordt nog 3x los gelezen (3x/6x/8x,
precies uitgeknipt met lege achtergrond eromheen); de lezing die het vaakst
voorkomt wint (≥2 van 4, geen gelijkspel; hoofd/kleine letters tellen niet
als verschil). Parallel over alle kernen, één OCR-motor per thread (één
motor tegelijk vanuit meerdere threads gaf "OCR mislukt: Error").
**Gemeten** op twee WMS-screenshots (lokaal): 94,5% → 95,5% en 93,3% → 94,9%.
Tijd voor een grote tabel: ~1,3 s → ~1,5–2,5 s.
**Bewust niet:** herlezen van alle woorden. Gemeten: dat brak underscores en
hoofdletters (`UV-C` → `uv-c`) en scoorde slechter (86%).
**Ook vastgesteld:** spaties die verdwijnen (`400x207x122-Frame`) komen uit de
OCR zelf, niet uit de underscore-herkenning.

## Tussentijdse update 2026-10-15 — herkenning op echte WMS-screenshot: 54% → 94,5%
**Meetmethode:** een echte WMS-screenshot van Steffan (47 rijen × 10
kolommen) cel voor cel overgetypt als juiste antwoord, en elke variant
daartegen gescoord. Screenshot + antwoorden staan alleen lokaal (werkdata),
niet in deze openbare repo. Nieuw: `DataPlakker.exe --ocr <afbeelding>`
leest een opgeslagen screenshot uit naar tab-gescheiden tekst.
**Wat hielp:**
- Kolommen: strenge goot (geen enkele rij tekst, ≥0,8 letterhoogte) naast de
  ruime goot → lege kolommen met alleen een kop blijven eigen kolommen.
  Woord hoort bij de kolom waar het begint (linkerrand) → afgekapte tekst
  (`...`) schuift niet meer naar de volgende kolom. 54% → 84,5%.
- Underscores: Windows-OCR laat ze weg; nu teruggezet als er onderaan het gat
  tussen twee woorden een streep staat (niet bij onderstreping). → 94,0%.
- Kolompatroon: codes rechtgetrokken (O→0, I/l→1) als ze dan precies passen
  op een vorm die ≥3× in de kolom voorkomt. → 94,5%.
**Wat níet hielp (gemeten):** 5x vergroten (82,6%), 3x (83,4%), scherp
vergroten (47%), zwart-wit (17–37%), meer contrast (72%), Nederlandse
OCR-taal (80,6%). 4x zacht vergroten + Engels blijft het beste.
**Resterende fouten** zitten in de OCR zelf (bv. `-A-` gelezen als `4` in korte
codes, `UV-C lamp` → `UV-Clamp`, tekst op een oranje gemarkeerde rij wordt
niet gelezen) — niet veilig te repareren zonder te gokken.
**Let op:** Smart App Control blokkeerde op Steffans laptop meerdere keren
een lokaal gebouwde testvariant. Komt het bij `install.ps1` voor, dan
opnieuw draaien (nieuwe build = nieuwe hash).
Selftest: 21 controles (o.a. underscore, onderstreping, lege kolom,
kolompatroon), alle groen.

## Tussentijdse update 2026-10-15 — Smart App Control blokkeerde de .exe bij collega
**Oorzaak (gereproduceerd op Steffans laptop, SAC staat daar AAN):** SAC
blokkeert een onbekende, niet-ondertekende .exe alleen als hij de markering
"gedownload van internet" (Zone.Identifier) heeft. Het oordeel blijft per
hash hangen: eenmaal geblokkeerd = op die pc altijd geblokkeerd, ook na
Unblock-File of een verse kopie; eenmaal zonder markering gestart =
toegestaan. Daarom werkte het bij Steffan altijd (lokaal gebouwd).
**Wat gedaan:** `pakket/installeer.cmd` haalt na het kopiëren de markering
eraf (`Unblock-File`) vóór de eerste start. Getest met een nooit eerder
geziene .exe én installeer.cmd, beide met markering: start zonder blokkade.
LEESMIJ + README: exe niet rechtstreeks uit de zip starten. Release v1.1
(nieuwe build = nieuwe hash, want v1.0 is bij de collega al geblokkeerd).
**Structurele oplossing (niet gedaan):** code signing (bv. Azure Artifact
Signing, betaald) — zelfs dan bouwt SAC-reputatie volgens bronnen niet
altijd direct op.

## Tussentijdse update 2026-10-15 — openbaar gemaakt, README + release
**Wat gedaan:** `README.md` voor collega's (gebruik, installeren via
release-link, zelf bouwen, beperkingen). Vóór openbaar maken, met akkoord
van Steffan, de hele git-geschiedenis herschreven: bedrijfs- en
systeemnamen geneutraliseerd (ERP/WMS), echte artikelcode en
leveranciersnaam uit de testdata vervangen, en alle commits naar het
anonieme GitHub-adres (repo-lokale `user.email` staat daar nu ook op).
Gecontroleerd: 0 sporen in alle versies, selftest groen. Force-push met
expliciete lease op de verwachte oude versie. Release **v1.0** met
`data-plakker.zip`; repo **openbaar** (download zonder inloggen: HTTP 200).
**Bewust geaccepteerd:** oude, losgekoppelde commits zijn bij GitHub
theoretisch nog op exacte SHA op te vragen.
**Let op bij nieuwe versies:** `scripts/maak-pakket.ps1` + een nieuwe
release (`gh release create v1.x dist/data-plakker.zip`); geen
bedrijfsdata in testdata of log zetten nu de repo openbaar is.

## Tussentijdse update 2026-10-15 — pakket voor collega's
**Wat gedaan:** `scripts/maak-pakket.ps1` bouwt+test (via install.ps1) en
maakt `dist/data-plakker.zip` met `DataPlakker.exe`, `pakket/installeer.cmd`
(kopieert naar %LOCALAPPDATA%\data-plakker, Opstarten-snelkoppeling,
geen beheerdersrechten) en `pakket/LEESMIJ.txt`. Getest door de zip uit
te pakken en installeer.cmd te draaien; daarna Steffans eigen installatie
teruggezet en de testkopie verwijderd.
- `install.ps1`: wacht nu tot de oude .exe echt gestopt is (eerste
  pakketpoging faalde doordat de .exe nog vergrendeld was).
**Aandachtspunten bij delen:** Gmail blokkeert .exe (ook in zip) → via
Slack/Drive; niet-ondertekende .exe geeft een SmartScreen-waarschuwing;
bedrijfs-IT kan onbekende programma's blokkeren (niet gecontroleerd).

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
