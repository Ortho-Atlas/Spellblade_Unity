using UnityEngine;

namespace Spellblade
{
    /// <summary>Simple mana pool with passive regen. Lives on the player.</summary>
    public class ManaPool : MonoBehaviour
    {
        public float maxMana = 100f;
        public float regenPerSecond = 9f;

        public float Current { get; private set; }
        public float Max => maxMana;

        private void Awake() => Current = maxMana;

        private void Update()
        {
            Current = Mathf.Min(maxMana, Current + regenPerSecond * Time.deltaTime);
        }

        /// <summary>Returns true and deducts the cost if there is enough mana.</summary>
        public bool TrySpend(float amount)
        {
            if (Current < amount) return false;
            Current -= amount;
            return true;
        }

        /// <summary>Instant mana refund (Crimson Pact). [PHASE2-02]</summary>
        public void Restore(float amount)
        {
            if (amount <= 0f) return;
            Current = Mathf.Min(maxMana, Current + amount);
        }
    }
}
