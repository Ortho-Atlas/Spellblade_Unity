using System.Collections;
using UnityEngine;
using UnityEngine.AI;

namespace Spellblade
{
    /// <summary>
    /// Training dummy: shows a floating health bar, tints blue while slowed,
    /// crimson-flashes on death, and respawns after a delay. Carves a hole in
    /// the NavMesh so the player paths around it.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class Dummy : MonoBehaviour
    {
        public float respawnDelay = 4f;

        private Health _health;
        private Renderer[] _renderers;
        private Collider[] _colliders;
        private NavMeshObstacle _obstacle;
        private Material _bodyMat;
        private Color _baseColor;
        private Transform _barFill;
        private GameObject _barRoot;
        private Coroutine _slowTint;

        public static Dummy Spawn(Vector3 position, float maxHealth, float respawnDelay)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            go.name = "Training Dummy";
            go.transform.position = position + Vector3.up * 1f; // capsule pivot is its center

            var baseColor = new Color(0.28f, 0.25f, 0.22f); // rain-darkened training-yard wood
            var mat = SpellbladeFx.MakeLit(baseColor, 0.15f);
            go.GetComponent<Renderer>().material = mat;

            // Head knob so it reads as a dummy, not a pillar.
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            // DestroyImmediate, not Destroy: Dummy.Start caches child colliders this
            // same frame, and a deferred destroy would leave a dead reference in that
            // cache (caused a MissingReferenceException on first dummy death).
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.05f, 0f);
            head.transform.localScale = Vector3.one * 0.55f;
            head.GetComponent<Renderer>().material = mat;

            var health = go.AddComponent<Health>();
            health.maxHealth = maxHealth;

            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Capsule;

            var dummy = go.AddComponent<Dummy>();
            dummy.respawnDelay = respawnDelay;
            dummy._bodyMat = mat;
            dummy._baseColor = baseColor;
            return dummy;
        }

        private void Awake()
        {
            _health = GetComponent<Health>();
            _obstacle = GetComponent<NavMeshObstacle>();
        }

        private void Start()
        {
            _renderers = GetComponentsInChildren<Renderer>();
            _colliders = GetComponentsInChildren<Collider>();
            BuildHealthBar();

            _health.OnHealthChanged += (cur, max) => UpdateBar(cur / max);
            _health.OnDied += () => StartCoroutine(DeathAndRespawn());
            _health.OnSlowed += OnSlowed;
        }

        // -- Health bar: two billboarded quads, no canvas needed -------------

        private void BuildHealthBar()
        {
            _barRoot = new GameObject("Health Bar");
            _barRoot.transform.SetParent(transform, false);
            _barRoot.transform.localPosition = new Vector3(0f, 1.8f, 0f);
            _barRoot.AddComponent<Billboard>();

            CreateBarQuad("BG", new Color(0.05f, 0.05f, 0.08f, 0.9f), 0.01f, out _);
            CreateBarQuad("Fill", new Color(0.85f, 0.2f, 0.25f, 0.95f), 0f, out _barFill);
            UpdateBar(1f);
        }

        private void CreateBarQuad(string name, Color color, float zOffset, out Transform quad)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Quad);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(_barRoot.transform, false);
            go.transform.localPosition = new Vector3(0f, 0f, zOffset); // BG sits slightly behind
            go.transform.localScale = new Vector3(1.3f, 0.16f, 1f);
            go.GetComponent<Renderer>().material = SpellbladeFx.MakeUnlitTransparent(color);
            quad = go.transform;
        }

        private void UpdateBar(float pct)
        {
            if (_barFill == null) return;
            pct = Mathf.Clamp01(pct);
            // Shrink from the right: scale down and shift left so the bar drains naturally.
            _barFill.localScale = new Vector3(1.3f * pct, 0.16f, 1f);
            _barFill.localPosition = new Vector3(-0.65f * (1f - pct), 0f, 0f);
        }

        // -- Status + lifecycle ----------------------------------------------

        private void OnSlowed(float percent, float duration)
        {
            if (_slowTint != null) StopCoroutine(_slowTint);
            _slowTint = StartCoroutine(SlowTint(duration));
        }

        private IEnumerator SlowTint(float duration)
        {
            _bodyMat.SetColor("_BaseColor", new Color(0.35f, 0.6f, 0.9f)); // frost blue
            yield return new WaitForSeconds(duration);
            _bodyMat.SetColor("_BaseColor", _baseColor);
        }

        private IEnumerator DeathAndRespawn()
        {
            SpellbladeFx.Flash(transform.position + Vector3.up, new Color(0.9f, 0.2f, 0.3f), 1.4f, 0.4f);

            // Null-guarded: components destroyed after caching must never throw here.
            foreach (var r in _renderers) if (r != null) r.enabled = false;
            foreach (var c in _colliders) if (c != null) c.enabled = false;
            _barRoot.SetActive(false);
            if (_obstacle != null) _obstacle.enabled = false;

            yield return new WaitForSeconds(respawnDelay);

            _health.Revive();
            _bodyMat.SetColor("_BaseColor", _baseColor);
            foreach (var r in _renderers) if (r != null) r.enabled = true;
            foreach (var c in _colliders) if (c != null) c.enabled = true;
            _barRoot.SetActive(true);
            if (_obstacle != null) _obstacle.enabled = true;
            UpdateBar(1f);
            SpellbladeFx.Flash(transform.position + Vector3.up, new Color(0.5f, 0.9f, 0.6f), 1f, 0.35f);
        }
    }
}
