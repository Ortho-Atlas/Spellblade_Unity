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

## Plan 02 — Radial Spell Wheel + Full Rosters · `phase2/wheel` · 2026-07-12

**Input scheme is live:** Q/W/E/R discipline keys deleted from `SpellCaster` (W is pure movement). Scroll cycles disciplines both directions any time (accumulator handles both notched ±120 mice and trackpad micro-deltas), firing `SpellCaster.DisciplineChanged`. Hold RMB → `SpellWheelUI` (built by `HudController.Build`) scales up bottom-right (80ms ease from a 90px idle compass to 260px); drag past an 18px dead zone highlights the sector under the drag vector; release selects via `caster.SelectSpell` (dead-zone release = no change; locked slots refused). LMB casts `ActiveSpell`. Per-discipline selection is remembered (`_activeSpellIndex[]`).

**Rosters:** all 16 spells from the plan table, built in `CreateDisciplines` (`[PHASE2-02]`) with stable spellIds `umbra_1..blood_4`. New mechanics, each in its intended home:
- `SpellSO` — new data fields per the plan + TWO fields the plan's list omitted but its spell specs require: `aoeDelaySeconds` (Thunderhead's 0.8s telegraph) and `sustainHealPerSecond` (Blood Nova's 3/s). Pure data, defaults off.
- `AimType.SelfNova` + `AimType.Blink` added. Blink resolves its destination BEFORE any cost is paid (a blocked blink refunds nothing because it spends nothing), wall-clamps by raycast (passes through living things — blinking through a Husk works), then NavMesh-samples. Origin shadow-burst deals the 12 dmg.
- `Projectile` — chain support (`chainCount`/`chainRadius`, −15% damage per jump, already-zapped roots are passed through, player arcs never bounce back to the player) + `lifestealPercent` heals the owner per hit. Enemy `PlayerOnly` bolts unaffected.
- `Health` — `Heal`, `SpendHealth` (shield-bypassing, never lethal — Crimson Pact is refused at ≤16 HP), `AddShield(amount, duration)` absorbing before HP (refresh, not stack). `ManaPool.Restore`. `WasdController.SpeedMultiplier` hook (Zephyr ×1.45).
- `Scripts/Player/SpellEffects.cs` — `HasteEffect` (refreshing timer + wind wisps), `WardVisual` (icy bubble lives while the shield holds), `BloodSustain` (heals while ≥1 DoT-afflicted target lives).
- `Scripts/Core/ProgressionGate.cs` — per plan: playground unlocks all; arena unlocks `*_1`; PLUS it already honors `SaveSystem.Data.unlockedSpells`, so **Plan 05 likely doesn't need to touch this file — just write ids into the save**.
- Thunderhead reuses Plan 04's `TelegraphRing` for its ground telegraph.

**HUD:** ability bar replaced by 4 element-colored discipline dots (active enlarged + lit) above the mana bar; HP bar unchanged; cooldown sweeps render as radial drains inside each wheel sector. `HudController.Build` signature unchanged.

**Verified:** Unity 6000.5.3f1 batchmode — 0 errors, 0 warnings. No Play-mode run (headless; Ryan's Wave-1 play test predates this branch). The wheel is the most feel-sensitive thing built so far — dead-zone size, ease timing, sector readability all warrant an in-editor pass.

**Deviations / notes:**
1. Two extra SpellSO data fields beyond the plan's list (see above) — required by the plan's own spell table.
2. Wheel sectors are radial-filled soft-disc sprites (pie wedges with feathered edges + gaps), not donut meshes — the sanctioned "4 rotated radial-filled Images" option.
3. `Discipline.keyLabel` now holds "1".."4" (labels are vestigial — nothing binds keys to disciplines anymore).
4. Wheel scale/spin animations use unscaled per-frame lerps on the HUD overlay canvas; no EventSystem needed (input read directly from `Mouse.current`, all wheel graphics non-raycast).
5. `SpellCaster.MeleeAttack()` stub kept (API stability) though real melee is Plan 01's `MeleeStrike`.

**For Plan 05:** unlock = append spellId to `SaveSystem.Data.unlockedSpells` (gate + wheel lock glyphs react automatically); spell ranks can hook `SpellCaster.ActiveSpell` reads; `MeleeStrike.damageMultiplier` + `EnemyBase.OnKilled` still the other hooks.

---

## Plan 05 — Progression, Save & The Sanctum · `phase2/progression` · 2026-07-12 · PHASE 2 COMPLETE

**Earning (`ObjectiveDirector.Victory`):** T1 20/8 · T2 30/12 · boss 60/25 Essence (first/replay); Shards 2 first-clear, 5 boss first-clear, +1 boss replay (read "mini-boss-capped wave node" as = boss nodes). Computed BEFORE `ReportArenaResult` marks the clear; reward toast + "New gear:" line stack under the VICTORY splash (`ShowSplash` gained a `yAnchor`). Saved immediately.

**Unlocks & ranks:** `ProgressionGate` needed ZERO changes — Plan 02 already made it save-backed; the Sanctum just writes ids into `unlockedSpells`. Ranks live in `spellRanks` ("id:rank"); `ProgressionMath.Apply` mutates the RUNTIME roster (SpellLibrary hands out fresh instances every load — the authored values are untouchable by construction). II = +20% (damage/DoT/shield/manaRestore), III = +40% + signature bumps: Lance pierces 1 (new `SpellSO.pierceCount` + Projectile pierce support), Shadowstep 11m, Rimeblast slow→0.6s root, Ice Ward 70, Chain Spark +2, Thunderhead r3.2, Hemorrhage r3.5, Leech 60%. Wheel shows rank pips (II/III, arena only) and shard prices on locked slots.

**Stats (Essence, 10×level, 5 levels):** Vitality +12 HP · Attunement +10 mana · Meditation +1.2 regen · Swiftness +0.2 speed (applied to `agent.speed` — WasdController drives from it) · Bladecraft +15% melee. Applied by `ProgressionMath.ApplyArenaLoadout` from ONE marked bootstrap line, ARENA ONLY — playground stays a baseline testbed with everything unlocked at rank I.

**Gear (feats, never currency):** 7 items in `GearCatalog` (Umbral hat, Rimeholt crown, Duelist's Plume, 2 staff crystals, 2 robe trims), granted in `GrantFeatGear` on boss/region-complete feats. One equipped item per category (equip swaps). `WizardGear.ApplyEquippedGear` renders them ADDITIVELY on the base hat/staff/robe — staff crystals are separate named pieces so DisciplineAura's "Staff Orb" retint never touches them. Cosmetics render in playground too.

**The Sanctum (`Scripts/Map/SanctumPanel.cs`):** replaces the placeholder button. Right-side 40% panel, 4 tabs — Spells (4×4 cards, unlock/rank buttons, insufficient-funds buttons disabled), Attributes (pips + buy), Gear (equip toggles; locked items shown grayed WITH their feat hint — slight upgrade over the plan's unlocked-only grid), Codex (placeholder lore, "the mists have not parted"). Header currencies + map footer refresh on every purchase; every buy saves immediately. **Roster factory moved: `SpellbladeBootstrap.CreateDisciplines` → `Scripts/Core/SpellLibrary.CreateDisciplines`** (the Sanctum needs the 16 spells on the map scene, where no SpellCaster exists); the bootstrap keeps a one-line delegate.

**Bonus mechanic (required by Rimeblast III):** enemy slows are now REAL — `EnemyBase` scales `agent.speed` by `OnSlowed` for the duration (100% = root). Rimeblast/Frost Nova bite on grunts now, not just dummies.

**Verified:** Unity 6000.5.3f1 batchmode — 0 errors, 0 warnings, first pass. Fresh-save flow verified by logic walk: missing JSON → shadow-only, 0 currencies, slot-1 spells, rank I, level-0 stats — identical baseline to pre-Plan-05 arenas, so completability is unchanged. In-editor economy pass recommended (earn → spend → next arena).

**Deviations / notes:**
1. Duelist's Plume: death currently ENDS an arena (defeat), so any boss victory is definitionally deathless — the `PlayerDiedThisArena` flag is wired for future respawn mechanics but is always false at victory today. The plume is effectively "clear any boss."
2. Rewards granted at the victory banner; Esc during the 1.5s banner still returns without the clear recorded, but currencies keep (earned fair and square).
3. Robe trims require the region's boss node cleared too ("all nodes in a region" read literally).
4. Sanctum tabs have no ScrollRect — all four fit 1080p reference; add one if content grows.
5. `SaveSystem._data` caches statically: deleting the JSON mid-session doesn't reset until domain reload (checklist's "survive editor restart" framing is the correct test).

**Phase 2 status: all 5 plans shipped.** Merge order held (01→03→04→02→05). Remaining known polish: melee swing animation, HDR beacons if map bloom underwhelms, wave-3 composition ("3+2+1") interpretation, Sanctum ScrollRect, and Ryan's lore brain-dump into RegionDefs + Codex.

---

## Post-Phase-2 — Unified bottom HUD bar + MAP button · `phase2/arena-hud` · 2026-07-12 (Ryan's request, no plan doc)

**Ask:** clickable UI to get back to the map / pick another arena, and a bottom bar showing selected spell, health, mana, etc.

**Shipped (`HudController` restructure + one `MeleeStrike` property):**
- **Bottom bar** (660×96, bottom-center): active spell card (discipline-tinted frame, spell initial, name, mana cost, LMB hint, vertical cooldown drain + countdown — mirrors the wheel selection live), melee card (crossed-blades glyph, SPACE hint, cooldown drain fed by new `MeleeStrike.CooldownRemaining`), HP bar with numbers (shows `(+N)` while an Ice Ward shield holds), mana bar with numbers, discipline dots + SCROLL hint. The wheel stays bottom-right, no overlap at reference resolution.
- **MAP button** (top-right, "abandon arena" subtext in arena mode): arena → `GameSession.ReportArenaResult(false)` (identical semantics to Esc — no clear recorded); playground → loads WorldMap directly. Arena scenes now get an `EventSystem` (`InputSystemUIInputModule`) since the HUD has its first clickable element.
- Arena-selection-from-UI = this button + the existing map node flow; no separate arena picker built.

**Verified:** Unity 6000.5.3f1 batchmode — 0 errors, 0 warnings. Layout numbers are reference-resolution reasoning, not eyeballed — worth a look in Play mode.

---

## Post-Phase-2 — Biome style system + The Verdant Deep (preview only) · `phase2/verdant-biome` · 2026-07-12 (Ryan's request)

**Ask:** arenas shouldn't all be Shadow gloom — show the earth-magic biome. **Ryan's call: preview only** — no map nodes, region stays locked until the lore brain-dump settles regions/elements.

**Shipped:**
- **`BiomeStyle`** (`Scripts/Art/`) — arena look is now data: mood lighting, fog, sun, ground/wall/pillar palette, camera background, post grade (filter/saturation/contrast/vignette), and a dressing builder, resolved per `regionId` with Shadow as the fallback. `SpellbladeBootstrap` reads it everywhere it used to hardcode Shadow (all marked `[BIOME]`); the `fogDensity`/`moonIntensity` public fields were absorbed into the style. **Arena nodes automatically get their region's style** — when Frost or any future region gets a kit, it's one new `BiomeStyle` entry + one dressing class, zero bootstrap changes.
- **`VerdantBiomeArt`** — the earth biome: warm gold sun through canopy haze, loam ground with moss patches, mossy boulders and fallen logs (pathing-blocking), a great root arch, rune stones pulsing green (reuses `RunePulse`), glowing mushroom clusters with soft point lights, and rising spore motes (the Verdant answer to the Shadow ground mist, `gravityModifier` negative — spores rise, mist sinks).
- **Preview access:** menu **Spellblade → Create Verdant Playground Scene** → `Assets/Spellblade/Spellblade Playground (Verdant).unity` (generated + committed, ready to open) — a playground whose bootstrap has `playgroundBiome = "verdant"`. The field is a public string on the bootstrap, so any biome can be previewed from the Inspector.

**Explicitly NOT done (deliberate):** no Verdant map nodes, no unlock wiring, no fifth "Earth" element — the counter-wheel stays Umbra/Frost/Storm/Blood. Verdant enemies (when they exist) would use mixed attunements until the lore pass decides whether Earth joins the wheel. `TraversalArena` (the corridor) is still Shadow-styled — restyle it when a non-Shadow region actually gets a Traversal node.

**Verified:** Unity 6000.5.3f1 batchmode — 0 errors, 0 warnings; Verdant preview scene generated headlessly and committed.

**Update (same session, Ryan's follow-up):** preview-only wasn't enough — Ryan wants the Deep ON the map. Shipped in the same branch:
- 4 Verdant nodes: `verdant_01` Mossgrave Hollow (WaveSurvival T1) · `verdant_02` The Rootway (Traversal T1) · `verdant_03` Elderbough Ring (WaveSurvival T2) · `verdant_boss` Heart of the Deep (WavesThenBoss T3). Standard boss gate (clear 3).
- New `RegionDef.unlockedFromStart` flag (checked by `GameSession.IsRegionUnlocked`) — Verdant is open on every save with NO migration; re-gate later by flipping the flag and setting `unlockedBy = {"shadow"}`.
- `TraversalArena.Build` now takes the `BiomeStyle` — the corridor wears its region's palette + ambient particles (new `BiomeStyle.buildAmbientParticles`: mist for Shadow, spores for Verdant). The Rootway is the first non-Shadow corridor.
- Map: green-gold spore flourish over the unlocked Deep; nodes/labels appear automatically (generic region rendering).
- Gear parity: `staff_crystal_verdant` (boss feat) + `robe_tint_verdant` (clear all 4) in the catalog, granted by the director, rendered in biome green (no Earth element exists — counter-wheel untouched; the Verdant Warden is Umbra-attuned via the `ElementFor` fallback until the lore pass).

**Verified:** batchmode compile after the map wiring — 0 errors, 0 warnings.

**Update (same session) — Verdant v2, "trees and open in a field in nature" (Ryan):** the walled-courtyard look is gone for earth. New `BiomeStyle.openField` flag: open-field biomes skip perimeter walls AND interior pillars in the bootstrap. `VerdantBiomeArt` rebuilt as an open sunlit meadow: two staggered treeline rings (~32 procedural trees — tapered trunk with collider, angled branches, 3 canopy lobes, per-tree random height/lean/green; canopy bottoms kept ~3m up so the bake never reads them as ceilings), 4 field trees as bolt cover, ~44 grass tufts + 16 glowing wildflowers, one nature-claimed rune monolith + mushroom clusters, spores unchanged. **Boundary = NavMesh, not geometry:** a 3× outer grass plane carries `NavMeshModifier` area NotWalkable — agents stay in the meadow, the eye wanders into the trees. Mood opened up: sun 1.1 warm gold, fog 0.010 sunlit haze, full saturation. Shadow untouched (openField=false). Batchmode verified — 0 errors, 0 warnings.

---
