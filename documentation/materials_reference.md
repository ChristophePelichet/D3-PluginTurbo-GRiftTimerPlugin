# Materials Reference — IPlayerMaterialInfo

All properties listed below are accessible via `Hud.Game.Me.Materials.*` in any TurboHUD plugin.

---

## Currency

| Property | In-game name | Type |
|----------|-------------|------|
| `Gold` | Or | `long` |
| `BloodShard` | Éclats de sang | `long` |
| `BNetStore` | Monnaie Battle.net | `long` |

---

## Crafting materials (common)

| Property | In-game name | Type |
|----------|-------------|------|
| `ReusableParts` | Pièces réutilisables | `long` |
| `ArcaneDust` | Poussière arcanique | `long` |
| `VeiledCrystal` | Cristal voilé | `long` |
| `DeathsBreath` | Souffle de mort | `long` |
| `ForgottenSoul` | Âme oubliée | `long` |
| `PrimordialAshes` | Cendres primordiales | `long` |

---

## Boss essences (Set crafting)

Used at the Blacksmith to craft class set pieces.

| Property | In-game name | Boss source |
|----------|-------------|-------------|
| `KhanduranRune` | Rune de Khanduras | Skeleton King |
| `CaldeumNightShade` | Ombre nocturne de Caldeum | Maghda |
| `ArreatWarTapestry` | Tapisserie de guerre d'Arreat | Zoltun Kulle |
| `CorruptedAngelFlesh` | Chair d'ange corrompue | Izual |
| `WestmarchHolyWater` | Eau bénite de Westmarch | Adria |

---

## Uber essences (Hellfire crafting)

Used at the Jeweler / Blacksmith to craft Hellfire items.

| Property | In-game name | Uber source |
|----------|-------------|-------------|
| `HeartOfFright` | Cœur de l'effroi | Diablo |
| `VialOfPutridness` | Fiole de putréfaction | Ghom + Rag |
| `IdolOfTerror` | Idole de la terreur | Siege Breaker + Zoltun |
| `LeoricsRegret` | Regret de Léoric | Skeleton King + Maghda |

---

## Rift key

| Property | In-game name | Type |
|----------|-------------|------|
| `GreaterRiftKeystone` | Clé de Rift supérieur | `long` |

---

## Usage example

```csharp
long dust    = Hud.Game.Me.Materials.ArcaneDust;
long parts   = Hud.Game.Me.Materials.ReusableParts;
long crystal = Hud.Game.Me.Materials.VeiledCrystal;
long breath  = Hud.Game.Me.Materials.DeathsBreath;
long souls   = Hud.Game.Me.Materials.ForgottenSoul;
long ashes   = Hud.Game.Me.Materials.PrimordialAshes;
long shards  = Hud.Game.Me.Materials.BloodShard;
long keys    = Hud.Game.Me.Materials.GreaterRiftKeystone;
```

---

## Interface source

```
TurboHUD\interfaces\...\IPlayerMaterialInfo.cs
```

Accessed via `IPlayer.Materials` → `Hud.Game.Me.Materials`.
