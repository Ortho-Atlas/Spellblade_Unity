using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// The Phase 1 "skin" system: a soul-shard crystal orbiting the player with
    /// a glow light and rising wisps, all tinted to the ACTIVE discipline.
    /// Swap disciplines (QWER) and the whole aura retints instantly — the
    /// knight stays black-and-silver; the magic carries the color.
    /// </summary>
    [RequireComponent(typeof(SpellCaster))]
    public class DisciplineAura : MonoBehaviour
    {
        [Header("Orbit")]
        public float orbitRadius = 0.85f;
        public float orbitHeight = 1.55f;
        public float orbitSpeed = 95f;   // degrees per second
        public float bobAmplitude = 0.12f;

        private SpellCaster _caster;
        private Transform _shard;
        private Material _shardMat;
        private Light _glow;
        private ParticleSystem _wisps;
        private Material _staffOrbMat;   // wizard staff orb — retints with the shard
        private Light _staffOrbLight;
        private Color _currentColor;
        private float _angle;

        private void Start()
        {
            _caster = GetComponent<SpellCaster>();

            // Orbiting soul-shard: a small elongated crystal.
            var shardGo = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shardGo.name = "Soul Shard";
            DestroyImmediate(shardGo.GetComponent<Collider>());
            shardGo.transform.localScale = new Vector3(0.14f, 0.34f, 0.14f);
            _shard = shardGo.transform;

            var color = ActiveColor();
            _shardMat = SpellbladeFx.MakeEmissive(color * 0.5f, color, 3f);
            shardGo.GetComponent<Renderer>().material = _shardMat;

            _glow = shardGo.AddComponent<Light>();
            _glow.type = LightType.Point;
            _glow.range = 5f;
            _glow.intensity = 2.6f;

            _wisps = SpellbladeParticles.AuraWisps(_shard, color);

            // Find the wizard staff orb (created by WizardGear) so it retints too.
            foreach (var r in GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name != "Staff Orb") continue;
                _staffOrbMat = r.material;
                _staffOrbLight = r.GetComponent<Light>();
                break;
            }

            ApplyColor(color);
        }

        private void Update()
        {
            if (_shard == null) return;

            // Orbit + bob + slow spin.
            _angle += orbitSpeed * Time.deltaTime;
            float rad = _angle * Mathf.Deg2Rad;
            float bob = Mathf.Sin(Time.time * 2.1f) * bobAmplitude;
            _shard.position = transform.position +
                new Vector3(Mathf.Cos(rad) * orbitRadius, orbitHeight + bob, Mathf.Sin(rad) * orbitRadius);
            _shard.Rotate(Vector3.up, 120f * Time.deltaTime, Space.World);
            _shard.rotation = Quaternion.Euler(45f, _shard.rotation.eulerAngles.y, 45f);

            // Retint the moment the active discipline changes.
            var color = ActiveColor();
            if (color != _currentColor) ApplyColor(color);
        }

        private Color ActiveColor() =>
            _caster.Active != null ? _caster.Active.themeColor : Color.white;

        private void ApplyColor(Color color)
        {
            _currentColor = color;
            _shardMat.SetColor("_BaseColor", color * 0.5f);
            _shardMat.SetColor("_EmissionColor", color * 3f);
            _glow.color = color;

            var main = _wisps.main;
            main.startColor = color;

            if (_staffOrbMat != null)
            {
                _staffOrbMat.SetColor("_BaseColor", color * 0.5f);
                _staffOrbMat.SetColor("_EmissionColor", color * 3f);
            }
            if (_staffOrbLight != null) _staffOrbLight.color = color;
        }
    }
}
