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

        // [PHASE2-02] Damage shield (Ice Ward): absorbs before HP, expires on its own.
        public float Shield { get; private set; }
        private float _shieldExpiry;
        public bool IsShielded => Shield > 0f && Time.time < _shieldExpiry;

        /// <summary>(current, max) — fired on any health change.</summary>
        public event Action<float, float> OnHealthChanged;
        public event Action OnDied;
        /// <summary>(slowPercent, duration) — movement debuff hook.</summary>
        public event Action<float, float> OnSlowed;

        private void Awake() => Current = maxHealth;

        public void TakeDamage(float amount)
        {
            if (IsDead) return;

            if (IsShielded) // [PHASE2-02] shield soaks first
            {
                float absorbed = Mathf.Min(Shield, amount);
                Shield -= absorbed;
                amount -= absorbed;
                if (amount <= 0f) return;
            }

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

        // ------------------------------------------------------------ [PHASE2-02]

        /// <summary>Restore HP (lifesteal, Blood Nova sustain). No effect when dead.</summary>
        public void Heal(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Min(maxHealth, Current + amount);
            OnHealthChanged?.Invoke(Current, maxHealth);
        }

        /// <summary>Voluntary HP payment (Crimson Pact) — bypasses the shield, never
        /// lethal by itself. Callers gate castability so this can't hit the floor.</summary>
        public void SpendHealth(float amount)
        {
            if (IsDead || amount <= 0f) return;
            Current = Mathf.Max(1f, Current - amount);
            OnHealthChanged?.Invoke(Current, maxHealth);
        }

        /// <summary>Damage shield: absorbs before HP until depleted or expired.
        /// Recasting refreshes rather than stacks.</summary>
        public void AddShield(float amount, float duration)
        {
            Shield = amount;
            _shieldExpiry = Time.time + duration;
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
