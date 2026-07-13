using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Spellblade
{
    /// <summary>
    /// The player's spellcasting brain.
    ///   Q/W/E/R  — select the active discipline (Umbra / Frost / Storm / Blood)
    ///   Left-click — cast the active discipline's PRIMARY spell at the cursor
    ///   Shift+Left-click — secondary spell hook (Phase 2, stubbed)
    /// Enforces mana + cooldowns. Every spell aims at the cursor's ground point.
    /// </summary>
    [RequireComponent(typeof(ManaPool))]
    public class SpellCaster : MonoBehaviour
    {
        [Tooltip("Chest-height offset projectiles fire from.")]
        public float castHeight = 1.2f;

        private readonly List<Discipline> _disciplines = new();
        private readonly Dictionary<SpellSO, float> _readyAt = new(); // spell → Time.time it comes off cooldown

        private ManaPool _mana;
        private MobaController _controller;

        public IReadOnlyList<Discipline> Disciplines => _disciplines;
        public int ActiveIndex { get; private set; }
        public Discipline Active => _disciplines.Count > 0 ? _disciplines[ActiveIndex] : null;

        private void Awake()
        {
            _mana = GetComponent<ManaPool>();
            _controller = GetComponent<MobaController>();
        }

        /// <summary>Called by the bootstrap with the four generated disciplines.</summary>
        public void SetDisciplines(IEnumerable<Discipline> disciplines)
        {
            _disciplines.Clear();
            _disciplines.AddRange(disciplines);
            ActiveIndex = 0;
        }

        /// <summary>Seconds until this spell is castable again (0 = ready).</summary>
        public float CooldownRemaining(SpellSO spell)
        {
            if (spell == null || !_readyAt.TryGetValue(spell, out float t)) return 0f;
            return Mathf.Max(0f, t - Time.time);
        }

        private void Update()
        {
            HandleDisciplineKeys();
            HandleCastInput();
        }

        // -- Input -------------------------------------------------------------

        private void HandleDisciplineKeys()
        {
            var kb = Keyboard.current;
            if (kb == null || _disciplines.Count == 0) return;

            if (kb.qKey.wasPressedThisFrame) SelectDiscipline(0);
            if (kb.wKey.wasPressedThisFrame) SelectDiscipline(1);
            if (kb.eKey.wasPressedThisFrame) SelectDiscipline(2);
            if (kb.rKey.wasPressedThisFrame) SelectDiscipline(3);
        }

        private void SelectDiscipline(int index)
        {
            if (index < 0 || index >= _disciplines.Count || index == ActiveIndex) return;
            ActiveIndex = index;
        }

        private void HandleCastInput()
        {
            var mouse = Mouse.current;
            var kb = Keyboard.current;
            if (mouse == null || !mouse.leftButton.wasPressedThisFrame) return;

            bool shiftHeld = kb != null && (kb.leftShiftKey.isPressed || kb.rightShiftKey.isPressed);
            if (shiftHeld)
            {
                CastSecondary(); // clean hook, intentionally empty for Phase 1
                return;
            }

            CastPrimary();
        }

        // -- Casting -------------------------------------------------------------

        private void CastPrimary()
        {
            var spell = Active?.Primary;
            if (spell == null) return;
            if (CooldownRemaining(spell) > 0f) return;
            if (!TryGetCursorPoint(out var cursorPoint)) return;
            if (!_mana.TrySpend(spell.manaCost)) return;

            _readyAt[spell] = Time.time + spell.cooldown;
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
                    DetonateAoE(spell, cursorPoint);
                    break;
            }
        }

        private void DetonateAoE(SpellSO spell, Vector3 center)
        {
            AoeBurst.Spawn(center, spell.aoeRadius, spell.themeColor);
            SpellbladeParticles.BloodRise(center, spell.aoeRadius, spell.themeColor);

            var hits = Physics.OverlapSphere(center, spell.aoeRadius, ~0, QueryTriggerInteraction.Ignore);
            var damaged = new HashSet<Health>(); // multi-collider targets take damage once
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
                DamageNumber.Spawn(health.transform.position + Vector3.up * 2.3f, damage, mod);
            }
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

        // -- Phase 2 hooks (intentionally stubbed) ---------------------------------

        /// <summary>Shift+Left-click → the active discipline's secondary spell. Phase 2.</summary>
        private void CastSecondary()
        {
            // Hook is live and reachable; implementation lands with secondary spells.
        }

        /// <summary>Melee strike stub — the "blade" half of Spellblade. Phase 2.</summary>
        public void MeleeAttack()
        {
            // Reserved: basic melee swing with its own timing/animation.
        }
    }
}
