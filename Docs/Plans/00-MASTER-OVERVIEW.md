# Spellblade Phase 2 — Master Overview & Shared Contracts

*Planning session: 2026-07-13. Read this doc first in EVERY Claude Code session, then execute one workstream plan (01–05).*

---

## What Phase 2 is

Phase 1 built a single Shadow-Realm playground: runtime-assembled arena, click-to-move, Q/W/E/R discipline swap, LMB cast, 4 disciplines (Umbra/Frost/Storm/Blood) with an elemental counter-wheel, training dummies.

Phase 2 turns the playground into a **game**:

1. **WASD movement** replaces right-click-to-move (top-down MOBA camera stays).
2. **Radial spell wheel**: hold RMB → wheel pops up bottom-right → drag toward a spell to highlight → release RMB selects it → LMB casts it. **Mouse scroll** cycles between the four discipline wheels (Umbra → Frost → Storm → Blood → Umbra). Q/W/E/R discipline keys are removed (W is movement now).
3. **World map interface**: a zoomed-out 2.5D map showing all 8 biome regions; each region contains clickable arena nodes. Click a node → load that battle. Win/die → back to the map.
4. **Real enemies & varied objectives**: chaser + ranged-caster grunts with elemental attunements; arenas are wave-survival, waves-ending-in-boss, or A-to-B traversal gauntlets.
5. **Basic melee** — the "blade" half: no-mana strike on its own input.
6. **Progression**: unlock new spells (wheels fill in), rank up existing spells, buy stat upgrades, earn cosmetic gear. Persistent JSON save.

Death penalty: none — back to map, retry freely.

## Fixed design decisions (do not relitigate in sessions)

| Area | Decision |
|---|---|
| Camera | Keep top-down MOBA cam (`MobaCamera`) unchanged |
| Movement | WASD, world-axis relative (camera yaw is fixed), via NavMeshAgent |
| Wheel flow | Release RMB = select active spell; LMB = cast. No slow-mo. |
| Wheel switching | Scroll wheel cycles discipline wheels; one wheel per discipline (4 spell slots each) |
| Map | All 8 regions visible from day one; Shadow playable now, Frost region second; others locked "the mists have not parted" |
| Map style | 2.5D — flat stylized map base + 3D/particle flourishes (fog over Shadow, region-themed particles) |
| Unlocks | Hub-and-spokes: several nodes open per region; clear 3 → boss node unlocks; boss clear → adjacent regions unlock |
| Enemies | Simple grunts this phase: melee chaser + ranged caster, each elementally attuned. Elites later. |
| Death | Return to world map; arena resets; free retry |
| Melee | Basic strike now, Space key, no mana, short cooldown |
| Progression | All four tracks: new spells, stat upgrades, spell ranks, cosmetic gear |

## Codebase facts every session must respect

- Everything is **runtime-generated** — no scene asset dependencies, no prefab wiring. `SpellbladeBootstrap` assembles the arena at Play. Keep this philosophy: new systems are built from code (primitives + `SpellbladeFx`/`SpellbladeParticles` helpers), tunable via public fields.
- Namespace: `Spellblade`. Scripts live under `Assets/Spellblade/Scripts/{Core,Player,Camera,UI,Art,Editor}` — add `{Enemies,Map,Meta}` folders as the plans specify.
- **New Input System only** (`UnityEngine.InputSystem`) — legacy `Input.*` throws.
- URP + runtime NavMesh bake (`Unity.AI.Navigation`). Post FX built in code.
- Spells are `SpellSO` ScriptableObjects created in code (`SpellSO.Create`). Disciplines are `Discipline.Create(...)` with a spell list — `spells[0]` is primary today; Phase 2 makes all 4 slots live.
- Counter-wheel (`ElementMath`): Umbra→beats Frost→beats Storm→beats Blood→beats Umbra. Attuned enemies resist their own element (0.3×), break to its counter (1.75×).
- Shadow biome mood: "Scotland gloom" — overcast, ancient, war-worn. NOT supernatural blackness. Keep it.

## Shared contracts (build these EXACTLY as specified — multiple workstreams depend on them)

Plan 03 (World Map) is the **owner** of these files; other plans may *read* them and code against the signatures below. If your session starts before 03 has merged, create a minimal stub with these exact signatures under `Scripts/Core/` and note it in your handoff.

