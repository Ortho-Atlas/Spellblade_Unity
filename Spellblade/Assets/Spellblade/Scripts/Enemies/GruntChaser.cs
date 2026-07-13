using System.Collections;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// "Husk" — the melee chaser grunt. Hunched primitive silhouette, runs the
    /// player down at 3.8, telegraphs a 0.35s windup lean before its swipe so
    /// it's dodgeable. 55 HP · 1.2m reach · 10 dmg · 1.2s cooldown.
    /// </summary>
    public class GruntChaser : EnemyBase
    {
        public float meleeRange = 1.2f;
        public float meleeDamage = 10f;
        public float meleeCooldown = 1.2f;
        public float windupSeconds = 0.35f;

        private Transform _body;   // leans during the windup
        private float _nextSwing;
        private bool _attacking;

        public static GruntChaser Spawn(Vector3 position, ElementType element,
                                        ObjectiveDirector director, float healthMultiplier = 1f)
        {
            var go = new GameObject($"Husk ({element})");
            go.transform.position = position;

            var collider = go.AddComponent<CapsuleCollider>();
            collider.center = new Vector3(0f, 1f, 0f);
            collider.height = 2f;
            collider.radius = 0.45f;

            var husk = go.AddComponent<GruntChaser>();
            husk.BuildBody();
            husk.Init(director, element, maxHealth: 55f, speed: 3.8f, healthMultiplier);
            husk.AddEyes(height: 1.30f, forward: 0.55f, spread: 0.09f, size: 0.07f); // after Init — eyes tint by Attunement
            return husk;
        }

        private void BuildBody()
        {
            var flesh = SpellbladeFx.MakeLit(new Color(0.13f, 0.115f, 0.10f), 0.15f);
            var darker = SpellbladeFx.MakeLit(new Color(0.10f, 0.09f, 0.08f), 0.12f);

            // Hunched torso, head slung low and forward, dragging knuckle plates.
            _body = AddPart(PrimitiveType.Capsule, "Body", new Vector3(0f, 0.85f, 0.05f),
                            new Vector3(0.85f, 0.68f, 0.85f), flesh,
                            localEuler: new Vector3(22f, 0f, 0f)).transform;
            AddPart(PrimitiveType.Sphere, "Head", new Vector3(0f, 1.28f, 0.34f),
                    new Vector3(0.5f, 0.44f, 0.5f), darker);
            AddPart(PrimitiveType.Cube, "Shoulders", new Vector3(0f, 1.12f, -0.05f),
                    new Vector3(0.95f, 0.28f, 0.5f), darker, localEuler: new Vector3(14f, 0f, 0f));
        }

        protected override void Tick(float distToPlayer)
        {
            if (_attacking) return;

            if (distToPlayer <= meleeRange && Time.time >= _nextSwing)
                StartCoroutine(Swipe());
            else if (Agent.isOnNavMesh)
                Agent.SetDestination(Player.position);
        }

        private IEnumerator Swipe()
        {
            _attacking = true;
            if (Agent.isOnNavMesh) Agent.isStopped = true;

            // Windup lean — the dodge window.
            var restRotation = _body.localRotation;
            float t = 0f;
            while (t < windupSeconds)
            {
                t += Time.deltaTime;
                _body.localRotation = Quaternion.Euler(22f + 26f * (t / windupSeconds), 0f, 0f);
                yield return null;
            }

            if (!Dead && Player != null)
            {
                var to = Player.position - transform.position;
                to.y = 0f;
                if (to.magnitude <= meleeRange + 0.4f) // still in reach after the windup?
                {
                    DamagePlayer(meleeDamage);
                    SpellbladeParticles.Burst(transform.position + Vector3.up + transform.forward * 0.8f,
                                              new Color(0.75f, 0.70f, 0.65f), 12, 3.5f, 0.1f);
                }
            }

            _body.localRotation = restRotation;
            _nextSwing = Time.time + meleeCooldown;
            if (!Dead && Agent.isOnNavMesh) Agent.isStopped = false;
            _attacking = false;
        }
    }
}
