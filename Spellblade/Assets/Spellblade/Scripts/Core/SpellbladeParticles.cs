using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Code-built particle effects — the Phase 1 "VFX pack." Everything uses the
    /// URP Particles/Unlit shader so startColor / fade-over-lifetime work, and
    /// nothing depends on imported textures.
    /// </summary>
    public static class SpellbladeParticles
    {
        private static Texture2D _softDot;

        /// <summary>
        /// Soft radial gradient texture, generated once in code. Without a texture,
        /// particles render as hard-edged squares — this makes mist and sparks round
        /// and feathered.
        /// </summary>
        private static Texture2D SoftDot
        {
            get
            {
                if (_softDot != null) return _softDot;

                const int size = 64;
                _softDot = new Texture2D(size, size, TextureFormat.RGBA32, false);
                float center = (size - 1) / 2f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dist = Vector2.Distance(new Vector2(x, y), new Vector2(center, center)) / center;
                        float a = Mathf.Clamp01(1f - dist);
                        a = a * a * (3f - 2f * a); // smoothstep falloff
                        _softDot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                    }
                }
                _softDot.Apply();
                return _softDot;
            }
        }

        /// <summary>Transparent particle material (vertex-color aware, feeds bloom via alpha).</summary>
        public static Material MakeParticleMat(Color tint)
        {
            var shader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Unlit"); // safety net
            var mat = new Material(shader);
            mat.SetColor("_BaseColor", tint);
            mat.SetTexture("_BaseMap", SoftDot);
            mat.SetFloat("_Surface", 1f);
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            mat.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            mat.SetInt("_ZWrite", 0);
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
            return mat;
        }

        /// <summary>Standard fade-in → fade-out alpha gradient.</summary>
        private static void FadeOverLifetime(ParticleSystem ps, float peakAlpha = 1f)
        {
            var col = ps.colorOverLifetime;
            col.enabled = true;
            var grad = new Gradient();
            grad.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[]
                {
                    new GradientAlphaKey(0f, 0f),
                    new GradientAlphaKey(peakAlpha, 0.15f),
                    new GradientAlphaKey(0f, 1f)
                });
            col.color = new ParticleSystem.MinMaxGradient(grad);
        }

        private static ParticleSystem NewSystem(string name, Color tint)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            go.GetComponent<ParticleSystemRenderer>().material = MakeParticleMat(tint);
            return ps;
        }

        /// <summary>One-shot spark burst — cast flashes and impacts.</summary>
        public static void Burst(Vector3 position, Color color, int count = 26, float speed = 6f, float size = 0.14f)
        {
            var ps = NewSystem("Fx Burst", Color.white);
            ps.transform.position = position;

            var main = ps.main;
            main.startLifetime = 0.5f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(speed * 0.4f, speed);
            main.startSize = new ParticleSystem.MinMaxCurve(size * 0.5f, size);
            main.startColor = color;
            main.gravityModifier = 0.4f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.12f;

            FadeOverLifetime(ps);
            ps.Emit(count);
            ps.Play();
            Object.Destroy(ps.gameObject, 1.2f);
        }

        /// <summary>Ember trail attached to a projectile — colored sparks left in its wake.</summary>
        public static void AttachEmberTrail(GameObject projectile, Color color)
        {
            var ps = NewSystem("Ember Trail", Color.white);
            ps.transform.SetParent(projectile.transform, false);

            var main = ps.main;
            main.startLifetime = 0.45f;
            main.startSpeed = 0.15f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.05f, 0.11f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World; // embers linger behind

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.rateOverDistance = 12f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.08f;

            FadeOverLifetime(ps);
            ps.Play();
        }

        /// <summary>Rising blood droplets for Hemorrhage's detonation area.</summary>
        public static void BloodRise(Vector3 center, float radius, Color color)
        {
            var ps = NewSystem("Fx Blood Rise", Color.white);
            ps.transform.position = center + Vector3.up * 0.1f;

            var main = ps.main;
            main.startLifetime = 1.1f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.8f, 2.2f); // upward drift
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = color;
            main.gravityModifier = -0.05f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 0f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = radius * 0.9f;

            FadeOverLifetime(ps, 0.85f);
            ps.Emit(40);
            ps.Play();
            Object.Destroy(ps.gameObject, 2f);
        }

        /// <summary>Low drifting ground mist across the whole courtyard — the Scotland gloom.</summary>
        public static void GroundMist(float areaSize)
        {
            var ps = NewSystem("Ambient Ground Mist", Color.white);
            ps.transform.position = new Vector3(0f, 0.7f, 0f);

            var main = ps.main;
            main.startLifetime = 14f;
            main.startSpeed = 0.25f;
            main.startSize = new ParticleSystem.MinMaxCurve(5f, 9f);
            main.startColor = new Color(0.55f, 0.60f, 0.58f, 0.075f); // faint gray-green haze
            main.maxParticles = 48;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.prewarm = true;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 3f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(areaSize * 0.95f, 0.4f, areaSize * 0.95f);

            var noise = ps.noise;
            noise.enabled = true;
            noise.strength = 0.18f;
            noise.frequency = 0.12f;

            FadeOverLifetime(ps, 0.8f);
            ps.Play();
        }

        /// <summary>Soft rising wisps for the player's discipline aura. Returns the system for retinting.</summary>
        public static ParticleSystem AuraWisps(Transform parent, Color color)
        {
            var ps = NewSystem("Aura Wisps", Color.white);
            ps.transform.SetParent(parent, false);

            var main = ps.main;
            main.startLifetime = 1.1f;
            main.startSpeed = 0.55f;
            main.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.09f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.loop = true;

            var emission = ps.emission;
            emission.rateOverTime = 7f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.35f;

            FadeOverLifetime(ps, 0.9f);
            ps.Play();
            return ps;
        }
    }
}
