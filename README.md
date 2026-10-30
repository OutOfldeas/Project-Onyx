# Project Onyx

Koop-Weltraumspiel für 2–4 Spieler: freie Erkundung, Survival, Basisbau und Logistik in einer dystopischen Galaxie. Top-down-Pixelart (16-px-Kacheln), gebaut mit Unity 6, Release auf Steam.

## Struktur

| Pfad | Inhalt |
|---|---|
| `docs/` | Designdokumente (Spielkonzept, Systeme, Roadmap) |
| `Onyx/` | Unity-Projekt (Vorlage „Universal 2D“) |

## Projekt öffnen

1. Repo klonen, z. B. mit [GitHub Desktop](https://desktop.github.com/).
2. Im Unity Hub: **Add → Add project from disk** und den Ordner `Onyx` im Repo wählen.
3. Editor-Version: **6000.6.7f1**. Alle im Team sollten dieselbe Version nutzen; die verwendete Version steht in `Onyx/ProjectSettings/ProjectVersion.txt`.

`Library/`, `Temp/`, `Logs/` und `UserSettings/` erzeugt Unity lokal; sie werden über die `.gitignore` aus dem Repo herausgehalten.

## Dokumentation

- [Game Design Document](docs/GDD.md)
