using UnityEngine;

namespace Spellblade
{
    /// <summary>
    /// Floating combat text. Reads the counter-wheel at a glance:
    ///   WEAK hit    → big gold number with "!"
    ///   RESISTED    → small gray number
    ///   normal      → white number
    /// Rises, fades, billboards to the camera, self-destructs.
    /// </summary>
    public class DamageNumber : MonoBehaviour
    {
        private const float Life = 0.85f;
        private const float RiseSpeed = 1.4f;

        private TextMesh _text;
        private float _age;
        private Color _color;

        public static void Spawn(Vector3 position, float amount, float modifier)
        {
            var go = new GameObject("Damage Number");
            go.transform.position = position;

            var tm = go.AddComponent<TextMesh>();
            bool weak = modifier > 1.3f;
            bool resisted = modifier < 0.7f;

            tm.text = Mathf.Max(1, Mathf.RoundToInt(amount)) + (weak ? "!" : "");
            tm.anchor = TextAnchor.MiddleCenter;
            tm.alignment = TextAlignment.Center;
            tm.fontSize = 64;
            tm.characterSize = weak ? 0.05f : resisted ? 0.026f : 0.035f;
            tm.fontStyle = weak ? FontStyle.Bold : FontStyle.Normal;
            tm.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            tm.GetComponent<MeshRenderer>().material = tm.font.material;

            var number = go.AddComponent<DamageNumber>();
            number._text = tm;
            number._color = weak ? new Color(1.0f, 0.82f, 0.25f)
                          : resisted ? new Color(0.55f, 0.55f, 0.60f)
                          : Color.white;
            tm.color = number._color;
        }

        private void Update()
        {
            _age += Time.deltaTime;
            transform.position += Vector3.up * (RiseSpeed * Time.deltaTime);

            // Fade out over the back half of the lifetime.
            float alpha = 1f - Mathf.Clamp01((_age - Life * 0.4f) / (Life * 0.6f));
            _text.color = new Color(_color.r, _color.g, _color.b, alpha);

            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            if (_age >= Life) Destroy(gameObject);
        }
    }
}
