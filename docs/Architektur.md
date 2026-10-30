# Architektur

Wie der Code von Project Onyx aufgebaut ist und welche Regeln dabei gelten.

## Drei Schichten

| Assembly | Ordner | Inhalt | Darf verwenden |
|---|---|---|---|
| **Onyx.Core** | `Onyx/Assets/_Project/Scripts/Core` | Reine Spiellogik: Überleben, Inventar, Skills, Weltdaten, Speicherformat | nur .NET – **kein** `UnityEngine` |
| **Onyx.Net** | `Onyx/Assets/_Project/Scripts/Net` | Sitzungen, Verbindung, Host/Beitreten, später Steam und Cloud-Sperre | Core, Unity, Netzwerkpakete |
| **Onyx.Game** | `Onyx/Assets/_Project/Scripts/Game` | Unity-Komponenten: Figuren, Kamera, Tilemaps, Licht, UI, Eingabe | Core, Net, Unity |
| **Onyx.Editor** | `Onyx/Assets/_Project/Scripts/Editor` | Werkzeuge nur für den Editor (Menü *Onyx*), z. B. Szenen anlegen | alles, nur im Editor |

Die Abhängigkeiten zeigen nur in eine Richtung: **Game → Net → Core**. Core weiß nichts von Unity oder vom Netzwerk.

**Warum?** Logik in Core lässt sich ohne Unity automatisiert testen, bleibt unabhängig vom Netzwerkcode und kann unverändert in einem dedizierten Server laufen.

### Faustregel: Wohin gehört neuer Code?

- Rechnet etwas nur mit Zahlen und Zuständen (Sauerstoff sinkt, Skill steigt, Rezept prüfen)? → **Core**
- Geht es darum, wer mit wem verbunden ist oder was übers Netz synchronisiert wird? → **Net**
- Braucht es `MonoBehaviour`, `GameObject`, Grafik, Physik oder Eingabe? → **Game**

## Ordnerstruktur in `Onyx/Assets/_Project`

```
_Project/
├── Scripts/
│   ├── Core/        Onyx.Core  – reine Logik
│   ├── Net/         Onyx.Net   – Netzwerk
│   ├── Game/        Onyx.Game  – Unity-Seite
│   │   ├── Bootstrap/   Startpunkt des Spiels
│   │   ├── Player/      Spielfigur: Bewegung, Darstellung
│   │   ├── World/       Boden, Tilemaps
│   │   ├── CameraRig/   Kamera
│   │   └── DevTools/    Entwicklerhilfen (z. B. Host/Beitreten-Menü)
│   └── Editor/      Onyx.Editor – Editor-Werkzeuge
└── Tests/
    └── Core/        Onyx.Core.Tests – Tests der reinen Logik
```

Weitere Ordner (`Art/`, `Audio/`, `Data/`, `Prefabs/`, `Scenes/`) kommen dazu, sobald es Inhalte dafür gibt. Leere Ordner werden nicht ins Repo gelegt.

Der Unterstrich in `_Project` sorgt dafür, dass unsere Inhalte oben stehen und sich nicht mit Paketen aus dem Asset Store vermischen.

## Start des Spiels

`GameBootstrap` läuft automatisch, bevor die erste Szene geladen wird, egal welche Szene im Editor offen ist. Es legt das dauerhafte Objekt `[Onyx]` an, an das später Netzwerk, Speicherung und andere Dienste gehängt werden.

## Netzwerk-Grundsätze

- **Host = Server + Spieler.** Spielzustände (Sauerstoff, Inventar, Welt) entscheidet immer der Host.
- **Ausnahme Bewegung:** Die eigene Figur bewegt der Besitzer selbst (`NetworkTransform` im Owner-Modus). So gibt es keine Eingabeverzögerung; für ein Koop-Spiel unter Freunden ist das vertretbar.
- **Startplätze** vergibt der Host beim Beitreten (`SessionLauncher` → `SpawnLayout`).

## Mehrspieler im Editor testen

- **Multiplayer Play Mode** (Unity-Paket `com.unity.multiplayer.playmode`): *Window → Multiplayer → Multiplayer Play Mode*, einen zweiten virtuellen Spieler aktivieren. Ein Fenster hostet, das andere tritt `127.0.0.1` bei.
- Alternativ: *File → Build And Run* für einen Mitspieler, der Editor ist der Host.

## Inhalte als Daten

Gegenstände, Module, Fähigkeiten und Biome werden als **ScriptableObjects** angelegt. Werte lassen sich so im Editor ändern, ohne Code anzufassen.

## Tests

- **In Unity:** *Window → General → Test Runner → EditMode → Run All*.
- **Automatisch auf GitHub:** Bei jedem Pull Request und jedem Push auf `main` laufen die Core-Tests über `tools/CoreTests` (GitHub Actions, Workflow „Core-Tests“). Das Projekt kompiliert dieselben Quelldateien wie Unity, nur ohne Unity.
- Core-Code muss **C# 9** bleiben (Stand von Unity): keine `record struct`, keine Namespaces ohne geschweifte Klammern (`namespace X;`), kein `required`.

## Regeln für Git und Unity

- **`.meta`-Dateien immer mitcommitten.** Sie enthalten die IDs, über die Unity Dateien verknüpft. Fehlt eine `.meta`-Datei, erzeugt Unity eine neue mit anderer ID, und Verweise gehen kaputt.
- **Ordner und Dateien in Unity verschieben oder umbenennen**, nicht im Explorer. Sonst verliert Unity die Verknüpfung.
- **Szenen nicht gleichzeitig bearbeiten.** Unity-Szenen lassen sich schlecht zusammenführen. Kurz absprechen, wer welche Szene bearbeitet.
- **Neue Arbeit in einem eigenen Branch**, dann Pull Request auf `main`.

## Konventionen

- Code und Bezeichner auf **Englisch**, Kommentare und Dokumentation auf **Deutsch**.
- Namespaces folgen den Ordnern: `Onyx.Core.Survival`, `Onyx.Game.Bootstrap` usw.
- Formatierung nach `.editorconfig` (4 Leerzeichen, geschweifte Klammern in eigener Zeile).
