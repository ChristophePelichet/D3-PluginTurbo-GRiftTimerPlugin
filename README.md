# GRiftTimerPlugin

🌐 [English](#english) · [Français](#français)

---

<a name="english"></a>
## 🇬🇧 English

TurboHUD plugin for Diablo III — Greater Rift history with real-time timer.

<img width="480" alt="GRiftTimerPlugin screenshot" src="https://github.com/user-attachments/assets/6a595bfd-943e-4d79-be99-c75bdb1f43f8" />

### Features

- **Ascending timer** `mm:ss:ms` displayed in real time during a GR
- **GR level** automatically detected
- **Result**: ✓ Killed (green) or ✗ Timeout (red)
- **Color-coded time**: green / orange / red based on configurable thresholds
- **History** of the last N runs (10 by default, configurable)
- **Average time** displayed in the title bar (based on visible runs)
- **Multi-floor support**: timer carries over correctly between floors of the same GR
- **Draggable window**: drag the title bar to reposition
- **Hot-reload config**: edit `.cfg` while in-game, changes apply instantly
- **Archive on reset**: pressing `[R]` renames the CSV with a timestamp instead of deleting it

### Installation

#### Quick method (PowerShell)

```powershell
.\install.ps1
```

The script auto-detects TurboHUD in default locations.
If TurboHUD is installed elsewhere:

```powershell
.\install.ps1 -TurboHudPath "C:\MyFolder\TurboHUD"
```

#### Manual method

Copy `plugins\GRiftTimer\GRiftTimerPlugin.cs` into the `plugins\GRiftTimer\` folder of your TurboHUD installation, then restart TurboHUD.

### Usage

Buttons appear in the title bar on hover — hold the cursor over a button for ~1.5s to trigger it.

| Button | Action |
|--------|--------|
| `[T]` | Toggle window visibility |
| `[R]` | Reset history (archives or deletes CSV based on config) |
| `[S]` | Open config file in Notepad |

### Configuration

Press `[S]` to open `GRiftTimer.cfg` in Notepad. Changes apply immediately without restarting TurboHUD.

| Parameter | Description | Default |
|-----------|-------------|---------|
| `lang` | Display language: `en`, `fr`, `de`, `es` | `en` |
| `max_runs` | Number of runs shown in history (1–50) | `10` |
| `grace_ms` | Delay (ms) before validating a rift end after leaving the zone | `60000` |
| `thresh_orange` | Seconds before timer bar turns orange | `90` |
| `thresh_red` | Seconds before timer bar turns red | `100` |
| `reset_action` | What to do with CSV on reset: `archive` or `delete` | `archive` |
| `x`, `y` | Window position (updated automatically on drag) | auto |

> Increase `grace_ms` if your connection is slow and floor changes create false Timeouts.

### File structure

```
plugins\GRiftTimer\
    GRiftTimerPlugin.cs
    data\
        GRiftTimer.cfg
        csv\
            GRiftHistory.csv
            GRiftHistory_20260425_143022.csv   ← archived sessions
```

All data folders are created automatically on first launch.

### Compatibility

- TurboHUD LightningMOD (tested)
- Diablo III — Greater Rifts only

---

<a name="français"></a>
## 🇫🇷 Français

Plugin TurboHUD pour Diablo III — Historique des Greater Rifts avec timer en temps réel.

### Fonctionnalités

- **Timer ascendant** `mm:ss:ms` affiché en temps réel pendant le GR
- **Niveau du GR** automatiquement détecté
- **Résultat** : ✓ Tué (vert) ou ✗ Timeout (rouge)
- **Couleur du temps** : vert / orange / rouge selon des seuils configurables
- **Historique** des N derniers runs (10 par défaut, configurable)
- **Temps moyen** affiché dans la barre de titre (basé sur les runs visibles)
- **Multi-étages** : le timer continue correctement entre les étages du même GR
- **Fenêtre repositionnable** par drag sur la barre de titre
- **Config à chaud** : modifier le `.cfg` pendant le jeu, les changements s'appliquent immédiatement
- **Archive au reset** : `[R]` renomme le CSV avec un horodatage au lieu de le supprimer

### Installation

#### Méthode rapide (PowerShell)

```powershell
.\install.ps1
```

Le script détecte automatiquement TurboHUD dans les emplacements par défaut.
Si TurboHUD est installé ailleurs :

```powershell
.\install.ps1 -TurboHudPath "C:\MonDossier\TurboHUD"
```

#### Méthode manuelle

Copier `plugins\GRiftTimer\GRiftTimerPlugin.cs` dans le dossier `plugins\GRiftTimer\` de TurboHUD, puis relancer TurboHUD.

### Utilisation

Les boutons apparaissent dans la barre de titre au survol — maintenir le curseur ~1,5s pour déclencher.

| Bouton | Action |
|--------|--------|
| `[T]` | Masquer / afficher la fenêtre |
| `[R]` | Réinitialiser l'historique (archive ou supprime le CSV selon config) |
| `[S]` | Ouvrir la configuration dans Notepad |

### Configuration

La touche `[S]` ouvre `GRiftTimer.cfg` dans Notepad. Les modifications sont prises en compte immédiatement.

| Paramètre | Description | Défaut |
|-----------|-------------|--------|
| `lang` | Langue d'affichage : `en`, `fr`, `de`, `es` | `en` |
| `max_runs` | Nombre de runs dans l'historique (1–50) | `10` |
| `grace_ms` | Délai (ms) avant de valider une fin de rift après avoir quitté la zone | `60000` |
| `thresh_orange` | Secondes avant que la barre passe orange | `90` |
| `thresh_red` | Secondes avant que la barre passe rouge | `100` |
| `reset_action` | Action au reset : `archive` ou `delete` | `archive` |
| `x`, `y` | Position de la fenêtre (mis à jour au drag) | auto |

> Augmenter `grace_ms` si votre connexion est lente et que les changements d'étage créent de faux Timeout.

### Structure des fichiers

```
plugins\GRiftTimer\
    GRiftTimerPlugin.cs
    data\
        GRiftTimer.cfg
        csv\
            GRiftHistory.csv
            GRiftHistory_20260425_143022.csv   ← sessions archivées
```

Tous les dossiers sont créés automatiquement au premier lancement.

### Compatibilité

- TurboHUD LightningMOD (testé)
- Diablo III — Greater Rifts uniquement
