# Project Onyx

Koop-Weltraumspiel für 2–4 Spieler: freie Erkundung, Survival, Basisbau und Logistik in einer dystopischen Galaxie. Top-down-Pixelart (16-px-Kacheln), gebaut mit Unity 6, Release auf Steam.

## Struktur

| Pfad | Inhalt |
|---|---|
| `docs/` | Designdokumente (Spielkonzept, Systeme, Roadmap) |
| `Onyx/` | Unity-Projekt (Unity 6 LTS, Vorlage „Universal 2D“) – wird noch angelegt |

## Unity-Projekt anlegen

1. Repo klonen.
2. Im Unity Hub: **New project → Unity 6 (LTS) → Universal 2D**, Projektname `Onyx`, Speicherort = Wurzelordner dieses Repos.
3. In Unity unter *Edit → Project Settings → Editor* prüfen: **Version Control Mode = Visible Meta Files**, **Asset Serialization = Force Text** (beides Standard in Unity 6).
4. Committen und pushen. Die `.gitignore` im Wurzelordner hält `Library/`, `Temp/` usw. bereits aus dem Repo heraus.

## Dokumentation

- [Game Design Document](docs/GDD.md)
