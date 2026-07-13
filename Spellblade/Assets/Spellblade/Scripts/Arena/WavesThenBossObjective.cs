using System.Collections;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Boss nodes (Plan 04): run the tier's waves, then a boss splash and the
    /// Court Warden. At 50% Warden HP, two Cultist adds join. Victory when the
    /// Warden falls (its surviving summons don't hold the arena hostage).
    /// </summary>
    public class WavesThenBossObjective : WaveSurvivalObjective
    {
        private MiniBoss _warden;
        private bool _addsSummoned;

        protected override void OnAllWavesCleared()
        {
            StartCoroutine(BossPhase());
        }

        private IEnumerator BossPhase()
        {
            Director.SetObjectiveText("Slay the Warden");
            Director.ShowSplash("THE COURT WARDEN", new Color(0.85f, 0.35f, 0.95f), 2f);
            yield return new WaitForSeconds(1.4f);

            // The Warden strides in from the north arc, attuned to its region.
            yield return Director.SpawnEnemy(EnemyKind.Warden, new Vector3(0f, 0f, 10f),
                Director.RegionElement, warden =>
                {
                    _warden = (MiniBoss)warden;
                    _warden.OnKilled += _ => Director.Victory();
                    _warden.Health.OnHealthChanged += OnWardenHealth;
                },
                HealthMultiplier);
        }

        private void OnWardenHealth(float current, float max)
        {
            if (_addsSummoned || current > max * 0.5f) return;
            _addsSummoned = true;

            // Reinforcements at half health: two Cultists on the flanks.
            StartCoroutine(Director.SpawnEnemy(EnemyKind.Cultist, new Vector3(-8f, 0f, 6f),
                Director.RegionElement, null, HealthMultiplier));
            StartCoroutine(Director.SpawnEnemy(EnemyKind.Cultist, new Vector3(8f, 0f, 6f),
                Director.RegionElement, null, HealthMultiplier));
        }
    }
}
