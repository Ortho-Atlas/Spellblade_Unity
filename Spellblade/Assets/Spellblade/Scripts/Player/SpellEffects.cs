using System.Collections.Generic;
using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Zephyr Rush: multiplies WasdController speed for a duration, with a wind
    /// wisp trail. Recasting refreshes the timer (no stacking).
    /// </summary>
    public class HasteEffect : MonoBehaviour
    {
        private WasdController _controller;
        private float _expiresAt;
        private ParticleSystem _trail;

        public static void Apply(GameObject player, float multiplier, float duration, Color color)
        {
            var effect = player.GetComponent<HasteEffect>();
            if (effect == null)
            {
                effect = player.AddComponent<HasteEffect>();
                effect._controller = player.GetComponent<WasdController>();
                effect._trail = SpellbladeParticles.AuraWisps(player.transform, color);
                var main = effect._trail.main;
                main.startSpeed = 1.4f; // streakier than the ambient aura wisps
            }
            if (effect._controller != null) effect._controller.SpeedMultiplier = multiplier;
            effect._expiresAt = Time.time + duration;
        }

        private void Update()
        {
            if (Time.time < _expiresAt) return;
            if (_controller != null) _controller.SpeedMultiplier = 1f;
            if (_trail != null) Destroy(_trail.gameObject);
            Destroy(this);
        }

        private void OnDestroy()
        {
            if (_controller != null) _controller.SpeedMultiplier = 1f;
            if (_trail != null) Destroy(_trail.gameObject);
        }
    }

    /// <summary>
    /// Ice Ward's bubble: translucent icy sphere that lives while the shield
    /// holds, vanishing when it breaks or expires.
    /// </summary>
    public class WardVisual : MonoBehaviour
    {
        private Health _health;
        private GameObject _bubble;

        public static void Attach(Transform player, Color color)
        {
            var existing = player.GetComponent<WardVisual>();
            if (existing != null) Destroy(existing._bubble);
            var visual = existing != null ? existing : player.gameObject.AddComponent<WardVisual>();
            visual._health = player.GetComponent<Health>();

            visual._bubble = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            visual._bubble.name = "Ward Bubble";
            Destroy(visual._bubble.GetComponent<Collider>());
            visual._bubble.transform.SetParent(player, false);
            visual._bubble.transform.localPosition = Vector3.up * 1.05f;
            visual._bubble.transform.localScale = Vector3.one * 2.5f;
            visual._bubble.GetComponent<Renderer>().material =
                SpellbladeFx.MakeUnlitTransparent(new Color(color.r, color.g, color.b, 0.16f));
        }

        private void Update()
        {
            if (_health != null && _health.IsShielded) return;
            if (_bubble != null) Destroy(_bubble);
            Destroy(this);
        }
    }

    /// <summary>
    /// Blood Nova's pact: heals the caster per second while at least one of the
    /// nova's DoT-afflicted targets is still alive, for the DoT's duration.
    /// </summary>
    public class BloodSustain : MonoBehaviour
    {
        private readonly List<Health> _targets = new();
        private Health _casterHealth;
        private float _healPerSecond;
        private float _expiresAt;

        public static void Apply(GameObject player, IEnumerable<Health> targets,
                                 float healPerSecond, float duration)
        {
            var sustain = player.GetComponent<BloodSustain>();
            if (sustain == null)
            {
                sustain = player.AddComponent<BloodSustain>();
                sustain._casterHealth = player.GetComponent<Health>();
            }
            sustain._targets.Clear();
            sustain._targets.AddRange(targets);
            sustain._healPerSecond = healPerSecond;
            sustain._expiresAt = Time.time + duration;
        }

        private void Update()
        {
            if (Time.time >= _expiresAt) { Destroy(this); return; }

            bool anyAlive = false;
            foreach (var target in _targets)
                if (target != null && !target.IsDead) { anyAlive = true; break; }

            if (anyAlive && _casterHealth != null)
                _casterHealth.Heal(_healPerSecond * Time.deltaTime);
        }
    }
}
