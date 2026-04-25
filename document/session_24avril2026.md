# Session du 24 avril 2026 — GRiftTimerPlugin

## Ce qui a été fait

### 1. Raccourci clavier [R] Reset
- Suppression du bouton cliquable (impossible dans TurboHUD, les clics passent au jeu)
- Ajout `ResetKeyEvent = Key.R` → vide l'historique + supprime le CSV

### 2. Raccourci clavier [S] Config
- `ConfigKeyEvent = Key.S` → ouvre `data/GRiftTimer.cfg` dans le Notepad
- `FileSystemWatcher` surveille le fichier → rechargement à chaud sans redémarrer TurboHUD
- Nouveaux paramètres dans le cfg :
  - `max_runs` : nombre de runs dans l'historique (1-50, défaut 10)
  - `grace_ms` : délai avant validation fin de rift (défaut 60000 ms)
- Le cfg est généré avec des commentaires explicatifs

### 3. Fix changement d'étage (dernier fix, pas encore testé)
**Problème** : `CurrentTimedEventStartTick` change entre les étages du même GR.  
L'ancien code voyait un nouveau StartTick et committait un faux "Timeout".

**Fix** :
```csharp
bool isFloorChange = _pendingSave && _lastPercent < 100f;
if (isFloorChange || newStart == _startTick)
{
    // Changement d'étage → on continue le run
    _startTick   = newStart;
    _pendingSave = false;
}
```
- Si on revient dans un GR avec `_lastPercent < 100%` → c'est un étage, pas une nouvelle rift
- Rift complétée (boss tué) : grace réduite à **3 secondes** au lieu de 60s

## État de l'overlay
- Titre : `GR Timer History  [T] masquer  [R] reset  [S] config`
- Colonnes : `#` | `GR` | `Temps` | `Résultat`
- Résultat vert = Tué, rouge = Timeout

## À tester demain
- [ ] Changement d'étage ne crée plus de fausse rift
- [ ] Rift complétée (boss tué) → historique mis à jour en ~3s
- [ ] [S] ouvre le cfg dans Notepad
- [ ] Modifier `max_runs` dans le cfg → s'applique immédiatement en jeu
- [ ] [R] vide l'historique sans déplacer le perso

## Si le bug d'étage persiste encore
Piste alternative : au lieu de regarder `_lastPercent` à l'entrée,
mémoriser `_percentAtLeave` au moment de la sortie du GR.
```csharp
if (_wasInGR && !nowInGR)
{
    _pendingSave    = true;
    _leaveTimeMs    = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
    _percentAtLeave = _lastPercent;  // snapshot au moment de partir
}
// puis utiliser _percentAtLeave < 100f pour détecter l'étage
```

## Fichiers clés
| Fichier | Rôle |
|---------|------|
| `plugins/User/GRiftTimerPlugin.cs` | Plugin principal |
| `data/GRiftTimer.cfg` | Config (position, max_runs, grace_ms) |
| `data/GRiftHistory.csv` | Historique des runs |
| `logs/plugins.txt` | Logs de compilation TurboHUD |
