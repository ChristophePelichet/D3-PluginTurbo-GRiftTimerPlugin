# Next Update — Features to Implement

## ~~1. Death count in GR~~ ✅ Done (merged in feat/death-count)

~~Track and display the number of times the player died during a GR run.~~

Implemented:
- `†` column (55 px), toggleable via `show_deaths=yes|no`
- Rising-edge detection on `Hud.Game.Me.IsDead`
- Display format `N (+Xs)` / `N (+M:SS)` with cumulative penalty
- CSV field 7, backward-compatible
- See `documentation/deaths_module.md`

---

## 1. Rift Guardian name (branch: to be created)

Capture and display the name of the rift guardian (final boss) for each run.

**API available:**
- `Hud.Game.AliveMonsters.FirstOrDefault(m => m.SnoMonster.Priority == MonsterPriority.boss)`
- `monster.SnoMonster.NameLocalized` — localized name (FR/EN depending on game language)
- `monster.SnoMonster.NameEnglish` — always English

**Plan:**
- Add `string GuardianName` to `RiftRun`
- Capture name in `AfterCollect()` when a `MonsterPriority.boss` monster is detected in GR
- Display in the Result column or as a tooltip/extra column
- Persist in CSV (add 8th field)
