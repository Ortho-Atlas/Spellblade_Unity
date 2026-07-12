using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// SHADOW BIOME ENVIRONMENT PACK (procedural, Phase 1)
    /// Art direction from the Spellblade vault lore: "Scotland gloom" — Game of
    /// Thrones meets Dark Souls. Weathered charcoal gothic stone, tall narrow
    /// spires, iron gates, standing stones, rubble, drifting mist. Muted palette:
    /// deep green, slate gray, charcoal black, cold silver. No warm tones.
    ///
    /// Everything is primitives + code materials so the project stays
    /// asset-free. Built BEFORE the NavMesh bake so stonework blocks pathing.
    /// </summary>
    public static class ShadowBiomeArt
    {
        // The muted Shadow Biome palette.
        private static readonly Color Charcoal    = new(0.115f, 0.115f, 0.135f);
        private static readonly Color OldStone    = new(0.155f, 0.155f, 0.175f);
        private static readonly Color Iron        = new(0.085f, 0.09f, 0.10f);
        private static readonly Color ColdSilver  = new(0.68f, 0.74f, 0.80f);

        public static void Build(float arenaSize)
        {
            var root = new GameObject("Shadow Biome Dressing").transform;
            float half = arenaSize / 2f;

            var charcoalMat = SpellbladeFx.MakeLit(Charcoal, 0.25f);
            var stoneMat = SpellbladeFx.MakeLit(OldStone, 0.2f);
            var ironMat = SpellbladeFx.MakeLit(Iron, 0.55f);

            BuildCornerSpires(root, half, charcoalMat, stoneMat);
            BuildRuinedArches(root, stoneMat);
            BuildIronGate(root, half, ironMat);
            BuildStandingStones(root, charcoalMat);
            BuildRubble(root, half, stoneMat);

            SpellbladeParticles.GroundMist(arenaSize);
        }

        // -- Gothic spires at the four corners: tall, narrow, tapering ----------

        private static void BuildCornerSpires(Transform root, float half, Material baseMat, Material topMat)
        {
            float inset = half - 1.6f;
            Vector2[] corners = { new(-inset, -inset), new(inset, -inset), new(-inset, inset), new(inset, inset) };

            foreach (var c in corners)
            {
                var spire = new GameObject("Gothic Spire").transform;
                spire.SetParent(root);
                spire.position = new Vector3(c.x, 0f, c.y);

                // Three tapering tiers + a needle tip — the "tall narrow spire" silhouette.
                Block(spire, baseMat, new Vector3(0, 1.5f, 0), new Vector3(2.2f, 3f, 2.2f));
                Block(spire, baseMat, new Vector3(0, 4.0f, 0), new Vector3(1.5f, 2.2f, 1.5f));
                Block(spire, topMat,  new Vector3(0, 6.0f, 0), new Vector3(1.0f, 1.8f, 1.0f));
                var tip = Block(spire, topMat, new Vector3(0, 8.2f, 0), new Vector3(0.45f, 2.6f, 0.45f));
                tip.transform.rotation = Quaternion.Euler(0f, 45f, 0f); // diamond profile
            }
        }

        // -- Ruined arches: one standing, one collapsed ---------------------------

        private static void BuildRuinedArches(Transform root, Material mat)
        {
            // Standing arch, west side.
            var arch = new GameObject("Ruined Arch").transform;
            arch.SetParent(root);
            arch.position = new Vector3(-8.5f, 0f, -7f);
            Block(arch, mat, new Vector3(-1.6f, 1.9f, 0), new Vector3(0.9f, 3.8f, 0.9f));
            Block(arch, mat, new Vector3(1.6f, 1.9f, 0), new Vector3(0.9f, 3.8f, 0.9f));
            Block(arch, mat, new Vector3(0f, 3.9f, 0), new Vector3(4.4f, 0.7f, 0.9f));

            // Collapsed arch, east side — one pillar snapped, lintel fallen against it.
            var broken = new GameObject("Collapsed Arch").transform;
            broken.SetParent(root);
            broken.position = new Vector3(9f, 0f, 8f);
            Block(broken, mat, new Vector3(-1.6f, 1.9f, 0), new Vector3(0.9f, 3.8f, 0.9f));
            Block(broken, mat, new Vector3(1.6f, 0.7f, 0), new Vector3(0.9f, 1.4f, 0.9f)); // stump
            var fallen = Block(broken, mat, new Vector3(0.6f, 2.4f, 0.1f), new Vector3(4.0f, 0.65f, 0.85f));
            fallen.transform.rotation = Quaternion.Euler(4f, 8f, -24f); // slumped lintel
        }

        // -- Iron gate set into the north wall ------------------------------------

        private static void BuildIronGate(Transform root, float half, Material ironMat)
        {
            var gate = new GameObject("Iron Gate").transform;
            gate.SetParent(root);
            gate.position = new Vector3(4f, 0f, half - 0.5f);

            for (int i = 0; i < 7; i++)
            {
                var bar = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                bar.name = "Gate Bar";
                bar.transform.SetParent(gate);
                bar.transform.localPosition = new Vector3(-1.5f + i * 0.5f, 1.9f, -0.55f);
                bar.transform.localScale = new Vector3(0.09f, 1.9f, 0.09f);
                bar.GetComponent<Renderer>().material = ironMat;
            }
            // Two horizontal straps.
            Block(gate, ironMat, new Vector3(0f, 1.1f, -0.55f), new Vector3(3.4f, 0.12f, 0.12f));
            Block(gate, ironMat, new Vector3(0f, 2.9f, -0.55f), new Vector3(3.4f, 0.12f, 0.12f));
        }

        // -- Standing stones with faint cold-silver runes ----------------------------

        private static void BuildStandingStones(Transform root, Material stoneMat)
        {
            var rng = new System.Random(7); // deterministic layout every run
            Vector3[] spots =
            {
                new(-3.5f, 0f, 10f),
                new(10f, 0f, -1f),
                new(-10.5f, 0f, 4f),
            };

            foreach (var spot in spots)
            {
                var stone = Block(root, stoneMat, spot + new Vector3(0f, 1.6f, 0f),
                                  new Vector3(1.0f, 3.2f, 0.7f));
                stone.name = "Standing Stone";
                stone.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 6f - 3f,
                    (float)rng.NextDouble() * 360f,
                    (float)rng.NextDouble() * 8f - 4f);

                // Thin rune strip — old magic, barely glowing cold silver.
                var rune = GameObject.CreatePrimitive(PrimitiveType.Cube);
                rune.name = "Rune Strip";
                Object.DestroyImmediate(rune.GetComponent<Collider>());
                rune.transform.SetParent(stone.transform, false);
                rune.transform.localPosition = new Vector3(0f, 0.05f, 0.52f);
                rune.transform.localScale = new Vector3(0.12f, 0.7f, 0.03f);
                rune.GetComponent<Renderer>().material =
                    SpellbladeFx.MakeEmissive(ColdSilver * 0.4f, ColdSilver, 1.1f);
                rune.AddComponent<RunePulse>();
            }
        }

        // -- Scattered rubble along the walls ----------------------------------------

        private static void BuildRubble(Transform root, float half, Material mat)
        {
            var rng = new System.Random(42);
            for (int i = 0; i < 16; i++)
            {
                // Bias rubble toward the perimeter so the fighting space stays open.
                float angle = (float)rng.NextDouble() * Mathf.PI * 2f;
                float dist = half - 2.2f - (float)rng.NextDouble() * 2.5f;
                var pos = new Vector3(Mathf.Cos(angle) * dist, 0f, Mathf.Sin(angle) * dist);

                float s = 0.3f + (float)rng.NextDouble() * 0.55f;
                var chunk = Block(root, mat, pos + new Vector3(0f, s / 2f, 0f), new Vector3(s, s, s));
                chunk.name = "Rubble";
                chunk.transform.rotation = Quaternion.Euler(
                    (float)rng.NextDouble() * 30f, (float)rng.NextDouble() * 360f, (float)rng.NextDouble() * 30f);
            }
        }

        private static GameObject Block(Transform parent, Material mat, Vector3 localPos, Vector3 scale)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.transform.SetParent(parent);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().material = mat;
            return go;
        }
    }

    /// <summary>Slow breathing glow on the standing-stone runes.</summary>
    public class RunePulse : MonoBehaviour
    {
        private Material _mat;
        private Color _baseEmission;
        private float _phase;

        private void Start()
        {
            _mat = GetComponent<Renderer>().material;
            _baseEmission = _mat.GetColor("_EmissionColor");
            _phase = Random.value * Mathf.PI * 2f; // stones don't pulse in sync
        }

        private void Update()
        {
            float pulse = 0.6f + 0.4f * Mathf.Sin(Time.time * 0.8f + _phase);
            _mat.SetColor("_EmissionColor", _baseEmission * pulse);
        }
    }
}
