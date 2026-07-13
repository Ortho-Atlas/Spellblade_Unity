# Plan 03 — World Map Interface + Game Flow

*Branch: `phase2/map` · Wave 1 (contracts owner) · Merges after 01*

## Session prompt (paste into Claude Code)

> Read `Docs/Plans/00-MASTER-OVERVIEW.md` and `Docs/Plans/03-WORLD-MAP-GAMEFLOW.md`, then implement this plan exactly. Work on branch `phase2/map`. You OWN the shared contracts (`GameSession`, `ArenaNodeDef`, `SaveSystem` skeleton) — build them exactly as the overview specifies. Compile clean, smoke-test per the acceptance checklist, commit, and append a handoff note to `Docs/Plans/HANDOFF.md`.

## Goal

The new front door of the game: a zoomed-out **2.5D world map** showing all 8 biome regions, with clickable arena nodes inside the unlocked ones. Click a node → the arena loads with that node's config. Win or die → back to the map (win marks the node cleared). Hub-and-spokes unlock logic. This plan also owns the two-scene structure and the save skeleton.

## Part A — Scenes & flow plumbing

1. Editor script (extend the `SpellbladeSceneSetup` menu pattern): **Spellblade → Create Game Scenes** generates `Assets/Scenes/WorldMap.unity` (empty GO + `WorldMapBootstrap`) and `Assets/Scenes/Arena.unity` (empty GO + `SpellbladeBootstrap`), adds both to Build Settings (WorldMap = 0).
2. **`Scripts/Core/GameSession.cs` + `Scripts/Core/ArenaNodeDef.cs`** — exactly per the overview contract. `ReportArenaResult(victory)`: if victory, add node id to `SaveSystem.Data.clearedNodes`, run unlock rules, `SaveSystem.Save()`; either way `SceneManager.LoadScene("WorldMap")`.
3. **`Scripts/Meta/SaveSystem.cs`** — the overview's `SaveData` schema, `JsonUtility` to `Application.persistentDataPath/spellblade_save.json`. Missing file → fresh save with `unlockedRegions = ["shadow"]`. (Plan 05 extends usage; you own the file format.)
4. **`SpellbladeBootstrap` integration** (marked `// [PHASE2-03]`, keep tiny): at `Start()`, read `GameSession.CurrentNode`. If null → playground mode, exactly today's behavior. If set → pass the node to whatever objective system exists (Plan 04's `ObjectiveDirector.Configure(node)` if present — feature-detect via `GetComponent`/reflection-free `#if` NOT needed; just a direct call guarded by a `TryFindObjectiveDirector()` helper that Plan 04 fills in; until then stub = playground behavior + an on-screen "Victory (debug: press V) / Return (Esc)" fallback so map flow is testable without Plan 04). Also add a permanent Esc = abandon arena → `ReportArenaResult(false)`.

## Part B — Region/node data (`Scripts/Map/RegionDefs.cs`, new)

Static list of 8 `RegionDef { id, displayName, elementTint, mapPosition (normalized 0-1), unlocked-by (list of region ids), status }` using the overview's placeholder table. Node sets:

- **shadow** (playable): 5 nodes — `shadow_01` "The Sunken Yard" (WaveSurvival, T1) · `shadow_02` "Cairn Hollow" (Traversal, T1) · `shadow_03` "The Broken Chapel" (WaveSurvival, T2) · `shadow_04` "Mistgate Causeway" (Traversal, T2) · `shadow_boss` "The Umbral Court" (WavesThenBoss, T3, boss).
- **frost** (playable second): 4 nodes — `frost_01` "Rimefall Quarry" (WaveSurvival, T1) · `frost_02` "The Glass Ravine" (Traversal, T2) · `frost_03` "Howling Terrace" (WaveSurvival, T2) · `frost_boss` "The Frozen Throne" (WavesThenBoss, T3, boss).
- Other 6 regions: zero nodes, locked presentation only.

**Unlock rules** (hub-and-spokes, implemented in `GameSession`): a region's non-boss nodes are all open once the region is unlocked; the boss node unlocks when **3+ non-boss nodes** in that region are cleared (frost: all 3); clearing a region's boss unlocks its adjacent regions (`shadow` → `frost`; `frost` → `storm` — which shows as "unlocked" but contains a "no arenas yet" state).

## Part C — The 2.5D map screen (`Scripts/Map/WorldMapBootstrap.cs` + `Scripts/Map/MapNodeUI.cs`, new)

Runtime-generated like everything else:

- **Base**: full-screen canvas over a dark world-parchment backdrop — build from code: a large soft-gradient background, 8 irregular region blobs (generated polygon sprites or layered soft-circle sprites) tinted per element, faint drawn borders, region name labels in a serif-feeling style (default font, letter-spaced caps are fine now).
- **3D flourishes** (the 2.5 part): a world-space layer behind the canvas — slow-drifting fog particles over the Shadow Reach (reuse the `ShadowBiomeArt` ground-mist approach), gentle cyan snow-glint particles over the Rimeholt, dim static tint for locked regions. A slow (barely perceptible) parallax: the flourish layer offsets a few pixels against mouse position.
- **Nodes**: glowing clickable beacons at hand-placed normalized positions inside their region. States: **open** (pulsing glow, element tint), **cleared** (steady dim glow + check mark), **boss-locked** (dark + chain glyph + "Clear 3 arenas" tooltip), **region locked** (not shown). Hover: node scales ~1.15× and shows a tooltip panel (name, objective type, tier, cleared state). Click: brief flash → set `GameSession.CurrentNode` → load `Arena`.
- **Locked regions**: desaturated, overlaid with drifting mist and the label "The mists have not parted." No nodes.
- **Header/footer**: game title small top-left; bottom bar shows currencies (essence/shards — reads `SaveSystem.Data`, live once Plan 05 adds earning) and a "Sanctum" button placeholder (Plan 05 replaces it with the upgrade panel).
- Map camera: plain UI camera, solid near-black background; keep the URP post volume (bloom makes the beacons sing).

## Acceptance checklist

- [ ] Play from `WorldMap` scene: all 8 regions render with names; Shadow shows 5 nodes (4 open + boss locked); Frost region visibly present but locked at fresh save.
- [ ] Clicking an open node loads Arena with `GameSession.CurrentNode` set; the debug Victory key returns to the map with the node marked cleared (persists across editor restarts — JSON on disk).
- [ ] Clearing 3 shadow nodes unlocks the boss node; the boss node's debug-clear unlocks Frost (its 4 nodes appear).
- [ ] Esc in arena returns to map with NO clear recorded; arena replayable.
- [ ] Playground mode intact: pressing Play directly in the old playground scene behaves exactly like Phase 1 (`CurrentNode == null`).
- [ ] Fog drifts over Shadow, glints over Frost, parallax on mouse move; locked regions misted; no console errors.
