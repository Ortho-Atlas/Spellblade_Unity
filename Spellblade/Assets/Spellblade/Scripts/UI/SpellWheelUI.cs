using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// The radial spell wheel (Plan 02) — the signature input scheme.
    ///   Idle: a small "compass" bottom-right showing the active discipline's
    ///         4 slots with the selected one highlighted.
    ///   Hold RMB: scales up (~80ms ease); dragging past an 18px dead zone
    ///         highlights the sector under the drag vector; release selects it
    ///         (release inside the dead zone keeps the previous selection).
    ///   Scroll: SpellCaster cycles disciplines; the wheel spin-ticks 90°,
    ///         re-tints, and flashes the discipline name.
    /// Slot order: 0 top, then clockwise — 1 right, 2 bottom, 3 left.
    /// Locked slots (ProgressionGate) render dimmed with a lock glyph and
    /// cannot be selected. Cooldown sweeps live on the wheel, not the HUD.
    /// </summary>
    public class SpellWheelUI : MonoBehaviour
    {
        private const float Diameter = 260f;
        private const float IdleScale = 90f / Diameter;
        private const float EaseSeconds = 0.08f;
        private const float DeadZonePx = 18f;
        private const float SectorFill = 0.22f; // of 360° — leaves gaps between sectors

        private SpellCaster _caster;
        private Font _font;
        private RectTransform _root;

        private class SlotUI
        {
            public Image sector;        // tinted body
            public Image cooldownSweep; // radial drain overlay
            public Text letter;
            public Text cost;
            public Text rankPips;       // [PHASE2-05] I / II / III
            public GameObject lockGlyph;
        }

        private readonly SlotUI[] _slots = new SlotUI[4];
        private Text _disciplineLabel;
        private float _labelFadeAt;

        private bool _holding;
        private Vector2 _pressPosition;
        private int _highlighted = -1;
        private float _scaleTarget = IdleScale;

        private static readonly Vector2[] SlotDirections =
        {
            new(0f, 1f), new(1f, 0f), new(0f, -1f), new(-1f, 0f) // top, right, bottom, left
        };

        public static SpellWheelUI Build(Transform hudRoot, SpellCaster caster)
        {
            var go = new GameObject("Spell Wheel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(hudRoot, false);
            rect.anchorMin = rect.anchorMax = new Vector2(1f, 0f); // bottom-right
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(-(40f + Diameter / 2f), 40f + Diameter / 2f);
            rect.sizeDelta = new Vector2(Diameter, Diameter);
            rect.localScale = Vector3.one * IdleScale;

            var wheel = go.AddComponent<SpellWheelUI>();
            wheel._caster = caster;
            wheel._root = rect;
            wheel._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            wheel.BuildVisual();
            wheel.Reskin();

            caster.DisciplineChanged += _ => wheel.OnDisciplineChanged();
            return wheel;
        }

        // -- Construction --------------------------------------------------------

        private void BuildVisual()
        {
            // Dark base disc behind the sectors.
            var baseDisc = MakeImage("Base", _root, Vector2.zero, new Vector2(Diameter, Diameter));
            baseDisc.sprite = WorldMapBootstrap.SoftCircle;
            baseDisc.color = new Color(0.03f, 0.03f, 0.06f, 0.72f);

            for (int i = 0; i < 4; i++)
            {
                var slot = new SlotUI();

                // Sector body: radial-filled soft disc → a feathered pie wedge.
                // Fill starts at 12 o'clock sweeping clockwise; rotating the image
                // +39.6° centers the 0.22 arc on top, then -90° per slot.
                float zRotation = SectorFill * 180f - 90f * i;

                slot.sector = MakeImage($"Sector {i}", _root, Vector2.zero, new Vector2(Diameter, Diameter));
                slot.sector.sprite = WorldMapBootstrap.SoftCircle;
                slot.sector.type = Image.Type.Filled;
                slot.sector.fillMethod = Image.FillMethod.Radial360;
                slot.sector.fillOrigin = (int)Image.Origin360.Top;
                slot.sector.fillClockwise = true;
                slot.sector.fillAmount = SectorFill;
                slot.sector.rectTransform.localEulerAngles = new Vector3(0f, 0f, zRotation);

                // Cooldown sweep: same wedge shape, dark, drains as the spell recovers.
                slot.cooldownSweep = MakeImage($"Cooldown {i}", _root, Vector2.zero, new Vector2(Diameter, Diameter));
                slot.cooldownSweep.sprite = WorldMapBootstrap.SoftCircle;
                slot.cooldownSweep.type = Image.Type.Filled;
                slot.cooldownSweep.fillMethod = Image.FillMethod.Radial360;
                slot.cooldownSweep.fillOrigin = (int)Image.Origin360.Top;
                slot.cooldownSweep.fillClockwise = true;
                slot.cooldownSweep.fillAmount = 0f;
                slot.cooldownSweep.rectTransform.localEulerAngles = new Vector3(0f, 0f, zRotation);
                slot.cooldownSweep.color = new Color(0f, 0f, 0f, 0.66f);

                // Spell initial + mana cost, out along the slot direction.
                var texPos = SlotDirections[i] * 82f;
                slot.letter = MakeText($"Letter {i}", texPos + new Vector2(0f, 7f), 34, FontStyle.Bold);
                slot.cost = MakeText($"Cost {i}", texPos + new Vector2(0f, -18f), 15, FontStyle.Normal);
                slot.rankPips = MakeText($"Rank {i}", texPos + new Vector2(0f, 27f), 13, FontStyle.Bold); // [PHASE2-05]
                slot.rankPips.color = new Color(0.95f, 0.85f, 0.5f, 0.95f);

                // Minimalist padlock (same silhouette as the map's boss lock).
                slot.lockGlyph = new GameObject($"Lock {i}", typeof(RectTransform));
                var lockRect = (RectTransform)slot.lockGlyph.transform;
                lockRect.SetParent(_root, false);
                lockRect.anchoredPosition = texPos + new Vector2(24f, 8f);
                lockRect.sizeDelta = new Vector2(16f, 16f);
                var body = MakeImage("Body", lockRect, new Vector2(0f, -3f), new Vector2(10f, 8f));
                body.color = new Color(0.6f, 0.55f, 0.7f, 0.9f);
                var shackle = MakeImage("Shackle", lockRect, new Vector2(0f, 3f), new Vector2(8f, 8f));
                shackle.sprite = WorldMapBootstrap.SoftCircle;
                shackle.color = new Color(0.6f, 0.55f, 0.7f, 0.9f);

                _slots[i] = slot;
            }

            // Discipline name flash above the wheel.
            _disciplineLabel = MakeText("Discipline Label", new Vector2(0f, Diameter / 2f + 28f), 30, FontStyle.Bold);
            _disciplineLabel.color = Color.clear;
        }

        private Image MakeImage(string name, Transform parent, Vector2 pos, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = size;
            var image = go.GetComponent<Image>();
            image.raycastTarget = false;
            return image;
        }

        private Text MakeText(string name, Vector2 pos, int size, FontStyle style)
        {
            var go = new GameObject(name, typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(_root, false);
            rect.anchoredPosition = pos;
            rect.sizeDelta = new Vector2(120f, 44f);
            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = size;
            text.fontStyle = style;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = Color.white;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Overflow;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        // -- Live behavior ---------------------------------------------------------

        private void Update()
        {
            HandleHold();
            UpdateVisualState();

            // Ease the open/close scale (~80ms).
            float k = Time.deltaTime / EaseSeconds;
            _root.localScale = Vector3.one * Mathf.Lerp(_root.localScale.x, _scaleTarget, Mathf.Clamp01(k));

            // Discipline label fade.
            if (_disciplineLabel.color.a > 0f && Time.time > _labelFadeAt)
            {
                var c = _disciplineLabel.color;
                _disciplineLabel.color = new Color(c.r, c.g, c.b, Mathf.Max(0f, c.a - Time.deltaTime * 2.2f));
            }
        }

        private void HandleHold()
        {
            var mouse = Mouse.current;
            if (mouse == null) return;

            if (mouse.rightButton.wasPressedThisFrame)
            {
                _holding = true;
                _pressPosition = mouse.position.ReadValue();
                _highlighted = -1;
                _scaleTarget = 1f;
            }

            if (_holding && mouse.rightButton.isPressed)
            {
                var drag = mouse.position.ReadValue() - _pressPosition;
                _highlighted = drag.magnitude >= DeadZonePx ? SectorFromDirection(drag) : -1;
            }

            if (_holding && mouse.rightButton.wasReleasedThisFrame)
            {
                if (_highlighted >= 0)
                    _caster.SelectSpell(_caster.ActiveIndex, _highlighted); // refuses locked slots
                _holding = false;
                _highlighted = -1;
                _scaleTarget = IdleScale;
            }
        }

        private static int SectorFromDirection(Vector2 direction)
        {
            int best = 0;
            float bestDot = float.MinValue;
            for (int i = 0; i < SlotDirections.Length; i++)
            {
                float dot = Vector2.Dot(direction.normalized, SlotDirections[i]);
                if (dot > bestDot) { bestDot = dot; best = i; }
            }
            return best;
        }

        private void UpdateVisualState()
        {
            var discipline = _caster.Active;
            if (discipline == null) return;

            for (int i = 0; i < 4; i++)
            {
                var slot = _slots[i];
                var spell = i < discipline.spells.Count ? discipline.spells[i] : null;
                bool unlocked = spell != null && ProgressionGate.IsUnlocked(spell.spellId);
                bool isActive = i == _caster.ActiveSpellIndex;
                bool isHighlighted = i == _highlighted;

                var theme = discipline.themeColor;
                Color color;
                if (spell == null) color = new Color(0.08f, 0.08f, 0.1f, 0.5f);
                else if (!unlocked) color = new Color(theme.r * 0.25f, theme.g * 0.25f, theme.b * 0.25f, 0.55f);
                else if (isHighlighted) color = Color.Lerp(theme, Color.white, 0.55f); // bloom-friendly hot
                else if (isActive) color = new Color(theme.r, theme.g, theme.b, 0.95f);
                else color = new Color(theme.r * 0.45f, theme.g * 0.45f, theme.b * 0.45f, 0.8f);
                slot.sector.color = color;

                // Cooldown drains within the wedge.
                if (spell != null && spell.cooldown > 0.01f)
                    slot.cooldownSweep.fillAmount =
                        SectorFill * Mathf.Clamp01(_caster.CooldownRemaining(spell) / spell.cooldown);
                else
                    slot.cooldownSweep.fillAmount = 0f;

                float textAlpha = unlocked ? 1f : 0.4f;
                slot.letter.color = new Color(1f, 1f, 1f, textAlpha);
                slot.cost.color = new Color(0.75f, 0.82f, 1f, textAlpha * 0.9f);
                slot.lockGlyph.SetActive(spell != null && !unlocked);
            }
        }

        /// <summary>Repaint letters/costs/locks/rank pips for the current discipline.</summary>
        private void Reskin()
        {
            var discipline = _caster.Active;
            if (discipline == null) return;

            for (int i = 0; i < 4; i++)
            {
                var spell = i < discipline.spells.Count ? discipline.spells[i] : null;
                _slots[i].letter.text = spell != null ? spell.displayName.Substring(0, 1) : "";

                bool unlocked = spell != null && ProgressionGate.IsUnlocked(spell.spellId);
                // [PHASE2-05] Locked slots advertise their shard price instead of mana.
                _slots[i].cost.text = spell == null ? ""
                    : !unlocked ? $"{ProgressionMath.SlotUnlockCost(i)} shards"
                    : spell.manaCost > 0f ? Mathf.RoundToInt(spell.manaCost).ToString() : "";

                // [PHASE2-05] Rank pips — arena only (playground runs base ranks).
                int rank = spell != null && GameSession.CurrentNode != null
                    ? ProgressionMath.GetRank(spell.spellId) : 1;
                _slots[i].rankPips.text = rank switch { 3 => "III", 2 => "II", _ => "" };
            }
        }

        private void OnDisciplineChanged()
        {
            Reskin();

            // Spin-tick + name flash.
            StopAllCoroutines();
            StartCoroutine(SpinTick());

            var discipline = _caster.Active;
            if (discipline != null)
            {
                _disciplineLabel.text = discipline.displayName.ToUpperInvariant();
                var theme = discipline.themeColor;
                _disciplineLabel.color = new Color(theme.r, theme.g, theme.b, 1f);
                _labelFadeAt = Time.time + 0.5f;
            }
        }

        private IEnumerator SpinTick()
        {
            const float duration = 0.15f;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float angle = Mathf.Lerp(90f, 0f, Mathf.SmoothStep(0f, 1f, t / duration));
                _root.localEulerAngles = new Vector3(0f, 0f, angle);
                yield return null;
            }
            _root.localEulerAngles = Vector3.zero;
        }
    }
}
