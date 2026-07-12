using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Shared material + effect helpers so every script builds visuals the same
    /// way. All primitives, no imported art — swap for real VFX in later phases.
    /// </summary>
    public static class SpellbladeFx
    {
        /// <summary>Opaque URP Lit material.</summary>
        public static Material MakeLit(Color baseColor, float smoothness = 0.4f)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            mat.SetColor("_BaseColor", baseColor);
            mat.SetFloat("_Smoothness", smoothness);
            return mat;
        }

        /// <summary>URP Lit material with an HDR emission color (feeds bloom).</summary>
        public static Material MakeEmissive(Color baseColor, Color emission, float intensity = 3f)
        {
            var mat = MakeLit(baseColor, 0.7f);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", emission * intensity);
            return mat;
        }

        /// <summary>Transparent URP Unlit material (trails, AoE ghosts, health bars).</summary>
        public static Material MakeUnlitTransparent(Color color)
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Unlit"));
            mat.SetColor("_BaseColor", color);
            // Standard URP transparent-surface recipe:
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        /// <summary>Quick light + emissive-sphere pop that fades out (impacts, click markers).</summary>
        public static void Flash(Vector3 position, Color color, float size = 0.6f, float life = 0.25f)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Fx Flash";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = position;
            go.transform.localScale = Vector3.one * size;
            go.GetComponent<Renderer>().material = MakeEmissive(color, color, 4f);

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 5f;
            light.range = size * 8f;

            go.AddComponent<FxFade>().life = life;
        }
    }

    /// <summary>Shrinks and dims its object over its lifetime, then destroys it.</summary>
    public class FxFade : MonoBehaviour
    {
        public float life = 0.25f;
        private float _age;
        private Vector3 _startScale;
        private Light _light;
        private float _startIntensity;

        private void Start()
        {
            _startScale = transform.localScale;
            _light = GetComponent<Light>();
            if (_light != null) _startIntensity = _light.intensity;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / life);
            transform.localScale = _startScale * (1f - t);
            if (_light != null) _light.intensity = _startIntensity * (1f - t);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>
    /// Expanding, fading blast disc for point-AoE spells (Hemorrhage).
    /// Purely visual — damage is applied by the caster via OverlapSphere.
    /// </summary>
    public class AoeBurst : MonoBehaviour
    {
        public static void Spawn(Vector3 center, float radius, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = "Fx AoE Burst";
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.position = center + Vector3.up * 0.1f;

            var burst = go.AddComponent<AoeBurst>();
            burst._radius = radius;
            burst._mat = SpellbladeFx.MakeUnlitTransparent(new Color(color.r, color.g, color.b, 0.55f));
            go.GetComponent<Renderer>().material = burst._mat;

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = 7f;
            light.range = radius * 4f;
            burst._light = light;
        }

        private const float Life = 0.45f;
        private float _radius;
        private float _age;
        private Material _mat;
        private Light _light;

        private void Update()
        {
            _age += Time.deltaTime;
            float t = Mathf.Clamp01(_age / Life);
            // Grow to full radius fast, fade alpha out. Flattened into a disc.
            float d = Mathf.Lerp(0.5f, _radius * 2f, Mathf.Sqrt(t));
            transform.localScale = new Vector3(d, 0.35f, d);
            var c = _mat.GetColor("_BaseColor");
            c.a = 0.55f * (1f - t);
            _mat.SetColor("_BaseColor", c);
            if (_light != null) _light.intensity = 7f * (1f - t);
            if (t >= 1f) Destroy(gameObject);
        }
    }

    /// <summary>Keeps a transform facing the camera (world-space health bars).</summary>
    public class Billboard : MonoBehaviour
    {
        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);
        }
    }
}
