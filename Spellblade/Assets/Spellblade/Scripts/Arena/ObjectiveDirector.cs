using System.Collections;
using System.Collections.Generic;
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
        public bool PlayerDiedThisArena { get; private set; } // [PHASE2-05] Duelist's Plume feat flag

        private ArenaNodeDef _node; // [PHASE2-05]
        private Canvas _canvas;
        private Text _objectiveText;
        private Font _font;
        private bool _ended;

        public void Configure(ArenaNodeDef node)
        {
            _node = node; // [PHASE2-05] rewards need it at victory time
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

            // [PHASE2-05] Earning — computed BEFORE ReportArenaResult marks the clear.
            bool firstClear = _node != null && !GameSession.IsNodeCleared(_node.id);
            int essence = 0, shards = 0;
            var newGear = new List<string>();
            if (_node != null)
            {
                essence = _node.isBossNode ? (firstClear ? 60 : 25)
                        : _node.difficultyTier >= 2 ? (firstClear ? 30 : 12)
                        : (firstClear ? 20 : 8);
                // First clears drop shards everywhere; only boss-capped nodes keep paying on replay.
                shards = _node.isBossNode ? (firstClear ? 5 : 1) : (firstClear ? 2 : 0);

                var save = SaveSystem.Data;
                save.arcaneEssence += essence;
                save.elementShards += shards;
                GrantFeatGear(newGear);
                SaveSystem.Save();
            }

            StartCoroutine(VictorySequence(essence, shards, newGear));
        }

        /// <summary>[PHASE2-05] Feat-based cosmetics (never bought with currency).</summary>
        private void GrantFeatGear(List<string> newGearNames)
        {
            void TryUnlock(string id)
            {
                if (GearCatalog.Unlock(id)) newGearNames.Add(GearCatalog.Find(id).displayName);
            }

            if (_node.isBossNode)
            {
                if (_node.regionId == "shadow") { TryUnlock("hat_umbral_court"); TryUnlock("staff_crystal_shadow"); }
                if (_node.regionId == "frost") { TryUnlock("rimeholt_crown"); TryUnlock("staff_crystal_frost"); }
                if (!PlayerDiedThisArena) TryUnlock("duelists_plume");
            }

            // Robe trim: every node in the region cleared (counting this one).
            var region = RegionDefs.Find(_node.regionId);
            if (region != null)
            {
                bool all = true;
                foreach (var placement in region.nodes)
                    if (placement.node.id != _node.id && !GameSession.IsNodeCleared(placement.node.id))
                    { all = false; break; }
                if (all) TryUnlock($"robe_tint_{_node.regionId}");
            }
        }

        private IEnumerator VictorySequence(int essence, int shards, List<string> newGear)
        {
            ShowSplash("VICTORY", new Color(0.95f, 0.82f, 0.35f), 1.5f, 64);
            if (essence > 0 || shards > 0) // [PHASE2-05] reward toast
                ShowSplash($"+{essence} Essence   ·   +{shards} Shards",
                           new Color(0.85f, 0.78f, 0.55f), 1.5f, 28, yAnchor: 0.55f);
            if (newGear.Count > 0)
                ShowSplash($"New gear: {string.Join(", ", newGear)}",
                           new Color(0.75f, 0.9f, 1f), 1.5f, 22, yAnchor: 0.50f);
            yield return new WaitForSeconds(1.5f);
            GameSession.ReportArenaResult(true);
        }

        /// <summary>Called by PlayerLife after its death fade.</summary>
        public void Defeat()
        {
            if (_ended) return;
            _ended = true;
            PlayerDiedThisArena = true; // [PHASE2-05]
            GameSession.ReportArenaResult(false);
        }

        // -- HUD -----------------------------------------------------------------

        public void SetObjectiveText(string text)
        {
            if (_objectiveText != null) _objectiveText.text = text;
        }

        /// <summary>Big center-screen text that fades out (wave numbers, boss intro,
        /// victory, reward toasts — yAnchor stacks multiple lines). </summary>
        public void ShowSplash(string message, Color color, float life, int fontSize = 52,
                               float yAnchor = 0.62f)
        {
            var go = new GameObject("Splash", typeof(Text));
            go.transform.SetParent(_canvas.transform, false);

            var rect = (RectTransform)go.transform;
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, yAnchor);
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
