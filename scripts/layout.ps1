# layout.ps1 - OCR en tabelherkenning voor data-plakker
#
# Wat:    functies die van een afbeelding plakbare tekst maken:
#         1. Get-OcrWords      afbeelding -> losse woorden met positie (Windows-OCR, lokaal)
#         2. ConvertTo-Grid    woorden -> rijen en kolommen (tabel) of gewone tekstregels
#         3. Format-PlainText  grid -> platte tekst (voor chat)
#         4. Format-Html       grid -> HTML-tabel (voor Gmail/mail)
#         5. Set-ClipboardTextAndHtml  zet beide versies tegelijk op het klembord
# Input:  een System.Drawing.Bitmap (het geselecteerde stuk scherm)
# Output: strings voor het klembord. Dit bestand doet zelf niets; het wordt
#         ingeladen ("dot-sourced") door data-plakker.ps1 en test-layout.ps1.
#
# Let op: bewust alleen ASCII in dit bestand. Windows PowerShell 5.1 leest
# scripts zonder BOM als ANSI, dan worden letters als e-trema verminkt.

Add-Type -AssemblyName System.Drawing
Add-Type -AssemblyName System.Windows.Forms
Add-Type -AssemblyName System.Runtime.WindowsRuntime

# Windows-OCR is een "WinRT"-onderdeel van Windows zelf; deze regels maken de
# types bekend in PowerShell. Er wordt niets gedownload of geinstalleerd.
$null = [Windows.Media.Ocr.OcrEngine, Windows.Foundation, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.BitmapDecoder, Windows.Graphics, ContentType = WindowsRuntime]
$null = [Windows.Graphics.Imaging.SoftwareBitmap, Windows.Graphics, ContentType = WindowsRuntime]

# WinRT-functies zijn asynchroon ("geef later antwoord"). PowerShell 5.1 kan
# daar niet direct op wachten, dus zetten we ze om naar een .NET-Task en
# wachten we daarop.
$script:AsTaskGeneric = [System.WindowsRuntimeSystemExtensions].GetMethods() |
    Where-Object { $_.Name -eq 'AsTask' -and $_.GetParameters().Count -eq 1 -and
                   $_.GetParameters()[0].ParameterType.Name -eq 'IAsyncOperation`1' } |
    Select-Object -First 1

function Wait-WinRt($Operation, [Type]$ResultType) {
    $task = $script:AsTaskGeneric.MakeGenericMethod($ResultType).Invoke($null, @($Operation))
    $null = $task.Wait(-1)
    return $task.Result
}

