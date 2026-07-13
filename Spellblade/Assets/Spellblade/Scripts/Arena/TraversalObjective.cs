using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// A-to-B gauntlet (Plan 04). Runs inside the corridor arena that
    /// TraversalArena builds (three chambers along +Z). Pre-placed enemy packs
    /// hold chambers 1-2 with a finite aggro range — fighting through is one
    /// strategy, sprinting past is another, both are legal. Touching the
    /// shimmering portal in chamber 3 wins.
    /// </summary>
    public class TraversalObjective : ObjectiveBase
    {
        public float portalRadius = 1.6f;
        public float packAggroRange = 11f;

        private Transform _portal;

        protected override void OnBegin()
        {
            Director.SetObjectiveText("Reach the far gate");
            BuildPortal(new Vector3(0f, 0f, 26f)); // chamber 3, far end
            StartCoroutine(PlacePacks());
        }

        private IEnumerator PlacePacks()
        {
            // Chamber 1 (south, player's entry): light welcome pack, north half.
            var chamber1 = new List<(EnemyKind kind, Vector3 pos)>
            {
                (EnemyKind.Husk, new Vector3(-3.5f, 0f, -14f)),
                (EnemyKind.Husk, new Vector3(3.5f, 0f, -13f)),
                (EnemyKind.Cultist, new Vector3(0f, 0f, -11.5f)),
            };
            // Chamber 2 (middle): the real fight.
            var chamber2 = new List<(EnemyKind kind, Vector3 pos)>
            {
                (EnemyKind.Husk, new Vector3(-5f, 0f, -2f)),
                (EnemyKind.Husk, new Vector3(5f, 0f, 0f)),
                (EnemyKind.Husk, new Vector3(0f, 0f, 4f)),
                (EnemyKind.Cultist, new Vector3(-4f, 0f, 5f)),
                (EnemyKind.Cultist, new Vector3(4f, 0f, 6f)),
            };

            int i = 0;
            foreach (var (kind, pos) in chamber1)
            {
                var element = (ElementType)(i++ % 4);
                yield return Director.SpawnEnemy(kind, pos, element, null, 1f, packAggroRange);
            }
            foreach (var (kind, pos) in chamber2)
            {
                var element = (ElementType)(i++ % 4);
                yield return Director.SpawnEnemy(kind, pos, element, null, 1f, packAggroRange);
            }
        }

        private void Update()
        {
            if (_portal == null || Director.Player == null) return;

            var to = Director.Player.position - _portal.position;
            to.y = 0f;
            if (to.magnitude <= portalRadius)
            {
                _portal = null; // fire once
                Director.Victory();
            }
        }

        /// <summary>Shimmering exit gate: a slowly turning ring of emissive shards
        /// over a light pool, with rising wisps. All primitives, no assets.</summary>
        private void BuildPortal(Vector3 position)
        {
            var root = new GameObject("Exit Portal");
            root.transform.position = position;
            _portal = root.transform;

            var gold = new Color(0.95f, 0.85f, 0.45f);
            var shardMat = SpellbladeFx.MakeEmissive(gold * 0.4f, gold, 3.5f);

            var ring = new GameObject("Shard Ring");
            ring.transform.SetParent(root.transform, false);
            ring.transform.localPosition = Vector3.up * 1.4f;
            const int shards = 8;
            for (int i = 0; i < shards; i++)
            {
                float angle = i * (360f / shards) * Mathf.Deg2Rad;
                var shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Shard";
                Destroy(shard.GetComponent<Collider>());
                shard.transform.SetParent(ring.transform, false);
                shard.transform.localPosition = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * 1.1f;
                shard.transform.localScale = new Vector3(0.12f, 0.34f, 0.12f);
                shard.transform.localRotation = Quaternion.Euler(0f, i * (360f / shards), 45f);
                shard.GetComponent<Renderer>().material = shardMat;
            }
            ring.AddComponent<SlowSpin>();

            var light = root.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = gold;
            light.intensity = 3.5f;
            light.range = 7f;
            light.transform.localPosition = Vector3.up * 1.4f;

            SpellbladeParticles.AuraWisps(root.transform, gold);
        }
    }

    /// <summary>Gentle constant rotation for portal rings and similar dressing.</summary>
    public class SlowSpin : MonoBehaviour
    {
        public float degreesPerSecond = 40f;
        private void Update() => transform.Rotate(0f, degreesPerSecond * Time.deltaTime, 0f);
    }
}
