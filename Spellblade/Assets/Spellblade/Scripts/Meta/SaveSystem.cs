using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// SHARED CONTRACT (owned by Plan 03 — see Docs/Plans/00-MASTER-OVERVIEW.md).
    /// Persistent JSON save at Application.persistentDataPath/spellblade_save.json.
    /// Loaded lazily on first access; a missing or unreadable file becomes a fresh
    /// save with the Shadow Reach unlocked. Plan 05 extends USAGE (earning,
    /// spending, gear) — the file format lives here.
    /// </summary>
    public static class SaveSystem
    {
        private static SaveData _data;

        public static SaveData Data => _data ??= Load();

        private static string FilePath => Path.Combine(Application.persistentDataPath, "spellblade_save.json");

        public static void Save()
        {
            try
            {
                File.WriteAllText(FilePath, JsonUtility.ToJson(Data, prettyPrint: true));
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[Spellblade] Save failed: {e.Message}");
            }
        }

        private static SaveData Load()
        {
            try
            {
                if (File.Exists(FilePath))
                {
                    var loaded = JsonUtility.FromJson<SaveData>(File.ReadAllText(FilePath));
                    if (loaded != null)
                    {
                        // Old or hand-edited saves must still know the starting region.
                        if (!loaded.unlockedRegions.Contains("shadow"))
                            loaded.unlockedRegions.Add("shadow");
                        return loaded;
                    }
                }
            }
            catch (System.Exception e)
            {
                Debug.LogWarning($"[Spellblade] Save unreadable ({e.Message}) — starting fresh.");
            }

            return new SaveData { unlockedRegions = new List<string> { "shadow" } };
        }
    }

    [System.Serializable]
    public class SaveData
    {
        public List<string> clearedNodes = new();      // ArenaNodeDef.id
        public List<string> unlockedRegions = new();   // starts ["shadow"]
        public int arcaneEssence;                       // stat-upgrade currency
        public int elementShards;                       // spell unlock/rank currency
        public List<string> unlockedSpells = new();    // spell ids, e.g. "umbra_2"
        public List<string> spellRanks = new();        // "spellId:rank", e.g. "umbra_1:2"
        public List<string> statUpgrades = new();      // "hp:3" style "stat:level"
        public List<string> unlockedGear = new();      // cosmetic ids
        public List<string> equippedGear = new();
    }
}
