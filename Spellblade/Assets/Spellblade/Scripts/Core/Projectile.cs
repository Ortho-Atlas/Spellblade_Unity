using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>Who a projectile is allowed to hurt. Everyone = Phase 1 behavior
    /// (player shots — hit any Health except the caster). PlayerOnly = enemy
    /// shots — pass straight through enemies and dummies, hurt only the player.</summary>
    public enum ProjectileTargets { Everyone, PlayerOnly } // [PHASE2-04]

    /// <summary>
    /// A traveling skillshot. Moves in a straight line at chest height, sweeps
    /// for hits with a sphere cast (no tunneling at high speeds), applies the
    /// spell's damage/DoT/slow to the first Health it touches, and dies on
    /// walls or at max range.
    /// </summary>
    public class Projectile : MonoBehaviour
    {
        private SpellSO _spell;
        private Transform _owner;
        private Vector3 _direction;
        private float _traveled;
        private ProjectileTargets _targets; // [PHASE2-04]

        // [PHASE2-02] Chain state (Chain Spark): jumps remaining, decayed damage,
        // and every root already zapped this chain (passed through, never re-hit).
        private int _chainsLeft;
        private float _chainMult = 1f;
        private HashSet<Transform> _chainHits;

        public static Projectile Spawn(SpellSO spell, Vector3 origin, Vector3 direction, Transform owner,
                                       ProjectileTargets targets = ProjectileTargets.Everyone) // [PHASE2-04] default = untouched SpellCaster behavior
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Projectile ({spell.displayName})";
            Object.Destroy(go.GetComponent<Collider>()); // movement is swept, no physics collider needed

            go.transform.position = origin;
            go.transform.localScale = Vector3.one * (spell.projectileRadius * 2f);
            go.GetComponent<Renderer>().material =
                SpellbladeFx.MakeEmissive(spell.themeColor, spell.themeColor, 4f);

            // Glow that spills onto the arena as it flies.
            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = spell.themeColor;
            light.intensity = 4f;
            light.range = 6f;

            // Ribbon trail so fast shots read clearly.
            var trail = go.AddComponent<TrailRenderer>();
            trail.time = 0.22f;
            trail.startWidth = spell.projectileRadius * 1.6f;
            trail.endWidth = 0f;
            trail.material = SpellbladeFx.MakeUnlitTransparent(
                new Color(spell.themeColor.r, spell.themeColor.g, spell.themeColor.b, 0.5f));

            SpellbladeParticles.AttachEmberTrail(go, spell.themeColor);

            var p = go.AddComponent<Projectile>();
            p._spell = spell;
            p._owner = owner;
            p._direction = direction.normalized;
            p._targets = targets; // [PHASE2-04]
            p._chainsLeft = spell.chainCount; // [PHASE2-02]
            if (spell.chainCount > 0) p._chainHits = new HashSet<Transform>();
            return p;
        }

        private void Update()
        {
            float step = _spell.projectileSpeed * Time.deltaTime;
            Vector3 from = transform.position;

            // Sweep the movement step so nothing is skipped between frames.
            var hits = Physics.SphereCastAll(from, _spell.projectileRadius, _direction, step,
                                             ~0, QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits, (a, b) => a.distance.CompareTo(b.distance));

            foreach (var hit in hits)
            {
                if (_owner != null && hit.transform.root == _owner.root) continue; // never hit the caster

                var health = hit.collider.GetComponentInParent<Health>();

                // [PHASE2-04] Enemy shots ghost through anything that isn't the player
                // (other enemies, dummies) — they only stop on the player or walls.
                if (_targets == ProjectileTargets.PlayerOnly && health != null &&
                    hit.collider.GetComponentInParent<PlayerLife>() == null)
                    continue;

                // [PHASE2-02] Chain segments pass through targets already zapped.
                if (health != null && _chainHits != null && _chainHits.Contains(health.transform.root))
                    continue;

                if (health != null && !health.IsDead)
                {
                    // Counter-wheel: scale damage by the target's elemental attunement.
                    var affinity = hit.collider.GetComponentInParent<ElementalAffinity>();
                    float mod = affinity != null ? affinity.ModifierFor(_spell.element) : 1f;

                    float damage = _spell.damage * _chainMult * mod; // [PHASE2-02] chain decay
                    health.TakeDamage(damage);
                    health.ApplyDot(_spell.dotDamagePerSecond * mod, _spell.dotDuration);
                    health.ApplySlow(_spell.slowPercent, _spell.slowDuration);
                    DamageNumber.Spawn(hit.point + Vector3.up * 1.1f, damage, mod);

                    // [PHASE2-02] Leech Bolt: a cut of damage dealt flows back as HP.
                    if (_spell.lifestealPercent > 0f && _owner != null)
                        _owner.root.GetComponent<Health>()?.Heal(damage * _spell.lifestealPercent);

                    // [PHASE2-02] Chain Spark: arc to the nearest fresh target in range.
                    if (_chainsLeft > 0) TryChainFrom(hit.point, health.transform.root);

                    Impact(hit.point);
                    return;
                }

                // Anything else solid (wall, pillar, ground) stops the shot.
                Impact(hit.point);
                return;
            }

            transform.position = from + _direction * step;
            _traveled += step;
            if (_traveled >= _spell.range)
            {
                SpellbladeFx.Flash(transform.position, _spell.themeColor, 0.4f);
                Destroy(gameObject);
            }
        }

        private void Impact(Vector3 point)
        {
            SpellbladeFx.Flash(point, _spell.themeColor, 0.9f);
            SpellbladeParticles.Burst(point, _spell.themeColor, 30, 7f);
            Destroy(gameObject);
        }

        /// <summary>[PHASE2-02] Spawn the next chain segment toward the nearest
        /// living, not-yet-zapped Health within chainRadius of the impact.</summary>
        private void TryChainFrom(Vector3 impactPoint, Transform justHitRoot)
        {
            Transform bestRoot = null;
            Health bestHealth = null;
            float bestDist = float.MaxValue;

            foreach (var candidate in Physics.OverlapSphere(impactPoint, _spell.chainRadius,
                                                            ~0, QueryTriggerInteraction.Ignore))
            {
                var health = candidate.GetComponentInParent<Health>();
                if (health == null || health.IsDead) continue;

                var root = health.transform.root;
                if (root == justHitRoot || _chainHits.Contains(root)) continue;
                if (_owner != null && root == _owner.root) continue;
                if (_targets == ProjectileTargets.PlayerOnly &&
                    candidate.GetComponentInParent<PlayerLife>() == null) continue;
                if (_targets == ProjectileTargets.Everyone &&
                    candidate.GetComponentInParent<PlayerLife>() != null) continue; // player arcs never bounce home

                float dist = (root.position - impactPoint).sqrMagnitude;
                if (dist < bestDist) { bestDist = dist; bestRoot = root; bestHealth = health; }
            }

            if (bestRoot == null) return;

            var target = bestHealth.transform.position + Vector3.up * 1.1f;
            var dir = target - impactPoint;
            var segment = Spawn(_spell, impactPoint + dir.normalized * 0.3f, dir, _owner, _targets);
            segment._chainsLeft = _chainsLeft - 1;
            segment._chainMult = _chainMult * 0.85f; // -15% per jump
            segment._chainHits = new HashSet<Transform>(_chainHits) { justHitRoot };
        }
    }
}
