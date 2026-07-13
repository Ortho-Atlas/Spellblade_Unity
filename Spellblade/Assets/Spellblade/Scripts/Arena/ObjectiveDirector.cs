using System.Collections;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.UI;

namespace Spellblade
{
    public enum EnemyKind { Husk, Cultist, Warden }

    /// <summary>
    /// Runs one arena visit (Plan 04). SpellbladeBootstrap adds this component
    /// when GameSession.CurrentNode != null; ArenaFlow.TryFindObjectiveDirector
    /// finds it and calls Configure(node), which replaces Plan 03's debug-victory
    /// fallback. Owns: objective selection, enemy spawning (with telegraph),
    /// the objective HUD line, splashes, and the win side of the flow
    /// (victory banner → ReportArenaResult(true)). Player death is PlayerLife's
    /// job — it calls Defeat() here.
    /// </summary>
    public class ObjectiveDirector : MonoBehaviour
    {
        public Transform Player { get; private set; }
        public ElementType RegionElement { get; private set; }

        private Canvas _canvas;
        private Text _objectiveText;
        private Font _font;
        private bool _ended;

        public void Configure(ArenaNodeDef node)
        {
            Player = GameObject.Find("Player")?.transform;
            RegionElement = ElementFor(node.regionId);
            _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            BuildHud();

            ObjectiveBase objective = node.objective switch
            {
                ObjectiveType.WaveSurvival => gameObject.AddComponent<WaveSurvivalObjective>(),
                ObjectiveType.WavesThenBoss => gameObject.AddComponent<WavesThenBossObjective>(),
                _ => gameObject.AddComponent<TraversalObjective>(),
            };
            objective.Begin(this, node);
        }

        /// <summary>Regions map to the counter-wheel; unknown/TBD regions fall back to Umbra.</summary>
        public static ElementType ElementFor(string regionId) => regionId switch
        {
            "frost" => ElementType.Frost,
            "storm" => ElementType.Storm,
            "blood" => ElementType.Blood,
            _ => ElementType.Umbra,
        };

        // -- Spawning ------------------------------------------------------------

        /// <summary>Telegraphed spawn: element flash on the ground, a beat, then the
        /// enemy materializes — nothing ever pops in on top of the player.</summary>
        public IEnumerator SpawnEnemy(EnemyKind kind, Vector3 position, ElementType element,
                                      System.Action<EnemyBase> onSpawned = null,
                                      float healthMultiplier = 1f, float aggroRange = 999f)
        {
            if (NavMesh.SamplePosition(position, out var navHit, 4f, NavMesh.AllAreas))
                position = navHit.position;

            SpellbladeFx.Flash(position + Vector3.up * 0.2f, ElementMath.ColorOf(element), 1f, 0.55f);
            yield return new WaitForSeconds(0.6f);
            if (_ended) yield break;

            EnemyBase enemy = kind switch
            {
                EnemyKind.Husk => GruntChaser.Spawn(position, element, this, healthMultiplier),
                EnemyKind.Cultist => GruntCaster.Spawn(position, element, this, healthMultiplier),
                _ => MiniBoss.Spawn(position, element, this, healthMultiplier),
            };
            enemy.aggroRange = aggroRange;
            onSpawned?.Invoke(enemy);
        }

        // -- Win / lose ----------------------------------------------------------

        public void Victory()
        {
            if (_ended) return;
            _ended = true;
            StartCoroutine(VictorySequence());
        }

        private IEnumerator VictorySequence()
        {
            ShowSplash("VICTORY", new Color(0.95f, 0.82f, 0.35f), 1.5f, 64);
            yield return new WaitForSeconds(1.5f);
            GameSession.ReportArenaResult(true);
        }

        /// <summary>Called by PlayerLife after its death fade.</summary>
        public void Defeat()
        {
            if (_ended) return;
            _ended = true;
            GameSession.ReportArenaResult(false);
        }

        // -- HUD -----------------------------------------------------------------

        public void SetObjectiveText(string text)
        {
            if (_objectiveText != null) _objectiveText.text = text;
        }

        /// <summary>Big center-screen text that fades out (wave numbers, boss intro, victory).</summary>
        public void ShowSplash(string message, Color color, float life, int fontSize = 52)
        {
            var go = new GameObject("Splash", typeof(Text));
            go.transform.SetParent(_canvas.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.62f);
            rect.sizeDelta = new Vector2(1200f, 110f);
            rect.anchoredPosition = Vector2.zero;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = FontStyle.Bold;
            text.alignment = TextAnchor.MiddleCenter;
            text.color = color;
            text.text = message;
            text.raycastTarget = false;

            StartCoroutine(FadeSplash(text, life));
        }

        private IEnumerator FadeSplash(Text text, float life)
        {
            float t = 0f;
            var baseColor = text.color;
            while (t < life && text != null)
            {
                t += Time.deltaTime;
                float alpha = 1f - Mathf.Clamp01((t - life * 0.5f) / (life * 0.5f));
                text.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
                yield return null;
            }
            if (text != null) Destroy(text.gameObject);
        }

        private void BuildHud()
        {
            var go = new GameObject("Objective HUD",
                typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            _canvas = go.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            _canvas.sortingOrder = 30; // under ArenaFlow's banner (40)

            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            var textGo = new GameObject("Objective Line", typeof(Text));
            textGo.transform.SetParent(go.transform, false);
            var rect = (RectTransform)textGo.transform;
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = new Vector2(0f, -52f); // below the ArenaFlow node banner
            rect.sizeDelta = new Vector2(800f, 34f);

            _objectiveText = textGo.GetComponent<Text>();
            _objectiveText.font = _font;
            _objectiveText.fontSize = 24;
            _objectiveText.fontStyle = FontStyle.Bold;
            _objectiveText.alignment = TextAnchor.UpperCenter;
            _objectiveText.color = new Color(0.92f, 0.88f, 0.72f, 0.95f);
            _objectiveText.raycastTarget = false;
        }
    }

    /// <summary>Base for the three node objectives. Director adds the right one
    /// and calls Begin exactly once.</summary>
    public abstract class ObjectiveBase : MonoBehaviour
    {
        protected ObjectiveDirector Director { get; private set; }
        protected ArenaNodeDef Node { get; private set; }

        public void Begin(ObjectiveDirector director, ArenaNodeDef node)
        {
            Director = director;
            Node = node;
            OnBegin();
        }

        protected abstract void OnBegin();
    }
}
