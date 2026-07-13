using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Spellblade
{
    /// <summary>
    /// Foundation for every arena enemy (Plan 04): NavMeshAgent + Health +
    /// ElementalAffinity + a small state flow (Spawning → Idle/Chase → Attack →
    /// Dead). Subclasses build their primitive bodies and implement Tick().
    ///
    /// Aggro: wave enemies spawn hot (aggroRange = huge); traversal packs get a
    /// finite range so the player can sneak or sprint past. Taking any damage
    /// aggros permanently.
    /// </summary>
    public abstract class EnemyBase : MonoBehaviour
    {
        public float aggroRange = 999f;

        /// <summary>Fired once on death, before the dissolve. Objectives count on this.</summary>
        public System.Action<EnemyBase> OnKilled;

        public ElementType Attunement { get; private set; }
        public Health Health { get; private set; }
        public bool Dead { get; private set; }

        protected NavMeshAgent Agent { get; private set; }
        protected ObjectiveDirector Director { get; private set; }
        protected Transform Player => Director != null ? Director.Player : null;
        protected bool Aggroed { get; set; }
        protected bool Ready { get; private set; }

        protected void Init(ObjectiveDirector director, ElementType element,
                            float maxHealth, float speed, float healthMultiplier,
                            float agentRadius = 0.45f, float agentHeight = 2f)
        {
            Director = director;
            Attunement = element;

            Agent = gameObject.AddComponent<NavMeshAgent>();
            Agent.speed = speed;
            Agent.acceleration = 20f;
            Agent.angularSpeed = 540f;
            Agent.stoppingDistance = 0.6f;
            Agent.radius = agentRadius;
            Agent.height = agentHeight;

            var affinity = gameObject.AddComponent<ElementalAffinity>();
            affinity.attunement = element;

            Health = gameObject.AddComponent<Health>();
            Health.maxHealth = maxHealth * healthMultiplier;
            Health.Revive(); // Awake ran before maxHealth was set — sync Current
            Health.OnHealthChanged += (cur, max) => { if (cur < max) Aggroed = true; };
            Health.OnDied += Die;

            StartCoroutine(SpawnIn());
        }

        private IEnumerator SpawnIn()
        {
            // Brief materialize so enemies never pop in fully formed.
            var full = transform.localScale;
            const float duration = 0.35f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                transform.localScale = full * Mathf.Lerp(0.25f, 1f, t / duration);
                yield return null;
            }
            transform.localScale = full;
            Ready = true;
        }

        private void Update()
        {
            if (Dead || !Ready || Player == null) return;

            var to = Player.position - transform.position;
            to.y = 0f;
            float dist = to.magnitude;

            if (!Aggroed)
            {
                if (dist > aggroRange) return; // dormant — traversal packs wait
                Aggroed = true;
            }

            Tick(dist);
        }

        /// <summary>Per-archetype behavior, called only while alive + aggroed.</summary>
        protected abstract void Tick(float distToPlayer);

        protected void DamagePlayer(float amount)
        {
            if (Player == null) return;
            var health = Player.GetComponent<Health>();
            if (health == null || health.IsDead) return;
            health.TakeDamage(amount);
            SpellbladeFx.Flash(Player.position + Vector3.up * 1.2f, new Color(0.9f, 0.2f, 0.2f), 0.45f, 0.2f);
        }

        private void Die()
        {
            if (Dead) return;
            Dead = true;
            StopAllCoroutines(); // cancel any windup mid-swing

            OnKilled?.Invoke(this);

            if (Agent != null) Agent.enabled = false;
            foreach (var c in GetComponentsInChildren<Collider>()) c.enabled = false;

            StartCoroutine(DeathDissolve());
        }

        private IEnumerator DeathDissolve()
        {
            var color = ElementMath.ColorOf(Attunement);
            SpellbladeParticles.Burst(transform.position + Vector3.up, color, 30, 5.5f, 0.14f);
            SpellbladeFx.Flash(transform.position + Vector3.up, color, 0.9f, 0.3f);

            // Dissolve-ish: sink and shrink.
            var startScale = transform.localScale;
            var startPos = transform.position;
            const float duration = 0.45f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = t / duration;
                transform.localScale = startScale * (1f - k);
                transform.position = startPos + Vector3.down * (0.6f * k);
                yield return null;
            }
            Destroy(gameObject);
        }

        // -- Shared body-building helpers (primitive kit, colliders stripped) ----

        protected GameObject AddPart(PrimitiveType type, string name, Vector3 localPos,
                                     Vector3 localScale, Material mat, Vector3? localEuler = null)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            Object.DestroyImmediate(part.GetComponent<Collider>()); // root capsule collider is the hitbox
            part.transform.SetParent(transform, false);
            part.transform.localPosition = localPos;
            part.transform.localScale = localScale;
            if (localEuler.HasValue) part.transform.localRotation = Quaternion.Euler(localEuler.Value);
            part.GetComponent<Renderer>().material = mat;
            return part;
        }

        protected void AddEyes(float height, float forward, float spread, float size)
        {
            var color = ElementMath.ColorOf(Attunement);
            var mat = SpellbladeFx.MakeEmissive(color * 0.4f, color, 3f);
            for (int side = -1; side <= 1; side += 2)
                AddPart(PrimitiveType.Sphere, "Eye",
                        new Vector3(spread * side, height, forward),
                        Vector3.one * size, mat);
        }
    }
}
