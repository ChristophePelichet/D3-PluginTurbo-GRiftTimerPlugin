# Deaths Module

Tracks and displays the number of player deaths during each Greater Rift run, along with the cumulative time penalty incurred.

---

## Config key

```ini
show_deaths=yes    # yes | no  (default: no)
```

Adds a **†** column (55 px wide) between the Result column and the Floors column.

---

## Display format

| Deaths | Cell value |
|--------|-----------|
| 0 | `0` (green) |
| 1 | `1 (+5s)` (red) |
| 2 | `2 (+15s)` (red) |
| 3 | `3 (+30s)` (red) |
| 4 | `4 (+50s)` (red) |
| 5 | `5 (+1:20)` (red) |
| 6 | `6 (+1:50)` (red) |

The value in parentheses is the **cumulative death penalty** (time added to the run clock).

---

## Death penalty table

Diablo III applies a cumulative time penalty per death within a Greater Rift:

| Death # | Penalty | Cumulative total |
|---------|---------|-----------------|
| 1st | +5s | 5s |
| 2nd | +10s | 15s |
| 3rd | +15s | 30s |
| 4th | +20s | 50s |
| 5th+ | +30s each | 80s, 110s, … |

The timer keeps running during the respawn delay, so this penalty is already embedded in `ElapsedSeconds`. The `(+Xs)` indicator is therefore **informational only** — it shows how much of the elapsed time was spent dying.

---

## Detection method

Deaths are tracked via a **rising-edge detector** on `Hud.Game.Me.IsDead`:

```csharp
bool isDead = me.IsDead;
if (isDead && !_prevMeDead) _currentDeaths++;
_prevMeDead = isDead;
```

`_currentDeaths` is reset to `0` at the start of each new run and when leaving a GR.

---

## CSV storage

Death count is stored as the **7th field** in `GRiftHistory.csv`:

```
#,GRLevel,ElapsedSeconds,Completed,Pylons,FloorCount,DeathCount
6,119,83.1,1,Sh:0:0:Sh|Po:0:0:Po|Co:0:0:Co,2,0
```

On load, falls back to `0` if the field is absent (backward compatibility with older CSV files).

---

## Relevant code

| Symbol | Role |
|--------|------|
| `_currentDeaths` | Death counter for the current run |
| `_prevMeDead` | Previous `IsDead` state (rising-edge detection) |
| `RiftRun.DeathCount` | Stored death count per completed run |
| `DeathTimeLostSeconds(int deaths)` | Returns cumulative penalty in seconds |
| `FormatDeathCell(int deaths)` | Returns display string: `"0"` or `"N (+Xs)"` / `"N (+M:SS)"` |
| `WIN_W_DEATH` | Column width constant (`88f`) |
| `COL_DEATH` | X position of the column (`WIN_W_BASE`) |
