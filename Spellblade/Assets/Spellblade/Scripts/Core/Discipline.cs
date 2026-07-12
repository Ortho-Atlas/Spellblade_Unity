using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// A magical discipline (Umbra / Frost / Storm / Blood). Q/W/E/R swaps the
    /// active discipline; left-click casts its primary spell (spells[0]).
    /// Additional spells per discipline slot in later phases.
    /// </summary>
    public class Discipline : ScriptableObject
    {
        public string displayName = "Discipline";
        public Color themeColor = Color.white;

        [Tooltip("Which key selects this discipline (display only — binding order is set by the caster's list).")]
        public string keyLabel = "Q";

        [Tooltip("Primary spell first. Secondary (Shift+LMB) and beyond come later.")]
        public List<SpellSO> spells = new();

        public SpellSO Primary => spells.Count > 0 ? spells[0] : null;

        public static Discipline Create(string name, Color color, string keyLabel, params SpellSO[] spells)
        {
            var d = CreateInstance<Discipline>();
            d.name = name;
            d.displayName = name;
            d.themeColor = color;
            d.keyLabel = keyLabel;
            d.spells.AddRange(spells);
            return d;
        }
    }
}
