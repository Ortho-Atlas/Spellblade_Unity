using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// "Court Warden" — the mini-boss. A 2.2×-scale chaser (320 HP, speed 3.2)
    /// attuned to its region's element, rotating two telegraphed abilities:
    ///   SLAM   — 1s telegraph ring, then 25 dmg to the player within 2.5m
    ///   SUMMON — raises 2 Husks (6s gate, max 4 of its summons alive)
    /// Falls back to a heavy swipe + chase between abilities.
    /// </summary>
    public class MiniBoss : EnemyBase
    {
        public float slamRadius = 2.5f;
        public float slamDamage = 25f;
        public float slamTelegraph = 1f;
        public float slamCooldown = 5f;
        public float summonCooldown = 6f;
        public int summonCap = 4;
        public float swipeRange = 2.0f;
        public float swipeDamage = 12f;

        private readonly List<GruntChaser> _summons = new();
        private float _slamReadyAt;
        private float _summonReadyAt = 3f; // absolute Time.time floor — no instant summon on spawn
        private float _nextSwing;
        private bool _busy;

        private int AliveSummons
        {
            get { _summons.RemoveAll(s => s == null || s.Dead); return _summons.Count; }
        }

        public static MiniBoss Spawn(Vector3 position, ElementType element,
                                     ObjectiveDirector director, float healthMultiplier = 1f)
        {
            var go = new GameObject($"Court Warden ({element})");
            go.transform.position = position;
            go.transform.localScale = Vector3.one * 2.2f;

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f); // local — world height scales with the root
            collider.height = 2f;
            collider.radius = 0.45f;

            var warden = go.AddComponent<MiniBoss>();
            warden.BuildBody(element);
            warden.Init(director, element, maxHealth: 320f, speed: 3.2f, healthMultiplier,
                        agentRadius: 0.95f, agentHeight: 4.4f);
            warden.AddEyes(height: 1.30f, forward: 0.55f, spread: 0.09f, size: 0.08f);
            return warden;
        }

        private void BuildBody(ElementType element)
        {
            var elementColor = ElementMath.ColorOf(element);
            var hide = SpellbladeFx.MakeLit(new Color(0.12f, 0.105f, 0.10f), 0.18f);
            var plate = SpellbladeFx.MakeLit(new Color(0.09f, 0.085f, 0.10f), 0.3f);

            AddPart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0.05f),
                    new Vector3(0.95f, 0.72f, 0.95f), hide, localEuler: new Vector3(18f, 0f, 0f));
            AddPart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.30f, 0.32f),
                    new Vector3(0.5f, 0.46f, 0.5f), plate);
            AddPart(PrimitiveType.Cube, "Pauldrons", new Vector3(0f, 1.14f, -0.04f),
                    new Vector3(1.1f, 0.3f, 0.55f), plate, localEuler: new Vector3(12f, 0f, 0f));

            // Court crest — the same pulsing diamond the cultist dummies wear, enlarged.
            var crest = AddPart(PrimitiveType.Cube, "Court Crest", new Vector3(0f, 1.85f, 0f),
                                new Vector3(0.22f, 0.33f, 0.22f),
                                SpellbladeFx.MakeEmissive(elementColor * 0.4f, elementColor, 2.4f),
                                localEuler: new Vector3(45f, 0f, 45f));
            crest.AddComponent<RunePulse>();
        }

        protected override void Tick(float distToPlayer)
        {
            if (_busy) return;

            if (distToPlayer <= slamRadius + 0.3f && Time.time >= _slamReadyAt)
                StartCoroutine(Slam());
            else if (Time.time >= _summonReadyAt && AliveSummons < summonCap - 1) // raises 2 at a time
                StartCoroutine(Summon());
            else if (distToPlayer <= swipeRange && Time.time >= _nextSwing)
                StartCoroutine(Swipe());
            else if (Agent.isOnNavMesh)
                Agent.SetDestination(Player.position);
        }

        private IEnumerator Slam()
        {
            _busy = true;
            if (Agent.isOnNavMesh) Agent.isStopped = true;

            // The telegraph is the dodge window: a ring on the ground for 1s.
            TelegraphRing.Spawn(transform.position, slamRadius, ElementMath.ColorOf(Attunement), slamTelegraph);
            yield return new WaitForSeconds(slamTelegraph);

            if (!Dead)
            {
                var color = ElementMath.ColorOf(Attunement);
                SpellbladeParticles.Burst(transform.position + Vector3.up * 0.3f, color, 40, 7f, 0.16f);
                SpellbladeFx.Flash(transform.position + Vector3.up * 0.3f, color, 1.6f, 0.35f);

                if (Player != null)
                {
                    var to = Player.position - transform.position;
                    to.y = 0f;
                    if (to.magnitude <= slamRadius) DamagePlayer(slamDamage);
                }
                _slamReadyAt = Time.time + slamCooldown;
            }

            if (!Dead && Agent.isOnNavMesh) Agent.isStopped = false;
            _busy = false;
        }

        private IEnumerator Summon()
        {
            _busy = true;
            if (Agent.isOnNavMesh) Agent.isStopped = true;

            // Raise gesture: crest-colored updraft while two Husks claw out of the ground.
            SpellbladeParticles.Burst(transform.position + Vector3.up * 2f,
                                      ElementMath.ColorOf(Attunement), 20, 3f, 0.12f);
            yield return new WaitForSeconds(0.5f);

            if (!Dead && Director != null)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    var pos = transform.position + transform.right * (2.2f * side) + transform.forward * 0.5f;
                    Director.StartCoroutine(Director.SpawnEnemy(EnemyKind.Husk, pos, Attunement,
                        spawned => _summons.Add((GruntChaser)spawned)));
                }
                _summonReadyAt = Time.time + summonCooldown;
            }

            if (!Dead && Agent.isOnNavMesh) Agent.isStopped = false;
            _busy = false;
        }

        private IEnumerator Swipe()
        {
            _busy = true;
            if (Agent.isOnNavMesh) Agent.isStopped = true;

            yield return new WaitForSeconds(0.35f); // same dodgeable windup as the Husk

            if (!Dead && Player != null)
            {
                var to = Player.position - transform.position;
                to.y = 0f;
                if (to.magnitude <= swipeRange + 0.5f) DamagePlayer(swipeDamage);
            }

            _nextSwing = Time.time + 1.4f;
            if (!Dead && Agent.isOnNavMesh) Agent.isStopped = false;
            _busy = false;
        }
    }

    /// <summary>Flat telegraph disc that pulses on the ground, then vanishes.
    /// Purely visual — the ability applies its own damage check when it lands.</summary>
    public class TelegraphRing : MonoBehaviour
    {
        private float _life;
        private float _age;
        private Material _mat;

        public static void Spawn(Vector3 center, float radius, Color color, float life)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = "Fx Telegraph Ring";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = center + Vector3.up * 0.05f;
            go.transform.localScale = new Vector3(radius * 2f, 0.02f, radius * 2f);

            var ring = go.AddComponent<TelegraphRing>();
            ring._life = life;
            ring._mat = SpellbladeFx.MakeUnlitTransparent(new Color(color.r, color.g, color.b, 0.3f));
            go.GetComponent<Renderer>().material = ring._mat;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / _life);

            // Urgency ramp: pulse faster and brighter as the hit approaches.
            float pulse = 0.25f + 0.2f * Mathf.Sin(_age * (10f + 14f * t));
            var c = _mat.GetColor("_BaseColor");
            _mat.SetColor("_BaseColor", new Color(c.r, c.g, c.b, Mathf.Lerp(pulse, 0.55f, t * t)));

            if (t >= 1f) Destroy(gameObject);
        }
    }
}
