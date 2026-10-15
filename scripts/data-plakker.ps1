# data-plakker.ps1 - screenshot -> plakbare tekst/tabel op het klembord
#
# Wat:    bevriest het scherm, laat je met de muis een gebied selecteren,
#         leest dat gebied lokaal uit met Windows-OCR en zet het resultaat op
#         het klembord (HTML-tabel voor mail, uitgelijnde tekst voor chat).
#         Esc of rechtermuisknop = annuleren.
# Input:  niets; wordt gestart via de sneltoets (zie install-sneltoets.ps1).
# Output: klembord gevuld + een kort geluidje. Niets wordt opgeslagen of
#         verstuurd; de schermafbeelding bestaat alleen in het geheugen.

$ErrorActionPreference = 'Stop'

# Zonder dit ziet het script op een geschaald scherm (bv. 125%) een kleinere,
# verschoven versie van het scherm en klopt de selectie niet met de pixels.
Add-Type -Namespace DataPlakker -Name Dpi -MemberDefinition @'
[DllImport("user32.dll")] public static extern bool SetProcessDpiAwarenessContext(System.IntPtr value);
[DllImport("user32.dll")] public static extern bool SetProcessDPIAware();
'@
if (-not [DataPlakker.Dpi]::SetProcessDpiAwarenessContext([IntPtr]-4)) { $null = [DataPlakker.Dpi]::SetProcessDPIAware() }

. "$PSScriptRoot\layout.ps1"

try {
    # Een foto van alle schermen samen maken; die tonen we als "bevroren"
    # achtergrond, zodat menu's/tooltips niet verdwijnen tijdens het selecteren.
    $vs = [System.Windows.Forms.SystemInformation]::VirtualScreen
    $shot = New-Object System.Drawing.Bitmap $vs.Width, $vs.Height
    $g = [System.Drawing.Graphics]::FromImage($shot)
    $g.CopyFromScreen($vs.Left, $vs.Top, 0, 0, $shot.Size)
    $g.Dispose()

    # Schermvullend venster zonder rand met die foto erin.
    $form = New-Object System.Windows.Forms.Form
    $form.FormBorderStyle = 'None'
    $form.StartPosition = 'Manual'
    $form.Bounds = $vs
    $form.TopMost = $true
    $form.ShowInTaskbar = $false
    $form.Cursor = [System.Windows.Forms.Cursors]::Cross
    $form.BackgroundImage = $shot
    $form.GetType().GetProperty('DoubleBuffered', [Reflection.BindingFlags]'NonPublic,Instance').SetValue($form, $true, $null)

    $state = @{ Start = $null; Rect = [System.Drawing.Rectangle]::Empty; Done = $false }
    $dim = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(90, 0, 0, 0))
    $pen = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(255, 245, 190, 0)), 2

    # Selectie bijhouden met de muis: indrukken = beginpunt, slepen = kader,
    # loslaten = klaar.
    $form.Add_MouseDown({
        if ($_.Button -eq 'Right') { $form.Close(); return }
        $state.Start = $_.Location
    })
    $form.Add_MouseMove({
        if (-not $state.Start) { return }
        $p = $_.Location; $s = $state.Start
        $state.Rect = [System.Drawing.Rectangle]::FromLTRB([math]::Min($s.X, $p.X), [math]::Min($s.Y, $p.Y), [math]::Max($s.X, $p.X), [math]::Max($s.Y, $p.Y))
        $form.Invalidate()
    })
    $form.Add_MouseUp({
        if ($state.Start -and $state.Rect.Width -gt 4 -and $state.Rect.Height -gt 4) { $state.Done = $true }
        $form.Close()
    })
    $form.Add_KeyDown({ if ($_.KeyCode -eq 'Escape') { $form.Close() } })

    # Alles buiten de selectie iets donkerder, de selectie zelf met een geel kader.
    $form.Add_Paint({
        $gr = $_.Graphics
        $region = New-Object System.Drawing.Region ([System.Drawing.Rectangle]::new(0, 0, $form.Width, $form.Height))
        if (-not $state.Rect.IsEmpty) { $region.Exclude($state.Rect) }
        $gr.FillRegion($dim, $region)
        $region.Dispose()
        if (-not $state.Rect.IsEmpty) { $gr.DrawRectangle($pen, $state.Rect) }
    })
    $form.Add_Shown({ $form.Activate() })
    [void]$form.ShowDialog()
    $form.Dispose()

    if (-not $state.Done) { return }

    # Alleen het geselecteerde stuk uit de foto knippen en uitlezen.
    $crop = $shot.Clone($state.Rect, $shot.PixelFormat)
    $shot.Dispose()
    $grid = ConvertTo-Grid (Get-OcrWords $crop)
    $crop.Dispose()

    if ($grid.Rows.Count -eq 0) {
        [System.Media.SystemSounds]::Hand.Play()
        [void][System.Windows.Forms.MessageBox]::Show('Geen tekst gevonden in de selectie.', 'data-plakker')
        return
    }
    Set-ClipboardTextAndHtml (Format-PlainText $grid) (Format-Html $grid)
    [System.Media.SystemSounds]::Asterisk.Play()
}
catch {
    # Sneltoets-start heeft geen zichtbaar venster, dus fouten tonen we zelf.
    [void][System.Windows.Forms.MessageBox]::Show("Er ging iets mis:`n$($_.Exception.Message)", 'data-plakker')
}
