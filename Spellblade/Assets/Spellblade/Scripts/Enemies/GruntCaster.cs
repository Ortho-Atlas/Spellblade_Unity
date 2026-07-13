using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// "Cultist" — the ranged caster grunt. Robed silhouette that holds 9-12m,
    /// retreats when the player closes inside 7m, strafes every few seconds,
    /// and fires an element-tinted bolt every 2.5s after a 0.5s orb-glow
    /// windup. 40 HP. Bolts hurt ONLY the player (Projectile target filter).
    /// </summary>
    public class GruntCaster : EnemyBase
    {
        public float preferredMin = 9f;
        public float preferredMax = 12f;
        public float panicRange = 7f;
        public float fireInterval = 2.5f;
        public float windupSeconds = 0.5f;

        private Transform _orb;
        private float _nextShot;
        private float _nextStrafe;
        private bool _casting;

        // One runtime bolt spell per element, shared by every cultist.
        private static readonly Dictionary<ElementType, SpellSO> _bolts = new();

        public static GruntCaster Spawn(Vector3 position, ElementType element,
                                        ObjectiveDirector director, float healthMultiplier = 1f)
        {
            var go = new GameObject($"Cultist ({element})");
            go.transform.position = position;

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = 0.42f;

            var cultist = go.AddComponent<GruntCaster>();
            cultist.BuildBody(element);
            cultist.Init(director, element, maxHealth: 40f, speed: 3.4f, healthMultiplier);
            cultist.AddEyes(height: 1.42f, forward: 0.24f, spread: 0.10f, size: 0.07f);
            return cultist;
        }

        private void BuildBody(ElementType element)
        {
            var elementColor = ElementMath.ColorOf(element);
            var robe = SpellbladeFx.MakeLit(
                Color.Lerp(new Color(0.14f, 0.14f, 0.17f), elementColor, 0.25f), 0.14f);
            var darkRobe = SpellbladeFx.MakeLit(
                Color.Lerp(new Color(0.11f, 0.11f, 0.13f), elementColor, 0.18f), 0.12f);

            AddPart(PrimitiveType.Capsule, "Body", new Vector3(0f, 1f, 0f),
                    new Vector3(0.8f, 1f, 0.8f), robe);
            AddPart(PrimitiveType.Cylinder, "Robe Skirt", new Vector3(0f, 0.38f, 0f),
                    new Vector3(0.85f, 0.38f, 0.85f), darkRobe);
            AddPart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.44f, 0f),
                    new Vector3(0.56f, 0.5f, 0.56f), darkRobe);

            // Casting orb at the raised hand — glows during the windup.
            _orb = AddPart(PrimitiveType.Sphere, "Casting Orb", new Vector3(0.42f, 1.25f, 0.22f),
                           Vector3.one * 0.16f,
                           SpellbladeFx.MakeEmissive(elementColor * 0.4f, elementColor, 2.5f)).transform;
        }

        protected override void Tick(float distToPlayer)
        {
            if (_casting) return;

            Reposition(distToPlayer);

            if (distToPlayer <= preferredMax + 3f && Time.time >= _nextShot)
                StartCoroutine(CastBolt());
        }

        private void Reposition(float dist)
        {
            if (!Agent.isOnNavMesh) return;

            if (dist < panicRange)
            {
                // Kite: straight away from the player.
                var away = (transform.position - Player.position).normalized;
                Agent.SetDestination(transform.position + away * 4.5f);
            }
            else if (dist > preferredMax)
            {
                Agent.SetDestination(Player.position);
            }
            else if (Time.time >= _nextStrafe)
            {
                // In the comfort band: sidestep every few seconds.
                var toPlayer = (Player.position - transform.position).normalized;
                var side = Vector3.Cross(Vector3.up, toPlayer) * (Random.value < 0.5f ? 3f : -3f);
                Agent.SetDestination(transform.position + side);
                _nextStrafe = Time.time + Random.Range(2.5f, 4f);
            }
        }

        private IEnumerator CastBolt()
        {
            _casting = true;
            if (Agent.isOnNavMesh) Agent.isStopped = true;

            // 0.5s glow windup — the orb swells.
            var restScale = _orb.localScale;
            float t = 0f;
            while (t < windupSeconds)
            {
                t += Time.deltaTime;
                _orb.localScale = restScale * Mathf.Lerp(1f, 2.1f, t / windupSeconds);
                if (Player != null) FaceFlat(Player.position);
                yield return null;
            }
            _orb.localScale = restScale;

            if (!Dead && Player != null)
            {
                var origin = transform.position + Vector3.up * 1.25f;
                var dir = Player.position + Vector3.up * 1.1f - origin;
                dir.y = 0f; // bolts travel flat, same as player skillshots
                if (dir.sqrMagnitude < 0.001f) dir = transform.forward;

                Projectile.Spawn(BoltFor(Attunement), origin + dir.normalized * 0.6f, dir,
                                 transform, ProjectileTargets.PlayerOnly);
            }

            _nextShot = Time.time + fireInterval;
            if (!Dead && Agent.isOnNavMesh) Agent.isStopped = false;
            _casting = false;
        }

        private void FaceFlat(Vector3 worldPoint)
        {
            var flat = worldPoint - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.001f)
                transform.rotation = Quaternion.LookRotation(flat);
        }

        private static SpellSO BoltFor(ElementType element)
        {
            if (_bolts.TryGetValue(element, out var bolt) && bolt != null) return bolt;

            bolt = SpellSO.Create($"{element} Bolt", ElementMath.ColorOf(element), AimType.LineSkillshot);
            bolt.element = element;
            bolt.damage = 12f;
            bolt.projectileSpeed = 14f;
            bolt.projectileRadius = 0.3f;
            bolt.range = 30f;
            bolt.manaCost = 0f;
            bolt.cooldown = 0f;
            _bolts[element] = bolt;
            return bolt;
        }
    }
}
