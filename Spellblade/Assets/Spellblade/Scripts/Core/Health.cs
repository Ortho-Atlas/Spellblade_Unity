using System;
using System.Collections;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Health with damage-over-time and slow hooks. Used by dummies now and by
    /// anything killable later (player, monsters).
    /// </summary>
    public class Health : MonoBehaviour
    {
        public float maxHealth = 120f;

        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        /// <summary>(current, max) — fired on any health change.</summary>
        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;
        /// <summary>(slowPercent, duration) — movement debuff hook.</summary>
        public event Action<float, float> OnSlowed;

        private void Awake() => Current = maxHealth;

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            Current = Mathf.Max(0f, Current - amount);
            OnHealthChanged?.Invoke(Current, maxHealth);

            if (IsDead)
            {
                StopAllCoroutines(); // kill any running DoT
                OnDied?.Invoke();
            }
        }

        public void ApplyDot(float damagePerSecond, float duration)
        {
            if (IsDead || damagePerSecond <= 0f || duration <= 0f) return;
            StartCoroutine(DotRoutine(damagePerSecond, duration));
        }

        public void ApplySlow(float percent, float duration)
        {
            if (IsDead || percent <= 0f) return;
            OnSlowed?.Invoke(percent, duration);
        }

        /// <summary>Reset to full — used by dummy respawn.</summary>
        public void Revive()
        {
            Current = maxHealth;
            OnHealthChanged?.Invoke(Current, maxHealth);
        }

        private IEnumerator DotRoutine(float dps, float duration)
        {
            const float tickInterval = 0.5f;
            float elapsed = 0f;
            while (elapsed < duration && !IsDead)
            {
                yield return new WaitForSeconds(tickInterval);
                elapsed += tickInterval;
                TakeDamage(dps * tickInterval);
            }
        }
    }
}
