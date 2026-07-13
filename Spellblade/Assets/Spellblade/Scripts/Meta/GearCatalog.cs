using System.Collections.Generic;

namespace Spellblade
{
    /// <summary>One cosmetic. Unlocked by feats (never currency); one equipped
    /// item per category — equipping swaps within the category.</summary>
    public class GearDef
    {
        public string id;
        public string displayName;
        public string category;   // "hat" / "plume" / "crystal" / "robe"
        public string featHint;   // how to earn it, shown in the Sanctum
    }

    /// <summary>
    /// Every cosmetic in the game (Plan 05). ObjectiveDirector grants ids on
    /// feats; WizardGear renders equipped ids; the Sanctum's Gear tab is the
    /// wardrobe. Regions beyond Shadow/Frost get entries when their bosses exist.
    /// </summary>
    public static class GearCatalog
    {
        public static readonly List<GearDef> All = new()
        {
            new GearDef { id = "hat_umbral_court", displayName = "Hat of the Umbral Court",
                          category = "hat", featHint = "Clear the Shadow Reach boss" },
            new GearDef { id = "rimeholt_crown", displayName = "Rimeholt Crown",
                          category = "hat", featHint = "Clear the Rimeholt boss" },
            new GearDef { id = "duelists_plume", displayName = "Duelist's Plume",
                          category = "plume", featHint = "Clear any boss without dying" },
            new GearDef { id = "staff_crystal_shadow", displayName = "Umbral Staff Crystal",
                          category = "crystal", featHint = "Clear the Shadow Reach boss" },
            new GearDef { id = "staff_crystal_frost", displayName = "Rimeholt Staff Crystal",
                          category = "crystal", featHint = "Clear the Rimeholt boss" },
            new GearDef { id = "robe_tint_shadow", displayName = "Shadow Reach Robe Trim",
                          category = "robe", featHint = "Clear every arena in the Shadow Reach" },
            new GearDef { id = "robe_tint_frost", displayName = "Rimeholt Robe Trim",
                          category = "robe", featHint = "Clear every arena in the Rimeholt" },
        };

        public static GearDef Find(string id) => All.Find(g => g.id == id);

        public static bool IsUnlocked(string id) => SaveSystem.Data.unlockedGear.Contains(id);
        public static bool IsEquipped(string id) => SaveSystem.Data.equippedGear.Contains(id);

        /// <summary>Unlock by feat — returns true if it was NEW (for the reward toast).</summary>
        public static bool Unlock(string id)
        {
            if (Find(id) == null || IsUnlocked(id)) return false;
            SaveSystem.Data.unlockedGear.Add(id);
            return true;
        }

        /// <summary>Equip, swapping out anything else in the same category. Saves.</summary>
        public static void Equip(string id)
        {
            var def = Find(id);
            if (def == null || !IsUnlocked(id)) return;

            SaveSystem.Data.equippedGear.RemoveAll(other =>
            {
                var otherDef = Find(other);
                return otherDef == null || otherDef.category == def.category;
            });
            SaveSystem.Data.equippedGear.Add(id);
            SaveSystem.Save();
        }

        public static void Unequip(string id)
        {
            SaveSystem.Data.equippedGear.Remove(id);
            SaveSystem.Save();
        }
    }
}
