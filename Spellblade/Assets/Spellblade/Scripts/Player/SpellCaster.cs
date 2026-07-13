using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Spellblade
{
    /// <summary>
    /// The player's spellcasting brain (Plan 02 owns this file).
    ///   Mouse scroll — cycle the active discipline wheel (Umbra→Frost→Storm→Blood)
    ///   Hold RMB    — SpellWheelUI opens; drag + release selects the active spell
    ///   LMB         — cast the active spell at the cursor
    ///   Shift+LMB   — secondary spell hook (still stubbed)
    /// Q/W/E/R discipline keys are GONE — W belongs to movement now.
    /// Enforces mana, cooldowns, unlock gating, and the new Phase 2 aim types
    /// (SelfNova, Blink, delayed PointAoE) plus chain/lifesteal/haste/shield/
    /// blood-price mechanics.
    /// </summary>
    [RequireComponent(typeof(ManaPool))]
    public class SpellCaster : MonoBehaviour
    {
        [Tooltip("Chest-height offset projectiles fire from.")]
        public float castHeight = 1.2f;

        /// <summary>Fired with the new ActiveIndex whenever scroll changes discipline.</summary>
        public event System.Action<int> DisciplineChanged;

        private readonly List<Discipline> _disciplines = new();
        private readonly Dictionary<SpellSO, float> _readyAt = new(); // spell → Time.time it comes off cooldown
        private int[] _activeSpellIndex = System.Array.Empty<int>();  // remembered per discipline

        private ManaPool _mana;
        private Health _health;
        private NavMeshAgent _agent;
        private WasdController _controller; // [PHASE2-01] type swap only — MobaController deleted
        private float _scrollAccumulator;

        public IReadOnlyList<Discipline> Disciplines => _disciplines;
        public int ActiveIndex { get; private set; }
        public Discipline Active => _disciplines.Count > 0 ? _disciplines[ActiveIndex] : null;

        /// <summary>The selected slot within the ACTIVE discipline's wheel.</summary>
        public int ActiveSpellIndex =>
            _activeSpellIndex.Length > ActiveIndex ? _activeSpellIndex[ActiveIndex] : 0;

        /// <summary>The spell LMB will cast right now.</summary>
        public SpellSO ActiveSpell
        {
            get
            {
                var d = Active;
                if (d == null || d.spells.Count == 0) return null;
                int index = Mathf.Clamp(ActiveSpellIndex, 0, d.spells.Count - 1);
                return d.spells[index];
            }
        }

        private void Awake()
        {
            _mana = GetComponent<ManaPool>();
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            _controller = GetComponent<WasdController>(); // [PHASE2-01] type swap only
        }

        /// <summary>Called by the bootstrap with the four generated disciplines.</summary>
        public void SetDisciplines(IEnumerable<Discipline> disciplines)
        {
            _disciplines.Clear();
            _disciplines.AddRange(disciplines);
            ActiveIndex = 0;
            _activeSpellIndex = new int[_disciplines.Count]; // slot 0 everywhere
        }

        /// <summary>Seconds until this spell is castable again (0 = ready).</summary>
        public float CooldownRemaining(SpellSO spell)
        {
            if (spell == null || !_readyAt.TryGetValue(spell, out float t)) return 0f;
            return Mathf.Max(0f, t - Time.time);
        }

        /// <summary>The wheel's selection entry point. Refuses locked slots.
        /// Returns true when the selection changed (or was already set).</summary>
        public bool SelectSpell(int disciplineIndex, int spellIndex)
        {
            if (disciplineIndex < 0 || disciplineIndex >= _disciplines.Count) return false;
            var discipline = _disciplines[disciplineIndex];
            if (spellIndex < 0 || spellIndex >= discipline.spells.Count) return false;
            if (!ProgressionGate.IsUnlocked(discipline.spells[spellIndex].spellId)) return false;

            _activeSpellIndex[disciplineIndex] = spellIndex;
            return true;
        }

        private void Update()
        {
            HandleScroll();
            HandleCastInput();
        }

        // -- Input -------------------------------------------------------------

        /// <summary>Scroll cycles the discipline wheel any time. Handles both
        /// notched mice (±120 per notch, Windows-style) and fine-grained
        /// trackpad deltas via an accumulator.</summary>
        private void HandleScroll()
        {
            var mouse = Mouse.current;
            if (mouse == null || _disciplines.Count == 0) return;

            float y = mouse.scroll.ReadValue().y;
            if (Mathf.Abs(y) > 0.01f) _scrollAccumulator += y;

            float notch = Mathf.Abs(_scrollAccumulator) >= 100f ? 120f : 1f;
            while (_scrollAccumulator <= -notch) { _scrollAccumulator += notch; StepDiscipline(1); }
            while (_scrollAccumulator >= notch) { _scrollAccumulator -= notch; StepDiscipline(-1); }
        }

        private void StepDiscipline(int direction)
        {
            if (_disciplines.Count == 0) return;
            ActiveIndex = (ActiveIndex + direction + _disciplines.Count) % _disciplines.Count;
            DisciplineChanged?.Invoke(ActiveIndex);
        }

        private void HandleCastInput()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            bool shiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (shiftHeld)
            {
                CastSecondary(); // clean hook, intentionally empty until secondary spells land
                return;
            }

            CastActive();
        }

        // -- Casting -------------------------------------------------------------

        private void CastActive()
        {
            var spell = ActiveSpell;
            if (spell == null) return;
            if (!ProgressionGate.IsUnlocked(spell.spellId)) return;
            if (CooldownRemaining(spell) > 0f) return;
            if (!TryGetCursorPoint(out var cursorPoint)) return;

            // Blood price: never castable if it would leave nothing to bleed.
            if (spell.healthCost > 0f && (_health == null || _health.Current <= spell.healthCost + 1f)) return;

            // Blink resolves its destination BEFORE any cost is paid — a blink
            // with nowhere to go must not eat mana.
            Vector3 blinkTarget = default;
            if (spell.aimType == AimType.Blink && !TryResolveBlink(spell, cursorPoint, out blinkTarget)) return;

            if (!_mana.TrySpend(spell.manaCost)) return;

            _readyAt[spell] = Time.time + spell.cooldown;
            if (spell.healthCost > 0f) _health.SpendHealth(spell.healthCost);
            if (spell.manaRestore > 0f) _mana.Restore(spell.manaRestore);
            if (spell.aimType == AimType.LineSkillshot || spell.aimType == AimType.PointAoE)
                _controller?.FaceToward(cursorPoint);

            // Cast flash at the hand — quick colored spark pop.
            SpellbladeParticles.Burst(transform.position + Vector3.up * castHeight + transform.forward * 0.5f,
                                      spell.themeColor, 14, 3.5f, 0.1f);

            switch (spell.aimType)
            {
                case AimType.LineSkillshot:
                    var origin = transform.position + Vector3.up * castHeight;
                    var dir = cursorPoint - transform.position;
                    dir.y = 0f; // skillshots travel flat
                    if (dir.sqrMagnitude < 0.001f) dir = transform.forward;
                    Projectile.Spawn(spell, origin + dir.normalized * 0.7f, dir, transform);
                    break;

                case AimType.PointAoE:
                    // Clamp the target point to the spell's max range.
                    var toTarget = cursorPoint - transform.position;
                    toTarget.y = 0f;
                    if (toTarget.magnitude > spell.range)
                        cursorPoint = transform.position + toTarget.normalized * spell.range;
                    cursorPoint.y = transform.position.y;

                    if (spell.aoeDelaySeconds > 0f) // [PHASE2-02] Thunderhead telegraph
                        StartCoroutine(DelayedDetonate(spell, cursorPoint));
                    else
                        DetonateAoE(spell, cursorPoint);
                    break;

                case AimType.SelfNova: // [PHASE2-02]
                    CastSelfNova(spell);
                    break;

                case AimType.Blink: // [PHASE2-02]
                    CastBlink(spell, blinkTarget);
                    break;
            }
        }

        // -- Phase 2 aim types ------------------------------------------------ [PHASE2-02]

        private IEnumerator DelayedDetonate(SpellSO spell, Vector3 point)
        {
            TelegraphRing.Spawn(point, spell.aoeRadius, spell.themeColor, spell.aoeDelaySeconds);
            yield return new WaitForSeconds(spell.aoeDelaySeconds);
            DetonateAoE(spell, point);
        }

        private void CastSelfNova(SpellSO spell)
        {
            float radius = spell.selfRadius > 0f ? spell.selfRadius : spell.aoeRadius;
            var center = transform.position;

            // Every nova shows its burst — Zephyr's is pure wind, no damage behind it.
            AoeBurst.Spawn(center, radius, spell.themeColor);
            SpellbladeParticles.Burst(center + Vector3.up, spell.themeColor, 24, 5f, 0.13f);

            List<Health> struck = null;
            if (spell.damage > 0f || spell.dotDamagePerSecond > 0f)
                struck = DetonateAoE(spell, center, radius, showBurst: false);

            if (spell.hasteMultiplier > 1f)
                HasteEffect.Apply(gameObject, spell.hasteMultiplier, spell.hasteDuration, spell.themeColor);

            if (spell.shieldAmount > 0f && _health != null)
            {
                _health.AddShield(spell.shieldAmount, spell.shieldDuration);
                WardVisual.Attach(transform, spell.themeColor);
            }

            if (spell.sustainHealPerSecond > 0f && struck != null && struck.Count > 0)
                BloodSustain.Apply(gameObject, struck, spell.sustainHealPerSecond, spell.dotDuration);
        }

        /// <summary>Blink destination: toward the cursor up to blinkDistance, wall-
        /// clamped by raycast (never through stone), then NavMesh-sampled.</summary>
        private bool TryResolveBlink(SpellSO spell, Vector3 cursorPoint, out Vector3 target)
        {
            target = default;

            var dir = cursorPoint - transform.position;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) return false;

            float distance = Mathf.Min(dir.magnitude, spell.blinkDistance);
            dir.Normalize();

            // Walls clamp the path — enemies don't (blinking THROUGH a Husk is the fantasy).
            var from = transform.position + Vector3.up * 1f;
            var blocks = Physics.RaycastAll(from, dir, distance + 0.5f, ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(blocks, (a, b) => a.distance.CompareTo(b.distance));
            foreach (var block in blocks)
            {
                if (block.transform.root == transform.root) continue;
                if (block.collider.GetComponentInParent<Health>() != null) continue; // pass through the living
                distance = Mathf.Min(distance, Mathf.Max(0f, block.distance - 0.6f));
                break;
            }
            if (distance < 0.5f) return false;

            var point = transform.position + dir * distance;
            if (!NavMesh.SamplePosition(point, out var navHit, 2f, NavMesh.AllAreas)) return false;

            target = navHit.position;
            return true;
        }

        private void CastBlink(SpellSO spell, Vector3 target)
        {
            var origin = transform.position;

            // Shadow-burst left behind at the departure point.
            SpellbladeParticles.Burst(origin + Vector3.up, spell.themeColor, 26, 5f, 0.13f);
            if (spell.damage > 0f)
                DetonateAoE(spell, origin, spell.selfRadius > 0f ? spell.selfRadius : 2f, showBurst: true);

            if (_agent != null) _agent.Warp(target);
            else transform.position = target;
            _controller?.FaceToward(target + (target - origin)); // keep facing the travel direction

            SpellbladeFx.Flash(target + Vector3.up, spell.themeColor, 0.8f, 0.25f);
        }

        /// <summary>Shared detonation: visuals + counter-wheel damage + debuffs.
        /// Returns every Health struck (Blood Nova's sustain tracks them).</summary>
        private List<Health> DetonateAoE(SpellSO spell, Vector3 center, float radiusOverride = -1f,
                                         bool showBurst = true)
        {
            float radius = radiusOverride > 0f ? radiusOverride : spell.aoeRadius;

            if (showBurst)
            {
                AoeBurst.Spawn(center, radius, spell.themeColor);
                SpellbladeParticles.BloodRise(center, radius, spell.themeColor);
            }

            var hits = Physics.OverlapSphere(center, radius, ~0, QueryTriggerInteraction.Ignore);
            var damaged = new HashSet<Health>(); // multi-collider targets take damage once
            var struck = new List<Health>();
            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue;
                var health = hit.GetComponentInParent<Health>();
                if (health == null || health.IsDead || !damaged.Add(health)) continue;

                // Counter-wheel: scale by the target's elemental attunement.
                var affinity = hit.GetComponentInParent<ElementalAffinity>();
                float mod = affinity != null ? affinity.ModifierFor(spell.element) : 1f;

                float damage = spell.damage * mod;
                health.TakeDamage(damage);
                health.ApplyDot(spell.dotDamagePerSecond * mod, spell.dotDuration);
                health.ApplySlow(spell.slowPercent, spell.slowDuration);
                if (damage > 0f) DamageNumber.Spawn(health.transform.position + Vector3.up * 2.3f, damage, mod);
                struck.Add(health);
            }
            return struck;
        }

        /// <summary>Cursor → point on the player's ground plane (stable aim even off-mesh).</summary>
        private bool TryGetCursorPoint(out Vector3 point)
        {
            point = default;
            var cam = Camera.main;
            var mouse = Mouse.current;
            if (cam == null || mouse == null) return false;

            var ray = cam.ScreenPointToRay(mouse.position.ReadValue());
            var aimPlane = new Plane(Vector3.up, new Vector3(0f, transform.position.y, 0f));
            if (!aimPlane.Raycast(ray, out float dist)) return false;

            point = ray.GetPoint(dist);
            return true;
        }

        // -- Later-phase hooks (intentionally stubbed) ---------------------------------

        /// <summary>Shift+Left-click → a secondary-cast hook. Still reserved.</summary>
        private void CastSecondary()
        {
            // Hook is live and reachable; implementation lands with secondary spells.
        }

        /// <summary>Melee strike hook retained for API stability — the real melee
        /// lives in MeleeStrike (Plan 01).</summary>
        public void MeleeAttack()
        {
        }
    }
}
