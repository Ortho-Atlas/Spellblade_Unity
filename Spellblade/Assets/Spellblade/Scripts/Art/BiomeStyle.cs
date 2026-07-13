using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Per-biome arena look ([BIOME]): mood lighting, fog, ground/wall palette,
    /// post-processing grade, and the dressing builder. SpellbladeBootstrap
    /// resolves one of these from the node's regionId (or the playground's
    /// preview field) instead of hardcoding the Shadow gloom everywhere.
    /// Regions without their own style yet fall back to Shadow.
    /// </summary>
    public class BiomeStyle
    {
        // Mood
        public Color ambient;
        public Color fogColor;
        public float fogDensity;
        public Color sunColor;
        public float sunIntensity;
        public Vector3 sunAngles;

        // Arena palette
        public Color groundColor;
        public float groundSmoothness;
        public Color wallColor;
        public Color pillarColor;
        public Color cameraBackground;

        /// <summary>Open-field biomes skip the perimeter walls and interior pillars —
        /// the dressing provides the visual boundary (treeline) and the NavMesh
        /// provides the real one (dressing marks the outside NotWalkable).</summary>
        public bool openField;

        // Post grade
        public Color postFilter;
        public float postSaturation;
        public float postContrast;
        public Color vignetteColor;

        /// <summary>Region-specific set dressing, called before the NavMesh bake.</summary>
        public System.Action<float> buildDressing;

        /// <summary>Ambient particle pass for arenas that lay their own geometry
        /// (the traversal corridor): mist for Shadow, spores for Verdant.</summary>
        public System.Action<float> buildAmbientParticles;

        public static BiomeStyle For(string regionId) => regionId switch
        {
            "verdant" => Verdant,
            _ => Shadow, // every un-styled region keeps the Scotland gloom for now
        };

        /// <summary>The Shadow Reach — the Phase 1 look, values unchanged.</summary>
        public static readonly BiomeStyle Shadow = new()
        {
            ambient = new Color(0.11f, 0.12f, 0.13f),
            fogColor = new Color(0.10f, 0.115f, 0.11f),
            fogDensity = 0.028f,
            sunColor = new Color(0.72f, 0.76f, 0.82f), // cold silver skylight
            sunIntensity = 0.55f,
            sunAngles = new Vector3(58f, -25f, 0f),

            groundColor = new Color(0.085f, 0.125f, 0.09f),
            groundSmoothness = 0.55f,
            wallColor = new Color(0.115f, 0.115f, 0.135f),
            pillarColor = new Color(0.15f, 0.15f, 0.17f),
            cameraBackground = new Color(0.02f, 0.02f, 0.05f),

            postFilter = new Color(0.86f, 0.92f, 0.94f), // cold silver-gray cast
            postSaturation = -22f,
            postContrast = 16f,
            vignetteColor = new Color(0.01f, 0.01f, 0.03f),

            buildDressing = ShadowBiomeArt.Build,
            buildAmbientParticles = SpellbladeParticles.GroundMist,
        };

        /// <summary>The Verdant Deep — earth magic (v2 per Ryan): an open sunlit
        /// meadow ringed by forest. Bright, airy, alive — grass and wildflowers
        /// under open sky, the treeline holding the edge of the world.</summary>
        public static readonly BiomeStyle Verdant = new()
        {
            ambient = new Color(0.21f, 0.24f, 0.17f),  // open-sky bounce light
            fogColor = new Color(0.42f, 0.52f, 0.36f), // sunlit green distance haze
            fogDensity = 0.010f,                        // thin — the meadow breathes
            sunColor = new Color(1.00f, 0.93f, 0.74f), // late-afternoon gold
            sunIntensity = 1.1f,
            sunAngles = new Vector3(50f, -38f, 0f),

            groundColor = new Color(0.14f, 0.20f, 0.09f), // living grass
            groundSmoothness = 0.25f,                      // dry meadow, not wet stone
            wallColor = new Color(0.13f, 0.15f, 0.11f),    // (corridor use — mossy stone)
            pillarColor = new Color(0.16f, 0.18f, 0.13f),
            cameraBackground = new Color(0.05f, 0.08f, 0.04f),
            openField = true, // treeline is the boundary, not walls

            postFilter = new Color(1.00f, 0.99f, 0.90f), // warm, faintly golden
            postSaturation = 0f,                          // the meadow keeps its color
            postContrast = 8f,
            vignetteColor = new Color(0.02f, 0.04f, 0.015f),

            buildDressing = VerdantBiomeArt.Build,
            buildAmbientParticles = VerdantBiomeArt.SporeMotes,
        };
    }
}
