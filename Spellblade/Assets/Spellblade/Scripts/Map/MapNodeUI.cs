using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// One clickable arena beacon on the world map. Three states:
    ///   Open        — pulsing element-tinted glow; click → load the arena
    ///   Cleared     — steady dim glow + check mark; still clickable (free replay)
    ///   BossLocked  — dark + lock glyph; tooltip explains "clear 3 arenas"
    /// Region-locked nodes are never spawned at all.
    /// </summary>
    public class MapNodeUI : MonoBehaviour,
        IPointerEnterHandler, IPointerExitHandler, IPointerClickHandler
    {
        private enum NodeState { Open, Cleared, BossLocked }

        private WorldMapBootstrap _owner;
        private ArenaNodeDef _node;
        private RegionDef _region;
        private NodeState _state;

        private Image _glow;
        private Image _core;
        private float _pulsePhase;
        private float _hoverScale = 1f;
        private bool _loading;

        public static MapNodeUI Spawn(WorldMapBootstrap owner, Transform canvas,
                                      NodePlacement placement, RegionDef region)
        {
            var node = placement.node;
            var go = new GameObject($"Node {node.id}", typeof(RectTransform));
            go.transform.SetParent(canvas, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = placement.mapPosition; // normalized map coords
            rect.anchoredPosition = Vector2.zero;
            rect.sizeDelta = new Vector2(52f, 52f);

            var ui = go.AddComponent<MapNodeUI>();
            ui._owner = owner;
            ui._node = node;
            ui._region = region;
            ui._pulsePhase = Random.Range(0f, Mathf.PI * 2f); // beacons don't pulse in lockstep
            ui._state = GameSession.IsNodeCleared(node.id) ? NodeState.Cleared
                      : node.isBossNode && !GameSession.IsBossUnlocked(node.regionId) ? NodeState.BossLocked
                      : NodeState.Open;
            ui.BuildVisual();
            return ui;
        }

        private void BuildVisual()
        {
            bool boss = _node.isBossNode;
            var tint = _region.elementTint;

            // Outer glow — the part bloom catches.
            _glow = MakeImage("Glow", new Vector2(boss ? 64f : 46f, boss ? 64f : 46f));
            _glow.sprite = WorldMapBootstrap.SoftCircle;
            _glow.raycastTarget = false;

            // Core dot.
            _core = MakeImage("Core", new Vector2(boss ? 26f : 18f, boss ? 26f : 18f));
            _core.sprite = WorldMapBootstrap.SoftCircle;
            _core.raycastTarget = false;

            // Invisible hit target covering the whole node (single raycast surface).
            var hit = MakeImage("Hit", ((RectTransform)transform).sizeDelta);
            hit.color = Color.clear;
            hit.raycastTarget = true;

            switch (_state)
            {
                case NodeState.Open:
                    _glow.color = new Color(tint.r, tint.g, tint.b, 0.55f);
                    _core.color = Color.Lerp(tint, Color.white, 0.65f);
                    break;

                case NodeState.Cleared:
                    _glow.color = new Color(tint.r, tint.g, tint.b, 0.22f);
                    _core.color = Color.Lerp(tint, Color.white, 0.3f) * new Color(1f, 1f, 1f, 0.8f);
                    AddGlyph("✓ Check", "✓", new Color(0.75f, 0.95f, 0.75f, 0.9f), 20);
                    break;

                case NodeState.BossLocked:
                    _glow.color = new Color(0.08f, 0.08f, 0.10f, 0.6f);
                    _core.color = new Color(0.22f, 0.20f, 0.28f, 0.9f);
                    BuildLockGlyph();
                    break;
            }
        }

        private Image MakeImage(string name, Vector2 size)
        {
            var go = new GameObject(name, typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = size;
            rect.anchoredPosition = Vector2.zero;
            return go.GetComponent<Image>();
        }

        private void AddGlyph(string name, string glyph, Color color, int size)
        {
            var go = new GameObject(name, typeof(Text));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.sizeDelta = new Vector2(40f, 40f);
            rect.anchoredPosition = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = size;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = glyph;
            text.raycastTarget = false;
        }

        /// <summary>Minimalist chain-lock silhouette: shackle ring over a dark body.</summary>
        private void BuildLockGlyph()
        {
            var body = MakeImage("Lock Body", new Vector2(12f, 9f));
            body.color = new Color(0.55f, 0.50f, 0.65f, 0.85f);
            ((RectTransform)body.transform).anchoredPosition = new Vector2(0f, -3f);
            body.raycastTarget = false;

            var shackle = MakeImage("Lock Shackle", new Vector2(9f, 9f));
            shackle.sprite = WorldMapBootstrap.SoftCircle;
            shackle.color = new Color(0.55f, 0.50f, 0.65f, 0.85f);
            ((RectTransform)shackle.transform).anchoredPosition = new Vector2(0f, 4f);
            shackle.raycastTarget = false;
        }

        private void Update()
        {
            // Open beacons breathe; everything eases its hover scale.
            if (_state == NodeState.Open && _glow != null)
            {
                float pulse = 0.55f + 0.25f * Mathf.Sin(Time.time * 2.6f + _pulsePhase);
                var c = _glow.color;
                _glow.color = new Color(c.r, c.g, c.b, pulse);
                _glow.rectTransform.localScale = Vector3.one * (1f + 0.06f * Mathf.Sin(Time.time * 2.6f + _pulsePhase));
            }

            transform.localScale = Vector3.Lerp(transform.localScale, Vector3.one * _hoverScale, 10f * Time.deltaTime);
        }

        // -- Pointer events -----------------------------------------------------

        public void OnPointerEnter(PointerEventData eventData)
        {
            _hoverScale = 1.15f;
            _owner.ShowTooltip(this, TooltipBody());
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _hoverScale = 1f;
            _owner.HideTooltip();
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_loading || _state == NodeState.BossLocked) return;
            StartCoroutine(FlashAndLoad());
        }

        private IEnumerator FlashAndLoad()
        {
            _loading = true;

            // Brief white flash on the beacon, then commit.
            if (_core != null) _core.color = Color.white;
            if (_glow != null) _glow.color = new Color(1f, 1f, 1f, 0.9f);
            yield return new WaitForSeconds(0.14f);

            GameSession.CurrentNode = _node;
            SceneManager.LoadScene("Arena");
        }

        private string TooltipBody()
        {
            string objective = _node.objective switch
            {
                ObjectiveType.WaveSurvival => "Wave Survival",
                ObjectiveType.WavesThenBoss => "Waves + Boss",
                _ => "Traversal",
            };

            string status = _state switch
            {
                NodeState.Cleared => "Cleared ✓",
                NodeState.BossLocked => $"Clear {GameSession.BossUnlockClears} arenas in this region to unlock",
                _ => "Open",
            };

            return $"{_node.displayName.ToUpperInvariant()}\n{objective}  ·  Tier {_node.difficultyTier}\n{status}";
        }
    }
}
