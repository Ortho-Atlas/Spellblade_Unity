using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;

namespace Spellblade
{
    /// <summary>
    /// All progression numbers in one place (Plan 05): spell unlock costs,
    /// rank costs and effects, stat-upgrade tracks, and the arena-load
    /// application pass. Ranks mutate the RUNTIME roster instances only —
    /// SpellLibrary hands out fresh spells every scene load, so the authored
    /// values are never touched.
    /// </summary>
    public static class ProgressionMath
    {
        public const int MaxRank = 3;
        public const int MaxStatLevel = 5;

        // -- Costs ---------------------------------------------------------------

        /// <summary>Shard cost to unlock a wheel slot (index 0 is free).</summary>
        public static int SlotUnlockCost(int slotIndex) => slotIndex switch
        {
            1 => 4,
            2 => 6,
            3 => 9,
            _ => 0,
        };

        /// <summary>Shard cost to advance FROM the given rank (I→II = 5, II→III = 10).</summary>
        public static int RankUpCost(int currentRank) =>
            currentRank == 1 ? 5 : currentRank == 2 ? 10 : int.MaxValue;

        /// <summary>Essence cost to buy the NEXT level of a stat (10 × level).</summary>
        public static int StatUpgradeCost(int currentLevel) => 10 * (currentLevel + 1);

        // -- "key:value" save-list helpers ----------------------------------------

        private static int ReadKeyed(List<string> list, string key, int fallback)
        {
            foreach (var entry in list)
            {
                int split = entry.LastIndexOf(':');
                if (split <= 0 || entry.Substring(0, split) != key) continue;
                return int.TryParse(entry.Substring(split + 1), out int value) ? value : fallback;
            }
            return fallback;
        }

        private static void WriteKeyed(List<string> list, string key, int value)
        {
            for (int i = 0; i < list.Count; i++)
            {
                int split = list[i].LastIndexOf(':');
                if (split > 0 && list[i].Substring(0, split) == key)
                {
                    list[i] = $"{key}:{value}";
                    return;
                }
            }
            list.Add($"{key}:{value}");
        }

        // -- Ranks -----------------------------------------------------------------

        public static int GetRank(string spellId) =>
            Mathf.Clamp(ReadKeyed(SaveSystem.Data.spellRanks, spellId, 1), 1, MaxRank);

        public static void SetRank(string spellId, int rank) =>
            WriteKeyed(SaveSystem.Data.spellRanks, spellId, Mathf.Clamp(rank, 1, MaxRank));

        /// <summary>Mutate a RUNTIME spell instance to its owned rank.
        /// II = +20% output · III = +40% total + one behavior bump per spell.</summary>
        public static void Apply(SpellSO spell, int rank)
        {
            if (spell == null || rank <= 1) return;

            float mult = rank >= 3 ? 1.4f : 1.2f;
            spell.damage *= mult;
            spell.dotDamagePerSecond *= mult;
            spell.shieldAmount *= mult;
            spell.manaRestore *= mult;

            if (rank < 3) return;
            switch (spell.spellId) // rank III signature bumps
            {
                case "umbra_1": spell.pierceCount = 1; break;                        // Lance pierces one
                case "umbra_2": spell.blinkDistance = 11f; break;                    // Shadowstep 11m
                case "frost_1": spell.slowPercent = 1f; spell.slowDuration = 0.6f; break; // slow → brief root
                case "frost_4": spell.shieldAmount = 70f; break;                     // Ice Ward 70 absorb
                case "storm_2": spell.chainCount += 2; break;                        // Chain Spark +2 chains
                case "storm_4": spell.aoeRadius = 3.2f; break;                       // Thunderhead wider
                case "blood_1": spell.aoeRadius = 3.5f; break;                       // Hemorrhage wider
                case "blood_2": spell.lifestealPercent = 0.6f; break;                // Leech 60%
            }
        }

        // -- Stat tracks -------------------------------------------------------------

        public static readonly (string id, string displayName, string effect)[] StatTracks =
        {
            ("vitality", "Vitality", "+12 max HP per level"),
            ("attunement", "Attunement", "+10 max mana per level"),
            ("meditation", "Meditation", "+1.2 mana regen per level"),
            ("swiftness", "Swiftness", "+0.2 move speed per level"),
            ("bladecraft", "Bladecraft", "+15% melee damage per level"),
        };

        public static int GetStatLevel(string statId) =>
            Mathf.Clamp(ReadKeyed(SaveSystem.Data.statUpgrades, statId, 0), 0, MaxStatLevel);

        public static void SetStatLevel(string statId, int level) =>
            WriteKeyed(SaveSystem.Data.statUpgrades, statId, Mathf.Clamp(level, 0, MaxStatLevel));

        // -- Currencies -----------------------------------------------------------------

        public static bool TrySpendShards(int amount)
        {
            var save = SaveSystem.Data;
            if (save.elementShards < amount) return false;
            save.elementShards -= amount;
            SaveSystem.Save();
            return true;
        }

        public static bool TrySpendEssence(int amount)
        {
            var save = SaveSystem.Data;
            if (save.arcaneEssence < amount) return false;
            save.arcaneEssence -= amount;
            SaveSystem.Save();
            return true;
        }

        // -- Arena-load application ---------------------------------------------------

        /// <summary>Applies owned ranks to the fresh roster and stat upgrades to the
        /// player. Called by the bootstrap in ARENA mode only — playground stays a
        /// clean baseline testbed.</summary>
        public static void ApplyArenaLoadout(SpellCaster caster, GameObject player)
        {
            foreach (var discipline in caster.Disciplines)
                foreach (var spell in discipline.spells)
                    Apply(spell, GetRank(spell.spellId));

            int vitality = GetStatLevel("vitality");
            int attunement = GetStatLevel("attunement");
            int meditation = GetStatLevel("meditation");
            int swiftness = GetStatLevel("swiftness");
            int bladecraft = GetStatLevel("bladecraft");

            var health = player.GetComponent<Health>();
            if (health != null && vitality > 0)
            {
                health.maxHealth += 12f * vitality;
                health.Revive(); // start the arena at the new full HP
            }

            var mana = player.GetComponent<ManaPool>();
            if (mana != null)
            {
                mana.maxMana += 10f * attunement;
                mana.regenPerSecond += 1.2f * meditation;
            }

            var agent = player.GetComponent<NavMeshAgent>();
            if (agent != null) agent.speed += 0.2f * swiftness; // WasdController drives from agent.speed

            var melee = player.GetComponent<MeleeStrike>();
            if (melee != null) melee.baseDamage *= 1f + 0.15f * bladecraft;
        }
    }
}
