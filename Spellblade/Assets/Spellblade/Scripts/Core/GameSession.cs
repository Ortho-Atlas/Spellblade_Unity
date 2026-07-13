using UnityEngine;
using UnityEngine.SceneManagement;

namespace Spellblade
{
    /// <summary>
    /// SHARED CONTRACT (owned by Plan 03 — see Docs/Plans/00-MASTER-OVERVIEW.md).
    /// Static carrier across scene loads. The world map sets CurrentNode and
    /// loads the Arena scene; the arena reports its result here and control
    /// returns to the map. CurrentNode == null means "playground mode: behave
    /// exactly like Phase 1" — every system must honor that.
    ///
    /// Also home to the hub-and-spokes unlock rules, since they run inside
    /// ReportArenaResult and every screen asks the same questions.
    /// </summary>
    public static class GameSession
    {
        public static ArenaNodeDef CurrentNode; // set by map before loading Arena scene

        /// <summary>Minimum non-boss clears in a region before its boss node opens
        /// (capped at the region's non-boss count, so Frost needs all 3).</summary>
        public const int BossUnlockClears = 3;

        /// <summary>Arena calls this exactly once (win, death, or Esc-abandon),
        /// then the WorldMap scene loads. Victory marks the node cleared and
        /// runs unlock rules; defeat/abandon records nothing.</summary>
        public static void ReportArenaResult(bool victory)
        {
            var node = CurrentNode;
            CurrentNode = null; // never leak arena state into playground mode

            if (victory && node != null)
            {
                var save = SaveSystem.Data;
                if (!save.clearedNodes.Contains(node.id))
                    save.clearedNodes.Add(node.id);

                // Hub-and-spokes: clearing a region's BOSS unlocks its adjacent regions.
                if (node.isBossNode)
                {
                    foreach (var region in RegionDefs.All)
                    {
                        if (!region.unlockedBy.Contains(node.regionId)) continue;
                        if (!save.unlockedRegions.Contains(region.id))
                            save.unlockedRegions.Add(region.id);
                    }
                }

                SaveSystem.Save();
            }

            SceneManager.LoadScene("WorldMap");
        }

        // -- Unlock queries (map UI + tooltips read these) ----------------------

        public static bool IsRegionUnlocked(string regionId) =>
            SaveSystem.Data.unlockedRegions.Contains(regionId) ||
            (RegionDefs.Find(regionId)?.unlockedFromStart ?? false); // [BIOME] dev-preview regions

        public static bool IsNodeCleared(string nodeId) =>
            SaveSystem.Data.clearedNodes.Contains(nodeId);

        /// <summary>Boss opens once 3+ non-boss nodes in its region are cleared
        /// (all of them in regions with fewer than 3).</summary>
        public static bool IsBossUnlocked(string regionId)
        {
            var region = RegionDefs.Find(regionId);
            if (region == null) return false;

            int nonBoss = 0, cleared = 0;
            foreach (var placement in region.nodes)
            {
                if (placement.node.isBossNode) continue;
                nonBoss++;
                if (IsNodeCleared(placement.node.id)) cleared++;
            }
            return nonBoss > 0 && cleared >= Mathf.Min(BossUnlockClears, nonBoss);
        }
    }
}
