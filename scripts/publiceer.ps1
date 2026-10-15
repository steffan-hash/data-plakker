# publiceer.ps1 - zet een goedgekeurde versie online
#
# Wat:    pas draaien als Steffan tevreden is met de versie die lokaal draait.
#         1. controleert dat er geen bedrijfsdata in de code staat (repo is openbaar)
#         2. pusht de lokale commits naar GitHub
#         3. bouwt + test + maakt de zip (maak-pakket.ps1)
#         4. maakt een GitHub-release; de downloadlink voor collega's wijst
#            daarna vanzelf naar deze versie
# Input:  -Versie, bv. 1.4  (en optioneel -Notitie met wat er nieuw is)
# Output: release v<Versie> op GitHub met data-plakker.zip

param([Parameter(Mandatory)][string]$Versie, [string]$Notitie = '')

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
Push-Location $root
try {
    # 1. Zie LESSONS.md, LESSON 1: echte bedrijfsdata mag nooit in deze openbare repo.
    #    De namen zelf staan in prive\verboden-woorden.txt (één per regel), dat
    #    bewust buiten git blijft - anders zou deze controle ze zelf openbaar maken.
    #    De hele te pushen geschiedenis wordt doorzocht, niet alleen de huidige versie.
    $lijst = Join-Path $root 'prive\verboden-woorden.txt'
    if (-not (Test-Path $lijst)) { throw "Ontbreekt: $lijst (lijst met bedrijfsnamen). Niet gepubliceerd." }
    $patroon = ((Get-Content $lijst) | Where-Object { $_.Trim() } | ForEach-Object { [regex]::Escape($_.Trim()) }) -join '|'
    $nieuw = @(git rev-list '@{u}..HEAD') + 'HEAD'
    $hits = git grep -niE $patroon @nieuw -- .
    if ($hits) { $hits; throw 'Bedrijfsdata gevonden in de code of de nieuwe commits; niet gepubliceerd.' }

    if (git status --porcelain) { throw 'Er zijn nog niet-gecommitte wijzigingen; eerst committen.' }

    # 2-4.
    git push -q
    if ($LASTEXITCODE) { throw 'Pushen mislukt.' }
    & (Join-Path $PSScriptRoot 'maak-pakket.ps1')
    if (-not $Notitie) { $Notitie = 'Pak de zip uit en dubbelklik op installeer.cmd.' }
    gh release create "v$Versie" (Join-Path $root 'dist\data-plakker.zip') --title "data-plakker $Versie" --notes $Notitie
    if ($LASTEXITCODE) { throw 'Release maken mislukt.' }
    Write-Host "Gepubliceerd: v$Versie"
}
finally { Pop-Location }
