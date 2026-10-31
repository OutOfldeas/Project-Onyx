@echo off
rem Einmal pro Rechner ausfuehren: richtet Git fuer die Arbeit am Onyx-Projekt ein.
rem - Unity Smart Merge (UnityYAMLMerge) fuer Szenen und Prefabs
rem - sinnvolle Grundeinstellungen fuer dieses Repo
setlocal
cd /d "%~dp0"

set "MERGE=C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Data\Tools\UnityYAMLMerge.exe"
if not exist "%MERGE%" (
    echo UnityYAMLMerge nicht gefunden:
    echo   %MERGE%
    echo Ist Unity 6000.6.7f1 ueber den Unity Hub installiert? Sonst den Pfad oben anpassen.
    pause
    exit /b 1
)

where git >nul 2>nul
if errorlevel 1 (
    echo Git ist nicht installiert. Bitte zuerst https://git-scm.com/download/win installieren.
    pause
    exit /b 1
)

set "MERGE_FWD=%MERGE:\=/%"
git config merge.unityyamlmerge.name "Unity SmartMerge"
git config merge.unityyamlmerge.driver "\"%MERGE_FWD%\" merge -p %%O %%B %%A %%A"
git config merge.unityyamlmerge.recursive binary
git config core.longpaths true
git config pull.rebase false

echo.
echo Fertig. Git ist fuer Project Onyx eingerichtet:
git config --get merge.unityyamlmerge.driver
echo.
pause
