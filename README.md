# GRiftTimerPlugin

Plugin TurboHUD pour Diablo III — Historique des Greater Rifts avec timer en temps réel.

![Aperçu](https://i.imgur.com/placeholder.png)

## Fonctionnalités

- **Timer ascendant** 0:00 → 15:00 affiché en temps réel pendant le GR
- **Niveau du GR** automatiquement détecté
- **Résultat** : ✓ Tué (vert) ou ✗ Timeout (rouge)
- **Historique** des N derniers runs (10 par défaut, configurable)
- **Multi-étages** : le timer continue correctement entre les étages du même GR
- **Fenêtre repositionnable** par drag sur la barre de titre
- **Config à chaud** : modifier le fichier `.cfg` pendant le jeu, les changements s'appliquent immédiatement

## Installation

### Méthode rapide (PowerShell)

```powershell
.\install.ps1
```

Le script détecte automatiquement TurboHUD dans les emplacements par défaut.  
Si TurboHUD est installé ailleurs :

```powershell
.\install.ps1 -TurboHudPath "C:\MonDossier\TurboHUD"
```

### Méthode manuelle

Copier `plugins\GRiftTimer\GRiftTimerPlugin.cs` dans le dossier `plugins\GRiftTimer\` de TurboHUD, puis relancer TurboHUD.

## Utilisation

| Touche | Action |
|--------|--------|
| `T` | Masquer / afficher la fenêtre |
| `R` | Réinitialiser l'historique |
| `S` | Ouvrir la configuration dans Notepad |

## Configuration

La touche `S` ouvre `data/GRiftTimer.cfg` dans Notepad. Les modifications sont prises en compte immédiatement (pas besoin de relancer TurboHUD).

| Paramètre | Description | Défaut |
|-----------|-------------|--------|
| `max_runs` | Nombre de runs dans l'historique (1–50) | `10` |
| `grace_ms` | Délai (ms) avant de valider une fin de rift | `60000` |
| `x`, `y` | Position de la fenêtre (mis à jour au drag) | auto |

> Augmenter `grace_ms` si votre connexion est lente et que les changements d'étage créent de faux Timeout.

## Structure des fichiers

```
./
├── plugins/
│   └── User/
│       └── GRiftTimerPlugin.cs   ← fichier à copier dans TurboHUD
├── install.ps1                   ← script d'installation automatique
└── README.md
```

TurboHUD crée automatiquement au premier lancement :

```
TurboHUD/
└── data/
    ├── GRiftTimer.cfg            ← configuration
    └── GRiftHistory.csv          ← historique des runs
```

## Compatibilité

- TurboHUD LightningMOD (testé)
- Diablo III — Greater Rifts uniquement
