# Plan 05 — Progression, Save & The Sanctum

*Branch: `phase2/progression` · Wave 3 (run LAST, after 02 + 03 merge) · Final merge*

## Session prompt (paste into Claude Code)

> Read `Docs/Plans/00-MASTER-OVERVIEW.md` and `Docs/Plans/05-PROGRESSION-SANCTUM.md`, then implement this plan exactly. Work on branch `phase2/progression` cut from main AFTER plans 01-04 are merged. Compile clean, smoke-test per the acceptance checklist, commit, and append a handoff note to `Docs/Plans/HANDOFF.md`.

## Goal

Make clearing arenas FEED something. Four tracks — new spells, spell ranks, stat upgrades, cosmetic gear — bought at **the Sanctum**, a panel on the world map. Persistent via Plan 03's `SaveSystem`.

## Part A — Earning

`ObjectiveDirector` victory grants (first clear / replay): T1 node 20/8 Essence, T2 30/12, boss 60/25; Element Shards: 2 first-clear per node, 5 per boss, +1 per mini-boss-capped wave node replay. Reward toast on the victory banner ("+30 Essence · +2 Shards"). Store in `SaveSystem.Data.arcaneEssence/elementShards`.

## Part B — Spell unlocks & ranks

1. **Unlocks**: each discipline's slot-1 spell is free; slots 2/3/4 cost 4/6/9 Shards. Replace Plan 02's stub `ProgressionGate.IsUnlocked(spellId)` with real save-backed checks (keep the playground-mode bypass).
2. **Ranks I→III**: rank II costs 5 Shards (+20% damage or effect); rank III costs 10 (+40% total AND one behavior bump per spell — pick sensible ones, e.g. Umbral Lance III pierces one enemy, Rimeblast III slow→brief root 0.6s, Chain Spark III +2 chains, Leech Bolt III 60% lifesteal, Thunderhead III radius 3.2, Ice Ward III 70 absorb, Hemorrhage III radius 3.5, Shadowstep III 11m). Implement as `ProgressionMath.Apply(spell, rank)` mutating a runtime CLONE of each `SpellSO` at arena load — never mutate the authored roster.
3. Wheel integration: locked slots show shard cost in the tooltip; rank pips (I/II/III) render on wheel sectors.

## Part C — Stat upgrades (Essence)

Five tracks, 5 levels each, cost 10 × level (10/20/30/40/50): **Vitality** +12 max HP/level · **Attunement** +10 max mana/level · **Meditation** +1.2 mana regen/level · **Swiftness** +0.2 move speed/level · **Bladecraft** +15% melee damage/level. Apply on arena load from save (`WasdController.moveSpeed`, `ManaPool` fields, `Health` max, `MeleeStrike.baseDamage` — all already public).

## Part D — Gear cosmetics (`WizardGear` variants)

Unlock by feats, not currency: **Hat of the Umbral Court** (clear Shadow boss — taller hat, violet band glow) · **Rimeholt Crown** (clear Frost boss — icy circlet) · **Duelist's Plume** (any boss without dying — feather) · **Staff crystals** per region boss (crystal color swap) · **Robe tints** (clear all nodes in a region — region-tinted trim). Extend `WizardGear.Dress` to read `equippedGear` from save; equip toggles in the Sanctum. Feat detection in `ObjectiveDirector` (track deaths-this-arena flag).

## Part E — The Sanctum (map-screen panel)

Replace Plan 03's placeholder button. A slide-in panel (right side, ~40% width) with four tabs: **Spells** (4 discipline columns, wheel-slot cards with unlock/rank buttons), **Attributes** (5 stat rows with level pips + buy button), **Gear** (unlocked cosmetics grid, click to equip), **Codex** (stub tab: region lore paragraphs — placeholder text now, Ryan's brain-dump content later). Currencies always visible in the panel header. Buy = immediate save.

## Acceptance checklist

- [ ] Clear a node → reward toast → currencies visible on map footer; values survive editor restart.
- [ ] Sanctum: buy a spell slot → it's selectable on the wheel in the next arena; insufficient shards → button disabled, no error.
- [ ] Rank up a spell → damage numbers reflect it; rank III behavior bumps work (spot-check 3 spells); playground mode unaffected (everything unlocked, base ranks).
- [ ] Buy Vitality/Swiftness → visibly more HP / faster movement in the next arena.
- [ ] Beat the Shadow boss → hat unlocks; equip in Sanctum → hat renders in arena; survives restart.
- [ ] Fresh-save flow: delete the JSON → new game starts with slot-1 spells only, 0 currencies, shadow region only — and is completable.
