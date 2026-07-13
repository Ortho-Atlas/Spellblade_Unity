# Plan 04 — Enemies + Arena Objectives

*Branch: `phase2/enemies` · Wave 1 (contracts consumer) · Merges after 03*

## Session prompt (paste into Claude Code)

> Read `Docs/Plans/00-MASTER-OVERVIEW.md` and `Docs/Plans/04-ENEMIES-OBJECTIVES.md`, then implement this plan exactly. Work on branch `phase2/enemies`. If Plan 03's `GameSession`/`ArenaNodeDef` aren't on main yet, create minimal stubs with the overview's exact signatures and note it in the handoff. Compile clean, smoke-test per the acceptance checklist, commit, and append a handoff note to `Docs/Plans/HANDOFF.md`.

## Goal

Real fights. Two grunt archetypes with elemental attunements, an objective framework that runs each node type (wave survival / waves-then-boss / A-to-B traversal), a mini-boss, and win/lose flow wired to `GameSession.ReportArenaResult`. All code-generated visuals, consistent with the Shadow gloom.

## Part A — Enemy foundation (`Scripts/Enemies/`)

1. **`EnemyBase.cs`** — NavMeshAgent + `Health` + `ElementalAffinity` + simple state machine (Spawn → Chase → Attack → Dead). Element tint applied to body emissive accents via `ElementMath.ColorOf`. Death: dissolve-ish shrink + particle burst in element color, notifies the director. Player damage dealt TO the player needs `Health` on the player — **add it**: player gets `Health` (120 HP) + a red vignette pulse on hit + HP bar on the HUD (slim bar over the mana bar; if Plan 02 has restyled the HUD, integrate; otherwise extend current `HudController`). Player death → brief slow-fade → `GameSession.ReportArenaResult(false)` (or playground respawn at spawn point when `CurrentNode == null`).
2. **`GruntChaser.cs`** — "Husk": capsule-built hunched silhouette (2-3 primitives + `WizardGear`-style assembly), speed 3.8, chases the player, melee swipe: 1.2m range, 10 dmg, 1.2s cooldown, brief windup lean (0.35s) so it's dodgeable. 55 HP.
3. **`GruntCaster.cs`** — "Cultist": robed silhouette, keeps 9-12m (kites: retreats if player <7m, NavMesh strafe every few seconds), fires an element-tinted `Projectile` at the player every 2.5s (speed 14, dmg 12, radius 0.3) with a 0.5s glow windup. 40 HP. **`Projectile` needs a team flag** so enemy shots hurt the player and ignore enemies (extend `Projectile.Spawn` with an optional target-filter; default = current behavior so `SpellCaster` calls are untouched).
4. **Attunements**: spawners assign attunements (visible via tint) so the counter-wheel matters — mixes defined per wave below.
5. **`MiniBoss.cs`** — "Court Warden" (used by boss nodes and as a wave capper): scaled-up chaser (2.2× size, 320 HP, speed 3.2) with two telegraphed abilities on a rotation: **Slam** (2.5m radius around self, 1s telegraph ring, 25 dmg) and **Summon** (raises 2 Husks, 6s cooldown gate, max 4 alive). Attuned to its region's element (Umbra in Shadow, Frost in Rimeholt).

## Part B — Objective framework (`Scripts/Arena/`)

1. **`ObjectiveDirector.cs`** — spawned by `SpellbladeBootstrap` when `GameSession.CurrentNode != null` (replace Plan 03's debug-victory stub; the `// [PHASE2-03]` hook point calls `ObjectiveDirector.Configure(node)`). Owns win/lose: victory banner (1.5s) → `ReportArenaResult(true)`; player death handled per Part A. Also spawns an objective HUD line (top-center: "Wave 2 / 4" or "Reach the far gate").
2. **`WaveSurvivalObjective.cs`** — waves scale with `difficultyTier`: T1 = 3 waves (3 Husks / 2+2 / 3+2+1 mixed attunements), T2 = 4 waves, +30% HP. 3s breather + "Wave N" splash between waves. Spawn points around the arena perimeter with a spawn-flash telegraph so nothing appears on top of the player.
3. **`WavesThenBossObjective.cs`** — the tier's waves, then a boss splash + `MiniBoss` (plus 2 Cultist adds at 50% HP).
4. **`TraversalObjective.cs`** — A-to-B gauntlet: repurpose the arena builder for a **corridor layout** — `SpellbladeBootstrap` gets a `BuildTraversalArena()` variant (marked `// [PHASE2-04]`): 3 connected chambers (~15×20 each) with door gaps, built from the same wall/pillar/dressing kit + `ShadowBiomeArt` dressing, NavMesh re-baked after build. Player enters chamber 1; a shimmering exit portal sits in chamber 3. Chambers 1-2 hold pre-placed enemy packs (mixed grunts); chamber doors are open (player CAN run past — speedrunning is legal and fun). Touch the portal → victory.
5. **Playground**: keep `Dummy` spawning exactly as-is when `CurrentNode == null`.

## Tuning starting points

Player: 120 HP, no regen in arenas (Frost's Ice Ward and Blood's lifesteal become valuable). Grunt counts above; revisit after playtest. Counter-wheel check: a Frost-attuned Cultist should die visibly faster to Umbral Lance (1.75×) and shrug off Rimeblast (0.3×) — damage numbers already color-code modifiers.

## Acceptance checklist

- [ ] Playground scene still works (dummies, no director).
- [ ] Wave node: telegraphed spawns, wave splashes, mixed attunements tinted, clearing final wave → victory → back to map, node cleared.
- [ ] Husks windup before swiping; Cultists kite and their shots damage ONLY the player; player shots never hurt other enemies' projectiles/each other incorrectly.
- [ ] Player HP bar visible; getting hit pulses vignette; death → back to map, no clear recorded.
- [ ] Boss node: waves then Warden; slam telegraph is dodgeable by walking out; summons cap at 4.
- [ ] Traversal node: corridor arena generates with NavMesh working, packs fight, portal in chamber 3 ends it; running past enemies works.
- [ ] Counter-wheel sanity: 1.75× / 0.3× visibly working against attuned grunts.
