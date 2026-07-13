using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Wave survival (Plan 04). Tier 1 = 3 waves (3 Husks / 2H+2C / 3H+3C),
    /// Tier 2+ = 4 waves at +30% enemy HP. 3s breather + "WAVE N" splash between
    /// waves; spawns come from perimeter points with a flash telegraph. Mixed
    /// attunements cycle the counter-wheel so element choice matters.
    /// WavesThenBossObjective reuses everything and overrides the ending.
    /// </summary>
    public class WaveSurvivalObjective : ObjectiveBase
    {
        protected struct WaveDef
        {
            public int husks;
            public int cultists;
            public WaveDef(int h, int c) { husks = h; cultists = c; }
        }

        private int _alive;
        private bool _doneSpawning;
        private int _waveNumber;
        private int _waveTotal;

        protected float HealthMultiplier { get; private set; } = 1f;

        protected override void OnBegin()
        {
            var waves = BuildWaves(Node.difficultyTier);
            HealthMultiplier = Node.difficultyTier >= 2 ? 1.3f : 1f;
            _waveTotal = waves.Count;
            StartCoroutine(RunWaves(waves));
        }

        private static List<WaveDef> BuildWaves(int tier) => tier >= 2
            ? new List<WaveDef> { new(3, 0), new(2, 2), new(3, 2), new(4, 3) }
            : new List<WaveDef> { new(3, 0), new(2, 2), new(3, 3) };

        private IEnumerator RunWaves(List<WaveDef> waves)
        {
            for (int i = 0; i < waves.Count; i++)
            {
                _waveNumber = i + 1;
                Director.SetObjectiveText($"Wave {_waveNumber} / {_waveTotal}");
                Director.ShowSplash($"WAVE {_waveNumber}", new Color(0.85f, 0.82f, 0.92f), 1.2f);
                yield return new WaitForSeconds(1.2f);

                yield return SpawnWave(waves[i]);
                yield return new WaitUntil(() => _alive == 0 && _doneSpawning);

                if (i < waves.Count - 1)
                    yield return new WaitForSeconds(3f); // breather
            }

            OnAllWavesCleared();
        }

        private IEnumerator SpawnWave(WaveDef wave)
        {
            _doneSpawning = false;

            var spawns = new List<EnemyKind>();
            for (int i = 0; i < wave.husks; i++) spawns.Add(EnemyKind.Husk);
            for (int i = 0; i < wave.cultists; i++) spawns.Add(EnemyKind.Cultist);

            for (int i = 0; i < spawns.Count; i++)
            {
                // Mixed attunements: walk the counter-wheel so every wave has
                // targets that punish and targets that shrug off each discipline.
                var element = (ElementType)(i % 4);
                _alive++;
                StartCoroutine(Director.SpawnEnemy(spawns[i], PerimeterPoint(i, spawns.Count), element,
                    enemy => enemy.OnKilled += _ => _alive--,
                    HealthMultiplier));
                yield return new WaitForSeconds(0.25f); // stagger the flashes slightly
            }

            // SpawnEnemy telegraphs for 0.6s — only count the wave as fully out
            // after the last spawn has materialized.
            yield return new WaitForSeconds(0.7f);
            _doneSpawning = true;
        }

        /// <summary>Perimeter ring spawn points — never on top of the player.</summary>
        private Vector3 PerimeterPoint(int index, int count)
        {
            float baseAngle = Random.Range(0f, 360f);
            float angle = (baseAngle + index * (360f / Mathf.Max(count, 1))) * Mathf.Deg2Rad;
            const float radius = 11f;
            return new Vector3(Mathf.Sin(angle) * radius, 0f, Mathf.Cos(angle) * radius);
        }

        /// <summary>Plain wave survival ends here; WavesThenBoss overrides.</summary>
        protected virtual void OnAllWavesCleared()
        {
            Director.Victory();
        }
    }
}
