namespace Spellblade
{
    /// <summary>
    /// Spell unlock gate (Plan 02, interim). Until Plan 05's progression lands:
    /// playground mode unlocks everything (testing), arena mode unlocks slot 1
    /// of each discipline ("*_1" ids) plus anything already recorded in the
    /// save's unlockedSpells — so Plan 05 can start granting unlocks without
    /// touching this file.
    /// </summary>
    public static class ProgressionGate
    {
        public static bool IsUnlocked(string spellId)
        {
            if (string.IsNullOrEmpty(spellId)) return true;              // pre-roster spells never lock
            if (GameSession.CurrentNode == null) return true;             // playground: all 16 castable
            if (spellId.EndsWith("_1")) return true;                      // every discipline's first spell
            return SaveSystem.Data.unlockedSpells.Contains(spellId);      // Plan 05 earns the rest
        }
    }
}
