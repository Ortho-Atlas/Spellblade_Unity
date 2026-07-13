using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// Runtime-built HUD. One unified bottom bar (Ryan's post-Phase-2 request):
    ///   [ active spell card | melee card | HP bar / mana bar / discipline dots ]
    /// plus the radial SpellWheelUI bottom-right and a clickable MAP button
    /// top-right that returns to the world map from any arena (counts as
    /// abandon — no clear) or from the playground.
    /// Everything is constructed in code — no prefabs, no canvas setup.
    /// </summary>
    public class HudController : MonoBehaviour
    {
        private SpellCaster _caster;
        private ManaPool _mana;
        private Health _playerHealth;
        private MeleeStrike _melee;
        private Font _font;

        // Active spell card
        private Image _spellFrame;
        private Image _spellInner;
        private Text _spellLetter;
        private Text _spellName;
        private Text _spellManaCost;
        private Image _spellCooldownFill;
        private Text _spellCooldownText;

        // Melee card
        private Image _meleeCooldownFill;

        // Bars + dots
        private Image _healthFill;
        private Text _healthText;
        private Image _manaFill;
        private Text _manaText;
        private readonly System.Collections.Generic.List<Image> _disciplineDots = new();

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

        /// <summary>Bootstrap entry point — builds the whole HUD hierarchy.</summary>
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
            hud._playerHealth = playerHealth;
            hud._melee = caster != null ? caster.GetComponent<MeleeStrike>() : null;
            hud._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            hud.BuildBottomBar();
            hud.BuildMapButton();
            SpellWheelUI.Build(go.transform, caster);
            EnsureEventSystem(); // the MAP button needs pointer events
            return hud;
        }

        // -- Construction ------------------------------------------------------

        private void BuildBottomBar()
        {
            var bar = MakeRect("Bottom Bar", transform,
                anchorMin: new Vector2(0.5f, 0f), anchorMax: new Vector2(0.5f, 0f),
                pivot: new Vector2(0.5f, 0f), size: new Vector2(660, 96), anchoredPos: new Vector2(0, 10));
            var barBg = bar.gameObject.AddComponent<Image>();
            barBg.color = new Color(0.025f, 0.025f, 0.06f, 0.82f);
            barBg.raycastTarget = false;

            BuildSpellCard(bar);
            BuildMeleeCard(bar);
            BuildBarsBlock(bar);
        }

        /// <summary>What LMB will do right now: discipline-tinted card, spell initial,
        /// cooldown drain, name + mana cost. Mirrors the wheel selection live.</summary>
        private void BuildSpellCard(Transform bar)
        {
            const float x = -262f;

            _spellFrame = MakeImage("Spell Frame", bar, new Vector2(x, 10f), new Vector2(66f, 66f),
                                    Color.white);
            MakeImage("Spell Back", bar, new Vector2(x, 10f), new Vector2(58f, 58f),
                      new Color(0.05f, 0.05f, 0.09f, 0.98f));
            _spellInner = MakeImage("Spell Color", bar, new Vector2(x, 10f), new Vector2(52f, 52f),
                                    Color.gray);

            _spellLetter = MakeText("?", bar, new Vector2(x, 10f), new Vector2(52f, 52f),
                                    30, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            // Cooldown: dark vertical drain + countdown.
            _spellCooldownFill = MakeImage("Spell Cooldown", bar, new Vector2(x, 10f), new Vector2(52f, 52f),
                                           new Color(0f, 0f, 0f, 0.78f));
            _spellCooldownFill.sprite = WhiteSprite;
            _spellCooldownFill.type = Image.Type.Filled;
            _spellCooldownFill.fillMethod = Image.FillMethod.Vertical;
            _spellCooldownFill.fillOrigin = (int)Image.OriginVertical.Top;
            _spellCooldownText = MakeText("", bar, new Vector2(x, 10f), new Vector2(52f, 30f),
                                          18, FontStyle.Bold, Color.white, TextAnchor.MiddleCenter);

            MakeText("LMB", bar, new Vector2(x - 24f, 36f), new Vector2(34f, 16f),
                     10, FontStyle.Bold, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleCenter);
            _spellManaCost = MakeText("", bar, new Vector2(x + 24f, -14f), new Vector2(40f, 16f),
                                      12, FontStyle.Bold, new Color(0.55f, 0.7f, 1f), TextAnchor.MiddleCenter);
            _spellName = MakeText("", bar, new Vector2(x, -34f), new Vector2(150f, 18f),
                                  13, FontStyle.Normal, new Color(0.85f, 0.83f, 0.9f), TextAnchor.MiddleCenter);
        }

        /// <summary>The blade: Space melee, crossed-blades glyph, cooldown drain.</summary>
        private void BuildMeleeCard(Transform bar)
        {
            const float x = -178f;

            MakeImage("Melee Frame", bar, new Vector2(x, 10f), new Vector2(48f, 48f),
                      new Color(0.35f, 0.33f, 0.4f, 0.95f));
            MakeImage("Melee Back", bar, new Vector2(x, 10f), new Vector2(42f, 42f),
                      new Color(0.05f, 0.05f, 0.09f, 0.98f));

            // Crossed blades: two thin rotated slivers, violet-white like the slash.
            var bladeColor = new Color(0.80f, 0.62f, 1.00f, 0.95f);
            var bladeA = MakeImage("Blade A", bar, new Vector2(x, 10f), new Vector2(5f, 34f), bladeColor);
            bladeA.rectTransform.localEulerAngles = new Vector3(0f, 0f, 40f);
            var bladeB = MakeImage("Blade B", bar, new Vector2(x, 10f), new Vector2(5f, 34f), bladeColor);
            bladeB.rectTransform.localEulerAngles = new Vector3(0f, 0f, -40f);

            _meleeCooldownFill = MakeImage("Melee Cooldown", bar, new Vector2(x, 10f), new Vector2(42f, 42f),
                                           new Color(0f, 0f, 0f, 0.78f));
            _meleeCooldownFill.sprite = WhiteSprite;
            _meleeCooldownFill.type = Image.Type.Filled;
            _meleeCooldownFill.fillMethod = Image.FillMethod.Vertical;
            _meleeCooldownFill.fillOrigin = (int)Image.OriginVertical.Top;

            MakeText("SPACE", bar, new Vector2(x, -24f), new Vector2(60f, 14f),
                     10, FontStyle.Bold, new Color(0.7f, 0.7f, 0.8f), TextAnchor.MiddleCenter);
        }

        /// <summary>HP + mana bars with numbers, discipline dots underneath.</summary>
        private void BuildBarsBlock(Transform bar)
        {
            const float x = 90f;       // block center
            const float width = 400f;

            // HP.
            MakeImage("HP BG", bar, new Vector2(x, 26f), new Vector2(width, 18f),
                      new Color(0.04f, 0.04f, 0.08f, 0.95f));
            _healthFill = MakeImage("HP Fill", bar, new Vector2(x, 26f), new Vector2(width - 6f, 12f),
                                    new Color(0.78f, 0.16f, 0.18f));
            _healthFill.sprite = WhiteSprite;
            _healthFill.type = Image.Type.Filled;
            _healthFill.fillMethod = Image.FillMethod.Horizontal;
            _healthText = MakeText("", bar, new Vector2(x, 26f), new Vector2(200f, 16f),
                                   12, FontStyle.Bold, new Color(1f, 0.9f, 0.9f), TextAnchor.MiddleCenter);

            // Mana.
            MakeImage("Mana BG", bar, new Vector2(x, 4f), new Vector2(width, 18f),
                      new Color(0.04f, 0.04f, 0.08f, 0.95f));
            _manaFill = MakeImage("Mana Fill", bar, new Vector2(x, 4f), new Vector2(width - 6f, 12f),
                                  new Color(0.25f, 0.45f, 0.95f));
            _manaFill.sprite = WhiteSprite;
            _manaFill.type = Image.Type.Filled;
            _manaFill.fillMethod = Image.FillMethod.Horizontal;
            _manaText = MakeText("", bar, new Vector2(x, 4f), new Vector2(200f, 16f),
                                 12, FontStyle.Bold, new Color(0.85f, 0.9f, 1f), TextAnchor.MiddleCenter);

            // Discipline dots (scroll to cycle).
            var disciplines = _caster.Disciplines;
            const float spacing = 28f;
            float startX = x - spacing * (disciplines.Count - 1) / 2f;
            for (int i = 0; i < disciplines.Count; i++)
            {
                var dot = MakeImage("Dot", bar, new Vector2(startX + i * spacing, -24f),
                                    new Vector2(14f, 14f), disciplines[i].themeColor);
                dot.sprite = WorldMapBootstrap.SoftCircle;
                _disciplineDots.Add(dot);
            }
            MakeText("SCROLL", bar, new Vector2(x + 105f, -24f), new Vector2(70f, 14f),
                     10, FontStyle.Normal, new Color(0.5f, 0.5f, 0.6f), TextAnchor.MiddleLeft);
        }

        /// <summary>Top-right MAP button: back to the world map from anywhere.
        /// In an arena this is an abandon (no clear recorded) — same as Esc.</summary>
        private void BuildMapButton()
        {
            var go = new GameObject("Map Button", typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(transform, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-24f, -24f);
            rect.sizeDelta = new Vector2(110f, 42f);
            go.GetComponent<Image>().color = new Color(0.25f, 0.18f, 0.40f, 0.9f);

            var label = MakeText("MAP", rect, Vector2.zero, new Vector2(110f, 42f),
                                 18, FontStyle.Bold, new Color(0.9f, 0.85f, 1f), TextAnchor.MiddleCenter);
            label.raycastTarget = false;

            MakeText(GameSession.CurrentNode != null ? "abandon arena" : "",
                     rect, new Vector2(0f, -30f), new Vector2(140f, 16f),
                     10, FontStyle.Italic, new Color(0.6f, 0.55f, 0.65f), TextAnchor.MiddleCenter)
                .raycastTarget = false;

            go.GetComponent<Button>().onClick.AddListener(() =>
            {
                if (GameSession.CurrentNode != null) GameSession.ReportArenaResult(false);
                else SceneManager.LoadScene("WorldMap");
            });
        }

        private static void EnsureEventSystem()
        {
            if (Object.FindAnyObjectByType<EventSystem>() != null) return;
            // InputSystemUIInputModule — this project is new-Input-System-only.
            new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
        }

        // -- Live updates --------------------------------------------------------

        private void Update()
        {
            var disciplines = _caster.Disciplines;
            var active = _caster.Active;
            var spell = _caster.ActiveSpell;

            // Active spell card mirrors the wheel selection.
            if (spell != null && active != null)
            {
                var theme = active.themeColor;
                _spellFrame.color = theme;
                _spellInner.color = new Color(theme.r * 0.55f, theme.g * 0.55f, theme.b * 0.55f, 0.95f);
                _spellLetter.text = spell.displayName.Substring(0, 1);
                _spellName.text = spell.displayName;
                _spellManaCost.text = spell.manaCost > 0f ? Mathf.RoundToInt(spell.manaCost).ToString() : "";

                float remaining = _caster.CooldownRemaining(spell);
                _spellCooldownFill.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(spell.cooldown, 0.01f));
                _spellCooldownText.text = remaining > 0.05f ? remaining.ToString("0.0") : "";
            }

            if (_melee != null)
                _meleeCooldownFill.fillAmount =
                    Mathf.Clamp01(_melee.CooldownRemaining / Mathf.Max(_melee.cooldown, 0.01f));

            if (_playerHealth != null)
            {
                _healthFill.fillAmount = _playerHealth.Current / Mathf.Max(1f, _playerHealth.maxHealth);
                string shield = _playerHealth.IsShielded ? $"  (+{Mathf.CeilToInt(_playerHealth.Shield)})" : "";
                _healthText.text = $"{Mathf.CeilToInt(_playerHealth.Current)} / {Mathf.CeilToInt(_playerHealth.maxHealth)}{shield}";
            }

            if (_mana != null)
            {
                _manaFill.fillAmount = _mana.Current / _mana.Max;
                _manaText.text = $"{Mathf.FloorToInt(_mana.Current)} / {Mathf.FloorToInt(_mana.Max)}";
            }

            for (int i = 0; i < _disciplineDots.Count && i < disciplines.Count; i++)
            {
                bool isActive = i == _caster.ActiveIndex;
                var theme = disciplines[i].themeColor;
                _disciplineDots[i].color = isActive
                    ? theme
                    : new Color(theme.r * 0.35f, theme.g * 0.35f, theme.b * 0.35f, 0.7f);
                _disciplineDots[i].rectTransform.sizeDelta = isActive
                    ? new Vector2(19f, 19f) : new Vector2(13f, 13f);
            }
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
            img.raycastTarget = false;
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
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }
    }
}
