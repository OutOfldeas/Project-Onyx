# CLAUDE.md – Project Onyx

Kontext für Claude-Sitzungen in diesem Repo. Ausführlich: `docs/GDD.md` (Spiel), `docs/Architektur.md` (Technik), `docs/Roadmap.md` (Stand und nächste Schritte), `docs/Team.md` (Zusammenarbeit).

## Projekt
Koop-Weltraumspiel für 2–4 Spieler (Steam), Unity **6000.6.7f1**, URP 2D, Top-down 3/4-Ansicht, 16-px-Kacheln, Figuren 16 × 32. Netcode for GameObjects 2.13 + Unity Transport. Host = Server + Spieler.

## Sprache
- Mit dem Team **auf Deutsch** kommunizieren.
- Code: englische Bezeichner, **deutsche Kommentare, Doku und Spieltexte**.

## Aufbau
| Assembly | Ordner | Regeln |
|---|---|---|
| `Onyx.Core` | `Onyx/Assets/_Project/Scripts/Core` | Reines C# 9, **keine Unity-Referenzen**, deterministisch (`SeededRandom`, `Noise`), alles getestet |
| `Onyx.Net` | `Scripts/Net` | Sitzung starten/beitreten (`SessionLauncher`) |
| `Onyx.Game` | `Scripts/Game` | MonoBehaviours, Netzwerk, Darstellung, UI (IMGUI im Pixel-Stil über `PixelGui`) |
| `Onyx.Editor` | `Scripts/Editor` | Szenen- und Prefab-Bau per Menü *Onyx → Setup* |
| `Onyx.Core.Tests` | `Onyx/Assets/_Project/Tests/Core` | NUnit |

Abhängigkeiten nur Game → Net → Core. `WorldState` (Core) ist der Spielstand, `GameSession` (Game) hält ihn am dauerhaften Objekt `[Onyx]` (angelegt von `GameBootstrap`). Host rechnet Spielzustand; Bewegung gehört dem Besitzer.

## Arbeitsweise
- **Erst Kernlogik in Core mit Tests, dann Unity-Seite.** Jedes neue System bekommt Dev-Befehle (`CoreDevCommands` bzw. `DevMode`, Taste F1).
- **Tests:** `dotnet test tools/CoreTests` (kompiliert Core + Tests ohne Unity, .NET 8). Läuft auch in GitHub Actions („Core-Tests“); das Ergebnis steht als Annotation „X von Y Tests bestanden“.
- **Szenen und Prefabs nicht als YAML von Hand schreiben** – sie entstehen über die Builder in `Scripts/Editor` (`M0SceneBuilder`, `M1SceneBuilder`). Nach Änderungen an Spieler-Komponenten oder Szenenaufbau muss der Builder im Editor erneut laufen.
- **`.meta`-Dateien** für neue Ordner, `.cs`- und `.asmdef`-Dateien mit fester GUID anlegen und mit committen.
- Pixel-Schrift ist Silkscreen (8 px, `Resources/Fonts`): keine Zeichen wie `→` oder `◄` in Spieltexten (fehlen im Font). Textbreiten auf der 480 × 270-Fläche im Blick behalten (~6 px je Zeichen).
- Spielstände: Welt beim Host (`welt.json`), Charakter bei jedem Spieler (`charakter.json`), beides über `WorldPersistence`/`CharacterPersistence` in Core.

## Git
- Der Besitzer wünscht **direkte Pushes auf `main`** statt PRs für Claudes Arbeit (Ausnahme: größere riskante Umbauten nach Absprache).
- Commit-Nachrichten auf Deutsch, kurz und beschreibend.
- Windows-Skripte (`*.bat`) mit CRLF; Unity-YAML mit LF (siehe `.gitattributes`).
