@echo off
rem Startet Unity direkt mit dem richtigen Projekt (Ordner "Onyx" neben dieser Datei).
rem Holt vorher die neueste Version aus dem Repo, wenn das gefahrlos geht.
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
if not exist "%UNITY%" (
  echo Unity 6000.6.7f1 wurde nicht gefunden unter:
  echo %UNITY%
  echo Bitte im Unity Hub genau diese Version installieren.
  pause
  exit /b 1
)

where git >nul 2>nul
if not errorlevel 1 (
  echo Hole neueste Version aus dem Repo ...
  git -C "%~dp0." pull --ff-only
  if errorlevel 1 (
    echo.
    echo Konnte nicht automatisch aktualisieren - vermutlich lokale Aenderungen.
    echo Unity startet trotzdem; dort hilft Menue Onyx - Git - Jetzt aktualisieren.
    timeout /t 5 >nul
  )
)

start "" "%UNITY%" -projectPath "%~dp0Onyx"
