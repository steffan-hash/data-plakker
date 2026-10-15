# install-sneltoets.ps1 - koppelt data-plakker aan een sneltoets
#
# Wat:    maakt een snelkoppeling in het Startmenu met sneltoets Ctrl+Alt+P.
#         Windows luistert zelf naar die sneltoets; er draait dus niets op
#         de achtergrond. Opnieuw draaien overschrijft de oude snelkoppeling.
# Input:  optioneel -Sneltoets, bv. "Ctrl+Alt+K" als Ctrl+Alt+P al bezet is.
# Output: %APPDATA%\Microsoft\Windows\Start Menu\Programs\data-plakker.lnk
#         Verwijderen = dat bestand weggooien.

param([string]$Sneltoets = 'Ctrl+Alt+P')

$lnk = Join-Path ([Environment]::GetFolderPath('Programs')) 'data-plakker.lnk'
$script = Join-Path $PSScriptRoot 'data-plakker.ps1'

$shell = New-Object -ComObject WScript.Shell
$sc = $shell.CreateShortcut($lnk)
$sc.TargetPath = "$env:SystemRoot\System32\WindowsPowerShell\v1.0\powershell.exe"
$sc.Arguments = "-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File `"$script`""
$sc.WindowStyle = 7          # geminimaliseerd starten, zodat er geen zwart venster in beeld springt
$sc.Hotkey = $Sneltoets
$sc.Description = 'Screenshot naar plakbare tekst/tabel'
$sc.Save()

Write-Host "Snelkoppeling gemaakt: $lnk"
Write-Host "Sneltoets: $Sneltoets  (werkt meteen; anders een keer uit- en inloggen)"
