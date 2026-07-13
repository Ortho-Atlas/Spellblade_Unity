using UnityEngine;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// Runtime-built HUD: a 4-slot ability bar (one slot per discipline, active
    /// one highlighted gold, cooldown drain overlay + countdown) and a mana bar.
    /// Everything is constructed in code — no prefabs, no canvas setup.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        private SpellCaster _caster;
        private ManaPool _mana;
        private Font _font;

        private class Slot
        {
            public Image highlight;   // gold ring shown when active
            public Image inner;       // discipline color
            public Image cooldownFill; // dark overlay that drains as cooldown recovers
            public Text cooldownText;
        }

        private readonly System.Collections.Generic.List<Slot> _slots = new();
        private Image _manaFill;
        private Text _manaText;
        private Health _playerHealth; // [PHASE2-04]
        private Image _healthFill;    // [PHASE2-04]

        private static Sprite _whiteSprite;
        private static Sprite WhiteSprite
        {
            get
            {
                if (_whiteSprite == null)
                {
                    var tex = Texture2D.whiteTexture;
                    _whiteSprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height),
                                                 new Vector2(0.5f, 0.5f));
                }
                return _whiteSprite;
            }
        }

        /// <summary>Bootstrap entry point — builds the whole HUD hierarchy.
        /// playerHealth is optional so pre-Phase-2 callers keep working. [PHASE2-04]</summary>
        public static HudController Build(SpellCaster caster, ManaPool mana, Health playerHealth = null)
        {
            var go = new GameObject("Spellblade HUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));

            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var hud = go.AddComponent<HudController>();
            hud._caster = caster;
            hud._mana = mana;
            hud._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud._playerHealth = playerHealth; // [PHASE2-04]
            hud.BuildAbilityBar();
            hud.BuildManaBar();
            if (playerHealth != null) hud.BuildHealthBar(); // [PHASE2-04]
            return hud;
        }

        // -- Construction ------------------------------------------------------

        private void BuildAbilityBar()
        {
            var bar = MakeRect("Ability Bar", transform,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(420, 100), anchoredPos: new Vector2(0, 52));

            var disciplines = _caster.Disciplines;
            const float slotSize = 84f, spacing = 100f;
            float startX = -spacing * (disciplines.Count - 1) / 2f;

            for (int i = 0; i < disciplines.Count; i++)
            {
                var d = disciplines[i];
                var slot = new Slot();
                var pos = new Vector2(startX + i * spacing, 0f);

                // Gold highlight ring (slightly larger, behind everything).
                slot.highlight = MakeImage("Highlight", bar, pos, new Vector2(slotSize + 10, slotSize + 10),
                                           new Color(0.91f, 0.77f, 0.35f));

                // Dark slot frame.
                MakeImage("Frame", bar, pos, new Vector2(slotSize, slotSize),
                          new Color(0.06f, 0.06f, 0.10f, 0.95f));

                // Discipline color panel.
                slot.inner = MakeImage("Color", bar, pos, new Vector2(slotSize - 10, slotSize - 10),
                                       new Color(d.themeColor.r, d.themeColor.g, d.themeColor.b, 0.85f));

                // Key label (Q/W/E/R) top-left.
                MakeText(d.keyLabel, bar, pos + new Vector2(-slotSize / 2f + 13, slotSize / 2f - 13),
                         new Vector2(30, 24), 20, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

                // Spell name under the slot.
                MakeText(d.Primary != null ? d.Primary.displayName : "—", bar,
                         pos + new Vector2(0, -slotSize / 2f - 14), new Vector2(110, 20),
                         12, FontStyle.Normal, new Color(0.8f, 0.8f, 0.9f), TextAnchor.MiddleCenter);

                // Cooldown overlay: filled image that drains top-down as the spell recovers.
                slot.cooldownFill = MakeImage("Cooldown", bar, pos, new Vector2(slotSize - 10, slotSize - 10),
                                              new Color(0f, 0f, 0f, 0.78f));
                slot.cooldownFill.sprite = WhiteSprite;
                slot.cooldownFill.type = Image.Type.Filled;
                slot.cooldownFill.fillMethod = Image.FillMethod.Vertical;
                slot.cooldownFill.fillOrigin = (int)Image.OriginVertical.Top;

                // Countdown number.
                slot.cooldownText = MakeText("", bar, pos, new Vector2(60, 30),
                                             24, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

                _slots.Add(slot);
            }
        }

        private void BuildManaBar()
        {
            var root = MakeRect("Mana Bar", transform,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(420, 16), anchoredPos: new Vector2(0, 28));

            MakeImage("BG", root, Vector2.zero, new Vector2(420, 16), new Color(0.04f, 0.04f, 0.08f, 0.95f));

            _manaFill = MakeImage("Fill", root, Vector2.zero, new Vector2(414, 10),
                                  new Color(0.25f, 0.45f, 0.95f));
            _manaFill.sprite = WhiteSprite;
            _manaFill.type = Image.Type.Filled;
            _manaFill.fillMethod = Image.FillMethod.Horizontal;

            _manaText = MakeText("", root, new Vector2(0, 0), new Vector2(200, 16),
                                 12, FontStyle.Bold, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);
        }

        // [PHASE2-04] Slim HP bar paired with the mana bar (just beneath it — the
        // slot above is occupied by the ability bar's spell-name labels).
        private void BuildHealthBar()
        {
            var root = MakeRect("Health Bar", transform,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(420, 10), anchoredPos: new Vector2(0, 14));

            MakeImage("BG", root, Vector2.zero, new Vector2(420, 10), new Color(0.04f, 0.04f, 0.08f, 0.95f));

            _healthFill = MakeImage("Fill", root, Vector2.zero, new Vector2(414, 6),
                                    new Color(0.78f, 0.16f, 0.18f)); // arterial crimson
            _healthFill.sprite = WhiteSprite;
            _healthFill.type = Image.Type.Filled;
            _healthFill.fillMethod = Image.FillMethod.Horizontal;
        }

        // -- Live updates --------------------------------------------------------

        private void Update()
        {
            var disciplines = _caster.Disciplines;
            for (int i = 0; i < _slots.Count && i < disciplines.Count; i++)
            {
                var slot = _slots[i];
                var spell = disciplines[i].Primary;

                slot.highlight.enabled = i == _caster.ActiveIndex;

                float remaining = _caster.CooldownRemaining(spell);
                float total = spell != null ? Mathf.Max(spell.cooldown, 0.01f) : 1f;
                slot.cooldownFill.fillAmount = Mathf.Clamp01(remaining / total);
                slot.cooldownText.text = remaining > 0.05f ? remaining.ToString("0.0") : "";
            }

            if (_mana != null)
            {
                _manaFill.fillAmount = _mana.Current / _mana.Max;
                _manaText.text = $"{Mathf.FloorToInt(_mana.Current)} / {Mathf.FloorToInt(_mana.Max)}";
            }

            if (_playerHealth != null && _healthFill != null) // [PHASE2-04]
                _healthFill.fillAmount = _playerHealth.Current / Mathf.Max(1f, _playerHealth.maxHealth);
        }

        // -- Tiny UI factory helpers ----------------------------------------------

        private RectTransform MakeRect(string name, Transform parent, Vector2 anchorMin,
            Vector2 anchorMax, Vector2 pivot, Vector2 size, Vector2 anchoredPos)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = go.GetComponent<RectTransform>();
            rect.SetParent(parent, false);
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.sizeDelta = size;
            rect.anchoredPosition = anchoredPos;
            return rect;
        }

        private Image MakeImage(string name, Transform parent, Vector2 pos, Vector2 size, Color color)
        {
            var rect = MakeRect(name, parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(0.5f, 0.5f), size, pos);
            var img = rect.gameObject.AddComponent<Image>();
            img.color = color;
            return img;
        }

        private Text MakeText(string content, Transform parent, Vector2 pos, Vector2 size,
            int fontSize, FontStyle style, Color color, TextAnchor anchor)
        {
            var rect = MakeRect("Text", parent, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                                new Vector2(0.5f, 0.5f), size, pos);
            var text = rect.gameObject.AddComponent<Text>();
            text.text = content;
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = anchor;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
