using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// Arena-side game flow (Plan 03). SpellbladeBootstrap calls Begin() at the
    /// end of Start(): in playground mode (GameSession.CurrentNode == null) this
    /// is a no-op and the scene behaves exactly like Phase 1. In arena mode it
    /// hands the node to the objective system when one exists (Plan 04), or runs
    /// a debug fallback (V = victory) so map flow is testable without Plan 04.
    /// Esc = abandon → ReportArenaResult(false) is PERMANENT either way.
    /// </summary>
    public static class ArenaFlow
    {
        public static void Begin(SpellbladeBootstrap host)
        {
            if (GameSession.CurrentNode == null) return; // playground mode — Phase 1 behavior
            host.gameObject.AddComponent<ArenaFlowRunner>();
        }

        /// <summary>Plan 04 fills this in with a direct call:
        ///   var director = Object.FindAnyObjectByType&lt;ObjectiveDirector&gt;();
        ///   if (director == null) return false;
        ///   director.Configure(node); return true;
        /// Until then the debug fallback below keeps the map loop playable.</summary>
        public static bool TryFindObjectiveDirector(ArenaNodeDef node)
        {
            return false; // [PHASE2-03] stub — Plan 04 owns the body
        }
    }

    /// <summary>Runs one arena visit: banner, Esc-abandon, debug victory when no
    /// objective system is present. Reports exactly once.</summary>
    public class ArenaFlowRunner : MonoBehaviour
    {
        private bool _debugFallback;
        private bool _resolved;

        private void Start()
        {
            var node = GameSession.CurrentNode;
            if (node == null) { Destroy(this); return; }

            _debugFallback = !ArenaFlow.TryFindObjectiveDirector(node);
            BuildBanner(node);
        }

        private void Update()
        {
            if (_resolved) return;
            var kb = Keyboard.current;
            if (kb == null) return;

            if (kb.escapeKey.wasPressedThisFrame) Resolve(false);          // permanent: abandon
            else if (_debugFallback && kb.vKey.wasPressedThisFrame) Resolve(true); // debug victory
        }

        private void Resolve(bool victory)
        {
            _resolved = true; // scene unloads next; guard against double-report this frame
            GameSession.ReportArenaResult(victory);
        }

        /// <summary>Small top-center banner: where you are + how to leave.</summary>
        private void BuildBanner(ArenaNodeDef node)
        {
            var go = new GameObject("Arena Flow Banner",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 40;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var textGo = new GameObject("Banner Text", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rect = textGo.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -14f);
            rect.sizeDelta = new Vector2(1100f, 60f);

            var text = textGo.GetComponent<Text>();
            text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            text.fontSize = 22;
            text.alignment = TextAnchor.UpperCenter;
            text.color = new Color(0.85f, 0.85f, 0.95f, 0.9f);
            text.text = _debugFallback
                ? $"{node.displayName.ToUpperInvariant()}  ·  V = Victory (debug)  ·  Esc = Return"
                : $"{node.displayName.ToUpperInvariant()}  ·  Esc = Abandon";
        }
    }
}