```csharp
// Scripts/Core/GameSession.cs — static carrier across scene loads
public static class GameSession
{
    public static ArenaNodeDef CurrentNode;          // set by map before loading Arena scene
    public static void ReportArenaResult(bool victory); // arena calls this, then loads WorldMap scene
}

// Scripts/Core/ArenaNodeDef.cs — plain data describing one map node
public class ArenaNodeDef
{
    public string id;               // "shadow_01"
    public string displayName;      // "The Sunken Yard"
    public string regionId;         // "shadow"
    public ObjectiveType objective; // WaveSurvival, WavesThenBoss, Traversal
    public int difficultyTier;      // 1..3 within a region
    public bool isBossNode;
}
public enum ObjectiveType { WaveSurvival, WavesThenBoss, Traversal }

// Scripts/Meta/SaveSystem.cs — JSON at Application.persistentDataPath/spellblade_save.json
public static class SaveSystem
{
    public static SaveData Data { get; }   // loaded lazily, created if missing
    public static void Save();
}

[System.Serializable]
public class SaveData
{
    public List<string> clearedNodes = new();      // ArenaNodeDef.id
    public List<string> unlockedRegions = new();   // starts ["shadow"]
    public int arcaneEssence;                       // stat-upgrade currency
    public int elementShards;                       // spell unlock/rank currency
    public List<string> unlockedSpells = new();    // spell ids, e.g. "umbra_2"
    public List<string> spellRanks = new();        // "spellId:rank", e.g. "umbra_1:2"
    public List<string> statUpgrades = new();      // "hp:3" style "stat:level"
    public List<string> unlockedGear = new();      // cosmetic ids
    public List<string> equippedGear = new();
}
```

**Scenes**: two scenes in Build Settings — `WorldMap` (index 0) and `Arena` (index 1). Each contains one empty GameObject with its bootstrap (`WorldMapBootstrap` / `SpellbladeBootstrap`). Plan 03 creates them via an editor script (extend the existing `SpellbladeSceneSetup` pattern). Until Plan 03 merges, the existing single playground scene keeps working — `GameSession.CurrentNode == null` must mean "playground mode: behave like Phase 1" everywhere.

**Spell identity**: every `SpellSO` gains a stable `public string spellId` (e.g. `"umbra_1"`..`"blood_4"`), set at creation. Progression, saves, and the wheel all key off it.

## The 5 workstreams

| # | Plan | Creates | Touches existing | Depends on |
|---|---|---|---|---|
| 01 | WASD Movement + Melee | `WasdController`, `MeleeStrike` | `SpellbladeBootstrap` (player assembly), `MobaController` (deleted at end) | nothing |
| 02 | Radial Spell Wheel + Full Spell Rosters | `SpellWheelUI`, `WheelInput`, new spells | `SpellCaster` (owns it), `Discipline`, `SpellSO`, `HudController`, `SpellbladeBootstrap` (roster) | nothing (better after 01) |
| 03 | World Map + Game Flow | `WorldMapBootstrap`, `RegionDef`, `ArenaNodeDef`, `GameSession`, map UI, scene setup | `SpellbladeBootstrap` (reads `GameSession`) | contracts only |
| 04 | Enemies + Arena Objectives | `Scripts/Enemies/*`, `ObjectiveDirector`, wave/boss/traversal logic | `SpellbladeBootstrap` (spawning), `Dummy` (kept for playground), `Projectile` (enemy-fired support) | contracts only |
| 05 | Progression + Save + Sanctum | `SaveSystem` fleshed out, `ProgressionMath`, Sanctum upgrade UI on map, gear unlocks | `WizardGear`, map UI hooks, `SpellCaster` (reads unlocks) | 02, 03 merged |

## Session execution order & git strategy

Branch per workstream: `phase2/movement`, `phase2/wheel`, `phase2/map`, `phase2/enemies`, `phase2/progression`.

- **Parallel-safe wave 1**: 01, 03, 04 simultaneously (disjoint files except one-line bootstrap insertions — see below).
- **Wave 2**: 02 (after 01 merges — both touch player input; 02 also removes the Q/W/E/R handler from `SpellCaster`).
- **Wave 3**: 05 (after 02 + 03 merge — it wires progression into wheel slots and map UI).

**Merge order: 01 → 03 → 04 → 02 → 05.**

`SpellbladeBootstrap` conflict rule: each plan adds AT MOST a few clearly-marked lines to `Start()`/`SpawnPlayer()` and puts all real logic in its own new static/setup classes. Mark insertions with `// [PHASE2-XX]` comments so merges are trivial.

Every session ends by: (1) compiling clean, (2) entering Play mode for a smoke test per the plan's acceptance checklist, (3) committing to its branch with a summary, (4) writing a short handoff note in `Docs/Plans/HANDOFF.md` (append-only).

## Placeholder region names (Ryan renames during the lore brain-dump — keep ids stable)

| id | Working name | Element affinity | Status |
|---|---|---|---|
| shadow | The Shadow Reach | Umbra | **Playable now** — Scotland gloom |
| frost | The Rimeholt | Frost | **Playable second** — build 3 nodes + boss |
| storm | The Tempest Shelf | Storm | Locked |
| blood | The Crimson Fen | Blood | Locked |
| ember | The Ember Wastes | TBD | Locked |
| verdant | The Verdant Deep | TBD | Locked |
| sunken | The Sunken Marches | TBD | Locked |
| radiant | The Radiant Steppe | TBD | Locked |

*(8 biomes per the lore; names/elements beyond the first four are placeholders pending Ryan's world-building brain dump — the map should treat names as data, trivially renameable.)*
