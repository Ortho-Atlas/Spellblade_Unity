using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// One biome region on the world map. Names are placeholder data pending
    /// Ryan's lore brain-dump — ids stay stable, displayName is trivially
    /// renameable (see the overview's placeholder table).
    /// </summary>
    public class RegionDef
    {
        public string id;
        public string displayName;
        public Color elementTint;
        public Vector2 mapPosition;               // normalized 0-1 across the map
        public List<string> unlockedBy = new();   // clearing THESE regions' bosses unlocks this one
        public bool unlockedFromStart;            // [BIOME] dev-preview override — no save migration needed, re-gate by flipping
        public string workingStatus;              // "playable" / "locked" — presentation note only
        public List<NodePlacement> nodes = new();
    }

    /// <summary>An arena node plus where it sits on the map. Position lives here,
    /// NOT on ArenaNodeDef — the shared contract stays pure gameplay data.</summary>
    public class NodePlacement
    {
        public ArenaNodeDef node;
        public Vector2 mapPosition; // normalized 0-1, hand-placed inside the region blob

        public NodePlacement(ArenaNodeDef node, float x, float y)
        {
            this.node = node;
            mapPosition = new Vector2(x, y);
        }
    }

    /// <summary>
    /// Static region + node data for the whole world — 8 regions, Shadow playable
    /// with 5 nodes, Frost second with 4, the rest locked presentation only.
    /// </summary>
    public static class RegionDefs
    {
        public static readonly List<RegionDef> All = Build();

        public static RegionDef Find(string regionId) =>
            All.Find(r => r.id == regionId);

        private static ArenaNodeDef Node(string id, string name, string regionId,
                                         ObjectiveType objective, int tier, bool boss = false) =>
            new ArenaNodeDef
            {
                id = id,
                displayName = name,
                regionId = regionId,
                objective = objective,
                difficultyTier = tier,
                isBossNode = boss,
            };

        private static List<RegionDef> Build()
        {
            var muted = new Color(0.45f, 0.45f, 0.50f); // placeholder tint for TBD elements

            var shadow = new RegionDef
            {
                id = "shadow",
                displayName = "The Shadow Reach",
                elementTint = ElementMath.ColorOf(ElementType.Umbra),
                mapPosition = new Vector2(0.24f, 0.36f),
                workingStatus = "playable",
                nodes =
                {
                    new NodePlacement(Node("shadow_01", "The Sunken Yard", "shadow", ObjectiveType.WaveSurvival, 1), 0.17f, 0.30f),
                    new NodePlacement(Node("shadow_02", "Cairn Hollow", "shadow", ObjectiveType.Traversal, 1), 0.24f, 0.24f),
                    new NodePlacement(Node("shadow_03", "The Broken Chapel", "shadow", ObjectiveType.WaveSurvival, 2), 0.31f, 0.33f),
                    new NodePlacement(Node("shadow_04", "Mistgate Causeway", "shadow", ObjectiveType.Traversal, 2), 0.20f, 0.42f),
                    new NodePlacement(Node("shadow_boss", "The Umbral Court", "shadow", ObjectiveType.WavesThenBoss, 3, boss: true), 0.28f, 0.44f),
                }
            };

            var frost = new RegionDef
            {
                id = "frost",
                displayName = "The Rimeholt",
                elementTint = ElementMath.ColorOf(ElementType.Frost),
                mapPosition = new Vector2(0.20f, 0.68f),
                unlockedBy = { "shadow" },
                workingStatus = "playable-second",
                nodes =
                {
                    new NodePlacement(Node("frost_01", "Rimefall Quarry", "frost", ObjectiveType.WaveSurvival, 1), 0.14f, 0.63f),
                    new NodePlacement(Node("frost_02", "The Glass Ravine", "frost", ObjectiveType.Traversal, 2), 0.22f, 0.60f),
                    new NodePlacement(Node("frost_03", "Howling Terrace", "frost", ObjectiveType.WaveSurvival, 2), 0.27f, 0.70f),
                    new NodePlacement(Node("frost_boss", "The Frozen Throne", "frost", ObjectiveType.WavesThenBoss, 3, boss: true), 0.18f, 0.76f),
                }
            };

            var storm = new RegionDef
            {
                id = "storm",
                displayName = "The Tempest Shelf",
                elementTint = ElementMath.ColorOf(ElementType.Storm),
                mapPosition = new Vector2(0.44f, 0.80f),
                unlockedBy = { "frost" },
                workingStatus = "locked",
            };

            var blood = new RegionDef
            {
                id = "blood",
                displayName = "The Crimson Fen",
                elementTint = ElementMath.ColorOf(ElementType.Blood),
                mapPosition = new Vector2(0.60f, 0.30f),
                workingStatus = "locked",
            };

            var ember = new RegionDef
            {
                id = "ember",
                displayName = "The Ember Wastes",
                elementTint = new Color(0.95f, 0.45f, 0.15f),
                mapPosition = new Vector2(0.80f, 0.42f),
                workingStatus = "locked",
            };

            var verdant = new RegionDef
            {
                id = "verdant",
                displayName = "The Verdant Deep",
                elementTint = new Color(0.30f, 0.80f, 0.35f),
                mapPosition = new Vector2(0.52f, 0.55f),
                unlockedFromStart = true, // [BIOME] Ryan's call 2026-07-12: open for exploration; re-gate later via unlockedBy = {"shadow"}
                workingStatus = "playable-preview",
                nodes =
                {
                    new NodePlacement(Node("verdant_01", "Mossgrave Hollow", "verdant", ObjectiveType.WaveSurvival, 1), 0.46f, 0.50f),
                    new NodePlacement(Node("verdant_02", "The Rootway", "verdant", ObjectiveType.Traversal, 1), 0.55f, 0.48f),
                    new NodePlacement(Node("verdant_03", "Elderbough Ring", "verdant", ObjectiveType.WaveSurvival, 2), 0.58f, 0.60f),
                    new NodePlacement(Node("verdant_boss", "Heart of the Deep", "verdant", ObjectiveType.WavesThenBoss, 3, boss: true), 0.49f, 0.62f),
                }
            };

            var sunken = new RegionDef
            {
                id = "sunken",
                displayName = "The Sunken Marches",
                elementTint = muted,
                mapPosition = new Vector2(0.38f, 0.14f),
                workingStatus = "locked",
            };

            var radiant = new RegionDef
            {
                id = "radiant",
                displayName = "The Radiant Steppe",
                elementTint = new Color(0.95f, 0.85f, 0.45f),
                mapPosition = new Vector2(0.82f, 0.74f),
                workingStatus = "locked",
            };

            return new List<RegionDef> { shadow, frost, storm, blood, ember, verdant, sunken, radiant };
        }
    }
}
