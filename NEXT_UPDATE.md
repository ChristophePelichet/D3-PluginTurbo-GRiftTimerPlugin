# Next Update — Features to Implement

## 1. Death count in GR (branch: feat/death-count)

Track and display the number of times the player died during a GR run.

**API available:**
- `Hud.Game.Me.IsDead` — live death state
- `Hud.Game.Me.LastDied` — timestamp of last death (IWatch)
- `Hud.Sno.Attributes.Tiered_Loot_Run_Death_Count` — in-game GR death counter attribute

**Plan:**
- Add `int _deathCount` to `RiftRun`
- Track deaths in `AfterCollect()` using `IsDead` edge detection (false→true transition)
- Display in a new column or as part of the Result column (e.g. `✓ Killed  0†`)
- Persist in CSV (add 6th field)

---

## 2. Rift Guardian name (branch: to be created)

Capture and display the name of the rift guardian (final boss) for each run.

**API available:**
- `Hud.Game.AliveMonsters.FirstOrDefault(m => m.SnoMonster.Priority == MonsterPriority.boss)`
- `monster.SnoMonster.NameLocalized` — localized name (FR/EN depending on game language)
- `monster.SnoMonster.NameEnglish` — always English

**Plan:**
- Add `string GuardianName` to `RiftRun`
- Capture name in `AfterCollect()` when a `MonsterPriority.boss` monster is detected in GR
- Display in the Result column or as a tooltip/extra column
- Persist in CSV (add 7th field)
