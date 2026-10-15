# data-plakker

Maak van een stukje scherm **plakbare tekst**, ook tabellen. Geen screenshots
meer delen waar de ontvanger niets mee kan: druk een sneltoets, trek een kader,
en plak het resultaat als echte tabel in je mail of als nette tekst in je chat.

## Hoe het werkt

1. Druk **Ctrl+Alt+P**.
2. Sleep een kader om de tekst of tabel (Esc = annuleren).
3. Geluidje = het staat op het klembord. Plakken:
   - **Gmail / mail / Excel**: gewoon Ctrl+V, je krijgt een echte tabel.
   - **Slack**: Ctrl+Shift+V en kies *Apply markdown*, dan blijven de kolommen
     uitgelijnd in een codeblok.

**Alles blijft op je eigen computer.** De tekstherkenning is de OCR die in
Windows zelf zit; er wordt niets verstuurd of opgeslagen.

## Installeren

Download [`data-plakker.zip`](https://github.com/steffan-hash/data-plakker/releases/latest/download/data-plakker.zip), pak hem uit en dubbelklik op `installeer.cmd`.
Geen beheerdersrechten nodig. Het programma start daarna vanzelf bij inloggen
en staat als icoontje rechtsonder bij de klok (rechtsklik → Afsluiten).

Het programma is niet digitaal ondertekend; Windows kan daarom waarschuwen
("Windows heeft uw pc beschermd" → *Meer info* → *Toch uitvoeren*).

Vereist: Windows 10 (1703+) of 11.

## Zelf bouwen

Er is geen ontwikkelomgeving nodig. De C#-compiler die in Windows zit is
genoeg:

```powershell
powershell -ExecutionPolicy Bypass -File scripts\install.ps1      # bouwen, testen, installeren
powershell -ExecutionPolicy Bypass -File scripts\maak-pakket.ps1  # zip maken voor anderen
bin\DataPlakker.exe --selftest | Out-Host                         # alleen de controles
```

Alle code staat in `src/DataPlakker.cs`.

## Beperkingen

- Heel kleine tekst wordt soms verkeerd gelezen (bv. een tijd zonder `:`).
  De tool raadt bewust niet, dus lees bij cijfers even mee, of zoom eerst in.
- Een kleine tabel (< 5 rijen) met een brede titel erboven wordt als gewone
  tekst gezien.
