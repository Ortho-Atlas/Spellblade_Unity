# Plan 02 — Radial Spell Wheel + Full Spell Rosters

*Branch: `phase2/wheel` · Wave 2 (run AFTER Plan 01 merges) · Merges after 01/03/04*

## Session prompt (paste into Claude Code)

> Read `Docs/Plans/00-MASTER-OVERVIEW.md` and `Docs/Plans/02-SPELL-WHEEL-ROSTERS.md`, then implement this plan exactly. Work on branch `phase2/wheel` cut from main AFTER `phase2/movement` is merged. Compile clean, smoke-test per the acceptance checklist, commit, and append a handoff note to `Docs/Plans/HANDOFF.md`.

## Goal

Replace Q/W/E/R discipline selection with the signature input scheme:

- **Hold RMB** → a radial spell wheel fades in at the **bottom-right** of the screen showing the current discipline's 4 spell slots.
- While holding, **drag the mouse** — the drag direction (relative to where the hold began) highlights the wheel sector it points at. Small dead zone (~18px) keeps the current selection.
- **Release RMB** → the highlighted spell becomes the ACTIVE spell. Wheel fades out. (Releasing inside the dead zone = keep previous selection, no change.)
- **LMB** still casts the active spell at the cursor, exactly as today.
- **Mouse scroll** cycles the active discipline: Umbra → Frost → Storm → Blood → Umbra (scroll down; scroll up reverses). Works any time, including while the wheel is open (wheel re-skins in place to the new discipline's color/spells).

Also: expand every discipline from 1 spell to a full roster of 4, so the wheel means something.

## Part A — Input & selection (`SpellCaster` rework)

Plan 02 **owns `SpellCaster`**. Changes:

1. Delete `HandleDisciplineKeys()` (Q/W/E/R — W is movement now).
2. Add scroll handling: `Mouse.current.scroll.ReadValue().y` accumulates; each ±120 (one notch) steps `ActiveIndex` around the discipline list. Expose `event Action<int> DisciplineChanged`.
3. Add active-SPELL state: `ActiveSpellIndex` per discipline (persisted per discipline, so flipping wheels remembers each wheel's selection). `Active.Primary` usages become `ActiveSpell`.
4. RMB hold/drag/release is read by the new `SpellWheelUI` (below), which calls `caster.SelectSpell(disciplineIndex, spellIndex)`.
5. Casting: unchanged flow (mana, cooldown, aim types) but now supports the new aim types in Part B. Locked spell slots (Plan 05 integration): if `SpellSO.spellId` is not unlocked, the wheel shows the slot dimmed with a lock glyph and `SelectSpell` refuses. Until Plan 05 merges, `ProgressionGate.IsUnlocked(spellId)` lives in `Scripts/Core/ProgressionGate.cs` (new, tiny) and returns TRUE for spell index 0 of each discipline and FALSE otherwise **unless** `GameSession.CurrentNode == null` (playground mode → everything unlocked for testing).

## Part B — Spell rosters (created in `SpellbladeBootstrap.CreateDisciplines`, marked `// [PHASE2-02]`)

New `SpellSO` fields (all default 0/off): `spellId`, `selfRadius`, `blinkDistance`, `chainCount`, `chainRadius`, `lifestealPercent`, `hasteMultiplier`, `hasteDuration`, `shieldAmount`, `shieldDuration`, `healthCost`, `manaRestore`. New `AimType` values: `SelfNova` (detonates centered on caster), `Blink` (teleport toward cursor up to `blinkDistance`, NavMesh-sampled).

Wheel slot order = index 0 top, then clockwise: right, bottom, left.

| Slot | UMBRA (violet — burst/assassin) | FROST (cyan — control) | STORM (pale blue — speed/chain) | BLOOD (crimson — DoT/lifesteal) |
|---|---|---|---|---|
| 1 | **Umbral Lance** (existing) | **Rimeblast** (existing) | **Tempest Bolt** (existing) | **Hemorrhage** (existing) |
| 2 | **Shadowstep** — Blink 8m, leaves a shadow-burst at origin (12 dmg SelfNova there). 20 mana, 6s cd | **Glacial Spike** — slow heavy skillshot: speed 14, dmg 46, range 14, radius 0.6. 30 mana, 8s cd | **Chain Spark** — skillshot, on hit jumps to nearest enemy ≤5m, up to 3 chains, dmg 20 (–15%/jump). 24 mana, 5s cd | **Leech Bolt** — skillshot, dmg 24, heals caster 40% of damage dealt. 22 mana, 4s cd |
| 3 | **Creeping Dark** — PointAoE, radius 3.5, dmg 10 + DoT 10/s for 3s. 26 mana, 7s cd | **Frost Nova** — SelfNova radius 4, dmg 18, slow 45% for 3s. 28 mana, 9s cd | **Zephyr Rush** — self haste ×1.45 for 3.5s (SelfNova, 0 dmg, wind burst VFX). 18 mana, 10s cd | **Crimson Pact** — self: costs 15 HP (`healthCost`), restores 40 mana (`manaRestore`). 0 mana, 12s cd |
| 4 | **Night Nova** — SelfNova radius 3.2, dmg 34. 32 mana, 9s cd | **Ice Ward** — self shield absorbing 40 dmg for 6s (`Health` gains shield support). 26 mana, 14s cd | **Thunderhead** — PointAoE, 0.8s telegraphed delay ring then strike: dmg 44, radius 2.5. 30 mana, 8s cd | **Blood Nova** — SelfNova radius 3.5, dmg 16 + DoT 9/s for 4s, heals 3/s while DoT ticks on ≥1 enemy. 34 mana, 11s cd |

Implementation notes: chain/lifesteal/haste/shield/delay behaviors go in small dedicated components or `Projectile`/`SpellCaster` extensions — keep `SpellSO` pure data. `Health` gets `AddShield(amount, duration)` absorbing damage before HP. Haste multiplies `WasdController` speed via a public modifier hook. Every spell keeps the discipline theme color for projectile/burst VFX (reuse `SpellbladeParticles`; Thunderhead needs a ground telegraph ring — `SpellbladeFx.Flash` scaled up works).

## Part C — Wheel UI (`Scripts/UI/SpellWheelUI.cs`, new; built in code by `HudController.Build`)

- Anchored bottom-right (~260px diameter, 40px margin). Four sectors (donut segments — build from generated sprite meshes or 4 rotated radial-filled Images), each showing spell initial letter now / icon later, mana cost, and a radial cooldown sweep overlay.
- Idle state (RMB not held): small — a 90px "compass" version showing the 4 slots + active spell highlighted + discipline color ring. Hold RMB → scales up with a ~80ms ease; release → eases back.
- Drag highlight: sector under the drag vector glows (theme color, bloom-friendly emissive-look color), others dim. Dead zone keeps current.
- Scroll feedback: whole wheel does a quick 90° spin-tick and re-tints to the new discipline color; discipline name flashes above the wheel ("FROST").
- Locked slots: dimmed + lock glyph (see Part A).
- `HudController`: replace the Q/W/E/R discipline bar with a slim discipline indicator (4 dots in element colors, active one lit) + keep mana bar. Cooldown display moves onto the wheel itself.

## Acceptance checklist

- [ ] Hold RMB: wheel scales up bottom-right; drag highlights sectors; release selects; LMB casts the selected spell.
- [ ] Release inside dead zone keeps the prior selection.
- [ ] Scroll cycles disciplines both directions, any time; wheel re-tints; each wheel remembers its own selected slot.
- [ ] Q/W/E/R no longer changes disciplines; W only moves.
- [ ] All 16 spells cast correctly in playground mode (unlock-gate bypassed when `GameSession.CurrentNode == null`): blink teleports (never through walls — NavMesh sample), chains jump, leech heals, pact trades HP→mana, shield absorbs, thunderhead telegraphs, haste speeds movement then expires.
- [ ] Cooldown sweeps render per slot; casting with an on-cooldown or unaffordable spell does nothing (no errors).
- [ ] No regression: counter-wheel damage modifiers + damage numbers still correct.
