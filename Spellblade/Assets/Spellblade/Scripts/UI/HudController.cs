using UnityEngine;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// Runtime-built HUD (Plan 02 layout): a slim discipline indicator (4 dots
    /// in element colors, active one lit), mana bar, HP bar, and the radial
    /// SpellWheelUI bottom-right. Cooldown display lives on the wheel now —
    /// the old Q/W/E/R ability bar is gone with the keys themselves.
    /// Everything is constructed in code — no prefabs, no canvas setup.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        private SpellCaster _caster;
        private ManaPool _mana;
        private Font _font;

        private Image _manaFill;
        private Text _manaText;
        private Health _playerHealth; // [PHASE2-04]
        private Image _healthFill;    // [PHASE2-04]

        private readonly System.Collections.Generic.List<Image> _disciplineDots = new(); // [PHASE2-02]

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
            hud.BuildDisciplineDots();        // [PHASE2-02] replaces the Q/W/E/R bar
            hud.BuildManaBar();
            if (playerHealth != null) hud.BuildHealthBar(); // [PHASE2-04]
            SpellWheelUI.Build(go.transform, caster);       // [PHASE2-02]
            return hud;
        }

        // -- Construction ------------------------------------------------------

        /// <summary>Four small element-colored dots above the mana bar; the active
        /// discipline's dot glows full-color and slightly larger. [PHASE2-02]</summary>
        private void BuildDisciplineDots()
        {
            var bar = MakeRect("Discipline Dots", transform,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(140, 24), anchoredPos: new Vector2(0, 50));

            var disciplines = _caster.Disciplines;
            const float spacing = 30f;
            float startX = -spacing * (disciplines.Count - 1) / 2f;

            for (int i = 0; i < disciplines.Count; i++)
            {
                var dot = MakeImage("Dot", bar, new Vector2(startX + i * spacing, 0f),
                                    new Vector2(14f, 14f), disciplines[i].themeColor);
                dot.sprite = WorldMapBootstrap.SoftCircle;
                _disciplineDots.Add(dot);
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

        // [PHASE2-04] Slim HP bar paired with the mana bar (just beneath it).
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
            for (int i = 0; i < _disciplineDots.Count && i < disciplines.Count; i++) // [PHASE2-02]
            {
                bool active = i == _caster.ActiveIndex;
                var theme = disciplines[i].themeColor;
                _disciplineDots[i].color = active
                    ? theme
                    : new Color(theme.r * 0.35f, theme.g * 0.35f, theme.b * 0.35f, 0.7f);
                _disciplineDots[i].rectTransform.sizeDelta = active
                    ? new Vector2(19f, 19f) : new Vector2(13f, 13f);
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
