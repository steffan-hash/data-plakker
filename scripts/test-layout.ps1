# test-layout.ps1 - controleert de OCR + tabelherkenning van layout.ps1
#
# Wat:    1. tabelherkenning op nagemaakte woordposities (zonder OCR)
#         2. echte OCR op een zelf getekende tabel en een stuk lopende tekst
#         3. klembord: beide versies (tekst + HTML) komen er goed op
# Input:  niets
# Output: "OK ..." per geslaagde controle; stopt met een fout bij de eerste
#         mislukte controle. Let op: overschrijft het klembord.

$ErrorActionPreference = 'Stop'
. "$PSScriptRoot\layout.ps1"

function Assert($cond, $msg) { if (-not $cond) { throw "MISLUKT: $msg" }; Write-Host "OK  $msg" }

# Tekent regels tekst op een witte afbeelding; elke regel is een lijst van
# (x-positie, tekst), zodat we kolommen kunnen nabootsen.
function New-TestImage($Lines, [int]$Width = 700) {
    $bmp = New-Object System.Drawing.Bitmap $Width, (40 + 26 * $Lines.Count)
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    $g.Clear([System.Drawing.Color]::White)
    $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::ClearTypeGridFit
    $font = New-Object System.Drawing.Font 'Segoe UI', 11
    for ($i = 0; $i -lt $Lines.Count; $i++) {
        foreach ($cell in $Lines[$i]) { $g.DrawString($cell[1], $font, [System.Drawing.Brushes]::Black, $cell[0], 20 + 26 * $i) }
    }
    $g.Dispose(); $font.Dispose()
    return $bmp
}

# --- 1. Tabelherkenning zonder OCR -----------------------------------------
function W($t, $x, $y) { [pscustomobject]@{ Text = $t; X = $x; Y = $y; W = 10 * $t.Length; H = 14 } }
$words = @(
    (W 'Artikel' 0 0), (W 'Aantal' 200 0),
    (W 'Zwembad' 0 30), (W 'pomp' 75 30), (W '12' 200 30),
    (W 'Filter' 0 60), (W '3' 200 60)
)
$grid = ConvertTo-Grid $words
Assert $grid.IsTable 'nagemaakte tabel wordt als tabel herkend'
Assert ($grid.Rows.Count -eq 3 -and $grid.Rows[0].Length -eq 2) 'nagemaakte tabel heeft 3 rijen x 2 kolommen'
Assert ($grid.Rows[1][0] -eq 'Zwembad pomp' -and $grid.Rows[1][1] -eq '12') 'woorden met gewone spatie blijven in dezelfde cel'
$expected = '```' + "`r`nArtikel       Aantal`r`nZwembad pomp  12`r`nFilter        3`r`n" + '```'
Assert ((Format-PlainText $grid) -eq $expected) 'tabel als uitgelijnde tekst tussen ``` voor chat'

$text = @((W 'Dit' 0 0), (W 'is' 40 0), (W 'tekst' 65 0), (W 'nog' 0 30), (W 'een' 40 30), (W 'regel' 85 30))
$grid = ConvertTo-Grid $text
Assert (-not $grid.IsTable) 'lopende tekst wordt niet als tabel gezien'
Assert ((Format-PlainText $grid) -eq "Dit is tekst`r`nnog een regel") 'lopende tekst blijft per regel behouden'

# --- 2. Echte OCR -------------------------------------------------------------
$bmp = New-TestImage @(
    , @(@(10, 'Artikel'), @(260, 'Leverancier'), @(480, 'Aantal'))
    , @(@(10, 'Zwembadpomp 3000'), @(260, 'Bestway'), @(480, '12'))
    , @(@(10, 'Filterpatroon'), @(260, 'Intex'), @(480, '240'))
    , @(@(10, 'Solarzeil groot'), @(260, 'Aqua Blauw'), @(480, '7'))
)
$grid = ConvertTo-Grid (Get-OcrWords $bmp)
$bmp.Dispose()
Write-Host (Format-PlainText $grid)
Assert $grid.IsTable 'OCR: getekende tabel wordt als tabel herkend'
Assert ($grid.Rows.Count -eq 4 -and $grid.Rows[0].Length -eq 3) "OCR: 4 rijen x 3 kolommen (kreeg $($grid.Rows.Count) x $($grid.Rows[0].Length))"
Assert ($grid.Rows[3][1] -eq 'Aqua Blauw' -and $grid.Rows[2][2] -eq '240') 'OCR: celinhoud klopt'

$bmp = New-TestImage @(
    , @(, @(10, 'De levering van vandaag mist twee dozen filterpatronen.'))
    , @(, @(10, 'Graag een nieuwe zending of een creditnota.'))
)
$grid = ConvertTo-Grid (Get-OcrWords $bmp)
$bmp.Dispose()
Assert (-not $grid.IsTable) 'OCR: lopende tekst wordt niet als tabel gezien'
Assert ((Format-PlainText $grid) -like 'De levering van vandaag mist twee dozen*') 'OCR: tekst klopt'

# --- 3. Klembord ----------------------------------------------------------------
$grid = [pscustomobject]@{ IsTable = $true; Rows = @(, @('a & b', 'c')) }
Set-ClipboardTextAndHtml (Format-PlainText $grid) (Format-Html $grid)
$back = [System.Windows.Forms.Clipboard]::GetDataObject()
Assert ($back.GetData([System.Windows.Forms.DataFormats]::UnicodeText) -like '*a & b  c*') 'klembord: platte tekst staat erop'
$html = $back.GetData([System.Windows.Forms.DataFormats]::Html)
$start = [int]([regex]::Match($html, 'StartFragment:(\d+)').Groups[1].Value)
$end = [int]([regex]::Match($html, 'EndFragment:(\d+)').Groups[1].Value)
$frag = [System.Text.Encoding]::UTF8.GetString([System.Text.Encoding]::UTF8.GetBytes($html), $start, $end - $start)
Assert ($frag -like '<table*a &amp; b*</table>') 'klembord: HTML-fragment staat op de juiste byte-posities'

Write-Host "`nAlle controles geslaagd."
