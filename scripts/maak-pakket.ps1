# maak-pakket.ps1 - maakt een zip om data-plakker aan een collega te geven
#
# Wat:    bouwt + test eerst via install.ps1 (zodat de zip altijd de laatste,
#         geteste versie bevat) en pakt dan DataPlakker.exe, installeer.cmd en
#         LEESMIJ.txt in een zip.
# Input:  niets
# Output: dist\data-plakker.zip  (verstuur via Slack of Drive; Gmail
#         blokkeert .exe-bestanden, ook in een zip)

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot

& (Join-Path $PSScriptRoot 'install.ps1')

$zip = Join-Path $root 'dist\data-plakker.zip'
$null = New-Item -ItemType Directory -Force (Split-Path $zip)
Compress-Archive -Force -DestinationPath $zip -Path `
    (Join-Path $root 'bin\DataPlakker.exe'), (Join-Path $root 'pakket\installeer.cmd'), (Join-Path $root 'pakket\LEESMIJ.txt')
Write-Host "Pakket klaar: $zip"
