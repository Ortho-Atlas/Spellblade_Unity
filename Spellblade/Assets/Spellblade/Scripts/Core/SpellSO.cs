using UnityEngine;

namespace Spellblade
{
    /// <summary>How a spell is aimed from the cursor.</summary>
    public enum AimType
    {
        LineSkillshot, // fires a projectile from the caster toward the cursor direction
        PointAoE,      // detonates at the cursor's ground point (clamped to range)
        SelfNova,      // [PHASE2-02] detonates centered on the caster (also carries self-buffs)
        Blink          // [PHASE2-02] teleport toward the cursor, NavMesh-sampled, never through walls
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
        [Tooltip("Element for the counter-wheel: enemies resist their own element, break to its counter.")]
        public ElementType element = ElementType.Umbra;

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

        // ---------------------------------------------------------- [PHASE2-02]
        // Full-roster mechanics. All default 0/off — a spell opts in per field.

        [Header("Identity (Phase 2)")]
        [Tooltip("Stable id ('umbra_1'..'blood_4'). Progression, saves, and the wheel key off it.")]
        public string spellId = "";

        [Header("Self / Blink")]
        [Tooltip("SelfNova blast radius (and Blink's origin-burst radius).")]
        public float selfRadius = 0f;
        public float blinkDistance = 0f;

        [Header("Chains")]
        [Tooltip("Extra jumps after the first hit (Chain Spark = 3). Damage decays 15% per jump.")]
        public int chainCount = 0;
        public float chainRadius = 0f;
        [Tooltip("Targets a skillshot passes through before stopping (Umbral Lance III = 1). [PHASE2-05]")]
        public int pierceCount = 0;

        [Header("Sustain")]
        [Tooltip("Fraction of damage dealt returned as caster HP (0.4 = 40%).")]
        public float lifestealPercent = 0f;
        [Tooltip("HP per second healed while this spell's DoT ticks on at least one living target.")]
        public float sustainHealPerSecond = 0f;

        [Header("Buffs")]
        public float hasteMultiplier = 0f;
        public float hasteDuration = 0f;
        public float shieldAmount = 0f;
        public float shieldDuration = 0f;

        [Header("Blood Price / Restore")]
        public float healthCost = 0f;
        public float manaRestore = 0f;

        [Header("Delayed AoE")]
        [Tooltip("PointAoE telegraph delay (Thunderhead = 0.8s). 0 = instant.")]
        public float aoeDelaySeconds = 0f;

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
