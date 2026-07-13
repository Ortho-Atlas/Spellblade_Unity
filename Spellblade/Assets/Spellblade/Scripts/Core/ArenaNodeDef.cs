namespace Spellblade
{
    /// <summary>
    /// SHARED CONTRACT (owned by Plan 03 — see Docs/Plans/00-MASTER-OVERVIEW.md).
    /// Plain data describing one arena node on the world map. The map sets
    /// GameSession.CurrentNode to one of these before loading the Arena scene;
    /// Plan 04's ObjectiveDirector configures the fight from it.
    /// Do not add or rename fields without updating the overview contract.
    /// </summary>
    public class ArenaNodeDef
    {
        public string id;               // "shadow_01"
        public string displayName;      // "The Sunken Yard"
        public string regionId;         // "shadow"
        public ObjectiveType objective; // WaveSurvival, WavesThenBoss, Traversal
        public int difficultyTier;      // 1..3 within a region
        public bool isBossNode;
    }

    public enum ObjectiveType { WaveSurvival, WavesThenBoss, Traversal }
}
