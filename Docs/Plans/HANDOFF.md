# Phase 2 — Session Handoffs (append-only)

*Each Claude Code session appends a short note here after committing: plan #, branch, what shipped, deviations from the plan, anything the next session must know.*

---

## Plan 01 — WASD Movement + Basic Melee · `phase2/movement` · 2026-07-12

**Shipped:**
- `Scripts/Player/WasdController.cs` (new) — WASD + arrow keys, camera-flattened world-relative direction, normalized (diagonals not faster), driven via `agent.Move` with `updateRotation` off (zero input = zero drift, walls still block). Smooth 720°/s turn toward movement. `BindAnimator` + `FaceToward` ported verbatim from `MobaController`; animator `Speed` driven from observed per-frame displacement (agent.velocity is not reliable under `Move()`), lightly smoothed.
- `Scripts/Player/MeleeStrike.cs` (new) — Space, 0.8s cooldown, 2.3m reach, 120° arc (dot-product filter), damage 22 physical (modifier 1.0 to damage numbers, counter-wheel not applied), no mana. 1.5m/0.12s knockback: `agent.Move` when the target has a NavMeshAgent (future grunts), raycast-guarded positional nudge otherwise (dummies). Violet-white slash: runtime quad swept across the arc + `SpellbladeParticles.Burst`, no camera shake. `baseDamage` public + `public static float damageMultiplier` hook for Plan 05.
- `SpellbladeBootstrap.SpawnPlayer` — `WasdController` + `MeleeStrike` added, control-hints log updated, all marked `// [PHASE2-01]`.
- `SpellCaster` — the two permitted lines ONLY (field type + `GetComponent`, `MobaController` → `WasdController`), marked `// [PHASE2-01]`. No logic touched.
- `MobaController.cs` deleted (click marker went with it — no other references).

**Verified:** Unity 6000.5.3f1 batchmode compile — 0 errors, 0 warnings. Play-mode smoke test NOT run (headless session — no interactive Play mode); acceptance checklist verified by code inspection, Ryan should do the 2-minute in-editor pass.

