# Textures & Icons Reference — TurboHUD

All textures are accessed via `Hud.Texture` (type `ITextureController`).  
All textures implement `ITexture` and expose a single draw method:

```csharp
texture.Draw(float x, float y, float w, float h, float opacity = 1.0f);
```

---

## 1. Built-in named properties (`Hud.Texture.*`)

Ready-to-use properties requiring no ID lookup.

### Craft / inventory UI

| Property | Visual |
|----------|--------|
| `Hud.Texture.EmptySocketTexture` | Emplacement de socket vide |
| `Hud.Texture.UnidTexture` | Fond objet non-identifié |
| `Hud.Texture.KanaiCubeTexture` | Icône Cube de Kanai |
| `Hud.Texture.InventorySlotTexture` | Case d'inventaire |
| `Hud.Texture.InventoryLegendaryBackgroundSmall` | Fond légendaire petit |
| `Hud.Texture.InventoryLegendaryBackgroundLarge` | Fond légendaire grand |
| `Hud.Texture.InventorySetBackgroundSmall` | Fond set item petit |
| `Hud.Texture.InventorySetBackgroundLarge` | Fond set item grand |

### Boutons & backgrounds génériques

| Property | Visual |
|----------|--------|
| `Hud.Texture.ButtonTextureGray` | Bouton gris |
| `Hud.Texture.ButtonTextureBlue` | Bouton bleu |
| `Hud.Texture.ButtonTextureOrange` | Bouton orange |
| `Hud.Texture.Button2TextureGray` | Bouton 2 gris |
| `Hud.Texture.Button2TextureOrange` | Bouton 2 orange |
| `Hud.Texture.Button2TextureBrown` | Bouton 2 marron |
| `Hud.Texture.BackgroundTextureOrange` | Fond orange |
| `Hud.Texture.BackgroundTextureGreen` | Fond vert |
| `Hud.Texture.BackgroundTextureYellow` | Fond jaune |
| `Hud.Texture.BackgroundTextureBlue` | Fond bleu |

### Buffs

| Property | Visual |
|----------|--------|
| `Hud.Texture.BuffFrameTexture` | Cadre de buff |
| `Hud.Texture.DebuffFrameTexture` | Cadre de debuff |

---

## 2. Textures par nom string — `GetTexture(string name)`

Textures du jeu accessibles par nom.

```csharp
ITexture tex = Hud.Texture.GetTexture("nom_texture");
```

| Nom string | Visual / usage |
|------------|----------------|
| `"inventory_materials"` | Fond de la barre de matériaux en inventaire |
| `"BattleNetContextMenu_Title"` | Bandeau titre menu B.net |
| `"BattleNetContextMenu_Bottom"` | Bas du menu B.net |
| `"WaypointMap_MarkerBountyComplete"` | Marqueur bounty complété sur la carte |
| `"WaypointMap_ButtonAct1Up"` | Bouton Acte 1 (état normal) |
| `"WaypointMap_ButtonAct1Over"` | Bouton Acte 1 (état hover) |
| `"WaypointMap_ButtonAct2Up"` … | Idem Actes 2 à 5 |

> **Pattern générique Actes :** `"WaypointMap_ButtonAct" + actNumber + (actCurrent ? "Over" : "Up")`

---

## 3. Textures par uint ID — `GetTexture(uint id)`

Textures internes du jeu identifiées par leur ID numérique.

```csharp
ITexture tex = Hud.Texture.GetTexture(1981524232u);
```

| ID | Visual / usage connu |
|----|----------------------|
| `1981524232` | Halo / glow générique |
| `1156241668` | Icône "désactiver" |
| `2560062517` | Icône "activer" |
| `503639160` | Coffre (chest) type 1 |
| `3627160803` | Coffre (chest) type 2 |

---

## 4. Textures par SNO + frame — `GetTexture(uint textureSno, int frameIndex)`

Textures issues du moteur SNO (sprites animés ou multi-frames).

