using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// The canonical spell roster factory ([PHASE2-05] — moved out of
    /// SpellbladeBootstrap so the Sanctum, which lives on the map scene with no
    /// SpellCaster, can read the same 16 spells for its unlock/rank cards).
    /// Four disciplines × 4 wheel slots, generated in code, stable spellIds
    /// "umbra_1".."blood_4". Wheel slot order: 0 top, then clockwise.
    /// Every call returns FRESH instances — safe to rank-mutate per arena load.
    /// </summary>
    public static class SpellLibrary
    {
        public static List<Discipline> CreateDisciplines()
        {
            var umbra = new Color(0.55f, 0.20f, 0.95f);
            var frost = new Color(0.30f, 0.85f, 0.95f);
            var storm = new Color(0.75f, 0.85f, 1.00f);
            var blood = new Color(0.85f, 0.10f, 0.20f);

            // ---------------- UMBRA — burst / assassin ----------------

            var umbralLance = SpellSO.Create("Umbral Lance", umbra, AimType.LineSkillshot);
            umbralLance.spellId = "umbra_1";
            umbralLance.element = ElementType.Umbra;
            umbralLance.manaCost = 18f;
            umbralLance.cooldown = 2.5f;
            umbralLance.damage = 32f;
            umbralLance.projectileSpeed = 26f;
            umbralLance.range = 16f;
            umbralLance.projectileRadius = 0.35f;

            var shadowstep = SpellSO.Create("Shadowstep", umbra, AimType.Blink);
            shadowstep.spellId = "umbra_2";
            shadowstep.element = ElementType.Umbra;
            shadowstep.manaCost = 20f;
            shadowstep.cooldown = 6f;
            shadowstep.blinkDistance = 8f;
            shadowstep.damage = 12f;      // the shadow-burst left at the origin
            shadowstep.selfRadius = 2.2f;

            var creepingDark = SpellSO.Create("Creeping Dark", umbra, AimType.PointAoE);
            creepingDark.spellId = "umbra_3";
            creepingDark.element = ElementType.Umbra;
            creepingDark.manaCost = 26f;
            creepingDark.cooldown = 7f;
            creepingDark.damage = 10f;
            creepingDark.range = 12f;
            creepingDark.aoeRadius = 3.5f;
            creepingDark.dotDamagePerSecond = 10f;
            creepingDark.dotDuration = 3f;

            var nightNova = SpellSO.Create("Night Nova", umbra, AimType.SelfNova);
            nightNova.spellId = "umbra_4";
            nightNova.element = ElementType.Umbra;
            nightNova.manaCost = 32f;
            nightNova.cooldown = 9f;
            nightNova.damage = 34f;
            nightNova.selfRadius = 3.2f;

            // ---------------- FROST — control ----------------

            var rimeblast = SpellSO.Create("Rimeblast", frost, AimType.LineSkillshot);
            rimeblast.spellId = "frost_1";
            rimeblast.element = ElementType.Frost;
            rimeblast.manaCost = 22f;
            rimeblast.cooldown = 5f;
            rimeblast.damage = 24f;
            rimeblast.projectileSpeed = 18f;
            rimeblast.range = 9f;
            rimeblast.projectileRadius = 0.5f;
            rimeblast.slowPercent = 0.35f;
            rimeblast.slowDuration = 2.5f;

            var glacialSpike = SpellSO.Create("Glacial Spike", frost, AimType.LineSkillshot);
            glacialSpike.spellId = "frost_2";
            glacialSpike.element = ElementType.Frost;
            glacialSpike.manaCost = 30f;
            glacialSpike.cooldown = 8f;
            glacialSpike.damage = 46f;
            glacialSpike.projectileSpeed = 14f;
            glacialSpike.range = 14f;
            glacialSpike.projectileRadius = 0.6f;

            var frostNova = SpellSO.Create("Frost Nova", frost, AimType.SelfNova);
            frostNova.spellId = "frost_3";
            frostNova.element = ElementType.Frost;
            frostNova.manaCost = 28f;
            frostNova.cooldown = 9f;
            frostNova.damage = 18f;
            frostNova.selfRadius = 4f;
            frostNova.slowPercent = 0.45f;
            frostNova.slowDuration = 3f;

            var iceWard = SpellSO.Create("Ice Ward", frost, AimType.SelfNova);
            iceWard.spellId = "frost_4";
            iceWard.element = ElementType.Frost;
            iceWard.manaCost = 26f;
            iceWard.cooldown = 14f;
            iceWard.damage = 0f;
            iceWard.shieldAmount = 40f;
            iceWard.shieldDuration = 6f;

            // ---------------- STORM — speed / chain ----------------

            var tempestBolt = SpellSO.Create("Tempest Bolt", storm, AimType.LineSkillshot);
            tempestBolt.spellId = "storm_1";
            tempestBolt.element = ElementType.Storm;
            tempestBolt.manaCost = 20f;
            tempestBolt.cooldown = 3.5f;
            tempestBolt.damage = 26f;
            tempestBolt.projectileSpeed = 34f;
            tempestBolt.range = 18f;
            tempestBolt.projectileRadius = 0.25f;

            var chainSpark = SpellSO.Create("Chain Spark", storm, AimType.LineSkillshot);
            chainSpark.spellId = "storm_2";
            chainSpark.element = ElementType.Storm;
            chainSpark.manaCost = 24f;
            chainSpark.cooldown = 5f;
            chainSpark.damage = 20f;
            chainSpark.projectileSpeed = 26f;
            chainSpark.range = 16f;
            chainSpark.projectileRadius = 0.3f;
            chainSpark.chainCount = 3;
            chainSpark.chainRadius = 5f;

            var zephyrRush = SpellSO.Create("Zephyr Rush", storm, AimType.SelfNova);
            zephyrRush.spellId = "storm_3";
            zephyrRush.element = ElementType.Storm;
            zephyrRush.manaCost = 18f;
            zephyrRush.cooldown = 10f;
            zephyrRush.damage = 0f;
            zephyrRush.selfRadius = 1.6f; // wind burst visual only
            zephyrRush.hasteMultiplier = 1.45f;
            zephyrRush.hasteDuration = 3.5f;

            var thunderhead = SpellSO.Create("Thunderhead", storm, AimType.PointAoE);
            thunderhead.spellId = "storm_4";
            thunderhead.element = ElementType.Storm;
            thunderhead.manaCost = 30f;
            thunderhead.cooldown = 8f;
            thunderhead.damage = 44f;
            thunderhead.range = 12f;
            thunderhead.aoeRadius = 2.5f;
            thunderhead.aoeDelaySeconds = 0.8f; // telegraphed strike

            // ---------------- BLOOD — DoT / lifesteal ----------------

            var hemorrhage = SpellSO.Create("Hemorrhage", blood, AimType.PointAoE);
            hemorrhage.spellId = "blood_1";
            hemorrhage.element = ElementType.Blood;
            hemorrhage.manaCost = 30f;
            hemorrhage.cooldown = 7f;
            hemorrhage.damage = 20f;
            hemorrhage.range = 12f;
            hemorrhage.aoeRadius = 2.75f;
            hemorrhage.dotDamagePerSecond = 8f;
            hemorrhage.dotDuration = 4f;

            var leechBolt = SpellSO.Create("Leech Bolt", blood, AimType.LineSkillshot);
            leechBolt.spellId = "blood_2";
            leechBolt.element = ElementType.Blood;
            leechBolt.manaCost = 22f;
            leechBolt.cooldown = 4f;
            leechBolt.damage = 24f;
            leechBolt.projectileSpeed = 20f;
            leechBolt.range = 14f;
            leechBolt.projectileRadius = 0.35f;
            leechBolt.lifestealPercent = 0.4f;

            var crimsonPact = SpellSO.Create("Crimson Pact", blood, AimType.SelfNova);
            crimsonPact.spellId = "blood_3";
            crimsonPact.element = ElementType.Blood;
            crimsonPact.manaCost = 0f;
            crimsonPact.cooldown = 12f;
            crimsonPact.damage = 0f;
            crimsonPact.selfRadius = 1.4f; // blood-rite visual only
            crimsonPact.healthCost = 15f;
            crimsonPact.manaRestore = 40f;

            var bloodNova = SpellSO.Create("Blood Nova", blood, AimType.SelfNova);
            bloodNova.spellId = "blood_4";
            bloodNova.element = ElementType.Blood;
            bloodNova.manaCost = 34f;
            bloodNova.cooldown = 11f;
            bloodNova.damage = 16f;
            bloodNova.selfRadius = 3.5f;
            bloodNova.dotDamagePerSecond = 9f;
            bloodNova.dotDuration = 4f;
            bloodNova.sustainHealPerSecond = 3f;

            return new List<Discipline>
            {
                Discipline.Create("Umbra", umbra, "1", umbralLance, shadowstep, creepingDark, nightNova),
                Discipline.Create("Frost", frost, "2", rimeblast, glacialSpike, frostNova, iceWard),
                Discipline.Create("Storm", storm, "3", tempestBolt, chainSpark, zephyrRush, thunderhead),
                Discipline.Create("Blood", blood, "4", hemorrhage, leechBolt, crimsonPact, bloodNova),
            };
        }
    }
}