function Get-OcrWords([System.Drawing.Bitmap]$Bitmap) {
    # Kleine schermtekst wordt slecht herkend; vergroten helpt Windows-OCR
    # flink. Gemeten op 8pt-tabeltekst: 2x gaf 10 foute cellen, 4x nog 3.
    # Zo groot mogelijk tot 4x, binnen de maximale OCR-afmeting.
    $max = [Windows.Media.Ocr.OcrEngine]::MaxImageDimension
    $scale = [math]::Max(1, [math]::Min(4, [math]::Floor($max / [math]::Max($Bitmap.Width, $Bitmap.Height))))
    $work = New-Object System.Drawing.Bitmap ($Bitmap.Width * $scale), ($Bitmap.Height * $scale)
    $g = [System.Drawing.Graphics]::FromImage($work)
    $g.InterpolationMode = [System.Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
    $g.DrawImage($Bitmap, 0, 0, $work.Width, $work.Height)
    $g.Dispose()

    # Afbeelding via het geheugen (niet via een bestand op schijf, want het
    # kan werkdata zijn) omzetten naar het formaat dat de OCR verwacht.
    $ms = New-Object System.IO.MemoryStream
    $work.Save($ms, [System.Drawing.Imaging.ImageFormat]::Png)
    $work.Dispose()
    $ms.Position = 0
    $stream = [System.IO.WindowsRuntimeStreamExtensions]::AsRandomAccessStream($ms)
    $decoder = Wait-WinRt ([Windows.Graphics.Imaging.BitmapDecoder]::CreateAsync($stream)) ([Windows.Graphics.Imaging.BitmapDecoder])
    $soft = Wait-WinRt ($decoder.GetSoftwareBitmapAsync()) ([Windows.Graphics.Imaging.SoftwareBitmap])

    # OCR in de talen van het Windows-profiel (bij Steffan: Nederlands/Engels).
    $engine = [Windows.Media.Ocr.OcrEngine]::TryCreateFromUserProfileLanguages()
    if (-not $engine) { throw 'Geen Windows-OCR-taal beschikbaar.' }
    $result = Wait-WinRt ($engine.RecognizeAsync($soft)) ([Windows.Media.Ocr.OcrResult])
    $ms.Dispose()

    # Alleen losse woorden + positie bewaren; de regels die Windows zelf maakt
    # negeren we, want die plakken tabelcellen naast elkaar aan elkaar.
    foreach ($line in $result.Lines) {
        foreach ($w in $line.Words) {
            $r = $w.BoundingRect
            [pscustomobject]@{ Text = $w.Text; X = $r.X / $scale; Y = $r.Y / $scale; W = $r.Width / $scale; H = $r.Height / $scale }
        }
    }
}

function Repair-OcrSpacing([string]$Text) {
    # Herstelt alleen spaties die de OCR in datums verkeerd zet; verandert
    # nooit een cijfer of letter (een gemiste ":" raden we bewust niet).
    # "15- 10-2026" -> "15-10-2026"
    $Text = $Text -replace '\b(\d{1,2})- (\d{1,2}-\d{4})\b', '$1-$2'
    # "15-10-202611:14" -> "15-10-2026 11:14"
    $Text = $Text -replace '\b(\d{1,2}-\d{1,2}-\d{4})(\d{1,2}:\d{2})\b', '$1 $2'
    return $Text
}

function ConvertTo-Grid($Words) {
    $Words = @($Words)
    if ($Words.Count -eq 0) { return [pscustomobject]@{ IsTable = $false; Rows = @() } }

    # Mediaan woordhoogte = "letterhoogte"; alle drempels hieronder zijn daar
    # een veelvoud van, zodat het op elke zoom/schermgrootte hetzelfde werkt.
    $h = ($Words | ForEach-Object { $_.H } | Sort-Object)[[int][math]::Floor($Words.Count / 2)]

    # Stap 1: rijen. Woorden waarvan het midden op dezelfde hoogte ligt
    # (binnen een halve letterhoogte) horen bij dezelfde rij.
    $rows = New-Object System.Collections.ArrayList
    foreach ($w in ($Words | Sort-Object { $_.Y + $_.H / 2 })) {
        $cy = $w.Y + $w.H / 2
        $last = if ($rows.Count) { $rows[$rows.Count - 1] } else { $null }
        if ($last -and [math]::Abs($cy - $last.Cy) -le $h * 0.5) {
            $null = $last.Words.Add($w)
            $last.Cy = ($last.Cy * ($last.Words.Count - 1) + $cy) / $last.Words.Count
        } else {
            $list = New-Object System.Collections.ArrayList
            $null = $list.Add($w)
            $null = $rows.Add([pscustomobject]@{ Cy = $cy; Words = $list })
        }
    }

    # Stap 2: kolommen. Een kolomgrens is een verticale "goot": een strook van
    # minstens 1 letterhoogte breed waar in (bijna) geen enkele rij tekst staat.
    # Gewone spaties tussen woorden zijn veel smaller, dus lopende tekst wordt
    # nooit per ongeluk in kolommen geknipt.
    # ponytail: 1 op de 5 rijen mag over een goot heen lopen (bv. een titel boven
    # de tabel); kleine tabellen met een brede titelregel worden dan als tekst
    # gezien. Upgraden als dat in de praktijk vaak misgaat.
    $minX = [int][math]::Floor(($Words | Measure-Object X -Minimum).Minimum)
    $maxX = [int][math]::Ceiling(($Words | ForEach-Object { $_.X + $_.W } | Measure-Object -Maximum).Maximum)
    $cover = New-Object int[] ($maxX - $minX + 1)
    foreach ($row in $rows) {
        $hit = New-Object bool[] $cover.Length
        foreach ($w in $row.Words) {
            for ($x = [int]($w.X - $minX); $x -lt [int]($w.X + $w.W - $minX) -and $x -lt $hit.Length; $x++) { $hit[$x] = $true }
        }
        for ($x = 0; $x -lt $hit.Length; $x++) { if ($hit[$x]) { $cover[$x]++ } }
    }
    $allowed = [math]::Floor($rows.Count / 5)
    $gutters = New-Object System.Collections.ArrayList   # x-midden van elke goot
    $start = -1
    for ($x = 0; $x -le $cover.Length; $x++) {
        $free = $x -lt $cover.Length -and $cover[$x] -le $allowed
        if ($free -and $start -lt 0) { $start = $x }
        elseif (-not $free -and $start -ge 0) {
            if ($x - $start -ge $h -and $start -gt 0 -and $x -lt $cover.Length) { $null = $gutters.Add($minX + ($start + $x) / 2) }
            $start = -1
        }
    }

    # Stap 3: elk woord in de kolom van zijn midden zetten, per cel de woorden
    # van links naar rechts aan elkaar.
    $nCols = $gutters.Count + 1
    $grid = foreach ($row in $rows) {
        $cells = New-Object 'string[]' $nCols
        foreach ($w in ($row.Words | Sort-Object X)) {
            $mid = $w.X + $w.W / 2
            $col = @($gutters | Where-Object { $_ -lt $mid }).Count
            $cells[$col] = if ($cells[$col]) { $cells[$col] + ' ' + $w.Text } else { $w.Text }
        }
        , @($cells | ForEach-Object { Repair-OcrSpacing $_ })
    }

    # Tabel = minstens 2 kolommen en minstens 2 rijen met meer dan 1 gevulde cel.
    $multi = @($grid | Where-Object { @($_ | Where-Object { $_ }).Count -ge 2 }).Count
    $isTable = $nCols -ge 2 -and $multi -ge 2
    if (-not $isTable) {
        # Geen tabel: elke rij is gewoon een regel tekst.
        $grid = foreach ($cells in $grid) { , @((@($cells | Where-Object { $_ }) -join ' ')) }
    }
    return [pscustomobject]@{ IsTable = $isTable; Rows = @($grid) }
}

function Format-PlainText($Grid) {
    if (-not $Grid.IsTable) { return (@($Grid.Rows | ForEach-Object { $_[0] }) -join "`r`n") }

    # Kolommen uitlijnen met spaties. Tussen ``` zodat Teams, Slack en WhatsApp
    # het in een vaste-breedte-letter tonen; anders loopt de uitlijning scheef.
    $n = $Grid.Rows[0].Length
    $widths = for ($c = 0; $c -lt $n; $c++) { ($Grid.Rows | ForEach-Object { "$($_[$c])".Length } | Measure-Object -Maximum).Maximum }
    $lines = foreach ($cells in $Grid.Rows) {
        (@(for ($c = 0; $c -lt $n; $c++) { "$($cells[$c])".PadRight($widths[$c]) }) -join '  ').TrimEnd()
    }
    return '```' + "`r`n" + ($lines -join "`r`n") + "`r`n" + '```'
}

function Format-Html($Grid) {
    $enc = { param($s) [System.Net.WebUtility]::HtmlEncode("$s") }
    if (-not $Grid.IsTable) { return (@($Grid.Rows | ForEach-Object { & $enc $_[0] }) -join '<br>') }
    $td = 'style="border:1px solid #999;padding:2px 6px"'
    $trs = foreach ($cells in $Grid.Rows) { '<tr>' + (@($cells | ForEach-Object { "<td $td>$(& $enc $_)</td>" }) -join '') + '</tr>' }
    return '<table style="border-collapse:collapse">' + ($trs -join '') + '</table>'
}

function Set-ClipboardTextAndHtml([string]$Plain, [string]$Html) {
    # Het klembord krijgt twee versies tegelijk; het programma waarin je plakt
    # kiest zelf: Gmail/Excel nemen de HTML-tabel, chat-apps de platte tekst.
    # HTML op het klembord vraagt een vaste kop met byte-posities (CF_HTML).
    $pre = '<html><body><!--StartFragment-->'
    $post = '<!--EndFragment--></body></html>'
    $header = "Version:0.9`r`nStartHTML:{0:D10}`r`nEndHTML:{1:D10}`r`nStartFragment:{2:D10}`r`nEndFragment:{3:D10}`r`n"
    $utf8 = [System.Text.Encoding]::UTF8
    $hLen = $utf8.GetByteCount(($header -f 0, 0, 0, 0))
    $startFrag = $hLen + $utf8.GetByteCount($pre)
    $endFrag = $startFrag + $utf8.GetByteCount($Html)
    $endHtml = $endFrag + $utf8.GetByteCount($post)
    $cfHtml = ($header -f $hLen, $endHtml, $startFrag, $endFrag) + $pre + $Html + $post

    $data = New-Object System.Windows.Forms.DataObject
    $data.SetData([System.Windows.Forms.DataFormats]::UnicodeText, $Plain)
    $data.SetData([System.Windows.Forms.DataFormats]::Html, $cfHtml)
    [System.Windows.Forms.Clipboard]::SetDataObject($data, $true)
}
