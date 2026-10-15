# Project: data-plakker

## Overzicht
Screenshot-tool die tekst en tabellen uit een stuk scherm haalt en ze als
plakbare tekst op het klembord zet. Aanleiding: Steffan deelt vaak tekst of
tabellen als screenshot, maar de ontvanger kan met een plaatje niets
(niet kopiëren, niet doorrekenen, niet zoeken).

## Kerndoel
Sneltoets → gebied op het scherm kiezen → resultaat staat direct op het
klembord, zonder venster tussendoor. Plakken in:
- **Gmail/mail** → een tabel komt als echte tabel in de mail.
- **Chat (Teams/Slack/WhatsApp)** → platte tekst; een tabel als uitgelijnde
  kolommen.

## Randvoorwaarden
- **Alles lokaal.** Screenshots bevatten werkdata en mogen de laptop niet
  verlaten — dus geen cloud-AI/OCR-dienst. Herkenning via de ingebouwde
  Windows-OCR (Nederlands + Engels).
- Proof of concept: alleen Steffans werklaptop (Windows 11).

## Bewust niet (nu)
- Bestaande afbeeldingen omzetten (alleen live scherm-selectie).
- Voorbeeldvenster/corrigeren vóór kopiëren.
- Plakken in Excel/Sheets of ERP/WMS als doel (werkt mogelijk
  vanzelf, maar wordt niet op getest).

## Later (als de PoC werkt)
- Installer zodat collega's het ook kunnen gebruiken.

## Status
Techkeuze: een klein C#-programma (`.exe`, ~20 MB geheugen) dat op de
achtergrond op de sneltoets wacht, met Windows-OCR. Gecompileerd met de
compiler die in Windows zit — niets te installeren. (Eerste versie was
PowerShell; vervangen op 2026-10-15 voor directe reactie op de sneltoets.)
Zie `Log.md` voor wat er gebouwd is.

## Belangrijkste commando's
```
# Bouwen + testen + installeren (start bij inloggen); na elke codewijziging
powershell -ExecutionPolicy Bypass -File scripts\install.ps1
# Alleen de controles draaien
bin\DataPlakker.exe --selftest | Out-Host
# Goedgekeurde versie online zetten (push + zip + GitHub-release)
powershell -ExecutionPolicy Bypass -File scripts\publiceer.ps1 -Versie 1.4
```

## Publiceren (afwijking van CLAUDE.md, besloten 2026-10-15)
De repo is openbaar en collega's downloaden de laatste release. Daarom, in
dit project, in plaats van "commit + push aan het eind van elke sessie":
- Tijdens het werken: bouwen, testen en installeren op Steffans laptop
  (`install.ps1`), en **alleen lokaal committen**. Niet pushen, geen release.
- Pas als Steffan zegt dat hij tevreden is: `publiceer.ps1` (push + release).
- Werkt Steffan daarna op een andere machine verder, dan eerst publiceren of
  op zijn verzoek pushen zonder release; anders mist die machine de commits.

## Context die niet vanzelfsprekend is
Tabellen komen van overal: ERP, WMS, Excel, mail, websites. De
tabelherkenning mag dus niet uitgaan van rasterlijnen of één vaste opmaak.
