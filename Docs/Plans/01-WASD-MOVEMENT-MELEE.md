# Plan 01 — WASD Movement + Basic Melee

*Branch: `phase2/movement` · Wave 1 (no dependencies) · Merges FIRST*

## Session prompt (paste into Claude Code)

> Read `Docs/Plans/00-MASTER-OVERVIEW.md` and `Docs/Plans/01-WASD-MOVEMENT-MELEE.md`, then implement this plan exactly. Work on branch `phase2/movement`. The project is at `Spellblade/` (Unity 6, URP, new Input System, everything runtime-generated). Compile clean, smoke-test per the acceptance checklist, commit, and append a handoff note to `Docs/Plans/HANDOFF.md`.

## Goal

Replace right-click-to-move with direct WASD movement under the existing top-down MOBA camera, and add the basic melee strike (the "blade" of Spellblade). Right-click becomes FREE — Plan 02 claims it for the spell wheel. Do not touch `SpellCaster` (Plan 02 owns it).

## Design

- **WASD** moves the character along world axes (camera yaw is fixed at the MOBA angle, so world-relative == screen-relative; read the camera's flat forward/right anyway so a future camera rotation "just works").
- Movement stays on the **NavMeshAgent** (walls/pillars keep blocking) but driven directly: disable `updateRotation`, and each frame call `agent.Move(dir * moveSpeed * dt)` with `agent.SetDestination` no longer used. Zero input = zero drift.
- **Facing**: face movement direction (smooth turn, ~720°/s) while moving. `FaceToward(point)` remains public for `SpellCaster` cast-snapping — keep that exact method name/signature available.
- **Melee — Space**: 120° arc, 2.3m reach, in the facing direction. No mana. Cooldown 0.8s. Damage 22 (physical — NOT elemental: `ElementalAffinity` modifier does not apply; pass modifier 1.0 to damage numbers). Small 1.5m knockback impulse on grunts/dummies. Feel: quick violet-white slash arc via `SpellbladeParticles`/`SpellbladeFx` (a stretched emissive quad sweep + spark burst), camera-shake-free.
- Melee should later be upgradeable (Plan 05 reads a `damageMultiplier` static hook) — expose `MeleeStrike.baseDamage` public.

## Implementation steps

1. **`Scripts/Player/WasdController.cs`** (new) — `RequireComponent(NavMeshAgent)`. Reads `Keyboard.current` WASD (+ arrow keys as free bonus), builds a normalized direction from camera-flattened forward/right, `agent.Move`. Handles animator exactly like `MobaController.DriveAnimator` does today (`Speed`/`MotionSpeed`/`Grounded` params, Starter Assets blend tree: 0 idle / ~2 walk / ~5.3 sprint — drive `Speed` with actual velocity magnitude). Port `BindAnimator` and `FaceToward` over verbatim.
2. **`Scripts/Player/MeleeStrike.cs`** (new) — Space input, cooldown gate, `Physics.OverlapSphere` (2.3m) filtered to a 120° arc via dot product against facing, damage once per `Health` root (copy the `damaged` HashSet pattern from `SpellCaster.DetonateAoE`), knockback via `NavMeshAgent.Move` displacement or rigidbody-free positional nudge on `Dummy`. Slash VFX + `DamageNumber.Spawn` with mod 1.0.
3. **`SpellbladeBootstrap.SpawnPlayer()`** — swap `AddComponent<MobaController>()` → `AddComponent<WasdController>()`, add `AddComponent<MeleeStrike>()`. Mark with `// [PHASE2-01]`. Update the `Debug.Log` control hints line.
4. **Delete `MobaController.cs`** once `WasdController` fully replaces it (it is only referenced in `SpellbladeBootstrap` and `SpellCaster._controller`). For `SpellCaster`: do NOT edit its logic — but it does `GetComponent<MobaController>()`. To avoid touching Plan 02's file meaningfully, keep compilation working by making `WasdController` expose `FaceToward` and change the single type reference in `SpellCaster` (`MobaController` → `WasdController`, two lines, marked `// [PHASE2-01]`). That is the only permitted `SpellCaster` edit.
5. **Click-marker cleanup** — the green click flash is gone with click-to-move; remove its usage.

## Tuning starting points

`moveSpeed` stays 5.5. Turn speed 720°/s. Melee: damage 22, cooldown 0.8s, reach 2.3m, arc 120°, knockback 1.5m over 0.12s.

## Acceptance checklist

- [ ] WASD moves in all 8 directions; diagonals not faster; character can't pass walls/pillars.
- [ ] Character faces movement direction smoothly; LMB casting still snap-faces the cursor (existing `SpellCaster` behavior intact).
- [ ] Right-click does NOTHING (freed for Plan 02).
- [ ] Space melee: hits dummies in front (damage numbers), misses dummies behind; respects cooldown; works at 0 mana.
- [ ] Walk/run animation plays on the Starter Assets rig; idle when still.
- [ ] Q/W/E/R note: pressing W still swaps discipline this wave (SpellCaster untouched) — ACCEPTED interim quirk; Plan 02 removes it. Confirm it causes no errors.
- [ ] Console shows updated control hints; no warnings/errors.
