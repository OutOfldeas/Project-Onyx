# Zusammen am Projekt arbeiten

> Für alle, die an Project Onyx mitarbeiten: einmal einrichten, dann gemeinsam entwickeln und testen.

## 1. Einmalig einrichten

1. **Zugang zum Repo:** Der Besitzer lädt dich auf GitHub ein (Repo → *Settings → Collaborators → Add people*). Einladung per Mail annehmen.
2. **Git installieren:** [git-scm.com](https://git-scm.com/download/win), alle Voreinstellungen lassen. Wer lieber klickt: zusätzlich [GitHub Desktop](https://desktop.github.com/).
3. **Unity Hub** installieren und darüber die Editor-Version **6000.6.7f1** (genau diese, sonst gibt es Konflikte in den Projektdateien).
4. **Repo klonen**, z. B. nach `D:\Project-Onyx`:
   ```
   git clone https://github.com/OutOfldeas/Project-Onyx.git D:\Project-Onyx
   ```
5. **Doppelklick auf `Team-Setup.bat`** im Repo-Ordner. Richtet Git für Unity ein (Smart Merge für Szenen und Prefabs).
6. **Doppelklick auf `Onyx-starten.bat`** – öffnet Unity mit dem Projekt. Der erste Start dauert ein paar Minuten.
7. In Unity: Menü **Onyx → Setup → M1-Absturzstelle erstellen**, dann **Play** und oben links **Hosten**.

## 2. Täglicher Ablauf

**Neue Versionen kommen automatisch:** `Onyx-starten.bat` holt vor dem Start den neuesten Stand, und solange Unity offen ist, schaut es alle drei Minuten auf GitHub nach (Menü **Onyx → Git**: *Jetzt aktualisieren*, *Automatisch aktualisieren* an/aus). Aktualisiert wird nur, wenn dabei nichts von dir verloren geht; sonst fragt ein Dialog. Nicht während Play.

Wer selbst Änderungen macht:

```
git pull                 # vor dem Arbeiten: Stand der anderen holen
… arbeiten, in Unity testen …
git add -A
git commit -m "Kurz und auf Deutsch, was sich ändert"
git pull                 # falls inzwischen jemand anderes gepusht hat
git push
```

- **Klein und oft committen.** Lieber fünf kleine Commits am Tag als einen riesigen am Wochenende.
- **Vor dem Pushen kurz testen:** Unity zeigt keine roten Fehler in der Konsole, Play startet.
- **Größere Umbauten auf einem eigenen Branch** (`git switch -c feature/werft-ui`) und per Pull Request zusammenführen. Kleine Sachen dürfen direkt auf `main`.
- Wenn Git nach einem `pull` einen **Konflikt** meldet: nicht in Panik verfallen, nichts löschen – Bescheid sagen und gemeinsam auflösen.

## 3. Regeln, damit wir uns nicht in die Quere kommen

| Was | Regel |
|---|---|
| **Szenen** (`*.unity`) | Werden per Menü *Onyx → Setup* erzeugt. Nicht von Hand bearbeiten – Änderungen gehören in den Szenenbau-Code (`Scripts/Editor`). |
| **Prefabs** | Ebenfalls per Code erzeugt (Spieler). Eigene neue Prefabs: vorher absprechen, wer daran arbeitet. |
| **Wer macht was** | Kurz im Chat sagen, woran man sitzt („ich mache die Werft-UI“). Später: GitHub Issues, einer pro Aufgabe. |
| **`.meta`-Dateien** | Immer mit committen. Fehlt eine, gehen Verweise kaputt. |
| **Spielstände** | Liegen nicht im Repo, sondern lokal (siehe unten). |
| **Code** | Englische Namen, deutsche Kommentare und Texte. Spiellogik in `Onyx.Core` mit Tests (siehe [Architektur](Architektur.md)). |

## 4. Zusammen spielen und testen

**Im selben Netzwerk:** Einer drückt *Hosten*, der andere trägt dessen lokale IP ein (z. B. `192.168.178.23`, zu finden mit `ipconfig`) und drückt *Beitreten*. Port **7777**.

**Über das Internet (bis Steam da ist):**
- Am einfachsten mit **[Tailscale](https://tailscale.com/)** (kostenlos): beide installieren, im selben Konto oder per Einladung verbinden, dann die Tailscale-IP des Hosts (`100.x.y.z`) zum Beitreten benutzen. Keine Portfreigabe nötig.
- Alternativ Portfreigabe für UDP 7777 im Router des Hosts.

**Allein mit zwei Spielern testen:** Unity-Paket *Multiplayer Play Mode* (Window → Package Manager → Unity Registry). Damit läuft ein zweiter Spieler im selben Editor.

**Nützliche Tasten:** WASD laufen · E halten: reparieren / wiederbeleben · M Sternenkarte · K Skills · F1 Dev-Modus (`help` zeigt alle Befehle, z. B. `repair all`, `heal`, `god`, `noclip`, `save`, `wipe`).

## 5. Wo liegt was auf dem Rechner?

| Was | Wo |
|---|---|
| Projekt | dein Klon, z. B. `D:\Project-Onyx` (Unity-Projekt im Unterordner `Onyx`) |
| Spielstand (Welt, nur beim Host) | `%USERPROFILE%\AppData\LocalLow\DefaultCompany\Project Onyx\Saves\welt.json` |
| Eigener Charakter (Skills) | gleicher Ordner, `charakter.json` |
| Unity-Logdatei bei Abstürzen | `%LOCALAPPDATA%\Unity\Editor\Editor.log` |

„DefaultCompany“ und „Project Onyx“ kommen aus *Edit → Project Settings → Player* (Company / Product Name). Ändert man sie, wandert der Speicherordner mit.

## 6. Hilfe, es geht nicht

| Problem | Lösung |
|---|---|
| Menü *Onyx* fehlt | Falsches Projekt offen. Unity schließen und `Onyx-starten.bat` benutzen. |
| Rote Fehler nach `git pull` | Unity einmal in den Vordergrund holen und warten, bis es fertig kompiliert hat. Bleiben sie: Screenshot in den Chat. |
| „Your local changes would be overwritten“ | Eigene Änderungen erst committen (oder mit `git stash` beiseitelegen), dann erneut `git pull`. |
| Kann nicht beitreten | Gleiche Spielversion (beide `git pull`)? Richtige IP? Firewall-Abfrage von Unity zugelassen? |