**Deviations:**
1. Branch carries a first commit (`a674a49`) snapshotting pre-existing uncommitted Phase 1 work found on main (Elements.cs, DamageNumber.cs, WizardGear.cs, modified SpellCaster/Bootstrap/Dummy/Projectile/SpellSO/DisciplineAura/ShadowBiomeArt + these plan docs). Plans 02–05 depend on those files, but they exist in NO other branch — **wave-1 branches (03, 04) cut from bare main will not compile against the plans until 01 merges.** Recommendation: merge 01 first (per the master plan's merge order) before cutting other branches, or cut them from `phase2/movement`.
2. Melee swing has no rig animation (no attack clip exists in Starter Assets ThirdPerson) — feel comes from the slash VFX; an animation pass can come with real combat art.

**Interim quirk (per plan, accepted):** W still swaps to Frost discipline while moving — `SpellCaster` Q/W/E/R handler is untouched by design; Plan 02 removes it. Harmless: no errors, movement and swap both fire.

---

## Plan 03 — World Map + Game Flow (contracts owner) · `phase2/map` · 2026-07-12

**Contracts are LIVE — code against these, do not stub them anymore:**
- `Scripts/Core/GameSession.cs` — `CurrentNode`, `ReportArenaResult(bool)` exactly per overview, plus query helpers other plans may use: `IsRegionUnlocked(regionId)`, `IsNodeCleared(nodeId)`, `IsBossUnlocked(regionId)`, `BossUnlockClears` (=3). Hub-and-spokes unlock rules live inside `ReportArenaResult` (boss clear → regions whose `unlockedBy` contains that region). `CurrentNode` is nulled on every report so playground mode can't be polluted by a stale node.
- `Scripts/Core/ArenaNodeDef.cs` — exact contract fields + `ObjectiveType` enum. Node MAP POSITIONS deliberately live in `RegionDefs.NodePlacement`, NOT on ArenaNodeDef — the contract stays pure gameplay data.
- `Scripts/Meta/SaveSystem.cs` — exact `SaveData` schema, JsonUtility at `persistentDataPath/spellblade_save.json`, lazy load, fresh save = `unlockedRegions:["shadow"]`, corrupt file → warn + fresh.

**Also shipped:**
- `Scripts/Map/RegionDefs.cs` — all 8 regions (placeholder names, ids stable), shadow 5 nodes / frost 4 per plan, hand-placed normalized positions.
- `Scripts/Map/WorldMapBootstrap.cs` + `Scripts/Map/MapNodeUI.cs` — runtime-generated 2.5D map: ortho camera + parchment-gradient world quad + soft-circle region blobs (world layer), screen-space-camera canvas (labels, beacons, tooltip, currency footer, Sanctum placeholder) so URP bloom hits the beacons (map volume threshold 0.6). Flourishes on a mouse-parallax root: fog over Shadow, cyan glints over Frost (when unlocked), mist over locked regions ("The mists have not parted."). Node states: open (pulsing, element tint), cleared (dim + ✓), boss-locked (dark + lock silhouette + tooltip). Click → flash → `CurrentNode` set → `Arena` loads. EventSystem uses `InputSystemUIInputModule` (new Input System only — StandaloneInputModule would throw).
- `Scripts/Core/ArenaFlow.cs` — arena-side flow. `SpellbladeBootstrap.Start()` got ONE marked line: `ArenaFlow.Begin(this); // [PHASE2-03]`. Playground (`CurrentNode == null`) = exact Phase 1 behavior, zero UI. Arena mode: top banner, **Esc = abandon (permanent), V = debug victory (only while `TryFindObjectiveDirector` returns false)**. **Plan 04: replace the body of `ArenaFlow.TryFindObjectiveDirector(node)`** with the director lookup + `Configure(node)` — the debug V key then disappears automatically.
- `SpellbladeSceneSetup` — new menu **Spellblade → Create Game Scenes** (batchmode-safe): generates `Assets/Scenes/WorldMap.unity` + `Arena.unity`, Build Settings WorldMap=0 / Arena=1. **Already run — both scenes are committed on this branch.**

**Verified:** Unity 6000.5.3f1 batchmode — 0 errors, 0 warnings; `CreateGameScenes` executed headlessly (scenes + Build Settings confirmed on disk). Play-mode smoke test NOT run (headless session); checklist verified by code inspection — Ryan should click through map → arena → V → map once in-editor.

**Deviations / notes:**
1. Beacon glow is canvas-UI driven (LDR) — bloom threshold lowered to 0.6 in the map scene so it reads. If beacons still feel flat in person, the fix is world-layer HDR quads behind each node; noted, not built.
2. "Chain glyph" on the boss lock is a minimalist two-image padlock silhouette (no glyph font available in LegacyRuntime.ttf); tooltip carries the real message.
3. Cleared nodes are re-clickable (free-retry ethos; plan doesn't forbid replay). Replaying a cleared node just re-reports victory — idempotent, no double-add.
4. `SampleScene.unity` left in place and pushed after WorldMap/Arena in Build Settings; the old playground scene at `Assets/Spellblade/Spellblade Playground.unity` keeps working as playground mode.

---

## Plan 04 — Enemies + Arena Objectives · `phase2/enemies` · 2026-07-12

**No contract stubs were needed** — Plan 03 had already merged to main, so this branch codes against the real `GameSession`/`ArenaNodeDef`/`SaveSystem`.

**Shipped — enemies (`Scripts/Enemies/`):**
- `EnemyBase` — NavMeshAgent + Health + ElementalAffinity + Spawning→Chase/Attack→Dead flow, materialize-in, element-tinted eyes/accents, dissolve death (shrink+sink+burst) firing `OnKilled`. Aggro model: waves spawn hot (range 999), traversal packs use range 11 so running past works; any damage aggros permanently.
- `GruntChaser` "Husk" — 55 HP, speed 3.8, 1.2m/10dmg/1.2s swipe with a 0.35s visible windup lean (the dodge window).
- `GruntCaster` "Cultist" — 40 HP, holds 9–12m, retreats inside 7m, strafes every 2.5–4s, fires an element bolt every 2.5s after a 0.5s orb-swell windup (speed 14, dmg 12, r 0.3). Bolts use the new `ProjectileTargets.PlayerOnly` filter.
- `MiniBoss` "Court Warden" — 2.2× scale, 320 HP, speed 3.2, region-attuned. SLAM: 1s `TelegraphRing` (pulsing ground disc, urgency ramp) then 25 dmg within 2.5m, 5s cd. SUMMON: 2 Husks, 6s gate, max 4 alive summons. Heavy swipe between abilities.

**Shipped — objectives (`Scripts/Arena/`):**
- `ObjectiveDirector` — added by the bootstrap when `CurrentNode != null`; `ArenaFlow.TryFindObjectiveDirector` now finds+configures it (the Plan-03 stub body was replaced exactly as designed, so the debug V key is gone in arena mode; Esc-abandon remains). Owns telegraphed spawning (flash → 0.6s → materialize, NavMesh-snapped), objective HUD line, splashes, victory banner (1.5s) → `ReportArenaResult(true)`, and `Defeat()` for PlayerLife.
- `WaveSurvivalObjective` — T1: 3H / 2H+2C / 3H+3C; T2+: 4 waves (3H / 2H+2C / 3H+2C / 4H+3C) at +30% HP; "WAVE N" splash + 3s breathers; perimeter-ring spawn points; attunements cycle all four elements.
- `WavesThenBossObjective` — tier waves → "THE COURT WARDEN" splash → Warden (region element); 2 Cultist adds at 50% boss HP; victory on Warden death.
- `TraversalObjective` + `TraversalArena` — 3-chamber 22×60 corridor (offset door gaps, pillar cover, rubble, ground mist), packs 2H+1C (ch1) and 3H+2C (ch2) at aggro 11, spinning gold shard-ring portal in chamber 3; touch radius 1.6 → victory.

**Shipped — player mortality:** `Health(120)` + `PlayerLife` + a capsule hitbox on the player (it had NO collider before — enemy projectiles would have flown straight through; this was a real gap, not in the plan text). Vignette pulses blood-red on hits (drives the bootstrap's URP volume), HUD gets a slim crimson HP bar, arena death = 1.2s fade → defeat (no clear), playground death = respawn at spawn point.

**Touched:** `Projectile` (optional `ProjectileTargets` param, default preserves SpellCaster behavior — enemy bolts ghost through enemies/dummies, stop on player/walls), `HudController` (optional third `Build` param + HP bar), `SpellbladeBootstrap` (6 small `[PHASE2-04]`-marked insertions), `ArenaFlow` (stub body filled). `SpellCaster` untouched.

**Verified:** Unity 6000.5.3f1 batchmode — 0 errors, 0 warnings. No Play-mode run (headless session); checklist code-verified, needs Ryan's in-editor pass for feel (windup readability, telegraph timing, kite distances).

**Deviations / interpretations:**
1. `ShadowBiomeArt.Build` NOT used in the corridor — its dressing is hand-placed for the 30×30 square and would intersect corridor walls. Same palette/mood recreated with the wall kit + rubble + `GroundMist`.
2. Plan's T1 wave-3 "3+2+1" read as 3 Husks + 3 Cultists (3H+2C+1C). Adjust in `WaveSurvivalObjective.BuildWaves` if the intent was different.
3. HP bar sits just BELOW the mana bar, not above — the slot above is occupied by the ability bar's spell-name labels; same paired-bar read.
4. Warden also has a basic swipe (12 dmg) between abilities so it isn't a pushover at melee range — plan implied it as a "scaled-up chaser."

**For Plan 02 (wheel):** nothing here claims RMB or scroll. **For Plan 05:** `MeleeStrike.damageMultiplier` static hook still waiting; enemies don't yet award essence/shards — that's your earning hook, `EnemyBase.OnKilled` is the place.

---
