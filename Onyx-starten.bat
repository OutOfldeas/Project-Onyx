@echo off
rem Startet Unity direkt mit dem richtigen Projekt (Ordner "Onyx" neben dieser Datei).
set "UNITY=C:\Program Files\Unity\Hub\Editor\6000.6.7f1\Editor\Unity.exe"
if not exist "%UNITY%" (
  echo Unity 6000.6.7f1 wurde nicht gefunden unter:
  echo %UNITY%
  echo Bitte im Unity Hub genau diese Version installieren.
  pause
  exit /b 1
)
start "" "%UNITY%" -projectPath "%~dp0Onyx"
