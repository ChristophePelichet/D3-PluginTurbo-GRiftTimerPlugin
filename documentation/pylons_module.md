# Pylons Module

Tracks and displays the pylons activated during each Greater Rift run.

---

## Config key

```ini
show_pylons=yes    # yes | no  (default: no)
```

Adds a **Pylons** column (180 px wide) to the right of the history table.

---

## What is tracked

Only pylons that are **activated** (`IsOperated=true`) during the current run are recorded.  
A dedup guard prevents the same pylon from being counted twice if it reappears under a different ACD ID after activation.

---

## Abbreviations displayed

| Pylon type | EN | FR |
|------------|----|----|
| Conduit | `Cond` | `Cond` |
| Power | `Pow` | `Puis` |
| Shield | `Shld` | `Boucl` |
| Channeling | `Chan` | `Canal` |
| Speed | `Spd` | `Vit` |

The language follows the `lang=` setting in `GRiftTimer.cfg`.

---

## CSV storage

Pylons are stored in `GRiftHistory.csv` using internal short keys that are **independent of the display language**:

| Pylon type | CSV key |
|------------|---------|
| Conduit | `Co` |
| Power | `Po` |
| Shield | `Sh` |
| Channeling | `Ch` |
| Speed | `Spd` |

Format per run: `TextureSno:FrameIndex:CsvKey` separated by `|`  
Example: `Co:0:0:Co|Sh:0:0:Sh`

---

## Relevant code

| Symbol | Role |
|--------|------|
| `_currentPylonList` | Pylons activated in the current run |
| `_seenPylonIds` | Dedup by ACD ID |
| `_seenPylonPositions` | Dedup by type + grid cell (10-unit grid) |
| `_pylonTexCache` | Maps ACD ID → texture SNO + frame (from minimap markers) |
| `PylonDisplayName(ShrineType)` | Returns the localized abbreviation for display |
| `PylonCsvKey(ShrineType)` | Returns the short ASCII key used in the CSV |
| `AbbrToShrineType(string)` | Parses a CSV key back to `ShrineType` on history load |
