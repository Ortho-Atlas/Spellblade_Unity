using UnityEngine;

namespace Spellblade
{
    /// <summary>How a spell is aimed from the cursor.</summary>
    public enum AimType
    {
        LineSkillshot, // fires a projectile from the caster toward the cursor direction
        PointAoE       // detonates at the cursor's ground point (clamped to range)
    }

    /// <summary>
    /// Data-driven spell definition. Phase 1 generates these at runtime in the
    /// bootstrap (reliable, zero asset-file dependencies), but because it's a
    /// ScriptableObject you can also save them as .asset files later and tune
    /// them in the Inspector without touching code.
    /// </summary>
    public class SpellSO : ScriptableObject
    {
        [Header("Identity")]
        public string displayName = "Spell";
        public Color themeColor = Color.white;

        [Header("Costs")]
        public float manaCost = 20f;
        public float cooldown = 4f;

        [Header("Damage")]
        public float damage = 30f;
        [Tooltip("Damage per second applied after impact (0 = no DoT).")]
        public float dotDamagePerSecond = 0f;
        public float dotDuration = 0f;

        [Header("Aiming")]
        public AimType aimType = AimType.LineSkillshot;
        [Tooltip("Max travel distance (skillshot) or max cast distance (AoE).")]
        public float range = 14f;

        [Header("Projectile (LineSkillshot)")]
        public float projectileSpeed = 22f;
        public float projectileRadius = 0.35f;

        [Header("Area (PointAoE)")]
        public float aoeRadius = 2.5f;

        [Header("Debuffs")]
        [Tooltip("Movement slow on hit, 0-1 (0.35 = 35% slower). Cosmetic on dummies for now.")]
        public float slowPercent = 0f;
        public float slowDuration = 0f;

        /// <summary>Factory used by the bootstrap to build spells in code.</summary>
        public static SpellSO Create(string name, Color color, AimType aim)
        {
            var spell = CreateInstance<SpellSO>();
            spell.name = name;
            spell.displayName = name;
            spell.themeColor = color;
            spell.aimType = aim;
            return spell;
        }
    }
}