```csharp
ITexture tex = Hud.Texture.GetTexture(218235u, 0);
```

| SNO | Frame | Visual / usage |
|-----|-------|----------------|
| `218235` | 0 | Shrine (autel) type 1 |
| `218234` | 0 | Shrine (autel) type 2 |
| `376779` | 0 | Shrine (autel) type 3 |
| `448992` | 0 | Portail (portal) |

---

## 5. Icônes d'items — `GetItemTexture(ISnoItem snoItem)`

Récupère l'icône in-game d'un item SNO.

```csharp
ISnoItem snoItem = Hud.Inventory.GetSnoItem(snoId);
ITexture tex     = Hud.Texture.GetItemTexture(snoItem);
```

### SNO IDs des matériaux de craft

| SNO ID | Matériau | Propriété Materials |
|--------|----------|---------------------|
| `3931359676` | Pièces réutilisables | `Materials.ReusableParts` |
| `2709165134` | Poussière arcanique | `Materials.ArcaneDust` |
| `3689019703` | Cristal voilé | `Materials.VeiledCrystal` |
| `2087837753` | Souffle de mort | `Materials.DeathsBreath` |
| `2073430088` | Âme oubliée | `Materials.ForgottenSoul` |
| `198281388` | Cendres primordiales | `Materials.PrimordialAshes` |
| `1948629088` | Rune de Khanduras | `Materials.KhanduranRune` |
| `1948629089` | Ombre nocturne de Caldeum | `Materials.CaldeumNightShade` |
| `1948629090` | Tapisserie de guerre d'Arreat | `Materials.ArreatWarTapestry` |
| `1948629091` | Chair d'ange corrompue | `Materials.CorruptedAngelFlesh` |
| `1948629092` | Eau bénite de Westmarch | `Materials.WestmarchHolyWater` |
| `1102953247` | Regret de Léoric | `Materials.LeoricsRegret` |
| `2029265596` | Fiole de putréfaction | `Materials.VialOfPutridness` |
| `2670343450` | Idole de la terreur | `Materials.IdolOfTerror` |
| `3336787100` | Cœur de l'effroi | `Materials.HeartOfFright` |
| `2835237830` | Clé de Rift supérieur | `Materials.GreaterRiftKeystone` |

---

## 6. Icônes de skills / pouvoirs

L'icône d'un pouvoir actif du joueur via son `NormalIconTextureId` :

```csharp
ITexture tex = Hud.Texture.GetTexture(skill.CurrentSnoPower.NormalIconTextureId);
tex.Draw(x, y, w, h);
```

Pour les icônes de buff (avec variante de rune) :

```csharp
// SnoPowerIcon[] icons = snoPower.Icons;
// icons[runeIndex].TextureId → passer dans GetTexture(uint)
ITexture tex = Hud.Texture.GetTexture(snoPower.Icons[0].TextureId);
```

---

## 7. Fond de background d'item — `GetItemBackgroundTexture(IItem item)`

Retourne le fond coloré selon la qualité de l'item (normal / magic / rare / légendaire / set).

```csharp
ITexture bg = Hud.Texture.GetItemBackgroundTexture(item);
bg.Draw(x, y, w, h);
```

---

## 8. Résumé — quelle méthode choisir ?

| Besoin | Méthode |
|--------|---------|
| UI générique (bouton, fond) | `Hud.Texture.ButtonTexture*` / `BackgroundTexture*` |
| Buff / debuff frame | `Hud.Texture.BuffFrameTexture` |
| Icône item du jeu | `Hud.Texture.GetItemTexture(snoItem)` |
| Fond qualité item | `Hud.Texture.GetItemBackgroundTexture(item)` |
| Icône skill actif | `GetTexture(skill.CurrentSnoPower.NormalIconTextureId)` |
| Texture UI nommée | `GetTexture("nom_string")` |
| Texture interne (ID connu) | `GetTexture(uint id)` |
| Sprite animé SNO | `GetTexture(uint sno, int frame)` |
