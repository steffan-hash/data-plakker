# install.ps1 - bouwt data-plakker en laat het bij inloggen starten
#
# Wat:    1. stopt een draaiende data-plakker (anders kan de .exe niet
#            overschreven worden)
#         2. compileert src\DataPlakker.cs naar bin\DataPlakker.exe met de
#            C#-compiler die in Windows zit (niets te installeren)
#         3. draait de controles (--selftest); stopt als er een mislukt
#         4. haalt de oude PowerShell-snelkoppeling met sneltoets weg
#         5. zet een snelkoppeling in de map Opstarten en start het programma
#         Na elke codewijziging opnieuw draaien.
# Input:  niets
# Output: bin\DataPlakker.exe, draaiend, met icoon in het systeemvak.
#         Verwijderen: icoon > Afsluiten, en data-plakker.lnk uit
#         shell:startup weggooien.

$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot
$exe = Join-Path $root 'bin\DataPlakker.exe'

# 1. Draaiende versie stoppen. Kan zonder risico: het programma houdt geen
#    bestanden of database open.
Get-Process DataPlakker -ErrorAction SilentlyContinue | Stop-Process
Start-Sleep -Milliseconds 300

# 2. Compileren. De Windows-OCR wordt aangesproken via de beschrijvingen
#    (.winmd) die in System32\WinMetadata staan.
$fw = "$env:SystemRoot\Microsoft.NET\Framework64\v4.0.30319"
$wm = "$env:SystemRoot\System32\WinMetadata"
$null = New-Item -ItemType Directory -Force (Split-Path $exe)
& "$fw\csc.exe" -nologo -optimize -codepage:65001 -target:winexe "-out:$exe" `
    -r:System.Drawing.dll -r:System.Windows.Forms.dll `
    "-r:$fw\System.Runtime.dll" "-r:$fw\System.Runtime.WindowsRuntime.dll" `
    "-r:$wm\Windows.Foundation.winmd" "-r:$wm\Windows.Media.winmd" `
    "-r:$wm\Windows.Graphics.winmd" "-r:$wm\Windows.Security.winmd" "-r:$wm\Windows.Storage.winmd" `
    (Join-Path $root 'src\DataPlakker.cs')
if ($LASTEXITCODE -ne 0) { throw 'Compileren mislukt.' }

# 3. Controles draaien (| Out-Host zodat PowerShell op de .exe wacht).
& $exe --selftest | Out-Host
if ($LASTEXITCODE -ne 0) { throw 'Controles mislukt; niet geinstalleerd.' }

# 4. De oude PowerShell-versie claimde Ctrl+Alt+P via een Startmenu-snelkoppeling.
$old = Join-Path ([Environment]::GetFolderPath('Programs')) 'data-plakker.lnk'
if (Test-Path $old) { Remove-Item $old; Write-Host "Oude snelkoppeling verwijderd: $old" }

# 5. Starten bij inloggen, en nu meteen starten.
$lnk = Join-Path ([Environment]::GetFolderPath('Startup')) 'data-plakker.lnk'
$sc = (New-Object -ComObject WScript.Shell).CreateShortcut($lnk)
$sc.TargetPath = $exe
$sc.Description = 'Screenshot naar plakbare tekst/tabel (Ctrl+Alt+P)'
$sc.Save()
Start-Process $exe
Write-Host "Geinstalleerd en gestart. Sneltoets: Ctrl+Alt+P"
