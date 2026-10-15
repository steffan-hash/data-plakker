@echo off
rem installeer.cmd - installeert data-plakker voor de huidige gebruiker
rem
rem Wat:    kopieert DataPlakker.exe naar %LOCALAPPDATA%\data-plakker, laat
rem         hem starten bij inloggen (map Opstarten) en start hem meteen.
rem         Geen beheerdersrechten nodig. Opnieuw draaien = bijwerken.
rem Input:  DataPlakker.exe in dezelfde map als dit bestand
rem Output: data-plakker draait, sneltoets Ctrl+Alt+P

set "DEST=%LOCALAPPDATA%\data-plakker"

rem Een eventueel draaiende oude versie stoppen, anders kan hij niet overschreven worden
taskkill /f /im DataPlakker.exe >nul 2>&1

mkdir "%DEST%" 2>nul
copy /y "%~dp0DataPlakker.exe" "%DEST%\" >nul || (echo Kopieren mislukt. & pause & exit /b 1)

rem Markering "gedownload van internet" eraf halen VOOR de eerste start (zelfde
rem als Eigenschappen > Blokkering opheffen). Anders blokkeert Smart App Control
rem het niet-ondertekende programma, en dat oordeel blijft daarna hangen.
powershell -NoProfile -Command "Unblock-File -LiteralPath '%DEST%\DataPlakker.exe'"

rem Snelkoppeling in de map Opstarten, zodat hij na elke herstart weer draait
powershell -NoProfile -Command "$s = (New-Object -ComObject WScript.Shell).CreateShortcut([Environment]::GetFolderPath('Startup') + '\data-plakker.lnk'); $s.TargetPath = '%DEST%\DataPlakker.exe'; $s.Save()"

start "" "%DEST%\DataPlakker.exe"
echo.
echo data-plakker is geinstalleerd en gestart.
echo Druk Ctrl+Alt+P, sleep een kader om tekst of een tabel, en plak.
echo.
pause