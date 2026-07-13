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
        private Color _elementColor;
        private Transform _barFill;
        private GameObject _barRoot;
        private Coroutine _slowTint;

        public static Dummy Spawn(Vector3 position, float maxHealth, float respawnDelay, ElementType element)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            var elementColor = ElementMath.ColorOf(element);
            go.name = $"Elemental Cultist ({element})";
            go.transform.position = position + Vector3.up * 1f; // capsule pivot is its center

            // Demarcation 1: robes tinted toward the element (kept muted for the biome).
            var robeCloth = new Color(0.15f, 0.15f, 0.18f); // charcoal cult robes
            var baseColor = Color.Lerp(robeCloth, elementColor, 0.30f);
            var mat = SpellbladeFx.MakeLit(baseColor, 0.15f);
            go.GetComponent<Renderer>().material = mat;
            var darkCloth = SpellbladeFx.MakeLit(baseColor * 0.75f, 0.12f);

            // Hooded head.
            var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            head.name = "Head";
            // DestroyImmediate, not Destroy: Dummy.Start caches child colliders this
            // same frame, and a deferred destroy would leave a dead reference in that
            // cache (caused a MissingReferenceException on first dummy death).
            Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(go.transform, false);
            head.transform.localPosition = new Vector3(0f, 1.02f, 0f);
            head.transform.localScale = new Vector3(0.62f, 0.55f, 0.62f);
            head.GetComponent<Renderer>().material = darkCloth;

            // Flared robe skirt over the legs.
            var skirt = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            skirt.name = "Robe Skirt";
            Object.DestroyImmediate(skirt.GetComponent<Collider>());
            skirt.transform.SetParent(go.transform, false);
            skirt.transform.localPosition = new Vector3(0f, -0.60f, 0f);
            skirt.transform.localScale = new Vector3(0.85f, 0.38f, 0.85f);
            skirt.GetComponent<Renderer>().material = darkCloth;

            // Glowing eyes under the hood, facing the player's side of the arena (-z).
            var eyeMat = SpellbladeFx.MakeEmissive(elementColor * 0.4f, elementColor, 3f);
            for (int side = -1; side <= 1; side += 2)
            {
                var eye = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                eye.name = "Eye";
                Object.DestroyImmediate(eye.GetComponent<Collider>());
                eye.transform.SetParent(go.transform, false);
                eye.transform.localPosition = new Vector3(0.105f * side, 1.05f, -0.24f);
                eye.transform.localScale = Vector3.one * 0.075f;
                eye.GetComponent<Renderer>().material = eyeMat;
            }

            // Each cultist carries its own staff, orb tinted to its element.
            var staffWood = SpellbladeFx.MakeLit(new Color(0.16f, 0.13f, 0.10f), 0.3f);
            var cultStaff = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cultStaff.name = "Cultist Staff";
            Object.DestroyImmediate(cultStaff.GetComponent<Collider>());
            cultStaff.transform.SetParent(go.transform, false);
            cultStaff.transform.localPosition = new Vector3(0.48f, -0.15f, -0.05f);
            cultStaff.transform.localScale = new Vector3(0.055f, 0.75f, 0.055f);
            cultStaff.transform.localRotation = Quaternion.Euler(0f, 0f, 7f);
            cultStaff.GetComponent<Renderer>().material = staffWood;

            var cultOrb = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            cultOrb.name = "Cultist Orb";
            Object.DestroyImmediate(cultOrb.GetComponent<Collider>());
            cultOrb.transform.SetParent(go.transform, false);
            cultOrb.transform.localPosition = new Vector3(0.54f, 0.68f, -0.05f);
            cultOrb.transform.localScale = Vector3.one * 0.16f;
            cultOrb.GetComponent<Renderer>().material = eyeMat;

            // Demarcation 2: glowing crest diamond floating above — pulses like the runes.
            var crest = GameObject.CreatePrimitive(PrimitiveType.Cube);
            crest.name = "Element Crest";
            Object.DestroyImmediate(crest.GetComponent<Collider>());
            crest.transform.SetParent(go.transform, false);
            crest.transform.localPosition = new Vector3(0f, 1.55f, 0f);
            crest.transform.localScale = new Vector3(0.2f, 0.3f, 0.2f);
            crest.transform.localRotation = Quaternion.Euler(45f, 0f, 45f);
            crest.GetComponent<Renderer>().material =
                SpellbladeFx.MakeEmissive(elementColor * 0.4f, elementColor, 2.2f);
            crest.AddComponent<RunePulse>();

            // The attunement itself — resists own element, breaks to its counter.
            var affinity = go.AddComponent<ElementalAffinity>();
            affinity.attunement = element;

            var health = go.AddComponent<Health>();
            health.maxHealth = maxHealth;

            var obstacle = go.AddComponent<NavMeshObstacle>();
            obstacle.carving = true;
            obstacle.shape = NavMeshObstacleShape.Capsule;

            var dummy = go.AddComponent<Dummy>();
            dummy.respawnDelay = respawnDelay;
            dummy._bodyMat = mat;
            dummy._baseColor = baseColor;
            dummy._elementColor = elementColor;
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

            // Demarcation 3: the health bar fills in the dummy's element color.
            CreateBarQuad("BG", new Color(0.05f, 0.05f, 0.08f, 0.9f), 0.01f, out _);
            CreateBarQuad("Fill", new Color(_elementColor.r, _elementColor.g, _elementColor.b, 0.95f), 0f, out _barFill);
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
