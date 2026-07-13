using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;

namespace Spellblade
{
    /// <summary>
    /// The "blade" of Spellblade (Phase 2): Space swings a 120° arc strike in
    /// the facing direction. No mana — works at zero. Physical damage, so the
    /// elemental counter-wheel does NOT apply (damage numbers get modifier 1.0).
    /// Hits knock grunts/dummies back a short step. Quick violet-white slash
    /// sweep + spark burst for feel; no camera shake.
    /// </summary>
    public class MeleeStrike : MonoBehaviour
    {
        [Tooltip("Damage per hit before the progression multiplier. Physical — no elemental modifier.")]
        public float baseDamage = 22f;
        public float cooldown = 0.8f;
        public float reach = 2.3f;
        [Range(10f, 360f)] public float arcDegrees = 120f;
        public float knockbackDistance = 1.5f;
        public float knockbackDuration = 0.12f;

        /// <summary>Progression hook — Plan 05 raises this via the Sanctum. Multiplies baseDamage.</summary>
        public static float damageMultiplier = 1f;

        private static readonly Color SlashColor = new Color(0.80f, 0.62f, 1.00f); // violet-white

        private float _readyAt;

        /// <summary>Seconds until the next swing (0 = ready). The HUD melee card reads this.</summary>
        public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);

        private void Update()
        {
            var kb = Keyboard.current;
            if (kb == null || !kb.spaceKey.wasPressedThisFrame) return;
            if (Time.time < _readyAt) return;

            _readyAt = Time.time + cooldown;
            Strike();
        }

        private void Strike()
        {
            var facing = transform.forward;
            facing.y = 0f;
            facing.Normalize();

            SlashSweep.Spawn(transform, facing, reach, arcDegrees);
            SpellbladeParticles.Burst(transform.position + Vector3.up * 1.1f + facing * (reach * 0.5f),
                                      SlashColor, 18, 4.5f, 0.12f);

            float cosHalfArc = Mathf.Cos(arcDegrees * 0.5f * Mathf.Deg2Rad);
            float damage = baseDamage * damageMultiplier;

            var hits = Physics.OverlapSphere(transform.position, reach, ~0, QueryTriggerInteraction.Ignore);
            var damaged = new HashSet<Health>(); // multi-collider targets take damage once
            foreach (var hit in hits)
            {
                if (hit.transform.root == transform.root) continue;
                var health = hit.GetComponentInParent<Health>();
                if (health == null || health.IsDead || !damaged.Add(health)) continue;

                // Arc filter: target must be within ±(arc/2) of facing.
                var to = health.transform.position - transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.0001f && Vector3.Dot(facing, to.normalized) < cosHalfArc) continue;

                health.TakeDamage(damage);
                DamageNumber.Spawn(health.transform.position + Vector3.up * 2.3f, damage, 1f); // physical: mod 1.0

                var pushDir = to.sqrMagnitude > 0.0001f ? to.normalized : facing;
                StartCoroutine(Knockback(health.transform.root, pushDir));
            }
        }

        /// <summary>Short shove away from the player — agent.Move when the target has an
        /// agent (future grunts), positional nudge otherwise (dummies), stopped early
        /// if static geometry is in the way so nothing tunnels through walls.</summary>
        private IEnumerator Knockback(Transform target, Vector3 dir)
        {
            var agent = target != null ? target.GetComponent<NavMeshAgent>() : null;
            float elapsed = 0f;

            while (elapsed < knockbackDuration)
            {
                if (target == null) yield break; // died/despawned mid-shove
                float dt = Time.deltaTime;
                elapsed += dt;
                float step = knockbackDistance * (dt / knockbackDuration);

                if (agent != null && agent.enabled)
                {
                    agent.Move(dir * step); // NavMesh keeps it legal
                }
                else
                {
                    // Wall check from chest height so the nudge can't push through stone.
                    var from = target.position + Vector3.up * 1f;
                    if (Physics.Raycast(from, dir, out var wall, step + 0.55f, ~0, QueryTriggerInteraction.Ignore)
                        && wall.transform.root != target)
                        yield break;
                    target.position += dir * step;
                }
                yield return null;
            }
        }

        /// <summary>Stretched emissive-style quad swept across the strike arc, fading out.
        /// Runtime-built like all Spellblade VFX — no prefabs, no textures.</summary>
        private class SlashSweep : MonoBehaviour
        {
            private const float Life = 0.16f;

            private float _age;
            private float _fromYaw, _toYaw;
            private Material _mat;

            public static void Spawn(Transform owner, Vector3 facing, float reach, float arcDegrees)
            {
                var pivot = new GameObject("Fx Melee Slash");
                pivot.transform.position = owner.position + Vector3.up * 1.2f;

                float facingYaw = Quaternion.LookRotation(facing).eulerAngles.y;

                var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
                quad.name = "Blade";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(pivot.transform, false);
                // Lie flat (visible from the top-down camera), extend forward from the pivot.
                quad.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                quad.transform.localPosition = new Vector3(0f, 0f, reach * 0.55f);
                quad.transform.localScale = new Vector3(0.4f, reach * 0.95f, 1f);

                var sweep = pivot.AddComponent<SlashSweep>();
                sweep._fromYaw = facingYaw - arcDegrees * 0.5f;
                sweep._toYaw = facingYaw + arcDegrees * 0.5f;
                sweep._mat = SpellbladeFx.MakeUnlitTransparent(
                    new Color(SlashColor.r, SlashColor.g, SlashColor.b, 0.85f));
                quad.GetComponent<Renderer>().material = sweep._mat;

                pivot.transform.rotation = Quaternion.Euler(0f, sweep._fromYaw, 0f);
            }

            private void Update()
            {
                _age += Time.deltaTime;
                float t = Mathf.Clamp01(_age / Life);

                transform.rotation = Quaternion.Euler(0f, Mathf.Lerp(_fromYaw, _toYaw, t), 0f);

                var c = _mat.GetColor("_BaseColor");
                c.a = 0.85f * (1f - t * t); // hold bright, drop fast at the end
                _mat.SetColor("_BaseColor", c);

                if (t >= 1f) Destroy(gameObject);
            }
        }
    }
}
