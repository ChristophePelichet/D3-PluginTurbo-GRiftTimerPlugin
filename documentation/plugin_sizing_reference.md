# GRiftTimerPlugin — Sizing & Layout Reference

> **Read this before modifying any size, position, or adding new columns.**
> All constants are defined at the top of `GRiftTimerPlugin.cs` under the `// ── Layout ──` section.

---

## Visual layout diagram

```
◄─────────────────── WIN_W (390 base + optional) ───────────────────►
┌────────────────────────────────────────────────────────────────────┐ ─┐
│  GR Timer History                              [T]  [R]  [S]       │  │ HDR_H = 32px
├────────────────────────────────────────────────────────────────────┤ ─┤
│  #      GR       Time            Result                            │  │ COL_H = 28px  (column headers)
├────────────────────────────────────────────────────────────────────┤ ─┤
│  #23    GR119    01:02:799  ░░░░░░░░░░░░░░░░░  37%                 │  │ BAR_ROW_H = 26px  (live timer bar)
├────────────────────────────────────────────────────────────────────┤ ─┤  ← separator (1px)
│  #22    GR119    01:08:500       ✓ Killed                          │  │ ROW_H = 28px
│  #21    GR119    01:19:599       ✓ Killed                          │  │ ROW_H = 28px
│  #20    GR119    01:58:299       ✓ Killed                          │  │ ROW_H = 28px
│  ...                                                               │  │ (× max_runs rows)
│                                                                    │  │ ← PAD = 8px gap (empty space)
├────────────────────────────────────────────────────────────────────┤ ─┤
│  ø 01:43:909 (10)  Σ 17:39:090 / 10                               │  │ ROW_H = 28px  (stats box)
└────────────────────────────────────────────────────────────────────┘ ─┘

Vertical stacking formula:
  totalH = HDR_H + COL_H + BAR_ROW_H + 1 + (max_runs × ROW_H) + PAD + ROW_H
         = 32    + 28    + 26         + 1 + (N × 28)           + 8   + 28
```

---

## Window dimensions

| Constant | Value | Description |
|---|---|---|
| `WIN_W_BASE` | `362f` | Fixed width of the window (core columns only, no optional columns). **Never shrink below `~340f`** — the header title + buttons must fit and `COL_RES + content_width` must not overflow. Note: the header average text requires `WIN_W >= 370f`; with at least one optional column active this is always satisfied. |
| `WIN_W` (property) | computed | `WIN_W_BASE + optional widths`. Use this everywhere in rendering code. |
| `HDR_H` | `32f` | Header row height — title + buttons. Increase for more air around the title. |
| `COL_H` | `28f` | Column header row height (#, GR, Time, Result). |
| `ROW_H` | `28f` | Height of each history row **and** the stats box row. |
| `BAR_ROW_H` | `26f` | Height of the live timer bar row. |
| `PAD` | `8f` | Gap between last history row and stats box. Also used as left margin. |

---

## Fixed columns (always visible)

These columns are **constants** — they do not change based on config options.

| Constant | X position | Content | Space to next column |
|---|---|---|---|
| `COL_NUM` | `8f` (= PAD) | Run number `#23` | 52px to `COL_GR` |
| `COL_GR` | `60f` | GR level `GR119` | 70px to `COL_TIME` |
| `COL_TIME` | `130f` | Elapsed time `01:08:500` | 125px to `COL_RES` |
| `COL_RES` | `255f` | Result `✓ Killed` / `✗ Timeout` | 105px to end of `WIN_W_BASE` |

**Rule:** If you change `COL_GR`, shift `COL_TIME` by the same delta.  
**Rule:** `COL_RES + content_width` must stay below `WIN_W_BASE` (362px). Longest result text `"✗ Timeout"` ≈ 80px → ends at ~335px, leaving ~27px margin.

---

## Optional columns

Optional columns are **computed properties** (not constants) so their X position automatically accounts for which preceding optional columns are currently active.

### Formula

```
COL_A => WIN_W_BASE
COL_B => WIN_W_BASE + (_showA ? WIN_W_A : 0f)
COL_C => WIN_W_BASE + (_showA ? WIN_W_A : 0f) + (_showB ? WIN_W_B : 0f)
```

And `WIN_W` must match:
```
WIN_W => WIN_W_BASE + (_showA ? WIN_W_A : 0f) + (_showB ? WIN_W_B : 0f) + ...
```

### Current optional columns

Optional columns are ordered — each one appears **to the right of the previous**. Order in code = order in the formula.

| Order | Property | Width constant | Default | Config key | Content |
|---|---|---|---|---|---|
| 1st | `COL_FLOOR` | `WIN_W_FLOOR = 60f` | hidden | `show_floors=yes\|no` | Floor count for the run: `3F`, `4F`, … |
| 2nd | `COL_PYLON` | `WIN_W_PYLON = 180f` | hidden | `show_pylons=yes\|no` | Space-separated pylon abbreviations: `Ch Co Po Sh Spd` |

### Pylon abbreviations

| Abbreviation | Shrine type |
|---|---|
| `Ch` | ChannelingPylon |
| `Co` | ConduitPylon |
| `Po` | PowerPylon |
| `Sh` | ShieldPylon |
| `Spd` | SpeedPylon |

Maximum pylons per GR rift: **5** (one of each type, each can appear at most once per floor but multiple floors exist — in practice 4–7 abbreviations may be recorded across all floors of a single run).

---

## Header row layout

The header contains (left to right):
1. **Title** `GR Timer History` — left-aligned at `_winX + PAD`
2. **Average text** `ø 01:43:909 (10)` — centered in the space between title and buttons
3. **Buttons** `[T] [R] [S]` — right-aligned, each `BTN_W=26px`, gap `BTN_GAP=4px`

| Constant | Value |
|---|---|
| `BTN_W` | `26f` |
| `BTN_H` | `16f` |
| `BTN_GAP` | `4f` |
| `HOVER_MS` | `1500ms` — hold duration to trigger a button action |

**Rule:** The average text is only drawn if `availW >= textWidth + 4f`. If `WIN_W_BASE` is ever reduced, the average text may disappear before the layout breaks.  
**Minimum safe `WIN_W_BASE`:** title (~130px) + 2×PAD + avg (~130px) + PAD + 3×BTN_W + 2×BTN_GAP + PAD ≈ 370px. Current value is 360px — intentionally below this threshold; the header average text is hidden when no optional column is active, but visible whenever `_showFloor` or `_showPylons` is on (`WIN_W ≥ 420px`).

---

## How to add a new optional column

1. Add width constant: `private const float WIN_W_NEW = Xf;`
2. Add visibility field: `private bool _showNew = false;`
3. Add position property **after all currently active optional columns** (currently `COL_FLOOR` then `COL_PYLON`):
   ```csharp
   private float COL_NEW => WIN_W_BASE + (_showFloor ? WIN_W_FLOOR : 0f) + (_showPylons ? WIN_W_PYLON : 0f);
   ```
4. Extend `WIN_W` with the new term at the end:
   ```csharp
   private float WIN_W => WIN_W_BASE + (_showFloor ? WIN_W_FLOOR : 0f) + (_showPylons ? WIN_W_PYLON : 0f) + (_showNew ? WIN_W_NEW : 0f);
   ```
5. Add config key in `LoadConfig` / `SaveConfig`.
6. Draw the column in `PaintTopInGame` and `DrawTimerBarRow` guarded by `if (_showNew)`.
7. Draw the column header in the column headers block guarded by `if (_showNew)`.
