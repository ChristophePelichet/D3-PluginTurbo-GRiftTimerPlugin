# Inventory Module

Displays a persistent row below the stats box showing **bag free slots** and **blood shards** in real time.

---

## Config key

```ini
show_inventory=yes    # yes | no  (default: yes)
```

Adds a row of height `ROW_H` (28px) immediately below the stats box. If `show_stats=no`, this row appears directly after the `PAD` gap. The `PAD` gap is added once as soon as either `show_stats` or `show_inventory` is active.

---

## Visual layout

```
├──────────────────────────────────────────────────────────────────┤
│  B  20 / 60                    ◆ 1400 / 1900                     │
└──────────────────────────────────────────────────────────────────┘
   ▲                              ▲
   Bag free / total               Blood shards current / max
```

**Left half — Bag slots**

| Element | Detail |
|---------|--------|
| Label | `B` in `_colFont` (grey, small) |
| Gap | 8px fixed between `B` and the number |
| Value | `free / total` in color (see thresholds below) |

| Free slots | Color |
|-----------|-------|
| < 5 | Red (`_badFont`) |
| < 15 | Orange (`_warnFont`) |
| ≥ 15 | Green (`_goodFont`) |

**Right half — Blood shards**

| Element | Detail |
|---------|--------|
| Prefix | `◆` (U+25C6) |
| Value | `current / max` |
| `max` formula | `500 + (HighestSoloRiftLevel × 10)` |
| Positioned | At `_winX + WIN_W / 2f` (horizontal center of window) |

| Remaining capacity (`max − current`) | Color |
|---------------------------------------|-------|
| < 100 | Red (`_badFont`) |
| < 300 | Orange (`_warnFont`) |
| ≥ 300 | Neutral (`_normFont`) |

---

## Data sources

| Value | API |
|-------|-----|
| Free slots | `Hud.Game.Me.InventorySpaceTotal − Hud.Game.InventorySpaceUsed` |
| Total slots | `Hud.Game.Me.InventorySpaceTotal` |
| Blood shards | `Hud.Game.Me.Materials.BloodShard` |
| Max shards | `500 + (Hud.Game.Me.HighestSoloRiftLevel × 10)` |

`InventorySpaceUsed` lives on `IGameController`; `InventorySpaceTotal` and `HighestSoloRiftLevel` on `IPlayer`.

---

## Height impact

The row occupies exactly `ROW_H = 28f`. The `bodyH` formula accounts for it:

```csharp
float bodyH = COL_H + BAR_ROW_H + 1f + historyRows * ROW_H
            + ((_showStats || _showInventory) ? PAD        : 0f)
            + (_showStats     ? ROW_H : 0f)
            + (_showInventory ? ROW_H : 0f);
```

| `show_stats` | `show_inventory` | Extra height |
|:---:|:---:|---|
| no | no | 0 |
| yes | no | PAD + ROW_H (36px) |
| no | yes | PAD + ROW_H (36px) |
| yes | yes | PAD + 2 × ROW_H (64px) |

---

## Relevant code

| Symbol | Role |
|--------|------|
| `_showInventory` | Visibility flag (default `true`) |
| `show_inventory` | Config key in `GRiftTimer.cfg` |
| `invY` | Y position of the row: `ry + PAD` (stats absent) or `ry + PAD + ROW_H` (stats present) |
| `freeSlots` | `Hud.Game.Me.InventorySpaceTotal − Hud.Game.InventorySpaceUsed` |
| `shards` / `maxShards` | Current and maximum blood shards |
| `tlBagLbl` | Text layout for the `B` label |
| `tlBagVal` | Text layout for `free / total` |
| `tlShard` | Text layout for `◆ current / max` |

---

## Recipe breakdown tooltips

Hovering the **col 3** (Kulle) or **col 4** (Hope of Cain) zone of the inventory row pops a floating tooltip with the full ingredient breakdown for that recipe.

### Positioning rules

| Axis | Rule |
|------|------|
| Horizontal | Appears to the **right** of the main window (`_winX + WIN_W + 6f`). Falls back to the **left** (`_winX − ttW − 6f`) if it would overflow the screen right edge. |
| Vertical | **Bottom-aligned** with the inventory row: `ttY = invY + invH − ttH`. Clamped upward if `ttY < 0`, clamped downward if it would overflow the screen bottom. |

Bottom-alignment means the tooltip's lower border is flush with the bottom edge of the main box regardless of how many ingredient rows it contains (Recipe 2 has 6 rows, Recipe 3 has 4 rows).

### Layout constants (local to `DrawRecipeTooltip`)

| Constant | Value | Role |
|----------|-------|------|
| `TT_PAD` | `6f` | Inner padding (top, bottom, left, right) |
| `TT_ROW` | `20f` | Height of each row (title row + each ingredient row) |
| `COL_GAP` | `12f` | Gap between name → qty, qty → arrow (`→`), arrow → craft count |

Column widths (`maxNameW`, `maxQtyW`, `maxNW`) are **measured** from actual text layouts so they adapt automatically to the font and language (`fr` / `en`).

### Color coding

The ingredient whose craft count equals `minCrafts` (the bottleneck) is rendered in `_badFont` (red). All other ingredients use `_normFont` (white).
