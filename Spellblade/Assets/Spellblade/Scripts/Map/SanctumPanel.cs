using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Spellblade
{
    /// <summary>
    /// THE SANCTUM (Plan 05) — the upgrade panel on the world map, replacing
    /// Plan 03's placeholder button. Slide-in panel on the right (~40% width)
    /// with four tabs:
    ///   SPELLS     — 4 discipline columns × 4 slot cards, unlock/rank buttons
    ///   ATTRIBUTES — 5 stat tracks, level pips, buy buttons
    ///   GEAR       — feat cosmetics, click to equip/unequip
    ///   CODEX      — region lore stubs (Ryan's brain-dump lands here later)
    /// Every purchase saves immediately and rebuilds the visible tab.
    /// </summary>
    public class SanctumPanel : MonoBehaviour
    {
        private WorldMapBootstrap _owner;
        private Font _font;
        private RectTransform _panel;
        private Text _headerWallet;
        private RectTransform _content;
        private readonly List<Button> _tabButtons = new();
        private int _activeTab;
        private List<Discipline> _roster; // display-only instance from SpellLibrary

        public static SanctumPanel Build(WorldMapBootstrap owner)
        {
            var go = new GameObject("Sanctum Panel", typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(owner.CanvasRoot, false);
            rect.anchorMin = new Vector2(0.6f, 0f);   // right ~40%
            rect.anchorMax = new Vector2(1f, 1f);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;

            var sanctum = go.AddComponent<SanctumPanel>();
            sanctum._owner = owner;
            sanctum._panel = rect;
            sanctum._font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            sanctum._roster = SpellLibrary.CreateDisciplines(); // names/ids/costs for the cards
            sanctum.BuildChrome();
            sanctum.SelectTab(0);
            go.SetActive(false);
            return sanctum;
        }

        public void Toggle()
        {
            bool opening = !gameObject.activeSelf;
            gameObject.SetActive(opening);
            if (opening) { RefreshHeader(); RebuildTab(); }
        }

        // -- Chrome ----------------------------------------------------------------

        private void BuildChrome()
        {
            var bg = _panel.gameObject.AddComponent<Image>();
            bg.color = new Color(0.025f, 0.025f, 0.055f, 0.97f);

            MakeText(_panel, "Title", "THE SANCTUM", new Vector2(0.5f, 1f), new Vector2(0f, -30f),
                     new Vector2(400f, 40f), 30, FontStyle.Bold, new Color(0.85f, 0.78f, 0.95f));

            _headerWallet = MakeText(_panel, "Wallet", "", new Vector2(0.5f, 1f), new Vector2(0f, -64f),
                                     new Vector2(600f, 24f), 16, FontStyle.Normal,
                                     new Color(0.75f, 0.72f, 0.62f));

            // Close button, top-right.
            var close = MakeButton(_panel, "Close", "✕", new Vector2(1f, 1f), new Vector2(-26f, -26f),
                                   new Vector2(36f, 36f), new Color(0.35f, 0.12f, 0.15f, 0.95f));
            close.onClick.AddListener(Toggle);

            // Tabs row.
            string[] tabs = { "SPELLS", "ATTRIBUTES", "GEAR", "CODEX" };
            for (int i = 0; i < tabs.Length; i++)
            {
                int index = i;
                var tab = MakeButton(_panel, $"Tab {tabs[i]}", tabs[i], new Vector2(0f, 1f),
                                     new Vector2(24f + i * 178f, -104f), new Vector2(170f, 38f),
                                     new Color(0.14f, 0.12f, 0.22f, 0.95f), 17, anchorIsPivotTopLeft: true);
                tab.onClick.AddListener(() => SelectTab(index));
                _tabButtons.Add(tab);
            }

            // Content area below the tabs.
            var contentGo = new GameObject("Content", typeof(RectTransform));
            _content = (RectTransform)contentGo.transform;
            _content.SetParent(_panel, false);
            _content.anchorMin = new Vector2(0f, 0f);
            _content.anchorMax = new Vector2(1f, 1f);
            _content.offsetMin = new Vector2(16f, 16f);
            _content.offsetMax = new Vector2(-16f, -152f);
        }

        private void SelectTab(int index)
        {
            _activeTab = index;
            for (int i = 0; i < _tabButtons.Count; i++)
                _tabButtons[i].GetComponent<Image>().color = i == index
                    ? new Color(0.32f, 0.24f, 0.5f, 0.95f)
                    : new Color(0.14f, 0.12f, 0.22f, 0.95f);
            RebuildTab();
        }

        private void RebuildTab()
        {
            RefreshHeader();
            _owner.RefreshWallet();
            for (int i = _content.childCount - 1; i >= 0; i--)
                Destroy(_content.GetChild(i).gameObject);

            switch (_activeTab)
            {
                case 0: BuildSpellsTab(); break;
                case 1: BuildAttributesTab(); break;
                case 2: BuildGearTab(); break;
                default: BuildCodexTab(); break;
            }
        }

        private void RefreshHeader()
        {
            var save = SaveSystem.Data;
            _headerWallet.text = $"ESSENCE  {save.arcaneEssence}      SHARDS  {save.elementShards}";
        }

        // -- SPELLS tab -------------------------------------------------------------

        private void BuildSpellsTab()
        {
            float columnWidth = 1f / _roster.Count;
            for (int d = 0; d < _roster.Count; d++)
            {
                var discipline = _roster[d];
                float xMin = d * columnWidth;

                MakeText(_content, $"{discipline.displayName} header", discipline.displayName.ToUpperInvariant(),
                         new Vector2(xMin + columnWidth / 2f, 1f), new Vector2(0f, -14f),
                         new Vector2(170f, 26f), 18, FontStyle.Bold, discipline.themeColor);

                for (int s = 0; s < discipline.spells.Count; s++)
                {
                    var spell = discipline.spells[s];
                    BuildSpellCard(spell, s,
                        anchor: new Vector2(xMin + columnWidth / 2f, 1f),
                        offset: new Vector2(0f, -46f - s * 168f));
                }
            }
        }

        private void BuildSpellCard(SpellSO spell, int slotIndex, Vector2 anchor, Vector2 offset)
        {
            var save = SaveSystem.Data;
            bool unlocked = slotIndex == 0 || save.unlockedSpells.Contains(spell.spellId);
            int rank = ProgressionMath.GetRank(spell.spellId);

            var card = new GameObject($"Card {spell.spellId}", typeof(Image));
            var rect = (RectTransform)card.transform;
            rect.SetParent(_content, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 1f);
            rect.anchoredPosition = offset;
            rect.sizeDelta = new Vector2(168f, 156f);
            card.GetComponent<Image>().color = new Color(0.07f, 0.07f, 0.12f, 0.9f);

            MakeText(rect, "Name", spell.displayName, new Vector2(0.5f, 1f), new Vector2(0f, -16f),
                     new Vector2(160f, 22f), 15, FontStyle.Bold,
                     unlocked ? Color.white : new Color(0.6f, 0.6f, 0.65f));

            string status = !unlocked ? $"Locked · {ProgressionMath.SlotUnlockCost(slotIndex)} shards"
                          : rank >= ProgressionMath.MaxRank ? "Rank III (max)"
                          : rank == 2 ? "Rank II" : "Rank I";
            MakeText(rect, "Status", status, new Vector2(0.5f, 1f), new Vector2(0f, -44f),
                     new Vector2(160f, 20f), 13, FontStyle.Normal, new Color(0.72f, 0.7f, 0.6f));

            if (!unlocked)
            {
                int cost = ProgressionMath.SlotUnlockCost(slotIndex);
                var buy = MakeButton(rect, "Unlock", $"Unlock ({cost} sh)", new Vector2(0.5f, 0f),
                                     new Vector2(0f, 34f), new Vector2(150f, 34f),
                                     new Color(0.22f, 0.32f, 0.2f, 0.95f), 14);
                buy.interactable = save.elementShards >= cost;
                buy.onClick.AddListener(() =>
                {
                    if (!ProgressionMath.TrySpendShards(cost)) return;
                    save.unlockedSpells.Add(spell.spellId);
                    SaveSystem.Save();
                    RebuildTab();
                });
            }
            else if (rank < ProgressionMath.MaxRank)
            {
                int cost = ProgressionMath.RankUpCost(rank);
                string next = rank == 1 ? "II" : "III";
                var buy = MakeButton(rect, "Rank", $"Rank {next} ({cost} sh)", new Vector2(0.5f, 0f),
                                     new Vector2(0f, 34f), new Vector2(150f, 34f),
                                     new Color(0.28f, 0.22f, 0.42f, 0.95f), 14);
                buy.interactable = save.elementShards >= cost;
                buy.onClick.AddListener(() =>
                {
                    if (!ProgressionMath.TrySpendShards(cost)) return;
                    ProgressionMath.SetRank(spell.spellId, rank + 1);
                    SaveSystem.Save();
                    RebuildTab();
                });
            }
        }

        // -- ATTRIBUTES tab ------------------------------------------------------------

        private void BuildAttributesTab()
        {
            var save = SaveSystem.Data;
            for (int i = 0; i < ProgressionMath.StatTracks.Length; i++)
            {
                var (id, displayName, effect) = ProgressionMath.StatTracks[i];
                int level = ProgressionMath.GetStatLevel(id);
                float y = -20f - i * 96f;

                MakeText(_content, $"{id} name", displayName, new Vector2(0f, 1f), new Vector2(30f, y),
                         new Vector2(240f, 26f), 19, FontStyle.Bold, new Color(0.9f, 0.87f, 0.8f),
                         TextAnchor.MiddleLeft);
                MakeText(_content, $"{id} effect", effect, new Vector2(0f, 1f), new Vector2(30f, y - 24f),
                         new Vector2(340f, 20f), 13, FontStyle.Normal, new Color(0.62f, 0.62f, 0.58f),
                         TextAnchor.MiddleLeft);

                // Level pips.
                for (int pip = 0; pip < ProgressionMath.MaxStatLevel; pip++)
                {
                    var dot = new GameObject("Pip", typeof(Image));
                    var dotRect = (RectTransform)dot.transform;
                    dotRect.SetParent(_content, false);
                    dotRect.anchorMin = dotRect.anchorMax = new Vector2(0f, 1f);
                    dotRect.anchoredPosition = new Vector2(300f + pip * 24f, y);
                    dotRect.sizeDelta = new Vector2(14f, 14f);
                    dot.GetComponent<Image>().sprite = WorldMapBootstrap.SoftCircle;
                    dot.GetComponent<Image>().color = pip < level
                        ? new Color(0.95f, 0.82f, 0.4f) : new Color(0.2f, 0.2f, 0.26f);
                }

                if (level < ProgressionMath.MaxStatLevel)
                {
                    int cost = ProgressionMath.StatUpgradeCost(level);
                    var buy = MakeButton(_content, $"{id} buy", $"Improve ({cost} es)", new Vector2(1f, 1f),
                                         new Vector2(-30f, y - 8f), new Vector2(170f, 36f),
                                         new Color(0.22f, 0.32f, 0.2f, 0.95f), 14, anchorIsPivotTopRight: true);
                    buy.interactable = save.arcaneEssence >= cost;
                    string statId = id;
                    int currentLevel = level;
                    buy.onClick.AddListener(() =>
                    {
                        if (!ProgressionMath.TrySpendEssence(cost)) return;
                        ProgressionMath.SetStatLevel(statId, currentLevel + 1);
                        SaveSystem.Save();
                        RebuildTab();
                    });
                }
                else
                {
                    MakeText(_content, $"{id} max", "MAX", new Vector2(1f, 1f), new Vector2(-70f, y - 10f),
                             new Vector2(90f, 26f), 16, FontStyle.Bold, new Color(0.95f, 0.82f, 0.4f));
                }
            }
        }

        // -- GEAR tab -----------------------------------------------------------------

        private void BuildGearTab()
        {
            for (int i = 0; i < GearCatalog.All.Count; i++)
            {
                var gear = GearCatalog.All[i];
                bool unlocked = GearCatalog.IsUnlocked(gear.id);
                bool equipped = GearCatalog.IsEquipped(gear.id);
                float y = -20f - i * 66f;

                MakeText(_content, $"{gear.id} name",
                         unlocked ? gear.displayName : $"??? — {gear.featHint}",
                         new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(420f, 24f), 16,
                         unlocked ? FontStyle.Bold : FontStyle.Italic,
                         unlocked ? new Color(0.9f, 0.87f, 0.8f) : new Color(0.45f, 0.45f, 0.5f),
                         TextAnchor.MiddleLeft);

                if (!unlocked) continue;

                var button = MakeButton(_content, $"{gear.id} equip",
                                        equipped ? "Unequip" : "Equip", new Vector2(1f, 1f),
                                        new Vector2(-30f, y), new Vector2(130f, 34f),
                                        equipped ? new Color(0.4f, 0.3f, 0.16f, 0.95f)
                                                 : new Color(0.22f, 0.32f, 0.2f, 0.95f),
                                        14, anchorIsPivotTopRight: true);
                string gearId = gear.id;
                bool wasEquipped = equipped;
                button.onClick.AddListener(() =>
                {
                    if (wasEquipped) GearCatalog.Unequip(gearId);
                    else GearCatalog.Equip(gearId);
                    RebuildTab();
                });
            }

            MakeText(_content, "Gear hint", "Gear is earned by feats, not bought. It appears on your wizard in the arena.",
                     new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(620f, 22f), 12,
                     FontStyle.Italic, new Color(0.5f, 0.5f, 0.55f));
        }

        // -- CODEX tab ------------------------------------------------------------------

        private void BuildCodexTab()
        {
            var lore = new (string title, string body)[]
            {
                ("The Shadow Reach", "Overcast and ancient, war-worn stone under gray-green mist. The Umbral Court holds what is left of its keeps."),
                ("The Rimeholt", "Blue ice over black rock. The Frozen Throne has not thawed in living memory."),
                ("The Tempest Shelf", "The mists have not parted."),
                ("The Crimson Fen", "The mists have not parted."),
                ("The Ember Wastes", "The mists have not parted."),
                ("The Verdant Deep", "The mists have not parted."),
                ("The Sunken Marches", "The mists have not parted."),
                ("The Radiant Steppe", "The mists have not parted."),
            };

            for (int i = 0; i < lore.Length; i++)
            {
                float y = -16f - i * 92f;
                MakeText(_content, $"Codex {i} title", lore[i].title.ToUpperInvariant(),
                         new Vector2(0f, 1f), new Vector2(30f, y), new Vector2(500f, 22f), 16,
                         FontStyle.Bold, new Color(0.82f, 0.78f, 0.66f), TextAnchor.MiddleLeft);
                MakeText(_content, $"Codex {i} body", lore[i].body,
                         new Vector2(0f, 1f), new Vector2(30f, y - 26f), new Vector2(620f, 48f), 13,
                         FontStyle.Normal, new Color(0.6f, 0.6f, 0.58f), TextAnchor.UpperLeft);
            }

            MakeText(_content, "Codex hint", "(Placeholder lore — the world-building brain-dump replaces these.)",
                     new Vector2(0.5f, 0f), new Vector2(0f, 14f), new Vector2(620f, 20f), 12,
                     FontStyle.Italic, new Color(0.45f, 0.45f, 0.5f));
        }

        // -- Tiny factory helpers ------------------------------------------------------

        private Text MakeText(Transform parent, string name, string content, Vector2 anchor,
                              Vector2 offset, Vector2 size, int fontSize, FontStyle style,
                              Color color, TextAnchor align = TextAnchor.MiddleCenter)
        {
            var go = new GameObject(name, typeof(Text));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;

            var text = go.GetComponent<Text>();
            text.font = _font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.text = content;
            text.alignment = align;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            return text;
        }

        private Button MakeButton(Transform parent, string name, string label, Vector2 anchor,
                                  Vector2 offset, Vector2 size, Color color, int fontSize = 16,
                                  bool anchorIsPivotTopLeft = false, bool anchorIsPivotTopRight = false)
        {
            var go = new GameObject(name, typeof(Image), typeof(Button));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            rect.anchorMin = rect.anchorMax = anchor;
            rect.pivot = anchorIsPivotTopLeft ? new Vector2(0f, 1f)
                       : anchorIsPivotTopRight ? new Vector2(1f, 1f)
                       : new Vector2(anchor.x, anchor.y);
            rect.anchoredPosition = offset;
            rect.sizeDelta = size;
            go.GetComponent<Image>().color = color;

            var text = MakeText(rect, "Label", label, new Vector2(0.5f, 0.5f), Vector2.zero,
                                size, fontSize, FontStyle.Bold, new Color(0.92f, 0.9f, 0.95f));
            text.raycastTarget = false;

            return go.GetComponent<Button>();
        }
    }
}
