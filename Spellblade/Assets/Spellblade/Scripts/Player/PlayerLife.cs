using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// The player's mortality (Plan 04). Sits next to Health on the player:
    /// pulses the URP vignette blood-red on hits, and on death either fades to
    /// black and reports defeat (arena mode) or respawns at the entry point
    /// (playground, CurrentNode == null). No regen in arenas — sustain comes
    /// from future spells.
    /// </summary>
    [RequireComponent(typeof(Health))]
    public class PlayerLife : MonoBehaviour
    {
        public float fadeSeconds = 1.2f;
        public float respawnDelay = 1f;

        private Health _health;
        private NavMeshAgent _agent;
        private Vector3 _spawnPoint;
        private float _lastKnownHealth;
        private bool _dying;

        private Vignette _vignette;
        private float _baseVignetteIntensity;
        private Color _baseVignetteColor;
        private Coroutine _pulse;

        private void Start()
        {
            _health = GetComponent<Health>();
            _agent = GetComponent<NavMeshAgent>();
            _spawnPoint = transform.position;
            _lastKnownHealth = _health.Current;

            _health.OnHealthChanged += OnHealthChanged;
            _health.OnDied += OnDied;

            // The bootstrap's global volume exists by the time Start runs.
            var volume = FindAnyObjectByType<Volume>();
            if (volume != null && volume.profile != null &&
                volume.profile.TryGet(out _vignette))
            {
                _baseVignetteIntensity = _vignette.intensity.value;
                _baseVignetteColor = _vignette.color.value;
            }
        }

        private void OnHealthChanged(float current, float max)
        {
            if (current < _lastKnownHealth && !_dying)
            {
                if (_pulse != null) StopCoroutine(_pulse);
                _pulse = StartCoroutine(VignettePulse());
            }
            _lastKnownHealth = current;
        }

        private IEnumerator VignettePulse()
        {
            if (_vignette == null) yield break;

            const float peak = 0.52f;
            var bloodRed = new Color(0.45f, 0.02f, 0.03f);
            const float decay = 0.4f;

            _vignette.intensity.Override(peak);
            _vignette.color.Override(bloodRed);

            float t = 0f;
            while (t < decay)
            {
                t += Time.deltaTime;
                float k = t / decay;
                _vignette.intensity.Override(Mathf.Lerp(peak, _baseVignetteIntensity, k));
                _vignette.color.Override(Color.Lerp(bloodRed, _baseVignetteColor, k));
                yield return null;
            }
            _vignette.intensity.Override(_baseVignetteIntensity);
            _vignette.color.Override(_baseVignetteColor);
        }

        private void OnDied()
        {
            if (_dying) return;
            _dying = true;
            SetControlsEnabled(false);

            if (GameSession.CurrentNode != null) StartCoroutine(FadeAndReport());
            else StartCoroutine(PlaygroundRespawn());
        }

        private void SetControlsEnabled(bool enabled)
        {
            var wasd = GetComponent<WasdController>();
            if (wasd != null) wasd.enabled = enabled;
            var melee = GetComponent<MeleeStrike>();
            if (melee != null) melee.enabled = enabled;
            var caster = GetComponent<SpellCaster>();
            if (caster != null) caster.enabled = enabled;
            if (_agent != null && _agent.isOnNavMesh) _agent.isStopped = !enabled;
        }

        /// <summary>Arena death: slow fade to black, then back to the map with no clear.</summary>
        private IEnumerator FadeAndReport()
        {
            var canvasGo = new GameObject("Death Fade", typeof(Canvas), typeof(Image));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 90; // over everything, including banners

            var image = canvasGo.GetComponent<Image>();
            image.color = new Color(0f, 0f, 0f, 0f);
            var rect = image.rectTransform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.sizeDelta = Vector2.zero;

            float t = 0f;
            while (t < fadeSeconds)
            {
                t += Time.deltaTime;
                image.color = new Color(0f, 0f, 0f, Mathf.Clamp01(t / fadeSeconds));
                yield return null;
            }

            var director = FindAnyObjectByType<ObjectiveDirector>();
            if (director != null) director.Defeat();
            else GameSession.ReportArenaResult(false);
        }

        /// <summary>Playground death: brief pause, then back on your feet at the spawn point.</summary>
        private IEnumerator PlaygroundRespawn()
        {
            SpellbladeFx.Flash(transform.position + Vector3.up, new Color(0.9f, 0.2f, 0.3f), 1.4f, 0.4f);
            yield return new WaitForSeconds(respawnDelay);

            if (_agent != null) _agent.Warp(_spawnPoint);
            else transform.position = _spawnPoint;

            _health.Revive();
            _lastKnownHealth = _health.Current;
            SetControlsEnabled(true);
            _dying = false;
            SpellbladeFx.Flash(transform.position + Vector3.up, new Color(0.5f, 0.9f, 0.6f), 1f, 0.35f);
        }
    }
}
